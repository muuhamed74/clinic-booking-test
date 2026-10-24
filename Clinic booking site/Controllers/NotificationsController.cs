using Clinic.Domain.DTOs;
using Clinic.Domain.Entities.Patient_Notifications;
using Clinic.Domain.Repositories;
using Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifiacations_Specs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Service_Abstraction;

namespace Clinic_booking_site.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPatientSessionResolver _sessionResolver;

        public NotificationsController(IUnitOfWork unitOfWork, IPatientSessionResolver sessionResolver)
        {
            _unitOfWork = unitOfWork;
            _sessionResolver = sessionResolver;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string token, [FromQuery] int skip = 0, [FromQuery] int take = 20)
        {
            var patient = await _sessionResolver.ResolveAsync(token);
            if (patient == null)
                return Unauthorized(new { message = "التوكين غير صالح." });

            var items = await _unitOfWork.Reposit<PatientNotification>()
                .ListAsync(new PatientNotificationsPageSpecification(patient.Id, skip, take));

            var unreadCount = await _unitOfWork.Reposit<PatientNotification>()
                .GetCountWithSpecAsync(new UnreadNotificationCountSpecification(patient.Id));

            var result = items.Select(n => new
            {
                n.Id,
                Type = n.Type.ToString(),
                n.Title,
                n.Message,
                n.CreatedAt,
                n.IsRead
            });

            return Ok(new { unreadCount, items = result });
        }

        [HttpPatch("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id, [FromQuery] string token)
        {
            var patient = await _sessionResolver.ResolveAsync(token);
            if (patient == null)
                return Unauthorized(new { message = "التوكين غير صالح." });

            var notification = await _unitOfWork.Reposit<PatientNotification>()
                .GetEntityWithSpec(new PatientNotificationByIdSpecification(id, patient.Id));

            if (notification == null)
                return NotFound();

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                _unitOfWork.Reposit<PatientNotification>().Update(notification);
                await _unitOfWork.CompleteAsync();
            }

            return NoContent();
        }

        [HttpPatch("read-all")]
        public async Task<IActionResult> MarkAllAsRead([FromQuery] string token)
        {
            var patient = await _sessionResolver.ResolveAsync(token);
            if (patient == null)
                return Unauthorized(new { message = "التوكين غير صالح." });

            var unread = await _unitOfWork.Reposit<PatientNotification>()
                .ListAsync(new UnreadNotificationCountSpecification(patient.Id)); 

            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = DateTime.UtcNow;
                _unitOfWork.Reposit<PatientNotification>().Update(n);
            }

            await _unitOfWork.CompleteAsync();
            return NoContent();
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> SaveSubscription([FromBody] PushSubscriptionRequestDto dto)
        {
            var patient = await _sessionResolver.ResolveAsync(dto.PatientToken);
            if (patient == null)
                return Unauthorized(new { message = "التوكين غير صالح." });

            var existingSubscription = await _unitOfWork.Reposit<PushSubscription>()
                .GetEntityWithSpec(new PushSubscriptionByEndpointSpecification(dto.Endpoint));

            if (existingSubscription != null)
            {
                existingSubscription.PatientId = patient.Id;
                existingSubscription.P256dh = dto.P256dh;
                existingSubscription.Auth = dto.Auth;

                _unitOfWork.Reposit<PushSubscription>().Update(existingSubscription);
            }
            else
            {
                var newSubscription = new PushSubscription
                {
                    PatientId = patient.Id,
                    Endpoint = dto.Endpoint,
                    P256dh = dto.P256dh,
                    Auth = dto.Auth
                };

                await _unitOfWork.Reposit<PushSubscription>().AddAsync(newSubscription);
            }
            await _unitOfWork.CompleteAsync();

            return Ok(new { message = "تم حفظ اشتراك الإشعارات بنجاح." });
        }










    }
}
