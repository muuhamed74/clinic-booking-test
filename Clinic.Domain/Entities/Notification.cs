using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clinic.Domain.Entities.Enums;

namespace Clinic.Domain.Entities
{
    public class Notification : BaseEntity
    {
        public int? AppointmentId { get; set; }
        public Appointment? Appointment { get; set; } = null!;
        public string Channel { get; set; } = "WhatsApp";
        public string Message { get; set; } = null!;
        public NotificationType Type { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsSent { get; set; } = false; public DateTime? SentAt { get; set; }

        // new property
        public string? PhoneNumber { get; set; } = null!;

        // another new property
        public string? SentBySessionId { get; set; }
    }
}
