using Clinic.Domain.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Entities.Patient_Notifications
{
    public class PatientNotification : BaseEntity
    {
        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;

        public int? AppointmentArchiveId { get; set; }
        public AppointmentArchive? AppointmentArchive { get; set; }

        public NotificationType Type { get; set; }   
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public DateTime? ReadAt { get; set; }
    }
}
