using Clinic.Domain.Entities;
using Clinic.Domain.Entities.Enums;
using Clinic.Domain.Entities.Patient_Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_Abstraction
{
    public interface IPatientNotificationService
    {
        Task SendStatusAsync(Appointment appointment, int? appointmentArchiveId = null);

        Task SendReminderAsync(Appointment appointment, int? appointmentArchiveId = null);
        Task DispatchNotificationAsync(PatientNotification notification);
    }
}
