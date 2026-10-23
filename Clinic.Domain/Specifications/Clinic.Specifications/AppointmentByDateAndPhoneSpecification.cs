using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clinic.Domain.Entities;
using Clinic.Domain.Entities.Enums;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Clinic.Domain.Specifications.Clinic.Specifications
{
    public class AppointmentByDateAndPhoneSpecification : BaseSpecification<Appointment>
    {
        public AppointmentByDateAndPhoneSpecification(string phone, DateTime utcDate)
         : base(a => a.Date.HasValue &&
                a.Date.Value >= DateTime.SpecifyKind(utcDate.Date, DateTimeKind.Utc) &&
                a.Date.Value < DateTime.SpecifyKind(utcDate.Date.AddDays(1), DateTimeKind.Utc) &&
                a.Status != AppointmentStatus.Cancelled &&
                a.Phone == phone)
        {
        }
    }
}
