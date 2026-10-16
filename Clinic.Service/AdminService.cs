using AutoMapper;
using Clinic.Domain.DTOs;
using Clinic.Domain.Entities;
using Clinic.Domain.Entities.Enums;
using Clinic.Domain.Repositories;
using Clinic.Domain.Specifications.Clinic.Specifications;
using Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifiacations_Specs;
using Microsoft.Extensions.Options;
using Service_Abstraction;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Service
{
    public class AdminService : IAdminService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly INotificationService _notificationService;
        private readonly IOptions<BookingSettings> _settings;
        private readonly Iwpsenderservice _wpsenderservice;
        private readonly IPatientNotificationService _patientNotificationService;

        public AdminService(IUnitOfWork unitOfWork,
                            IMapper mapper,
                            INotificationService notificationService,
                            IOptions<BookingSettings> settings,
                            Iwpsenderservice wpsenderservice,
                            IPatientNotificationService patientNotificationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _notificationService = notificationService;
            _settings = settings;
            _wpsenderservice = wpsenderservice;
            _patientNotificationService = patientNotificationService;
        }


        public async Task<(int Count, List<AppointmentDto> Appointments)> GetAllAppointmentsWithCountAsync()
        {

            var specAppointments = new AppointmentsWithPatientsSpecification();
            var appointments = await _unitOfWork.Reposit<Appointment>().ListAsync(specAppointments);
            var appointmentDtos = _mapper.Map<List<AppointmentDto>>(appointments);

            DateTime todayUtc = DateTime.SpecifyKind(DateTime.Now.Date, DateTimeKind.Utc);

            var specArchive = new AppointmentArchiveCountByDateSpecification(todayUtc);
            var archiveAppointments = await _unitOfWork.Reposit<AppointmentArchive>().ListAsync(specArchive);
            int count = archiveAppointments.Count;

            return (count, appointmentDtos);
        }



        public async Task<(int Count, List<AppointmentDto> Appointments)> GetArchivedAppointmentsByDateAsync(DateTime? date = null)
        {
            DateTime filterDate = date ?? DateTime.SpecifyKind(DateTime.Now.Date, DateTimeKind.Utc);

            var specArchive = new AppointmentArchiveCountByDateSpecification(filterDate);
            var archiveAppointments = await _unitOfWork.Reposit<AppointmentArchive>().ListAsync(specArchive);

            var appointmentDtos = _mapper.Map<List<AppointmentDto>>(archiveAppointments);

            return (archiveAppointments.Count, appointmentDtos);
        }



        public async Task<int> DeleteArchivedAppointmentsByDateAsync(DateTime? date = null)
        {
            DateTime filterDate = date ?? DateTime.SpecifyKind(DateTime.Now.Date, DateTimeKind.Utc);

            var specArchive = new AppointmentArchiveCountByDateSpecification(filterDate);
            var archiveAppointments = await _unitOfWork.Reposit<AppointmentArchive>().ListAsync(specArchive);

            if (!archiveAppointments.Any())
                return 0;

            foreach (var appointment in archiveAppointments)
            {
                _unitOfWork.Reposit<AppointmentArchive>().Delete(appointment);
            }

            await _unitOfWork.CompleteAsync();

            return archiveAppointments.Count;
        }



        public async Task<AppointmentDto> CancelAppointmentAsync(int appointmentId)
        {
            var appointment = await _unitOfWork.Reposit<Appointment>()
                .GetEntityWithSpec(new AppointmentByIdWithPatientSpecification(appointmentId));
            if (appointment == null)
                throw new KeyNotFoundException("Appointment not found");

            await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
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

                //await _wpsenderservice.SendStatusAsync(appointment);

                await _patientNotificationService.SendStatusAsync(appointment, null);

                var archiveAppointment = await _unitOfWork.Reposit<AppointmentArchive>()
                   .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appointment.Id));

                _unitOfWork.Reposit<AppointmentArchive>().Delete(archiveAppointment);

                _unitOfWork.Reposit<Appointment>().Delete(appointment);


                await _unitOfWork.CompleteAsync();


                int newQueue = oldQueue;
                DateTime currentTime = oldEstimated.Value;

                foreach (var appt in affectedAppointments)
                {
                    var archiveAppt = await _unitOfWork.Reposit<AppointmentArchive>()
                    .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appt.Id));


                    archiveAppt.QueueNumber = appt.QueueNumber;
                    archiveAppt.EstimatedTime = appt.EstimatedTime;
                    archiveAppt.Status = appt.Status;
                    archiveAppt.Date = appt.Date;
                    _unitOfWork.Reposit<AppointmentArchive>().Update(archiveAppt);


                    appt.QueueNumber = newQueue++;
                    appt.EstimatedTime = currentTime;
                    //appt.Status = AppointmentStatus.Rescheduled;
                    currentTime = currentTime.AddMinutes(_settings.Value.MinutesPerCase);
                    _unitOfWork.Reposit<Appointment>().Update(appt);

                }


                await _unitOfWork.CompleteAsync();

                //foreach (var appt in affectedAppointments)
                //{
                //    await _wpsenderservice.SendStatusAsync(appt);
                //}

                await transaction.CommitAsync();





                return _mapper.Map<AppointmentDto>(appointment);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }



        public async Task<AppointmentDto> CompleteAppointmentAsync(int appointmentId)
        {
            var appointment = await _unitOfWork.Reposit<Appointment>()
                    .GetEntityWithSpec(new AppointmentByIdWithPatientSpecification(appointmentId));

            if (appointment == null)
                throw new KeyNotFoundException("Appointment not found");

            await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
                var nowEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone);

                var appointmentDate = appointment.EstimatedTime.Value.Date;
                if (nowEgypt.Date != appointmentDate)
                    throw new InvalidOperationException("لا يمكن إنهاء الموعد في يوم مختلف عن موعده.");

                var oldQueue = appointment.QueueNumber;

                appointment.Status = AppointmentStatus.Completed;

                //_unitOfWork.Reposit<Appointment>().Update(appointment);
                _unitOfWork.Reposit<Appointment>().Delete(appointment);
                await _unitOfWork.CompleteAsync();

                var allAppointmentsToday = await _unitOfWork.Reposit<Appointment>()
                    .GetAllWithSpecAsync(new AppointmentsWithPatientsSpecification());

                var affectedAppointments = allAppointmentsToday
                    .Where(a =>
                    {
                        var apptDate = a.EstimatedTime.Value.Date;
                        return apptDate == appointmentDate &&
                               a.QueueNumber > oldQueue &&
                               (a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.Rescheduled);
                    })
                    .OrderBy(a => a.QueueNumber)
                    .ToList();

                foreach (var appt in affectedAppointments)
                {
                    appt.QueueNumber -= 1;
                    _unitOfWork.Reposit<Appointment>().Update(appt);
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



        public async Task<AppointmentDto> RescheduleAppointmentAsync(RescheduleAppointmentRequestDto requestDto)
        {
            var appointment = await _unitOfWork.Reposit<Appointment>()
                .GetEntityWithSpec(new AppointmentByIdWithPatientSpecification(requestDto.AppointmentId));

            if (appointment == null)
                throw new ArgumentException("Appointment not found");

            await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);

            try
            {
                var egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
                var newEgyptTime = DateTime.SpecifyKind(requestDto.NewTime, DateTimeKind.Unspecified);
                var newUtcTime = TimeZoneInfo.ConvertTimeToUtc(newEgyptTime, egyptZone);
                var appointmentDate = TimeZoneInfo.ConvertTimeFromUtc(appointment.EstimatedTime.Value, egyptZone).Date;


                if (newEgyptTime.Date != appointmentDate)
                    throw new ArgumentException("لا يمكن تغيير المعاد ليوم مختلف.");


                //var clinicOpenTime = new TimeSpan(11, 30, 0);
                //var clinicCloseTime = new TimeSpan(22, 0, 0);
                //var newTimeOfDay = newEgyptTime.TimeOfDay;

                //if (newTimeOfDay < clinicOpenTime || newTimeOfDay > clinicCloseTime)
                //    throw new InvalidOperationException($"أوقات العيادة من {clinicOpenTime:hh\\:mm} صباحاً إلى {clinicCloseTime:hh\\:mm} مساءً.");






                var overrideSetting = await _unitOfWork.Reposit<BookingOverride>()
                       .GetEntityWithSpec(new BookingOverrideByDateSpecification(appointmentDate));

                TimeSpan clinicOpenTime, clinicCloseTime;

                if (overrideSetting != null)
                {
                    clinicOpenTime = overrideSetting.ClinicStartTime.Value;
                    clinicCloseTime = overrideSetting.ClinicEndTime.Value;
                }
                else
                {
                    clinicOpenTime = _settings.Value.ClinicStartTime.Value;
                    clinicCloseTime = _settings.Value.ClinicEndTime.Value;
                }

                var newTimeOfDay = newEgyptTime.TimeOfDay;

                if (newTimeOfDay < clinicOpenTime || newTimeOfDay > clinicCloseTime)
                    throw new InvalidOperationException($"أوقات العيادة لهذا اليوم من {clinicOpenTime:hh\\:mm} إلى {clinicCloseTime:hh\\:mm}.");







                appointment.EstimatedTime = newUtcTime;
                appointment.Status = AppointmentStatus.Rescheduled;
                _unitOfWork.Reposit<Appointment>().Update(appointment);
                await _unitOfWork.CompleteAsync();


                //await _wpsenderservice.SendStatusAsync(appointment);
                await _patientNotificationService.SendStatusAsync(appointment);

                var allAppointmentsToday = await _unitOfWork.Reposit<Appointment>()
                    .GetAllWithSpecAsync(new AppointmentsWithPatientsSpecification());


                //error here 
                //الترتيب غلط هنا محتاج اخليه يرتب للي بعده بس مش ال في ال  que كله
                // a.QueueNumber > appointment.QueueNumber هتتاكد انها تجيب المواعيد الي بعده
                var sameDayAppointments = allAppointmentsToday
                          .Where(a =>
                              TimeZoneInfo.ConvertTimeFromUtc(a.EstimatedTime.Value, egyptZone).Date == appointmentDate &&
                              a.Id != appointment.Id &&
                              (a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.Rescheduled) &&
                              a.QueueNumber > appointment.QueueNumber)
                          .OrderBy(a => a.QueueNumber)
                          .ToList();

                var currentTime = newEgyptTime.AddMinutes(_settings.Value.MinutesPerCase);

                foreach (var appt in sameDayAppointments)
                {
                    appt.EstimatedTime = TimeZoneInfo.ConvertTimeToUtc(currentTime, egyptZone);
                    appt.Status = AppointmentStatus.Rescheduled;
                    currentTime = currentTime.AddMinutes(_settings.Value.MinutesPerCase);
                    _unitOfWork.Reposit<Appointment>().Update(appt);
                }


                foreach (var appt in sameDayAppointments)
                {
                    //await _wpsenderservice.SendStatusAsync(appt);
                    await _patientNotificationService.SendStatusAsync(appt);
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



        public async Task<AppointmentDto> RescheduleAppointmentToAnotherDayAsync(RescheduleAppointmentRequestDto requestDto)
        {

            #region old code
            //   var appointment = await _unitOfWork.Reposit<Appointment>()
            //.GetEntityWithSpec(new AppointmentByIdWithPatientSpecification(requestDto.AppointmentId));

            //   if (appointment == null)
            //       throw new ArgumentException("Appointment not found");

            //   var archiveAppointment = await _unitOfWork.Reposit<AppointmentArchive>()
            //       .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appointment.Id));

            //   await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            //   try
            //   {
            //       var egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
            //       int minutesPerCase = _settings.Value.MinutesPerCase;

            //       var newDateEgypt = DateTime.SpecifyKind(requestDto.NewTime.Date, DateTimeKind.Unspecified);
            //       var oldDateEgypt = TimeZoneInfo.ConvertTimeFromUtc(appointment.EstimatedTime.Value, egyptZone).Date;
            //       var nowEgypt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone);


            //       if (newDateEgypt == oldDateEgypt)
            //           throw new ArgumentException("لا يمكن تغيير المعاد لنفس اليوم.");

            //       var newDateUtc = TimeZoneInfo.ConvertTimeToUtc(newDateEgypt, egyptZone);

            //       ////add
            //       //var overrideDate = newDateEgypt.Date;


            //       var overrideSetting = await _unitOfWork.Reposit<BookingOverride>()
            //           .GetEntityWithSpec(new BookingOverrideByDateSpecification(newDateEgypt));

            //       if (overrideSetting != null && overrideSetting.IsClosed)
            //           throw new ArgumentException("لا يمكن نقل الموعد ليوم مغلق.");

            //       TimeSpan clinicStart, clinicEnd;

            //       if (overrideSetting != null)
            //       {
            //           clinicStart = overrideSetting.ClinicStartTime.Value;
            //           clinicEnd = overrideSetting.ClinicEndTime.Value;
            //       }
            //       else
            //       {
            //           clinicStart = _settings.Value.ClinicStartTime.Value;
            //           clinicEnd = _settings.Value.ClinicEndTime.Value;
            //       }

            //       var clinicOpenEgypt = newDateEgypt.Add(clinicStart);
            //       var clinicCloseEgypt = newDateEgypt.Add(clinicEnd);

            //       var clinicOpenUtc = TimeZoneInfo.ConvertTimeToUtc(clinicOpenEgypt, egyptZone);
            //       var clinicCloseUtc = TimeZoneInfo.ConvertTimeToUtc(clinicCloseEgypt, egyptZone);

            //       //*** edit
            //       var allAppointments = await _unitOfWork.Reposit<Appointment>()
            //           .GetAllWithSpecAsync(new AppointmentsWithPatientsSpecification());


            //       var spec = new AppointmentByDateSpecification(newDateEgypt);
            //       var sameDayAppointments = await _unitOfWork.Reposit<Appointment>().ListAsync(spec);

            //       sameDayAppointments = sameDayAppointments
            //           .Where(a => a.Id != appointment.Id &&
            //           (a.Status == AppointmentStatus.Waiting ||
            //           a.Status == AppointmentStatus.Rescheduled))
            //           .OrderBy(a => a.EstimatedTime)
            //           .ToList();



            //       //var sameDayAppointments = allAppointments
            //       //    .Where(a =>
            //       //        a.EstimatedTime.HasValue &&
            //       //        TimeZoneInfo.ConvertTimeFromUtc(a.EstimatedTime.Value, egyptZone).Date == newDateEgypt &&
            //       //        (a.Status == AppointmentStatus.Waiting ||
            //       //         a.Status == AppointmentStatus.Rescheduled))
            //       //    .OrderBy(a => a.EstimatedTime)
            //       //    .ToList();



            //       //***

            //       ////////////// Add
            //       DateTime startTimeEgypt;

            //       if (newDateEgypt.Date == nowEgypt.Date)
            //       {
            //           startTimeEgypt = nowEgypt > clinicOpenEgypt? nowEgypt: clinicOpenEgypt;
            //       }
            //       else
            //       {
            //           startTimeEgypt = clinicOpenEgypt;
            //       }

            //       var startTimeUtc = TimeZoneInfo.ConvertTimeToUtc(startTimeEgypt, egyptZone);
            //       /////////

            //       //edit
            //       DateTime newUtcTime;
            //       if (sameDayAppointments.Any())
            //       {
            //           var lastEstimatedUtc = sameDayAppointments.Last().EstimatedTime.Value;
            //           var lastEstimatedEgypt = TimeZoneInfo.ConvertTimeFromUtc(lastEstimatedUtc, egyptZone);
            //           var nextAvailableEgypt = lastEstimatedEgypt.AddMinutes(minutesPerCase);

            //           var finalStartEgypt = new[] 
            //           {clinicOpenEgypt, nextAvailableEgypt }.Max();

            //           newUtcTime = TimeZoneInfo.ConvertTimeToUtc(finalStartEgypt, egyptZone);
            //       }
            //       else
            //       {
            //           newUtcTime = clinicOpenUtc;
            //       }

            //       if (newUtcTime >= clinicCloseUtc)
            //           throw new ArgumentException("لا يوجد مواعيد متاحة في اليوم الجديد.");


            //               var followingAppointments = allAppointments
            //                   .Where(a =>
            //                       a.EstimatedTime.HasValue &&
            //                       TimeZoneInfo.ConvertTimeFromUtc(a.EstimatedTime.Value, egyptZone).Date == newDateEgypt &&
            //                       a.QueueNumber > appointment.QueueNumber &&
            //                       (a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.Rescheduled))
            //                   .OrderBy(a => a.QueueNumber)
            //                   .ToList();

            //               archiveAppointment.EstimatedTime = newUtcTime;
            //               archiveAppointment.Status = AppointmentStatus.Rescheduled;
            //               archiveAppointment.Date = DateTime.SpecifyKind(newDateUtc, DateTimeKind.Utc);
            //               _unitOfWork.Reposit<AppointmentArchive>().Update(archiveAppointment);

            //               appointment.EstimatedTime = newUtcTime;
            //               appointment.Status = AppointmentStatus.Rescheduled;
            //               appointment.Date = DateTime.SpecifyKind(newDateUtc, DateTimeKind.Utc);
            //               _unitOfWork.Reposit<Appointment>().Update(appointment);

            //               var currentTime = TimeZoneInfo.ConvertTimeFromUtc(newUtcTime, egyptZone).AddMinutes(minutesPerCase);

            //               foreach (var appt in followingAppointments)
            //               {
            //                   appt.EstimatedTime = TimeZoneInfo.ConvertTimeToUtc(currentTime, egyptZone);
            //                   appt.Status = AppointmentStatus.Rescheduled;
            //                   appt.Date = DateTime.SpecifyKind(newDateUtc, DateTimeKind.Utc);

            //                   var archiveAppt = await _unitOfWork.Reposit<AppointmentArchive>()
            //                       .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appt.Id));
            //                   archiveAppt.EstimatedTime = appt.EstimatedTime;
            //                   archiveAppt.Status = appt.Status;
            //                   archiveAppt.Date = appt.Date;

            //                   _unitOfWork.Reposit<AppointmentArchive>().Update(archiveAppt);
            //                   _unitOfWork.Reposit<Appointment>().Update(appt);

            //                   currentTime = currentTime.AddMinutes(minutesPerCase);
            //               }

            //               var updatedSameDayAppointments = allAppointments
            //                   .Where(a =>
            //                       a.EstimatedTime.HasValue &&
            //                       TimeZoneInfo.ConvertTimeFromUtc(a.EstimatedTime.Value, egyptZone).Date == newDateEgypt &&
            //                       (a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.Rescheduled))
            //                   .OrderBy(a => a.EstimatedTime)
            //                   .ToList();

            //               int queue = 1;
            //               foreach (var appt in updatedSameDayAppointments)
            //               {
            //                   var archiveAppt = await _unitOfWork.Reposit<AppointmentArchive>()
            //                       .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appt.Id));

            //                   appt.QueueNumber = queue;
            //                   archiveAppt.QueueNumber = queue;

            //                   _unitOfWork.Reposit<AppointmentArchive>().Update(archiveAppt);
            //                   _unitOfWork.Reposit<Appointment>().Update(appt);

            //                   queue++;
            //               }



            //               //await _wpsenderservice.SendStatusAsync(appointment);
            //               await _patientNotificationService.SendStatusAsync(appointment);
            //               foreach (var appt in followingAppointments)
            //                   //await _wpsenderservice.SendStatusAsync(appt);
            //                   await _patientNotificationService.SendStatusAsync(appt);

            //               await _unitOfWork.CompleteAsync();
            //               await transaction.CommitAsync();


            //               return _mapper.Map<AppointmentDto>(appointment);
            //           }
            //           catch
            //           {
            //               await transaction.RollbackAsync();
            //               throw;
            //           }
            //       }
            #endregion  

            var appointment = await _unitOfWork.Reposit<Appointment>()
                .GetEntityWithSpec(new AppointmentByIdWithPatientSpecification(requestDto.AppointmentId));

            if (appointment == null)
                throw new ArgumentException("Appointment not found");

            var archiveAppointment = await _unitOfWork.Reposit<AppointmentArchive>()
                .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appointment.Id));

            await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
                int minutesPerCase = _settings.Value.MinutesPerCase;

                var oldEstimatedTimeUtc = appointment.EstimatedTime.Value;
                var oldEstimatedEgypt = TimeZoneInfo.ConvertTimeFromUtc(oldEstimatedTimeUtc, egyptZone);
                var oldDateEgypt = oldEstimatedEgypt.Date;
                var oldQueue = appointment.QueueNumber;

                var newEgyptTime = DateTime.SpecifyKind(requestDto.NewTime, DateTimeKind.Unspecified);
                var newDateEgypt = newEgyptTime.Date;

                if (newDateEgypt == oldDateEgypt)
                    throw new ArgumentException("لا يمكن تغيير المعاد لنفس اليوم.");

 
                var overrideSetting = await _unitOfWork.Reposit<BookingOverride>()
                    .GetEntityWithSpec(new BookingOverrideByDateSpecification(newDateEgypt));

                if (overrideSetting != null && overrideSetting.IsClosed)
                    throw new ArgumentException("لا يمكن نقل الموعد ليوم مغلق.");

                TimeSpan clinicStart = overrideSetting?.ClinicStartTime ?? _settings.Value.ClinicStartTime.Value;
                TimeSpan clinicEnd = overrideSetting?.ClinicEndTime ?? _settings.Value.ClinicEndTime.Value;


                if (newEgyptTime.TimeOfDay == TimeSpan.Zero)
                {
                    newEgyptTime = newDateEgypt.Add(clinicStart);
                }


                if (newEgyptTime.TimeOfDay < clinicStart || newEgyptTime.TimeOfDay > clinicEnd)
                    throw new ArgumentException($"أوقات العيادة لهذا اليوم من {clinicStart:hh\\:mm} إلى {clinicEnd:hh\\:mm}.");

                var newUtcTime = TimeZoneInfo.ConvertTimeToUtc(newEgyptTime, egyptZone);
                var newDateUtc = DateTime.SpecifyKind(newDateEgypt, DateTimeKind.Utc);

                var allAppointments = await _unitOfWork.Reposit<Appointment>()
                    .GetAllWithSpecAsync(new AppointmentsWithPatientsSpecification());


                var remainingOnOldDay = allAppointments
                    .Where(a => TimeZoneInfo.ConvertTimeFromUtc(a.EstimatedTime.Value, egyptZone).Date == oldDateEgypt &&
                                a.Id != appointment.Id &&
                                (a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.Rescheduled))
                    .OrderBy(a => a.QueueNumber)
                    .ToList();

                var affectedOnOldDay = remainingOnOldDay.Where(a => a.QueueNumber > oldQueue).ToList();

                int shiftQueue = oldQueue;
                DateTime shiftTime = oldEstimatedTimeUtc;
                foreach (var appt in affectedOnOldDay)
                {
                    appt.QueueNumber = shiftQueue;
                    appt.EstimatedTime = shiftTime;
                    //appt.Status = AppointmentStatus.Rescheduled;
                    _unitOfWork.Reposit<Appointment>().Update(appt);

                    var archiveAppt = await _unitOfWork.Reposit<AppointmentArchive>()
                        .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appt.Id));
                    if (archiveAppt != null)
                    {
                        archiveAppt.QueueNumber = shiftQueue;
                        archiveAppt.EstimatedTime = shiftTime;
                        //archiveAppt.Status = AppointmentStatus.Rescheduled;
                        _unitOfWork.Reposit<AppointmentArchive>().Update(archiveAppt);
                    }

                    shiftQueue++;
                    shiftTime = shiftTime.AddMinutes(minutesPerCase);
                }

            
                appointment.EstimatedTime = newUtcTime;
                appointment.Status = AppointmentStatus.Rescheduled;
                appointment.Date = newDateUtc;
                appointment.QueueNumber = 1; 
                _unitOfWork.Reposit<Appointment>().Update(appointment);

                if (archiveAppointment != null)
                {
                    archiveAppointment.EstimatedTime = newUtcTime;
                    archiveAppointment.Status = AppointmentStatus.Rescheduled;
                    archiveAppointment.Date = newDateUtc;
                    archiveAppointment.QueueNumber = 1;
                    _unitOfWork.Reposit<AppointmentArchive>().Update(archiveAppointment);
                }

                await _patientNotificationService.SendStatusAsync(appointment, archiveAppointment?.Id);

                //foreach (var appt in affectedOnOldDay)
                //{
                //    var arch = await _unitOfWork.Reposit<AppointmentArchive>()
                //        .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appt.Id));
                //    await _patientNotificationService.SendStatusAsync(appt, arch?.Id);
                //}

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



        public async Task<bool> RescheduleClinicDayAsync(RescheduleClinicDayRequestDto requestDto)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
                int minutesPerCase = _settings.Value.MinutesPerCase;

                var oldDateEgypt = DateTime.SpecifyKind(requestDto.OldDate.Date, DateTimeKind.Unspecified);
                var newDateEgypt = DateTime.SpecifyKind(requestDto.NewDate.Date, DateTimeKind.Unspecified);

                if (newDateEgypt == oldDateEgypt)
                    throw new ArgumentException("لا يمكن تأجيل العيادة لنفس اليوم.");

                var overrideSetting = await _unitOfWork.Reposit<BookingOverride>()
                    .GetEntityWithSpec(new BookingOverrideByDateSpecification(newDateEgypt));

                if (overrideSetting != null && overrideSetting.IsClosed)
                    throw new ArgumentException("لا يمكن نقل مواعيد العيادة ليوم مغلق.");

                TimeSpan clinicStart = overrideSetting?.ClinicStartTime ?? _settings.Value.ClinicStartTime.Value;
                TimeSpan clinicEnd = overrideSetting?.ClinicEndTime ?? _settings.Value.ClinicEndTime.Value;

                var clinicOpenEgypt = newDateEgypt.Add(clinicStart);
                var clinicCloseEgypt = newDateEgypt.Add(clinicEnd);
                var clinicCloseUtc = TimeZoneInfo.ConvertTimeToUtc(clinicCloseEgypt, egyptZone);

                var newDateUtc = DateTime.SpecifyKind(newDateEgypt, DateTimeKind.Utc);


                var allAppointments = await _unitOfWork.Reposit<Appointment>()
                    .GetAllWithSpecAsync(new AppointmentsWithPatientsSpecification());

                var appointmentsToReschedule = allAppointments
                    .Where(a => TimeZoneInfo.ConvertTimeFromUtc(a.EstimatedTime.Value, egyptZone).Date == oldDateEgypt &&
                                (a.Status == AppointmentStatus.Waiting || a.Status == AppointmentStatus.Rescheduled))
                    .OrderBy(a => a.QueueNumber)
                    .ToList();

                if (!appointmentsToReschedule.Any())
                    throw new ArgumentException("لا توجد مواعيد نشطة في اليوم القديم لتأجيلها.");


                DateTime currentEgyptTime = clinicOpenEgypt;
                int newQueue = 1;

                foreach (var appointment in appointmentsToReschedule)
                {
                    var newUtcTime = TimeZoneInfo.ConvertTimeToUtc(currentEgyptTime, egyptZone);

           
                    if (newUtcTime > clinicCloseUtc)
                        throw new ArgumentException("الوقت المتاح في اليوم الجديد لا يكفي لجميع الحالات، يرجى اختيار يوم آخر أو تقليل عدد الحالات.");

                    appointment.EstimatedTime = newUtcTime;
                    appointment.Status = AppointmentStatus.Rescheduled;
                    appointment.Date = newDateUtc;
                    appointment.QueueNumber = newQueue;
                    _unitOfWork.Reposit<Appointment>().Update(appointment);

       
                    var archiveAppointment = await _unitOfWork.Reposit<AppointmentArchive>()
                        .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appointment.Id));

                    if (archiveAppointment != null)
                    {
                        archiveAppointment.EstimatedTime = newUtcTime;
                        archiveAppointment.Status = AppointmentStatus.Rescheduled;
                        archiveAppointment.Date = newDateUtc;
                        archiveAppointment.QueueNumber = newQueue;
                        _unitOfWork.Reposit<AppointmentArchive>().Update(archiveAppointment);
                    }

  
                    currentEgyptTime = currentEgyptTime.AddMinutes(minutesPerCase);
                    newQueue++;
                }

   
                foreach (var appointment in appointmentsToReschedule)
                {
                    var archiveAppt = await _unitOfWork.Reposit<AppointmentArchive>()
                        .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appointment.Id));

                    await _patientNotificationService.SendStatusAsync(appointment, archiveAppt?.Id);
                }

                await _unitOfWork.CompleteAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }




        public async Task CloseClinicDayAsync(ChangeClinicDayStatusDto request)
        {
            TimeZoneInfo egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
            var bookingOverrideRepo = _unitOfWork.Reposit<BookingOverride>();
            var appointmentRepo = _unitOfWork.Reposit<Appointment>();

            DateTime dateEgypt = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Unspecified);
            DateTime dateUtc = TimeZoneInfo.ConvertTimeToUtc(dateEgypt, egyptZone);
            if (dateUtc.Date < dateEgypt.Date)
                dateUtc = dateUtc.AddDays(1);

            var overrideEntity = await bookingOverrideRepo
                .GetEntityWithSpec(new BookingOverrideByDateSpecification(dateUtc));

            if (overrideEntity == null)
            {
                overrideEntity = new BookingOverride
                {
                    Date = dateUtc,
                    ClinicStartTime = _settings.Value.ClinicStartTime,
                    ClinicEndTime = _settings.Value.ClinicEndTime,
                    IsClosed = request.IsClosed
                };
                await bookingOverrideRepo.AddAsync(overrideEntity);
            }
            else
            {
                overrideEntity.IsClosed = request.IsClosed;
                bookingOverrideRepo.Update(overrideEntity);
            }

            if (request.IsClosed)
            {
                var todaysAppointments = await appointmentRepo
                    .ListAsync(new AppointmentByDateSpecification(dateUtc));

                foreach (var appointment in todaysAppointments)
                {
                    appointment.Status = AppointmentStatus.Cancelled;

                    //await _wpsenderservice.SendStatusAsync(appointment);
                    await _patientNotificationService.SendStatusAsync(appointment);
                    appointmentRepo.Delete(appointment);
                }
            }

            await _unitOfWork.CompleteAsync();
        }


        public async Task ToggleClinicDayStatusAsync(ChangeClinicDayStatusDto request)
        {
            TimeZoneInfo egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
            var bookingOverrideRepo = _unitOfWork.Reposit<BookingOverride>();
            DateTime dateEgypt = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Unspecified);
            DateTime dateUtc = TimeZoneInfo.ConvertTimeToUtc(dateEgypt, egyptZone);
            if (dateUtc.Date < dateEgypt.Date)
                dateUtc = dateUtc.AddDays(1);


            var overrideEntity = await bookingOverrideRepo
                .GetEntityWithSpec(new BookingOverrideByDateSpecification(dateUtc));
            if (overrideEntity == null)
            {
                overrideEntity = new BookingOverride
                {
                    Date = dateUtc,
                    ClinicStartTime = _settings.Value.ClinicStartTime,
                    ClinicEndTime = _settings.Value.ClinicEndTime,
                    IsClosed = request.IsClosed
                };
                await bookingOverrideRepo.AddAsync(overrideEntity);
            }
            else
            {
                overrideEntity.IsClosed = request.IsClosed;
                bookingOverrideRepo.Update(overrideEntity);
            }
            await _unitOfWork.CompleteAsync();
        }






        //_Not Used Yet

        // _____Added to app_____
        public async Task<BookingOverrideDto> UpsertBookingOverrideAsync(BookingOverrideDto request)
        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                TimeZoneInfo egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

                //var dateEgyptLocal = DateTime.SpecifyKind(request.Date.Value.Date, DateTimeKind.Unspecified);
                //var dateUtc = TimeZoneInfo.ConvertTimeToUtc(dateEgyptLocal, egyptZone);

                // الكود بقا متلصم
                DateTime dateEgyptLocal = DateTime.SpecifyKind(request.Date.Value.Date, DateTimeKind.Unspecified);
                DateTime dateUtc = TimeZoneInfo.ConvertTimeToUtc(dateEgyptLocal, egyptZone);
                if (dateUtc.Date < dateEgyptLocal.Date)
                    dateUtc = dateUtc.AddDays(1);


                var bookingOverrideRepo = _unitOfWork.Reposit<BookingOverride>();
                var appointmentRepo = _unitOfWork.Reposit<Appointment>();
                var archiveRepo = _unitOfWork.Reposit<AppointmentArchive>();

                var existingOverride = await bookingOverrideRepo
                    .GetEntityWithSpec(new BookingOverrideByDateSpecification(dateUtc));

                BookingOverride entity;
                if (existingOverride != null)
                {
                    existingOverride.ClinicStartTime = request.ClinicStartTime;
                    existingOverride.ClinicEndTime = request.ClinicEndTime;
                    existingOverride.Date = dateUtc;
                    bookingOverrideRepo.Update(existingOverride);
                    entity = existingOverride;
                }
                else
                {
                    entity = new BookingOverride
                    {
                        Date = dateUtc,
                        ClinicStartTime = request.ClinicStartTime,
                        ClinicEndTime = request.ClinicEndTime
                    };
                    await bookingOverrideRepo.AddAsync(entity);
                }

                var todaysAppointments = await appointmentRepo.ListAsync(new AppointmentByDateSpecification(dateUtc));

                var todaysAppointmentsSameLocalDay = todaysAppointments
                    .Where(a => a.EstimatedTime.HasValue &&
                                TimeZoneInfo.ConvertTimeFromUtc(a.EstimatedTime.Value, egyptZone).Date == dateEgyptLocal.Date)
                    .ToList();

                if (!todaysAppointmentsSameLocalDay.Any())
                {
                    await _unitOfWork.CompleteAsync();
                    await transaction.CommitAsync();

                    var dtoEmpty = _mapper.Map<BookingOverrideDto>(entity);
                    dtoEmpty.Date = TimeZoneInfo.ConvertTimeFromUtc(entity.Date.Value, egyptZone);
                    return dtoEmpty;
                }

                var orderedAppts = todaysAppointmentsSameLocalDay
                    .OrderBy(a => a.QueueNumber)
                    .ToList();

                var minutesPerCase = _settings.Value.MinutesPerCase;

                var clinicStart = (request.ClinicStartTime ?? _settings.Value.ClinicStartTime).Value;
                var clinicEnd = (request.ClinicEndTime ?? _settings.Value.ClinicEndTime).Value;
                var clinicOpenLocal = dateEgyptLocal.Add(clinicStart);
                var clinicCloseLocal = dateEgyptLocal.Add(clinicEnd);

                var nowEgyptLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, egyptZone);
                bool isCurrentDay = dateEgyptLocal == nowEgyptLocal.Date;

                var slots = new List<DateTime>();
                var curSlot = clinicOpenLocal;
                while (curSlot < clinicCloseLocal)
                {
                    if (!isCurrentDay || curSlot >= nowEgyptLocal)
                        slots.Add(curSlot);

                    curSlot = curSlot.AddMinutes(minutesPerCase);
                }

                var occupied = new HashSet<DateTime>();
                int slotIndex = 0;

                var toNotify = new List<Appointment>();

                for (int i = 0; i < orderedAppts.Count; i++)
                {
                    var appt = orderedAppts[i];

                    DateTime? chosenSlot = null;
                    while (slotIndex < slots.Count && occupied.Contains(slots[slotIndex]))
                        slotIndex++;

                    if (slotIndex < slots.Count)
                    {
                        chosenSlot = slots[slotIndex];
                        occupied.Add(chosenSlot.Value);
                        slotIndex++; 
                    }

                    if (chosenSlot.HasValue)
                    {
                        var newUtc = TimeZoneInfo.ConvertTimeToUtc(chosenSlot.Value, egyptZone);

                        if (!appt.EstimatedTime.HasValue || appt.EstimatedTime.Value != newUtc)
                        {
                            appt.EstimatedTime = newUtc;
                            appt.Date = dateUtc;
                            appt.Status = AppointmentStatus.Rescheduled;
                            appointmentRepo.Update(appt);

                            var archiveAppt = await archiveRepo
                                .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appt.Id));
                            if (archiveAppt != null)
                            {
                                archiveAppt.EstimatedTime = appt.EstimatedTime;
                                archiveAppt.Date = appt.Date;
                                archiveAppt.Status = appt.Status;
                                archiveRepo.Update(archiveAppt);
                            }

                            toNotify.Add(appt);
                        }
                    }
                    else
                    {
                        appt.Status = AppointmentStatus.Cancelled;

                        var archiveAppt = await archiveRepo
                            .GetEntityWithSpec(new AppointmentArchiveByAppointmentIdSpecification(appt.Id));
                        if (archiveAppt != null)
                        {
                            archiveRepo.Delete(archiveAppt);
                        }

                        //try
                        //{
                        //    await _wpsenderservice.SendStatusAsync(appt);
                        //}
                        //catch { }

                        await _patientNotificationService.SendStatusAsync(appt);
                        appointmentRepo.Delete(appt);
                    }
                }

                foreach (var ap in toNotify)
                {
                    try
                    {
                        await _patientNotificationService.SendStatusAsync(ap);
                    }
                    catch { }
                }





                await _unitOfWork.CompleteAsync();
                await transaction.CommitAsync();

                //foreach (var ap in toNotify)
                //{
                //    try
                //    { 
                //        await _wpsenderservice.SendStatusAsync(ap);
                //    }
                //    catch { }
                //}

                var dto = _mapper.Map<BookingOverrideDto>(entity);
                dto.Date = TimeZoneInfo.ConvertTimeFromUtc(entity.Date.Value, egyptZone);
                return dto;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }





        // for sending notifications to patients after rescheduling or cancelling appointments
        //private async Task QueueNotificationsAsync(IEnumerable<Appointment> appointments)
        //{
        //    var list = appointments.ToList();
        //    if (list.Count == 0) return;

        //    var archives = await _unitOfWork.Reposit<AppointmentArchive>()
        //        .ListAsync(new AppointmentArchivesByAppointmentIdsSpecification(list.Select(a => a.Id).ToList()));

        //    var archiveIds = archives
        //        .Where(a => a.AppointmentId.HasValue)
        //        .ToDictionary(a => a.AppointmentId!.Value, a => a.Id);

        //    foreach (var a in list)
        //    {
        //        int? archiveId = archiveIds.TryGetValue(a.Id, out var id) ? id : null;
        //        await _patientNotificationService.SendStatusAsync(a, archiveId);
        //    }
        //}




    }

}

