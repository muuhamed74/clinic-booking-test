using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clinic.Domain.Entities.Enums;

namespace Clinic.Domain.DTOs
{
    public class AppointmentRequestDto
    {
        public string Name { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public DateTime BookingDate { get; set; }
        public AppointmentType AppointmentType { get; set; }


        public string? PatientToken { get; set; }
    }
}
