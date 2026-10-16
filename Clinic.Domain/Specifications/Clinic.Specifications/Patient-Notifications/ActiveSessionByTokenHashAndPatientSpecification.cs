using Clinic.Domain.Entities.Patient_Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifications
{
    public class ActiveSessionByTokenHashAndPatientSpecification : BaseSpecification<PatientSession>
    {
        public ActiveSessionByTokenHashAndPatientSpecification(string tokenHash, int patientId)
        : base(s => s.TokenHash == tokenHash && s.PatientId == patientId && s.IsActive) { }
    }
}
