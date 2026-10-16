using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Entities.Patient_Notifications
{
    public class PushSubscription : BaseEntity
    {
        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;
        public string Endpoint { get; set; } = null!;
        public string P256dh { get; set; } = null!;
        public string Auth { get; set; } = null!;
    }
}
