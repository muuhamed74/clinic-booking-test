using Clinic.Domain.DTOs;
using Clinic_booking_site.Helpers.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Service_Abstraction;

namespace Clinic_booking_site.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public AppointmentsController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }


        [HttpPost("book")]
        public async Task<IActionResult> Book([FromBody] AppointmentRequestDto request)
        {
            var appointment = await _bookingService.BookAppointmentAsync(request);
            return Ok(appointment);
        }

        [HttpGet("track")]
        public async Task<IActionResult> Track([FromQuery] string phoneNumber, [FromQuery] DateTime date)
        {
            var appointment = await _bookingService.GetAppointmentByPhoneAsync(phoneNumber, date);
            if (appointment == null)
                return NotFound(new ApiResponce( 400, "لا يوجد حجز بهذا الرقم في هذا اليوم."));

            return Ok(appointment);
        }

        [HttpPost("cancel-by-patient")]
        public async Task<ActionResult<AppointmentDto>> CancelByPatientAsync([FromQuery] string phoneNumber, [FromQuery] DateTime date)
        {

            var result = await _bookingService.CancelByPatientAsync(phoneNumber, date);
            if (result == null)
                return NotFound(new ApiResponce(400, "لا يوجد حجز بهذا الرقم في هذا اليوم."));

            return Ok(result);
        }



        [HttpGet("patients/count")]
        [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
        public async Task<ActionResult<int>> GetTotalPatientsCount()
        {
            try
            {
                var count = await _bookingService.GetTotalPatientsCountAsync();
                return Ok(count);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ أثناء جلب عدد المرضى", error = ex.Message });
            }
        }


    }
}
