using Clinic.Domain.Entities.Patient_Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifiacations_Specs
{
    public class PushSubscriptionsByPatientSpecification : BaseSpecification<PushSubscription>
    {
        public PushSubscriptionsByPatientSpecification(int patientId)
            : base(x => x.PatientId == patientId)
        {
        }
    }
}
