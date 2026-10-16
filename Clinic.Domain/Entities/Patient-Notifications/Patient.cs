using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Entities.Patient_Notifications
{
    public class Patient : BaseEntity
    {
        public string Name { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    }
}
