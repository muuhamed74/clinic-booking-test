using Clinic.Domain.Entities.Patient_Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifiacations_Specs
{
    public class PushSubscriptionByEndpointSpecification : BaseSpecification<PushSubscription>
    {
        public PushSubscriptionByEndpointSpecification(string endpoint)
            : base(s => s.Endpoint == endpoint)
        {
        }
    }
}
