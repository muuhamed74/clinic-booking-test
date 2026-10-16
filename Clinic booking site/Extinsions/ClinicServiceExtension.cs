using Clinic.Domain.Entities;
using Clinic.Domain.Repositories;
using Clinic.Domain.Specifications;
using Clinic.Repo.Data;
using Clinic.Repo.Repositories;
using Clinic.Service;
using Clinic.Service.Notifications_Providers;
using Clinic.Service.Patien_Notofcations;
using Clinic_booking_site.Helpers.Hangfire;
using Clinic_booking_site.Helpers.Hashing;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Service_Abstraction;

namespace Clinic_booking_site.Extinsions
{
    public static class ClinicServiceExtension
    {
        public static async Task<IServiceCollection> AddClinicServices(this IServiceCollection services, IConfiguration config)
        {
            services.AddDbContext<ClinicContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

            services.AddHangfire(options =>
            {
                options.UsePostgreSqlStorage(storageOptions =>
                {
                    storageOptions.UseNpgsqlConnection(config.GetConnectionString("DefaultConnection"));     
                    
                });
            });



            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped(typeof(ISpecification<>), typeof(BaseSpecification<>));
            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IAdminService, AdminService>();
            services.AddScoped<IDoctorAnnouncementService, DoctorAnnouncementService>();

            //Background Services
            //services.AddScoped<ReminderJob>();
            services.AddScoped<NotificationDispatchJob>();

            // Local Notification Provider
            services.AddScoped<IPatientNotificationService, PatientNotificationService>();
            services.AddScoped<IPatientSessionResolver, PatientSessionResolver>();


            services.AddScoped<Iwpsenderservice, wpsenderservice>();
            services.AddHttpClient<IWhatsAppProvider, WhatsAppProvider>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
            });

            // For Development:
            services.AddScoped<IMessageProvider, NewSmsProvider>();

            //// For Production:
            //services.AddScoped<IWhatsAppProvider, MetaWhatsAppProvider>();


            // Hosted Services 
            // Replaced with hangfire recurring job
            //services.AddHostedService<CleanupAppointmentsService>();
            //services.AddHostedService<ReminderBackgroundService>();


            services.Configure<WhatsAppSettings>(config.GetSection("WhatsAppSettings"));
            //services.Configure<TwilioSettings>(config.GetSection("TwilioSettings"));
            services.Configure<NotificationSettings>(config.GetSection("NotificationSettings"));
            services.Configure<BookingSettings>(config.GetSection("BookingSettings"));
            //services.Configure<SmsSettings>(config.GetSection("SmsSettings"));





            // Identity Services


            services.AddScoped<ILoginService, LoginService>();
            services.AddScoped<ITokenService, TokenService>();

            services.AddIdentity<Appuser, IdentityRole>()
                           .AddEntityFrameworkStores<ClinicContext>()
                           .AddDefaultTokenProviders();

            services.AddAuthentication(Options =>
            {
                Options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                Options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })


            .AddJwtBearer(Options =>
            {
              Options.TokenValidationParameters = new TokenValidationParameters
              {
              ValidateIssuer = true,
              ValidateAudience = true,
              ValidateLifetime = true,
              ValidateIssuerSigningKey = true,
              ValidIssuer = config["JWT:Issuer"],
              ValidAudience = config["JWT:Audience"],
              IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(config["JWT:Key"])),
              ClockSkew = TimeSpan.Zero // No clock skew for token validation
              };

            });




            return services;

        }
    }
}
