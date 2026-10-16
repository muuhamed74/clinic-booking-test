using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.DTOs
{
    public class AppointmentDto
    {
        public int Id { get; set; }
        public string PatientName { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public DateTime Date { get; set; }
        public int QueueNumber { get; set; }
        public DateTime EstimatedTime { get; set; }
        public string Status { get; set; } = null!;
        public string AppointmentType { get; set; } = null!;



        public string? PatientToken { get; set; }
    }
}
