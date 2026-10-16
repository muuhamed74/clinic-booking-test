using Clinic.Domain.Entities.Patient_Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_Abstraction
{
    public interface IPatientSessionResolver
    {
        Task<Patient?> ResolveAsync(string? rawToken);
    }
}
