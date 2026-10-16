using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clinic.Domain.DTOs;
using Clinic.Domain.Entities;

namespace Service_Abstraction
{
    public interface IBookingService
    {
        Task<AppointmentDto> BookAppointmentAsync(AppointmentRequestDto request);
        Task<AppointmentDto?> GetAppointmentByPhoneAsync(string phoneNumber, DateTime date);
        Task<AppointmentDto> CancelByPatientAsync(string phone, DateTime date);
        Task<int> GetTotalPatientsCountAsync();
    }
}
