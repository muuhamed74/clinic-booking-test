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
    public class PatientSessionConfiguration : IEntityTypeConfiguration<PatientSession>
    {
        public void Configure(EntityTypeBuilder<PatientSession> builder)
        {
            builder.ToTable("PatientSessions");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.TokenHash).IsRequired().HasMaxLength(64);
            builder.HasIndex(s => s.TokenHash).IsUnique();

            builder.HasOne(s => s.Patient)
                   .WithMany()
                   .HasForeignKey(s => s.PatientId)
                   .OnDelete(DeleteBehavior.Cascade);
            {
            }

        }
    }
}
