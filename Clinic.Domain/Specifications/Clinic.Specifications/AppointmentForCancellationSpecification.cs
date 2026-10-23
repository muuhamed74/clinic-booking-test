using Clinic.Domain.Entities;
using Clinic.Domain.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Specifications.Clinic.Specifications
{
    public class AppointmentForCancellationSpecification : BaseSpecification<Appointment>
    {
        public AppointmentForCancellationSpecification(string phone, DateTime utcStart, DateTime utcEnd)
            : base(a => a.Date.HasValue &&
                   a.Date.Value >= utcStart &&
                   a.Date.Value < utcEnd &&
                   a.Status != AppointmentStatus.Cancelled &&
                   a.Phone == phone)
        {
        }
    }
}
