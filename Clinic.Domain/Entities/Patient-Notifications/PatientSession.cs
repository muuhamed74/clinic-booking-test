using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Entities.Patient_Notifications
{
    public class PatientSession : BaseEntity
    {
        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;
        public string TokenHash { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSeenAt { get; set; }
        public bool IsActive { get; set; } = true;

    }
}
