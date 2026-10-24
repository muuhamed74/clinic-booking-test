using Clinic.Domain.Entities;
using Clinic.Domain.Entities.Enums;
using Clinic.Domain.Entities.Patient_Notifications;
using Clinic.Domain.Repositories;
using Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifiacations_Specs;
using Clinic.Service.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Service_Abstraction;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Twilio.TwiML.Messaging;
using WebPush;

namespace Clinic.Service.Patien_Notofcations
{
    public class PatientNotificationService : IPatientNotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly NotificationSettings _notificationSettings;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IConfiguration _configuration;
        private readonly IServiceScopeFactory _scopeFactory;

        private static readonly TimeZoneInfo EgyptZone =
            TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

        public PatientNotificationService(
            IUnitOfWork unitOfWork,
            IOptions<NotificationSettings> notificationOptions,
            IHubContext<NotificationHub> hubContext,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory)
        {
            _unitOfWork = unitOfWork;
            _notificationSettings = notificationOptions.Value;
            _hubContext = hubContext;
            _configuration = configuration;
            _scopeFactory = scopeFactory;
        }

        public async Task SendStatusAsync(Appointment appointment, int? appointmentArchiveId = null)
        {

            #region old code
            //switch (appointment.Status)
            //{
            //    case AppointmentStatus.Waiting:

            //        if (_notificationSettings.SendBookingConfirmation)
            //        {
            //            await SendInternalAsync(appointment, appointmentArchiveId, NotificationType.BookingConfirmation);
            //        }

            //        break;


            //    case AppointmentStatus.Cancelled:

            //        if (_notificationSettings.SendCancellation)
            //        {
            //            await SendInternalAsync(appointment, appointmentArchiveId, NotificationType.Cancellation);
            //        }

            //        break;


            //    case AppointmentStatus.Rescheduled:

            //        {
            //            await SendInternalAsync(appointment, appointmentArchiveId, NotificationType.Rescheduling);
            //        }

            //        break;
            #endregion

            PatientNotification? notification = null;

            switch (appointment.Status)
            {
                case AppointmentStatus.Waiting when _notificationSettings.SendBookingConfirmation:
                    notification = await SendInternalAsync(appointment, appointmentArchiveId, NotificationType.BookingConfirmation);
                    break;

                case AppointmentStatus.Cancelled when _notificationSettings.SendCancellation:
                    notification = await SendInternalAsync(appointment, appointmentArchiveId, NotificationType.Cancellation);
                    break;

                case AppointmentStatus.Rescheduled:
                    notification = await SendInternalAsync(appointment, appointmentArchiveId, NotificationType.Rescheduling);
                    break;
            }
            if (notification != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var scopedNotificationService = scope.ServiceProvider.GetRequiredService<IPatientNotificationService>();
                        await scopedNotificationService.DispatchNotificationAsync(notification);
                    }
                    catch(Exception ex)
                    {
                    }
                });
            }
        }


        public async Task SendReminderAsync(Appointment appointment, int? appointmentArchiveId = null)
        {
            if (_notificationSettings.SendReminder)
            {
                await SendInternalAsync(appointment, appointmentArchiveId, NotificationType.Reminder);
            }
        }


        private async Task<PatientNotification?>SendInternalAsync(Appointment appointment, int? appointmentArchiveId, NotificationType type)
        {
            if (appointment.PatientId is not int patientId)
                return null;

            var (title, message) = BuildContent(appointment,type);

            var notification = new PatientNotification
            {
                PatientId = patientId,
                AppointmentArchiveId = appointmentArchiveId,
                Type = type,
                Title = title,
                Message = message,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork
                .Reposit<PatientNotification>()
                .AddAsync(notification);

            return notification;
            //await _unitOfWork.CompleteAsync();
        }



        public async Task DispatchNotificationAsync(PatientNotification notification)
        {
            #region
            //var connectionIds = NotificationHub.GetConnections(notification.PatientId);

            //if (connectionIds != null && connectionIds.Any())
            //{

            //    foreach (var connId in connectionIds)
            //    {
            //        await _hubContext.Clients.Client(connId).SendAsync("ReceiveNotification", new
            //        {
            //            notification.Title,
            //            notification.Message,
            //            notification.CreatedAt
            //        });
            //    }
            //}
            //else
            //{
            //    await SendWebPushAsync(notification.PatientId, notification.Title, notification.Message);
            //}
            #endregion
            var connectionIds = NotificationHub.GetConnections(notification.PatientId);

            if (connectionIds != null && connectionIds.Any())
            {
                Console.WriteLine($"[Notification Debug] Patient {notification.PatientId} has active SignalR connections. Trying to send via SignalR...");
                try
                {
                    foreach (var connId in connectionIds)
                    {
                        await _hubContext.Clients.Client(connId).SendAsync("ReceiveNotification", new
                        {
                            notification.Title,
                            notification.Message,
                            notification.CreatedAt
                        });
                    }
                    Console.WriteLine($"[Notification Debug] Successfully sent via SignalR.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Notification Error] SignalR send failed: {ex.Message}. Falling back to WebPush...");
                    await SendWebPushAsync(notification.PatientId, notification.Title, notification.Message);
                }
            }
            else
            {
                Console.WriteLine($"[Notification Debug] Patient {notification.PatientId} is offline (No SignalR connections). Sending via WebPush...");
                await SendWebPushAsync(notification.PatientId, notification.Title, notification.Message);
            }
        }


        private async Task SendWebPushAsync(int patientId, string title, string message)
        {
            #region
            var subscriptions = await _unitOfWork.Reposit<Clinic.Domain.Entities.Patient_Notifications.PushSubscription>()
                .ListAsync(new PushSubscriptionsByPatientSpecification(patientId));

            if (!subscriptions.Any()) return;

            var vapidPublicKey = _configuration["VapidKeys:PublicKey"];
            var vapidPrivateKey = _configuration["VapidKeys:PrivateKey"];
            var vapidSubject = _configuration["VapidKeys:Subject"];

            if (string.IsNullOrEmpty(vapidPublicKey) || string.IsNullOrEmpty(vapidPrivateKey))
                return;

            var vapidDetails = new VapidDetails(vapidSubject, vapidPublicKey, vapidPrivateKey);
            var webPushClient = new WebPushClient();

            var payload = System.Text.Json.JsonSerializer.Serialize(new { title, body = message });

            foreach (var sub in subscriptions)
            {
                try
                {
                    var pushSubscription = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                    await webPushClient.SendNotificationAsync(pushSubscription, payload, vapidDetails);
                }
                catch (Exception)
                {
                }
            }
            #endregion
            try
            {
                var webPushSubscriptions = await _unitOfWork.Reposit<Clinic.Domain.Entities.Patient_Notifications.PushSubscription>()
                    .ListAsync(new PushSubscriptionsByPatientSpecification(patientId));

                if (!webPushSubscriptions.Any())
                {
                    Console.WriteLine($"[WebPush Debug] No push subscriptions found in DB for PatientId: {patientId}");
                    return;
                }

                var publicKey = _configuration["VapidKeys:PublicKey"];
                var privateKey = _configuration["VapidKeys:PrivateKey"];
                var subjectKey = _configuration["VapidKeys:Subject"];

                if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey))
                {
                    Console.WriteLine("[WebPush Error] VAPID Keys are missing in configuration!");
                    return;
                }

                var vapidObj = new VapidDetails(subjectKey, publicKey, privateKey);
                var pushClient = new WebPushClient();

                var jsonPayload = System.Text.Json.JsonSerializer.Serialize(new { title, body = message });

                foreach (var subItem in webPushSubscriptions)
                {
                    try
                    {
                        var pushSub = new WebPush.PushSubscription(subItem.Endpoint, subItem.P256dh, subItem.Auth);
                        await pushClient.SendNotificationAsync(pushSub, jsonPayload, vapidObj);
                        Console.WriteLine($"[WebPush Success] Push notification sent successfully to endpoint: {subItem.Endpoint}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WebPush Sub Error] Failed for endpoint {subItem.Endpoint}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebPush General Error]: {ex.Message}");
            }
        }































        private static (string Title, string Message) BuildContent(Appointment appointment,NotificationType type)
        {
            var utcTime = DateTime.SpecifyKind(
                appointment.EstimatedTime!.Value,
                DateTimeKind.Utc);

            var egyptTime = TimeZoneInfo.ConvertTimeFromUtc(
                utcTime,
                EgyptZone);

            var arCulture = new CultureInfo("ar-EG");

            var formattedDate =
                egyptTime.ToString("yyyy/MM/dd", arCulture);

            var formattedHour =
                egyptTime.ToString("hh\\:mm tt", arCulture);


            return type switch
            {
                NotificationType.BookingConfirmation => (
                    "تم تأكيد الحجز",
                    $"أهلا بيك: {appointment.PatientName}\n" +
                    $"موعدك يوم {formattedDate} الساعة {formattedHour}\n" +
                    $"ودورك رقم {appointment.QueueNumber}"
                ),


                NotificationType.Cancellation => (
                    "تم إلغاء الحجز",
                    $"تنويه: تم إلغاء حجز " +
                    $"{appointment.PatientName} " +
                    $"للتأخر أو ظرف طارئ."
                ),


                NotificationType.Rescheduling => (
                    "تم تغيير الميعاد",
                    $"أهلا بيك: {appointment.PatientName}\n" +
                    $"تم تغيير ميعادك ليصبح يوم {formattedDate} " +
                    $"الساعة {formattedHour}، " +
                    $"وذلك لوجود حالة طارئة."
                ),


                NotificationType.Reminder => (
                    "تذكير بالموعد",
                    $"يُرجى الحضور لعيادة د/ أميرة محسن " +
                    $"لتفادي إلغاء الحجز، " +
                    $"موعدك الساعة {formattedHour} " +
                    $"ودورك رقم {appointment.QueueNumber}."
                ),


                _ => throw new ArgumentOutOfRangeException(nameof(type),type,null)
            };
        }
    }
}
