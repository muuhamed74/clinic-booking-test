using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Clinic.Domain.DTOs;
using Clinic.Domain.Entities;
using Clinic.Domain.Entities.Enums;
using Clinic.Domain.Repositories;
using Clinic.Domain.Specifications.Clinic.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Service_Abstraction;
using Twilio.Http;

namespace Clinic.Service
{
    public class BookingService : IBookingService
    {
        private readonly IUnitOfWork _unitOfWork;
        //private readonly INotificationService _notificationService;
        private readonly IMapper _mapper;
        private readonly BookingSettings _settings;
        private readonly Iwpsenderservice _wpsenderservice;

        public BookingService(
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            IOptions<BookingSettings> settings,
             IMapper mapper,
             Iwpsenderservice wpsenderservice)
        {
            _unitOfWork = unitOfWork;
            //_notificationService = notificationService;
            _mapper = mapper;
            _settings = settings.Value;
            _wpsenderservice = wpsenderservice;
        }

        public async Task<AppointmentDto> BookAppointmentAsync(AppointmentRequestDto request)
        {
            //   غيرها لـ "Africa/Cairo" اما تيجي تشغل الدوكر
            TimeZoneInfo egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
            const int maxRetries = 3;
            const int maxContractsPerDay = 5;
            int attempt = 0;
            int queueNumber = 1;


            DateTime bookingDateEgypt, startTimeEgypt, estimatedTimeEgypt, bookingDateUtc,
                clinicOpenUtc, clinicCloseUtc, startTimeUtc, estimatedTimeUtc;

            while (true)
            {

                // Add IsolationLevel to prevent the duplicate booking issue
                // just in case two requests come at the same time

                attempt++;
                await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);

                try
                {
                    #region Check Booking Date Validations
                    DateTime nowUtc = DateTime.UtcNow;
                    DateTime nowEgypt = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, egyptZone);
                    var todayEgypt = nowEgypt.Date;
                    var tomorrowEgypt = todayEgypt.AddDays(1);

                    bookingDateEgypt = DateTime.SpecifyKind(request.BookingDate.Date, DateTimeKind.Unspecified);
                    bookingDateUtc = DateTime.SpecifyKind(request.BookingDate.Date, DateTimeKind.Utc);
                    //if (bookingDateUtc.Date < bookingDateEgypt.Date)
                    //    bookingDateUtc = bookingDateUtc.AddDays(1);

                    Console.WriteLine("---- Booking Debug ----");
                    Console.WriteLine($"Now (UTC): {nowUtc}, Kind: {nowUtc.Kind}");
                    Console.WriteLine($"Now (Egypt): {nowEgypt}, Kind: {nowEgypt.Kind}");
                    Console.WriteLine($"Request.BookingDate (raw): {request.BookingDate}, Kind: {request.BookingDate.Kind}");
                    Console.WriteLine($"BookingDateEgypt: {bookingDateEgypt}, Kind: {bookingDateEgypt.Kind}");
                    Console.WriteLine($"BookingDateUtc: {bookingDateUtc}, Kind: {bookingDateUtc.Kind}");


                    if (bookingDateEgypt.Date < todayEgypt)
                        throw new ArgumentException("لا يمكن الحجز في تاريخ قديم.");

                    if (bookingDateEgypt.Date > tomorrowEgypt)
                        throw new ArgumentException("يمكنك الحجز لليوم الحالي أو الغد فقط.");
                    #endregion


                    #region  Validate and Check AppointmentType
                    if (!Enum.IsDefined(typeof(AppointmentType), request.AppointmentType))
                        throw new ArgumentException("نوع الموعد يجب أن يكون Checkup أو Contract.");


                    if (request.AppointmentType == AppointmentType.Contract)
                    {
                        var AppTypspec = new AppointmentByDateSpecification(bookingDateUtc);
                        var todaysAppointmentsTypes = await _unitOfWork.Reposit<Appointment>().ListAsync(AppTypspec);
                        int contractCount = todaysAppointmentsTypes.Count(a => a.AppointmentType == AppointmentType.Contract);
                        if (contractCount >= maxContractsPerDay)
                            throw new ArgumentException("عدد التعاقدات لليوم ممتلئ. يرجى اختيار موعد كشف أو يوم آخر.");
                    }
                    #endregion

                    #region Check Patient Existence
                    var archiveSpec = new AppointmentArchiveByPhoneSpecification(request.Phone);
                    var archivedPatient = await _unitOfWork.Reposit<AppointmentArchive>().GetEntityWithSpec(archiveSpec);

                    if (archivedPatient != null)
                    {
                        var existingAppointmentSpec = new AppointmentByDateAndPhoneSpecification(request.Phone, bookingDateUtc);
                        var existingAppointment = await _unitOfWork.Reposit<Appointment>()
                            .GetEntityWithSpec(existingAppointmentSpec);

                        if (existingAppointment != null)
                            throw new ArgumentException("لا يمكنك الحجز أكثر من مرة في نفس اليوم.");
                    }

                    var normalizedName = NormalizeName(request.Name);

                    var nameParts = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (nameParts.Length != 3)
                    {
                        throw new ArgumentException("يجب إدخال اسم ثلاثي .");
                    }

                    var existingAppointmentByNameSpec = new AppointmentByDateAndNameSpecification(normalizedName, bookingDateUtc);

                    var existingAppointmentByName = await _unitOfWork.Reposit<Appointment>()
                        .GetEntityWithSpec(existingAppointmentByNameSpec);

                    if (existingAppointmentByName != null)
                    {
                        throw new ArgumentException("يوجد حجز مسبق بنفس الاسم");
                    }
                    #endregion



                    #region Clinc Start and End Time
                    TimeSpan clinicStart, clinicEnd;
                    int minutesPerCase = _settings.MinutesPerCase;

                    var overrideSetting = await _unitOfWork.Reposit<BookingOverride>()
                        .GetEntityWithSpec(new BookingOverrideByDateSpecification(bookingDateUtc));

                    // For Closed Days
                    if (overrideSetting != null && overrideSetting.IsClosed)
                    {
                        throw new ArgumentException("لا يمكن الحجز في هذا اليوم لأنه مغلق .");
                    }

                    // check if doctor update the time or not 
                    if (overrideSetting != null)
                    {
                        clinicStart = overrideSetting.ClinicStartTime.Value;
                        clinicEnd = overrideSetting.ClinicEndTime.Value;
                    }
                    else
                    {
                        clinicStart = _settings.ClinicStartTime.Value;
                        clinicEnd = _settings.ClinicEndTime.Value;
                    }


                    // clinic working hours in egypt time zone
                    var clinicOpenEgypt = bookingDateEgypt.Date.Add(clinicStart);
                    clinicOpenUtc = TimeZoneInfo.ConvertTimeToUtc(clinicOpenEgypt, egyptZone);
                    var clinicCloseEgypt = bookingDateEgypt.Add(clinicEnd);
                    clinicCloseUtc = TimeZoneInfo.ConvertTimeToUtc(clinicCloseEgypt, egyptZone);
                    startTimeEgypt = clinicOpenEgypt;
                    startTimeUtc = TimeZoneInfo.ConvertTimeToUtc(startTimeEgypt, egyptZone);

                    if (bookingDateEgypt.Date == nowEgypt.Date)
                    {
                        startTimeEgypt = nowEgypt > clinicOpenEgypt ? nowEgypt : clinicOpenEgypt;
                    }
                    else
                    {
                        startTimeEgypt = clinicOpenEgypt;
                    }
                    startTimeUtc = TimeZoneInfo.ConvertTimeToUtc(startTimeEgypt, egyptZone);
                    #endregion


                    #region Detrmine Estimated Time and Queue Number
                    var spec = new AppointmentByDateSpecification(bookingDateUtc);
                    var todaysAppointments = await _unitOfWork.Reposit<Appointment>().ListAsync(spec);

                    if (todaysAppointments.Any())
                    {
                        var lastAppointment = todaysAppointments.OrderByDescending(a => a.EstimatedTime).FirstOrDefault();


                        if (lastAppointment != null)
                        {
                            var lastEstimatedUtc = lastAppointment.EstimatedTime.Value;
                            var lastEstimatedEgypt = TimeZoneInfo.ConvertTimeFromUtc(lastEstimatedUtc, egyptZone);
                            var nextAvailableEgypt = lastEstimatedEgypt.AddMinutes(_settings.MinutesPerCase);

                            estimatedTimeEgypt = new[]
                            {
                             startTimeEgypt,
                             nextAvailableEgypt
                            }.Max();

                            queueNumber = todaysAppointments.Max(a => a.QueueNumber) + 1;
                        }
                        else
                        {
                            estimatedTimeEgypt = startTimeEgypt;
                            queueNumber = 1;
                        }
                    }
                    else
                    {
                        estimatedTimeEgypt = startTimeEgypt;
                        queueNumber = 1;
                    }

                    estimatedTimeUtc = TimeZoneInfo.ConvertTimeToUtc(estimatedTimeEgypt, egyptZone);
                    if (estimatedTimeUtc >= clinicCloseUtc)
                        throw new ArgumentException("لا يوجد مواعيد متاحة في هذا اليوم.");
                    #endregion


                    #region Make The Appointment and sending Notification
                    var appointment = _mapper.Map<Appointment>(request);


                    appointment.PatientName = normalizedName;
                    appointment.Phone = request.Phone;
                    appointment.QueueNumber = queueNumber;
                    appointment.Status = AppointmentStatus.Waiting;
                    appointment.EstimatedTime = estimatedTimeUtc;
                    appointment.Date = bookingDateUtc.Date;
                    appointment.AppointmentType = request.AppointmentType;

                    await _unitOfWork.Reposit<Appointment>().AddAsync(appointment);
                    await _unitOfWork.CompleteAsync();

                    var appointmentArchive = new AppointmentArchive
                    {
                        AppointmentId = appointment.Id,
                        PatientName = request.Name,
                        Phone = request.Phone,
                        QueueNumber = queueNumber,
                        Status = AppointmentStatus.Waiting,
                        EstimatedTime = estimatedTimeUtc,
                        Date = bookingDateUtc.Date,
                        AppointmentType = request.AppointmentType,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Reposit<AppointmentArchive>().AddAsync(appointmentArchive);



                    await _unitOfWork.CompleteAsync();
                    await transaction.CommitAsync();

                    var appointmentWithPatient = await _unitOfWork.Reposit<Appointment>()
                        .GetEntityWithSpec(new AppointmentByIdWithPatientSpecification(appointment.Id));

                    var dto = _mapper.Map<AppointmentDto>(appointment);

                    dto.EstimatedTime = TimeZoneInfo.ConvertTimeFromUtc(appointment.EstimatedTime.Value, egyptZone);
                    dto.Date = TimeZoneInfo.ConvertTimeFromUtc(appointment.Date.Value, egyptZone);
                    await _wpsenderservice.SendStatusAsync(appointment);
                    return dto;
                }
                catch (DbUpdateException ex) when (attempt < maxRetries)
                {
                    if (transaction != null && transaction.GetDbTransaction().Connection != null)
                    {
                        await transaction.RollbackAsync();
                    }
                    await Task.Delay(50);
                    continue;
                }
                catch
                {
                    if (transaction != null && transaction.GetDbTransaction().Connection != null)
                    {
                        await transaction.RollbackAsync();
                    }
                    throw;
                }
            }
        }
        #endregion


        public async Task<AppointmentDto?> GetAppointmentByPhoneAsync(string phoneNumber, DateTime date)
        {
            TimeZoneInfo egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
            DateTime dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

            var spec = new AppointmentByDateAndPhoneSpecification(phoneNumber, dateUtc);
            var appointment = await _unitOfWork.Reposit<Appointment>().GetEntityWithSpec(spec);
            var dto = _mapper.Map<AppointmentDto?>(appointment);
            return dto ?? throw new KeyNotFoundException("لا يوجد حجز بهذا الرقم في هذا اليوم.");
        }



        public async Task<AppointmentDto> CancelByPatientAsync(string phone, DateTime date)
        {
            var dateUtc = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

            var spec = new AppointmentByDateAndPhoneSpecification(phone, dateUtc);
            var appointment = await _unitOfWork.Reposit<Appointment>().GetEntityWithSpec(spec);

            if (appointment == null)
                throw new KeyNotFoundException("لا يوجد حجز بهذا الرقم في هذا اليوم.");

            await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var archiveAppointment = await _unitOfWork.Reposit<AppointmentArchive>()
                    .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appointment.Id));

                if (archiveAppointment != null)
                    _unitOfWork.Reposit<AppointmentArchive>().Delete(archiveAppointment);

                _unitOfWork.Reposit<Appointment>().Delete(appointment);

                await _unitOfWork.CompleteAsync();
                await transaction.CommitAsync();

                return _mapper.Map<AppointmentDto>(appointment);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }





        // for name filtering
        private string NormalizeName(string name)
        {
            return string.Join(" ",
                name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            );
        }
    }
}
