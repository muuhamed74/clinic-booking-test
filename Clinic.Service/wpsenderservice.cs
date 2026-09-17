using Clinic.Domain.Entities;
using Clinic.Domain.Entities.Enums;
using Clinic.Domain.Repositories;
using Microsoft.Extensions.Options;
using Service_Abstraction;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Twilio.Types;

namespace Clinic.Service
{
    public class wpsenderservice : Iwpsenderservice
    {
        //private readonly IWhatsAppProvider _messageProvider;
        private readonly WhatsAppSettings _settings;
        private readonly IUnitOfWork _unitOfWork;
        private readonly NotificationSettings _notificationSettings;
        //private readonly HttpClient _httpClient;

        public wpsenderservice(
            //IWhatsAppProvider whatsAppProvider,
            IOptions<WhatsAppSettings> settings,
            IUnitOfWork unitOfWork,
            IOptions<NotificationSettings> notificationOptions
            //HttpClient httpClient
            )
        {
            //_messageProvider = whatsAppProvider;
            _settings = settings.Value;
            _unitOfWork = unitOfWork;
            _notificationSettings = notificationOptions.Value;
            //_httpClient = httpClient;
        }

        public async Task SendStatusAsync(Appointment appointment)
        {
            switch (appointment.Status)
            {
                case AppointmentStatus.Waiting:
                    if (_notificationSettings.SendBookingConfirmation)
                        await SendInternalAsync(appointment, NotificationType.BookingConfirmation);
                    break;

                case AppointmentStatus.Cancelled:
                    if (_notificationSettings.SendCancellation)
                        await SendInternalAsync(appointment, NotificationType.Cancellation);
                    break;

                case AppointmentStatus.Rescheduled:
                    await SendInternalAsync(appointment, NotificationType.Rescheduling);
                    break;
            }
        }

        public async Task SendReminderAsync(Appointment appointment)
        {
            if (_notificationSettings.SendReminder)
                await SendInternalAsync(appointment, NotificationType.Reminder);
        }

        private async Task SendInternalAsync(Appointment appointment, NotificationType type)
        {
            TimeZoneInfo egyptZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
            //var estimatedTime = TimeZoneInfo.ConvertTimeFromUtc(appointment.EstimatedTime.Value, egyptZone);
            //var dateEgypt = TimeZoneInfo.ConvertTimeFromUtc(appointment.Date.Value, egyptZone);
            //var arCulture = new CultureInfo("ar-EG");

            var utcTime = DateTime.SpecifyKind(appointment.EstimatedTime.Value, DateTimeKind.Utc);

            var egyptFullTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, egyptZone);

            var arCulture = new CultureInfo("ar-EG");

            string formattedDate = egyptFullTime.ToString("yyyy/MM/dd", arCulture);
            string formattedHour = egyptFullTime.ToString("hh:mm tt", arCulture);


            string messageBody = type switch
            {
                NotificationType.BookingConfirmation =>
                    $"أهلا بيك: {appointment.PatientName}\n" +
                    $"موعدك يوم {formattedDate} الساعة {formattedHour}\n" +
                    $"ودورك رقم {appointment.QueueNumber}\n" +
                    $"ولمتابعة الدور: https://amiramohsenclinic.com/info",


                NotificationType.Cancellation =>
                    $"تنويه: تم إلغاء حجز {appointment.PatientName} للتأخر أو ظرف طارئ.",

                NotificationType.Rescheduling =>
                     $"أهلا بيك: {appointment.PatientName}\n" +
                     $"تم تغيير ميعادك ليصبح يوم {formattedDate} الساعة {formattedHour}، وذلك لوجود حالة طارئة.",


                // Not used in the current version, but kept for future use
                NotificationType.Reminder =>
                    $"يُرجى الحضور لعيادة د/ أميرة محسن لتفادي إلغاء الحجز، موعدك الساعة {formattedHour} ودورك رقم {appointment.QueueNumber}.",

                _ => throw new ArgumentOutOfRangeException()
            };

            var notification = await SaveNotificationAsync(appointment.Id, appointment.Phone, messageBody, type);

            // willn't use it anymore for sending messages directly,
            // instead we will use the notification table to send messages in a background service

            //await _messageProvider.SendAsync(appointment.Phone, messageBody);
            //notification.IsSent = true;
            //notification.SentAt = DateTime.UtcNow;
            //await _unitOfWork.CompleteAsync();
        }

        private async Task<Notification> SaveNotificationAsync(int appointmentId, string phoneNumber, string message, NotificationType type)
        {
            var notification = new Notification
            {
                AppointmentId = appointmentId,
                Channel = "WhatsApp",
                Message = message,
                IsSent = false,
                Type = type,
                // new property 
                PhoneNumber = phoneNumber,
            };

            await _unitOfWork.Reposit<Notification>().AddAsync(notification);
            await _unitOfWork.CompleteAsync();
            return notification;
        }



    }
}
