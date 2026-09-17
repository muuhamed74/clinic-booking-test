using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Specifications.Clinic.Specifications
{
    public class PendingNotificationsSpecification : BaseSpecification<Notification>
    {
        public PendingNotificationsSpecification()
        : base(n => !n.IsSent)
        {
            ApplyOrderBy(n => n.Id); 
        }
    }
}
