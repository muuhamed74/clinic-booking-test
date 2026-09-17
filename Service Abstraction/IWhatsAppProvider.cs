using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_Abstraction
{
    public interface IWhatsAppProvider
    {
        Task SendAsync(string sessionId , string phoneNumber, string message);
    }
}
