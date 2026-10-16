using AutoMapper;
using Clinic.Domain.DTOs;
using Clinic.Domain.Entities;
using Clinic.Domain.Entities.Enums;
using Clinic.Domain.Entities.Patient_Notifications;

//using Clinic.Domain.Entities.Patient_Notifications;
using Clinic.Domain.Repositories;
using Clinic.Domain.Specifications.Clinic.Specifications;
using Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifications;
using Clinic.Service.Patien_Notofcations;
using Clinic_booking_site.Helpers.Hashing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Service_Abstraction;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        private readonly IPatientNotificationService _patientNotificationService;
        private readonly IPatientSessionResolver _patientSessionResolver;

        public BookingService(
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            IOptions<BookingSettings> settings,
             IMapper mapper,
             Iwpsenderservice wpsenderservice,
             IPatientNotificationService patientNotificationService,
             IPatientSessionResolver patientSessionResolver)
        {
            _unitOfWork = unitOfWork;
            //_notificationService = notificationService;
            _mapper = mapper;
            _settings = settings.Value;
            _wpsenderservice = wpsenderservice;
            _patientNotificationService = patientNotificationService;
            _patientSessionResolver = patientSessionResolver;
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

                    var phone = NormalizePhone(request.Phone);



                    //var archiveSpec = new AppointmentArchiveByPhoneSpecification(request.Phone);
                    //var archivedPatient = await _unitOfWork.Reposit<AppointmentArchive>().GetEntityWithSpec(archiveSpec);


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

                    // resolve patient and session
                    var (patient, newRawToken) = await ResolvePatientAndSessionAsync(phone, normalizedName, request.PatientToken);



                    var existingPatientAppointmentSpec = new AppointmentByPatientIdAndDateSpecification(patient.Id, bookingDateUtc);
                    var existingPatientAppointment = await _unitOfWork.Reposit<Appointment>()
                        .GetEntityWithSpec(existingPatientAppointmentSpec);

                    if (existingPatientAppointment != null)
                        throw new ArgumentException("لا يمكنك الحجز أكثر من مرة في نفس اليوم.");


                    //{
                    //    var existingAppointmentSpec = new AppointmentByDateAndPhoneSpecification(request.Phone, bookingDateUtc);
                    //    var existingAppointment = await _unitOfWork.Reposit<Appointment>()
                    //        .GetEntityWithSpec(existingAppointmentSpec);

                    //    if (existingAppointment != null)
                    //        throw new ArgumentException("لا يمكنك الحجز أكثر من مرة في نفس اليوم.");
                    //}





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


                    #region Create The Appointment 
                    var appointment = _mapper.Map<Appointment>(request);


                    appointment.PatientName = normalizedName;
                    appointment.Phone = phone;
                    appointment.QueueNumber = queueNumber;
                    appointment.Status = AppointmentStatus.Waiting;
                    appointment.EstimatedTime = estimatedTimeUtc;
                    appointment.Date = bookingDateUtc.Date;
                    appointment.AppointmentType = request.AppointmentType;
                    appointment.PatientId = patient.Id;

                    await _unitOfWork.Reposit<Appointment>().AddAsync(appointment);
                    await _unitOfWork.CompleteAsync();

                    var appointmentArchive = new AppointmentArchive
                    {
                        AppointmentId = appointment.Id,
                        PatientName = request.Name,
                        PatientId = patient.Id,
                        Phone = phone,
                        QueueNumber = queueNumber,
                        Status = AppointmentStatus.Waiting,
                        EstimatedTime = estimatedTimeUtc,
                        Date = bookingDateUtc.Date,
                        AppointmentType = request.AppointmentType,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Reposit<AppointmentArchive>().AddAsync(appointmentArchive);



                    await _unitOfWork.CompleteAsync();
                    //await transaction.CommitAsync();

                    var appointmentWithPatient = await _unitOfWork.Reposit<Appointment>()
                        .GetEntityWithSpec(new AppointmentByIdWithPatientSpecification(appointment.Id));

                    var dto = _mapper.Map<AppointmentDto>(appointment);

                    dto.EstimatedTime = TimeZoneInfo.ConvertTimeFromUtc(appointment.EstimatedTime.Value, egyptZone);
                    dto.Date = TimeZoneInfo.ConvertTimeFromUtc(appointment.Date.Value, egyptZone);



                    await _patientNotificationService.SendStatusAsync(appointment, appointmentArchive.Id);
                    await _unitOfWork.CompleteAsync();
                    await transaction.CommitAsync();


                    //await _wpsenderservice.SendStatusAsync(appointment);


                    dto.PatientToken = newRawToken;
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
                #region old code

                //    var archiveAppointment = await _unitOfWork.Reposit<AppointmentArchive>()
                //        .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appointment.Id));

                //    if (archiveAppointment != null)
                //        _unitOfWork.Reposit<AppointmentArchive>().Delete(archiveAppointment);

                //    _unitOfWork.Reposit<Appointment>().Delete(appointment);

                //    await _unitOfWork.CompleteAsync();
                //    await transaction.CommitAsync();

                //    return _mapper.Map<AppointmentDto>(appointment);
                //}
                //catch
                //{
                //    await transaction.RollbackAsync();
                //    throw;
                //}
                #endregion

                var oldQueue = appointment.QueueNumber;
                var oldEstimated = appointment.EstimatedTime;

                var egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
                var appointmentDate = TimeZoneInfo.ConvertTimeFromUtc(appointment.EstimatedTime.Value, egyptZone).Date;

                var allAppointmentsToday = await _unitOfWork.Reposit<Appointment>()
                   .GetAllWithSpecAsync(new AppointmentsWithPatientsSpecification());

                var remainingAppointments = allAppointmentsToday
                        .Where(a =>
                        {
                            var apptDate = TimeZoneInfo.ConvertTimeFromUtc(a.EstimatedTime.Value, egyptZone).Date;
                            return apptDate == appointmentDate &&
                                   a.Id != appointment.Id &&
                                   (a.Status == AppointmentStatus.Waiting ||
                                    a.Status == AppointmentStatus.Rescheduled);
                        })
                         .OrderBy(a => a.QueueNumber)
                         .ToList();

                var affectedAppointments = remainingAppointments
                    .Where(a => a.QueueNumber > oldQueue)
                    .OrderBy(a => a.QueueNumber)
                    .ToList();

                appointment.Status = AppointmentStatus.Cancelled;

                //await _patientNotificationService.SendStatusAsync(appointment, null);

                var archiveAppointment = await _unitOfWork.Reposit<AppointmentArchive>()
                   .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appointment.Id));

                if (archiveAppointment != null)
                    _unitOfWork.Reposit<AppointmentArchive>().Delete(archiveAppointment);

                _unitOfWork.Reposit<Appointment>().Delete(appointment);

                await _unitOfWork.CompleteAsync();

                int newQueue = oldQueue;
                DateTime currentTime = oldEstimated.Value;


                foreach (var appt in affectedAppointments)
                {
                    var archiveAppt = await _unitOfWork.Reposit<AppointmentArchive>()
                    .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appt.Id));

                    if (archiveAppt != null)
                    {
                        archiveAppt.QueueNumber = appt.QueueNumber;
                        archiveAppt.EstimatedTime = appt.EstimatedTime;
                        archiveAppt.Status = AppointmentStatus.Rescheduled; 
                        archiveAppt.Date = appt.Date;
                        _unitOfWork.Reposit<AppointmentArchive>().Update(archiveAppt);
                    }

                    appt.QueueNumber = newQueue++;
                    appt.EstimatedTime = currentTime;
                    appt.Status = AppointmentStatus.Rescheduled; 

                    currentTime = currentTime.AddMinutes(_settings.MinutesPerCase);
                    _unitOfWork.Reposit<Appointment>().Update(appt);
                }

                foreach (var appt in affectedAppointments)
                {
                    await _patientNotificationService.SendStatusAsync(appt, null);
                }


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


        public async Task<int> GetTotalPatientsCountAsync()
        {
            var patients = await _unitOfWork.Reposit<Patient>().ListAllAsync();
            return patients.Count;
        }


        // Helpers

        private string NormalizeName(string name)
        {
            return string.Join(" ",
                name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            );
        }



        private static string NormalizePhone(string phone)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("20") && digits.Length == 12)
                digits = "0" + digits[2..];
            return digits;
        }




        //private static string HashToken(string rawToken)
        //{
        //    var bytes = System.Security.Cryptography.SHA256.HashData(
        //        System.Text.Encoding.UTF8.GetBytes(rawToken));
        //    return Convert.ToHexString(bytes);
        //}




        private async Task<(Patient patient, string? newRawToken)> ResolvePatientAndSessionAsync(string phone, string normalizedName, string? incomingToken)
        {
            var patient = await _unitOfWork.Reposit<Patient>()
                .GetEntityWithSpec(new PatientByPhoneSpecification(phone));

            if (patient == null)
            {
                patient = new Patient { Phone = phone, Name = normalizedName, CreatedAt = DateTime.UtcNow };
                await _unitOfWork.Reposit<Patient>().AddAsync(patient);
                await _unitOfWork.CompleteAsync();
            }

            //if (!string.IsNullOrWhiteSpace(incomingToken))
            //{
            //    var hash = HashToken(incomingToken);
            //    var existingSession = await _unitOfWork.Reposit<PatientSession>()
            //        .GetEntityWithSpec(new ActiveSessionByTokenHashAndPatientSpecification(hash, patient.Id));

            //    if (existingSession != null)
            //    {
            //        existingSession.LastSeenAt = DateTime.UtcNow;
            //        _unitOfWork.Reposit<PatientSession>().Update(existingSession);
            //        return (patient, null);
            //    }
            //}

            var resolvedFromToken = await _patientSessionResolver.ResolveAsync(incomingToken);
            if (resolvedFromToken != null && resolvedFromToken.Id == patient.Id)
            {
                return (patient, null);
            }

            var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var session = new PatientSession
            {
                PatientId = patient.Id,
                TokenHash = TokenHasher.Hash(rawToken),
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };
            await _unitOfWork.Reposit<PatientSession>().AddAsync(session);

            return (patient, rawToken);
        }













    }
}
