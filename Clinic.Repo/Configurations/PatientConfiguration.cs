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
    public class PatientConfiguration : IEntityTypeConfiguration<Patient>
    {
        public void Configure(EntityTypeBuilder<Patient> builder)
    {
         builder.ToTable("Patients");
         builder.HasKey(p => p.Id);

              builder.Property(p => p.Phone).IsRequired().HasMaxLength(20);
               builder.HasIndex(p => p.Phone).IsUnique();

               builder.Property(p => p.Name).HasMaxLength(100);
    }

    }
}
