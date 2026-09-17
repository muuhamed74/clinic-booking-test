using Clinic.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Specifications.Clinic.Specifications
{
    public class NotificationsSentBySenderTodaySpecification : BaseSpecification<Notification>
    {
        public NotificationsSentBySenderTodaySpecification(string sessionId, DateTime todayStartUtc)
            : base(n => n.SentBySessionId == sessionId &&
                        n.SentAt.HasValue &&
                        n.SentAt.Value >= todayStartUtc)
        {
        }
    }
}
