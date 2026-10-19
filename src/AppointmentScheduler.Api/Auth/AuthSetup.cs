using AppointmentScheduler.Application.Auth;
using AppointmentScheduler.Application.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

namespace AppointmentScheduler.Api.Auth;

internal static class AuthSetup
{
    /// <summary>Role policies, 401/403 as ProblemDetails (<c>unauthorized</c> / <c>forbidden</c>) and <see cref="ICurrentUser"/>.</summary>
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
                return WriteProblemAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized", "unauthorized",
                    "Authentication is required.");
            },
            OnForbidden = ctx => WriteProblemAsync(ctx.HttpContext, StatusCodes.Status403Forbidden, "Forbidden", "forbidden",
                "You are not allowed to access this resource."),
        });

        return services.AddHttpContextAccessor().AddScoped<ICurrentUser, HttpCurrentUser>();
    }

    private static async Task WriteProblemAsync(HttpContext http, int status, string title, string code, string detail)
    {
        http.Response.StatusCode = status;
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail, Extensions = { ["code"] = code } };
        await http.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem });
    }
}
