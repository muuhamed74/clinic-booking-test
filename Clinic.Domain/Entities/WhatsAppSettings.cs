using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Domain.Entities
{
    public class WhatsAppSettings
    {
        public string ApiUrl { get; set; }
        public string ApiToken { get; set; }
        //public string SessionId { get; set; }
       
        
        // new property 
        public List<WhatsAppSenderSettings> Senders { get; set; } = new();


    }
}
