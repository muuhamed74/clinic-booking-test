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
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");

            builder.HasKey(n => n.Id);

            builder.Property(n => n.Channel)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(n => n.Message)
                   .IsRequired()
                   .HasMaxLength(500);

            // new property 

            builder.Property(n => n.PhoneNumber)
              .IsRequired()
              .HasMaxLength(20);

            builder.HasIndex(n => n.IsSent);


            // another new property

            builder.Property(n => n.SentBySessionId)
             .HasMaxLength(50);
        }
    }
}
