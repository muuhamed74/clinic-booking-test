using Clinic.Domain.Entities;
using Clinic.Domain.Entities.Enums;
using Clinic.Domain.Repositories;
using Clinic.Domain.Specifications.Clinic.Specifications;
using Clinic.Repo.Repositories;
using Clinic.Service;
using Hangfire;
using Microsoft.AspNetCore.Routing;
using Service_Abstraction;

namespace Clinic_booking_site.Helpers.Hangfire
{
    public class ReminderJob
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ReminderJob> _logger;
        private readonly Iwpsenderservice _wpsenderservice;
        private readonly IPatientNotificationService _patientNotificationService;

        public ReminderJob(IUnitOfWork unitOfWork,
            INotificationService notificationService,
            ILogger<ReminderJob> logger,
            Iwpsenderservice wpsenderservice,
            IPatientNotificationService patientNotificationService)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _logger = logger;
            _wpsenderservice = wpsenderservice;
            _patientNotificationService = patientNotificationService;
        }


        [DisableConcurrentExecution(timeoutInSeconds: 5)]
        [AutomaticRetry(Attempts = 0)]
        public async Task SendRemindersAsync()
        {
            try
            {
                DateTime nowUtc = DateTime.UtcNow;
                DateTime fromUtc = nowUtc;
                DateTime toUtc = nowUtc.AddMinutes(35);


                var spec = new AppointmentsForReminderSpecification(fromUtc, toUtc);
                var appointments = await _unitOfWork.Reposit<Appointment>().ListAsync(spec);

                _logger.LogInformation($"Querying appointments between {fromUtc:HH:mm:ss} and {toUtc:HH:mm:ss} (UTC)");

                foreach (var appointment in appointments)
                {

                    var existingReminderSpec = new NotificationsByAppointmentIdAndTypeSpecification(appointment.Id, NotificationType.Reminder);
                    var existingReminder = await _unitOfWork.Reposit<Notification>().GetEntityWithSpec(existingReminderSpec);

                    if (existingReminder != null && existingReminder.IsSent)
                        continue;

                    //await _wpsenderservice.SendReminderAsync(appointment);
                    await _patientNotificationService.SendStatusAsync(appointment);


                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReminderJob: Error during sending reminders.");
            }
        }
    }
}

