using Clinic.Domain.Entities.Patient_Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifiacations_Specs
{
    public class PatientNotificationByIdSpecification : BaseSpecification<PatientNotification>
    {
        public PatientNotificationByIdSpecification(int id, int patientId)
        : base(n => n.Id == id && n.PatientId == patientId) { }
    }
}
