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
                var nowUtc = DateTime.UtcNow;
                var toUtc = nowUtc.AddMinutes(35);

                var appointments = await _unitOfWork.Reposit<Appointment>()
                    .ListAsync(new AppointmentsForReminderSpecification(nowUtc, toUtc));

                _logger.LogInformation("Querying appointments between {From:HH:mm:ss} and {To:HH:mm:ss} (UTC)", nowUtc, toUtc);

                foreach (var appointment in appointments)
                {
                    try
                    {
                        await _patientNotificationService.SendReminderAsync(appointment);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Reminder failed for patient {PatientId}", appointment.PatientId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReminderJob: Error during sending reminders.");
            }
        }
    }
}

