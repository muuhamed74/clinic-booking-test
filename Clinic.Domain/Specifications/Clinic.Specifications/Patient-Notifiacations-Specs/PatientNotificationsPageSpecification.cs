using Clinic.Domain.Entities.Patient_Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifiacations_Specs
{
    public class PatientNotificationsPageSpecification : BaseSpecification<PatientNotification>
    {
        public PatientNotificationsPageSpecification(int patientId, int skip, int take)
       : base(n => n.PatientId == patientId)
        {
            ApplyOrderByDesc(n => n.CreatedAt);
            ApplyPaging(skip, take);
        }
    }
}
