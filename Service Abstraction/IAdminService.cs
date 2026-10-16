using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clinic.Domain.DTOs;

namespace Service_Abstraction
{
    public interface IAdminService
    {
        Task<(int Count, List<AppointmentDto> Appointments)> GetAllAppointmentsWithCountAsync();
        Task<(int Count, List<AppointmentDto> Appointments)> GetArchivedAppointmentsByDateAsync(DateTime? date = null);
        Task<int> DeleteArchivedAppointmentsByDateAsync(DateTime? date = null);
        Task<AppointmentDto> CancelAppointmentAsync(int appointmentId);
        Task<AppointmentDto> CompleteAppointmentAsync(int appointmentId);
        Task<AppointmentDto> RescheduleAppointmentAsync(RescheduleAppointmentRequestDto requestDto);
        Task<AppointmentDto> RescheduleAppointmentToAnotherDayAsync(RescheduleAppointmentRequestDto requestDto);
        Task<BookingOverrideDto> UpsertBookingOverrideAsync(BookingOverrideDto request);
        Task CloseClinicDayAsync(ChangeClinicDayStatusDto request);
        Task ToggleClinicDayStatusAsync(ChangeClinicDayStatusDto request);


        Task<bool> RescheduleClinicDayAsync(RescheduleClinicDayRequestDto requestDto);
    }
}
