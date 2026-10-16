using Clinic.Domain.Entities.Patient_Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clinic.Repo.Configurations
{
    public class PatientNotificationConfiguration : IEntityTypeConfiguration<PatientNotification>
    {
        public void Configure(EntityTypeBuilder<PatientNotification> builder)
        {
            builder.ToTable("PatientNotifications");
            builder.HasKey(n => n.Id);

            builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
            builder.Property(n => n.Message).IsRequired().HasMaxLength(1000);

            builder.HasOne(n => n.Patient)
                   .WithMany()
                   .HasForeignKey(n => n.PatientId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(n => n.AppointmentArchive)
                   .WithMany()
                   .HasForeignKey(n => n.AppointmentArchiveId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(n => new { n.PatientId, n.CreatedAt });
            builder.HasIndex(n => new { n.PatientId, n.IsRead });
        }
    }
}
