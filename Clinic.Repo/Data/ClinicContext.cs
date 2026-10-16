using Clinic.Domain.Entities;
using Clinic.Domain.Entities.Patient_Notifications;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Repo.Data
{
    public class ClinicContext : IdentityDbContext<Appuser>
    {
        public ClinicContext(DbContextOptions<ClinicContext> options) : base(options)
        {
        }

        public DbSet<AppointmentArchive> AppointmentArchives { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<BookingOverride> BookingOverride { get; set; }
        public DbSet<DoctorAnnouncement> DoctorAnnouncements { get; set; }
        public DbSet<PatientSession> PatientSessions { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<PatientNotification> PatientNotifications { get; set; }
        public DbSet<PushSubscription> PushSubscriptions { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClinicContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
