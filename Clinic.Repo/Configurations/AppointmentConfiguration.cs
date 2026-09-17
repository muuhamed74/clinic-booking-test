using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Clinic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Clinic.Repo.Configurations
{
    public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
    {
        public void Configure(EntityTypeBuilder<Appointment> builder)
        {
            builder.ToTable("Appointments");

            builder.HasKey(a => a.Id);

            builder.Property(a => a.Date)
                .HasColumnType("timestamptz");

            builder.Property(a => a.QueueNumber)
                   .IsRequired();

            builder.Property(a => a.EstimatedTime)
                .HasColumnType("timestamptz");


            builder.Property(a => a.Status)
                   .HasConversion<string>()
                   .HasMaxLength(20);


            // from Cascade to SetNull
            builder.HasMany(a => a.Notifications)
                   .WithOne(n => n.Appointment)
                   .HasForeignKey(n => n.AppointmentId)
                   .OnDelete(DeleteBehavior.SetNull);


            builder.Property(a => a.AppointmentType)
             .HasConversion<string>()
             .HasMaxLength(20)
             .IsRequired();
        }
    }
}
