using AppointmentScheduler.Api.Auth;

namespace AppointmentScheduler.Api.Configuration;

internal static class SecurityConfiguration
{
    /// <summary>
    /// Role policies + JWT 401/403 bodies + <c>ICurrentUser</c> (spec 004), HSTS for non-Development, and CORS limited to
    /// <c>Cors:AllowedOrigins</c> (never any origin: the refresh cookie must not be usable cross-site).
    /// </summary>
    public static WebApplicationBuilder AddSecurityConfiguration(this WebApplicationBuilder builder)
    {
        builder.Services.AddApiAuth();
        builder.Services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(365));
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
            .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()));
        return builder;
    }
}
