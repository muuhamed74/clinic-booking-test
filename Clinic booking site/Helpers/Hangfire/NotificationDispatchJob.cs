using Clinic.Domain.Entities;
using Clinic.Domain.Repositories;
using Clinic.Domain.Specifications.Clinic.Specifications;
using Hangfire;
using Microsoft.Extensions.Options;
using Service_Abstraction;
using System.ComponentModel;
using System.Reflection;

namespace Clinic_booking_site.Helpers.Hangfire
{
    public class NotificationDispatchJob
    {


        private readonly IUnitOfWork _unitOfWork;
        private readonly IWhatsAppProvider _messageProvider;
        private readonly NotificationSettings _notificationSettings;
        private readonly ILogger<NotificationDispatchJob> _logger;
        private readonly WhatsAppSettings _whatsAppSettings;

        public NotificationDispatchJob(
            IUnitOfWork unitOfWork,
            IWhatsAppProvider messageProvider,
            IOptions<NotificationSettings> notificationOptions,
            ILogger<NotificationDispatchJob> logger,
            IOptions<WhatsAppSettings> whatsAppOptions
            )
        {
            _unitOfWork = unitOfWork;
            _messageProvider = messageProvider;
            _notificationSettings = notificationOptions.Value;
            _logger = logger;
            _whatsAppSettings = whatsAppOptions.Value;
        }

        [DisableConcurrentExecution(timeoutInSeconds: 5)]
        [AutomaticRetry(Attempts = 0)]
        public async Task DispatchPendingNotificationsAsync()
        {
            //var spec = new PendingNotificationsSpecification();
            //var pending = await _unitOfWork.Reposit<Notification>().ListAsync(spec);
            //var notification = pending.FirstOrDefault();

            //if (notification == null)
            //{
            //    _logger.LogInformation("No pending notifications found.");
            //    return;
            //}

            //_logger.LogInformation($"Dispatching {pending.Count} pending notification(s).");

            //var sender = await SelectSenderAsync();
            //if (sender == null)
            //{
            //    _logger.LogWarning("WhatsApp sender reached daily cap or is missing.");
            //    return;
            //}


            //    try
            //    {
            //        await _messageProvider.SendAsync(sender.SessionId, notification.PhoneNumber, notification.Message);

            //        notification.IsSent = true;
            //        notification.SentAt = DateTime.UtcNow;
            //        notification.SentBySessionId = sender.SessionId;
            //        _unitOfWork.Reposit<Notification>().Update(notification);
            //        await _unitOfWork.CompleteAsync();
            //    }
            //    catch (Exception ex)
            //    {
            //        _logger.LogError(ex, $"Failed to send notification {notification.Id} to {notification.PhoneNumber}");
            //    }

            //    var delaySeconds = Random.Shared.Next(
            //        _notificationSettings.MinDelaySeconds,
            //        _notificationSettings.MaxDelaySeconds + 1);
            //    _logger.LogInformation($"Waiting for {delaySeconds} seconds before next notification...");

            //    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            //    _logger.LogInformation("Delay finished.");
            try
            {
                var spec = new PendingNotificationsSpecification();
                var pending = await _unitOfWork.Reposit<Notification>().ListAsync(spec);
                var notification = pending.FirstOrDefault();

                if (notification == null)
                {
                    _logger.LogInformation("No pending notifications found.");
                    return;
                }

                _logger.LogInformation($"Dispatching {pending.Count} pending notification(s).");

                var sender = await SelectSenderAsync();
                if (sender == null)
                {
                    _logger.LogWarning("WhatsApp sender reached daily cap or is missing.");
                    return;
                }

                await _messageProvider.SendAsync(sender.SessionId, notification.PhoneNumber, notification.Message);

                notification.IsSent = true;
                notification.SentAt = DateTime.UtcNow;
                notification.SentBySessionId = sender.SessionId;
                _unitOfWork.Reposit<Notification>().Update(notification);
                await _unitOfWork.CompleteAsync();

                var delaySeconds = Random.Shared.Next(
                    _notificationSettings.MinDelaySeconds,
                    _notificationSettings.MaxDelaySeconds + 1);
                _logger.LogInformation($"Waiting for {delaySeconds} seconds before next notification...");

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                _logger.LogInformation("Delay finished.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NotificationDispatchJob: Unexpected error during dispatch.");
            }


        }

        


        private async Task<WhatsAppSenderSettings?> SelectSenderAsync()
        {
            //    var todayStartUtc = DateTime.UtcNow.Date;
            //    var candidates = new List<(WhatsAppSenderSettings Sender, int SentToday)>();

            //    foreach (var sender in _whatsAppSettings.Senders)
            //    {
            //        var spec = new NotificationsSentBySenderTodaySpecification(sender.SessionId, todayStartUtc);
            //        var count = await _unitOfWork.Reposit<Notification>().GetCountWithSpecAsync(spec);

            //        if (count < sender.DailyCap)
            //            candidates.Add((sender, count));
            //    }

            //    return candidates.OrderBy(c => c.SentToday).FirstOrDefault().Sender;
            //}

            var sender = _whatsAppSettings.Senders.FirstOrDefault();
            if (sender == null)
            {
                _logger.LogWarning("No senders configured in WhatsAppSettings.");
                return null;
            }

            var todayStartUtc = DateTime.UtcNow.Date;
            var spec = new NotificationsSentBySenderTodaySpecification(sender.SessionId, todayStartUtc);
            var count = await _unitOfWork.Reposit<Notification>().GetCountWithSpecAsync(spec);

            if (count < sender.DailyCap)
            {
                return sender;
            }

            _logger.LogWarning($"Sender {sender.SessionId} reached daily cap ({sender.DailyCap}).");
            return null;
        }
    }
}

