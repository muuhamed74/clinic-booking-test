using System.Diagnostics.Metrics;
using Clinic.Domain.DTOs;
using Clinic.Service;
using Clinic_booking_site.Helpers.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Service_Abstraction;

namespace Clinic_booking_site.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly ILoginService _loginService;
        private readonly IAdminService _adminService;

        public DashboardController(ILoginService loginService,
            IAdminService adminService)
        {
            _loginService = loginService;
            _adminService = adminService;
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto loginRequest)
        {
            var result = await _loginService.LoginAsync(loginRequest);

            if (result == null)
                return Unauthorized(new ApiResponce(401 ,"اسم المستخدم أو كلمة المرور غير صحيحة."));

            return Ok(result);
        }



        [Authorize(Roles = "Admin")]
        [HttpGet("appointments")]
        public async Task<IActionResult> GetAllAppointments()
        {

            var (count, appointments) = await _adminService.GetAllAppointmentsWithCountAsync();

            if (!appointments.Any())
                return NotFound(new ApiResponce(400, "مفيش حجوزات النهارده"));

            return Ok(new 
            {
                Count = count,
                Appointments = appointments 
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("appointments/archive")]
        public async Task<IActionResult> GetArchivedAppointments([FromQuery] DateTime? date)
        {
            var (count, archivedAppointments) = await _adminService.GetArchivedAppointmentsByDateAsync(date);

            if (!archivedAppointments.Any())
                return NotFound("مفيش حجوزات في الأرشيف لليوم المطلوب");

            return Ok(new
            {
                Count = count,
                Appointments = archivedAppointments
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("appointments/archive")]
        public async Task<IActionResult> DeleteArchivedAppointments([FromQuery] DateTime? date)
        {
            var deletedCount = await _adminService.DeleteArchivedAppointmentsByDateAsync(date);

            if (deletedCount == 0)
                return NotFound("مفيش حجوزات في الأرشيف لليوم المطلوب");

            return Ok(new
            {
                Message = $"تم مسح {deletedCount} حجز من الأرشيف لليوم {date?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd")}"
            });
        }


        [Authorize(Roles = "Admin")]
        [HttpPut("appointments/cancel")]
        public async Task<IActionResult> CancelAppointment([FromBody] UpdateAppointmentStatusDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Invalid data" });

            var updatedAppointment = await _adminService.CancelAppointmentAsync(dto.AppointmentId);
            return Ok(updatedAppointment);
        }


        [Authorize(Roles = "Admin")]
        [HttpPut("appointments/complete")]
        public async Task<IActionResult> CompleteAppointment([FromBody] UpdateAppointmentStatusDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Invalid data" });

            var updatedAppointment = await _adminService.CompleteAppointmentAsync(dto.AppointmentId);
            return Ok(updatedAppointment);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("reschedule")]
        public async Task<ActionResult<AppointmentDto>> Reschedule([FromBody] RescheduleAppointmentRequestDto requestDto)
        {
            Console.WriteLine($"Received Reschedule request for appointmentId: {requestDto.AppointmentId} at {DateTime.UtcNow}");
            var result = await _adminService.RescheduleAppointmentAsync(requestDto);
            return Ok(result);
        }

        // dont forget to add authorize attribute
        [HttpPut("reschedule-other-day")]
        public async Task<ActionResult<AppointmentDto>> RescheduleAppointmentToAnotherDay([FromBody] RescheduleAppointmentRequestDto requestDto)
        {
            var result = await _adminService.RescheduleAppointmentToAnotherDayAsync(requestDto);
            return Ok(result);
        }


        [Authorize(Roles = "Admin")]
        [HttpPost("set-clinic-hours")]
        public async Task<ActionResult<BookingOverrideDto>> SetClinicHours([FromBody] BookingOverrideDto request)
        {
            var result = await _adminService.UpsertBookingOverrideAsync(request);
            return Ok(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("change-day-status")]
        public async Task<IActionResult> CloseClinicDay([FromBody] ChangeClinicDayStatusDto request)
        {
            await _adminService.CloseClinicDayAsync(request);
            string message = request.IsClosed
                   ? "تم إغلاق اليوم بنجاح وإلغاء جميع الحجوزات."
                   : "تم فتح اليوم بنجاح.";

            return Ok(new { Message = message });
        }


        [Authorize(Roles = "Admin")]
        [HttpPost("toggle-clinic-day-status")]
        public async Task<IActionResult> ToggleClinicDayStatus([FromBody] ChangeClinicDayStatusDto request)
        {
             await _adminService.ToggleClinicDayStatusAsync(request);
            string message = request.IsClosed
                ? "تم إغلاق اليوم بنجاح وتم منع الحجوزات الجديدة."
                : "تم فتح اليوم بنجاح وأصبح متاحًا للحجوزات.";
            return Ok(new { Message = message });
        }


        [Authorize(Roles = "Admin")]
        [HttpPut("reschedule-clinic-day")]
        public async Task<IActionResult> RescheduleClinicDay([FromBody] RescheduleClinicDayRequestDto requestDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { message = "Invalid data" });

            var result = await _adminService.RescheduleClinicDayAsync(requestDto);

            return Ok(new
            {
                Message = $"تم تأجيل مواعيد العيادة من يوم {requestDto.OldDate:yyyy-MM-dd} إلى يوم {requestDto.NewDate:yyyy-MM-dd} بنجاح وإرسال الإشعارات للمرضى."
            });
        }

    }
}
