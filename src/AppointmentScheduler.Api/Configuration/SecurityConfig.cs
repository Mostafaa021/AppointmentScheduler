using AppointmentScheduler.Api.Auth;

namespace AppointmentScheduler.Api.Configuration;

public static class SecurityConfig
{
    extension(WebApplicationBuilder builder)
    {
        /// <summary>
        /// Role policies, JWT 401/403 bodies and <c>ICurrentUser</c> (spec 004), HSTS for non-Development, and CORS limited
        /// to <c>Cors:AllowedOrigins</c>. Never any origin: the refresh cookie must not be usable cross-site.
        /// </summary>
        public WebApplicationBuilder AddSecurityConfiguration()
        {
            var services = builder.Services;

            services.AddApiAuth();
            services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(365));
            services.AddCors(o => o.AddDefaultPolicy(p => p
                .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                .AllowAnyHeader()
                .AllowAnyMethod()));

            return builder;
        }
    }
}
