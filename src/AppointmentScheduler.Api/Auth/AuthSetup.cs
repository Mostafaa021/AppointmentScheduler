using AppointmentScheduler.Api.Errors;
using AppointmentScheduler.Application.Auth;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace AppointmentScheduler.Api.Auth;

internal static class AuthSetup
{
    /// <summary>Role policies, 401/403 as ApiResponse (<c>UNAUTHORIZED</c> / <c>FORBIDDEN</c>) and <see cref="ICurrentUser"/>.</summary>
    public static IServiceCollection AddApiAuth(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Policies.FrontDesk, p => p.RequireRole(Roles.Admin, Roles.Receptionist))
            .AddPolicy(Policies.ScheduleReader, p => p.RequireRole(Roles.Admin, Roles.Receptionist, Roles.Doctor));

        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o => o.Events = new JwtBearerEvents
        {
            OnChallenge = ctx =>
            {
                ctx.HandleResponse(); // replace the default empty 401
                ctx.Response.Headers.WWWAuthenticate = "Bearer";
                return ApiErrorWriter.WriteAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized,
                    "Authentication is required.");
            },
            OnForbidden = ctx => ApiErrorWriter.WriteAsync(ctx.HttpContext, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden,
                "You are not allowed to access this resource."),
        });

        return services.AddHttpContextAccessor().AddScoped<ICurrentUser, HttpCurrentUser>();
    }
}
