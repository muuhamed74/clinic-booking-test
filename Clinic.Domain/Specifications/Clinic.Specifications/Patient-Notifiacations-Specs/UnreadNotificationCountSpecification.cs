using Clinic.Domain.Entities.Patient_Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifiacations_Specs
{
    public class UnreadNotificationCountSpecification : BaseSpecification<PatientNotification>
    {
        public UnreadNotificationCountSpecification(int patientId)
            : base(n => n.PatientId == patientId && !n.IsRead) { }
    }
}
