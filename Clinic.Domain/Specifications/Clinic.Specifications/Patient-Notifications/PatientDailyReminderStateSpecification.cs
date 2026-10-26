using Clinic.Domain.Entities.Enums;
using Clinic.Domain.Entities.Patient_Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifications
{
    public class PatientDailyReminderStateSpecification : BaseSpecification<PatientNotification>
    {
        public PatientDailyReminderStateSpecification(int patientId, DateTime dayStartUtc)
            : base(n => n.PatientId == patientId
                     && n.CreatedAt >= dayStartUtc
                     && (n.Type == NotificationType.Reminder
                      || n.Type == NotificationType.Rescheduling))
        { }
    }
}
