using Clinic.Domain.Entities;
using Clinic.Domain.Entities.Patient_Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifications
{
    public class PatientByPhoneSpecification : BaseSpecification<Patient>
    {
        public PatientByPhoneSpecification(string phone) : base(p => p.Phone == phone) { }
    }
}
