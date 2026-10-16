using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.DTOs
{
    public class RescheduleClinicDayRequestDto
    {
        public DateTime OldDate { get; set; }
        public DateTime NewDate { get; set; }
    }
}
