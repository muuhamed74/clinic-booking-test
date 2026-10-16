using Clinic.Domain.Entities;
using Clinic.Domain.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Specifications.Clinic.Specifications.Patient_Notifications
{
    public class AppointmentByPatientIdAndDateSpecification : BaseSpecification<Appointment>
    {
        public AppointmentByPatientIdAndDateSpecification(int patientId, DateTime date)
            : base(a => a.PatientId == patientId && a.Date == date && a.Status != AppointmentStatus.Cancelled) 
        { }
    }
}
