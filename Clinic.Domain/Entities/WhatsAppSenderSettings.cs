using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Entities
{
    public class WhatsAppSenderSettings
    {
        public string SessionId { get; set; } = null!;
        public int DailyCap { get; set; } = 300;
    }
}
