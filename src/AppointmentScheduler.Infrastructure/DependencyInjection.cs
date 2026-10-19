using System.Text;
using AppointmentScheduler.Application.Auth;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Infrastructure.Auth;
using AppointmentScheduler.Infrastructure.BackgroundJobs;
using AppointmentScheduler.Infrastructure.Persistence;
using AppointmentScheduler.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AppointmentScheduler.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Persistence (PostgreSQL, seed), Identity + JWT bearer, and Hangfire. Called once from the Api host.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Scheduler")
            ?? throw new InvalidOperationException("Connection string 'Scheduler' is missing.");

        return services
            .AddPersistence(configuration, connectionString)
            .AddIdentityAndJwt(configuration)
            .AddBackgroundJobs(configuration, connectionString);
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        var seed = configuration.GetValue("Seed:Enabled", true);

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
            if (!seed) return;

            var schedule = sp.GetRequiredService<IOptions<ClinicOptions>>().Value.ToDaySchedule();
            var clock = sp.GetRequiredService<TimeProvider>();
            // AC-004-26: demo users only in Development, even when Seed:Enabled is true elsewhere.
            var includeDemoUsers = sp.GetRequiredService<IHostEnvironment>().IsDevelopment();
            options.UseAsyncSeeding((db, _, ct) => DbSeeder.SeedAsync(db, schedule, clock.GetUtcNow(), includeDemoUsers, ct));
            options.UseSeeding((db, _) =>
                DbSeeder.SeedAsync(db, schedule, clock.GetUtcNow(), includeDemoUsers, CancellationToken.None).GetAwaiter().GetResult());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        return services;
    }

    private static IServiceCollection AddIdentityAndJwt(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityCore<AppUser>(o =>
            {
                // Backstop copy of the validator's policy (PasswordPolicy; the 128 maximum is validator-only).
                o.Password.RequiredLength = PasswordPolicy.MinLength;
                o.Password.RequireLowercase = true;
                o.Password.RequireUppercase = true;
                o.Password.RequireDigit = true;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredUniqueChars = 1;
                o.User.RequireUniqueEmail = true;
                o.User.AllowedUserNameCharacters = ""; // user name = e-mail, already validated; don't reject e.g. o'neil@…
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.Section))
            .PostConfigure(o =>
            {
                if (string.IsNullOrEmpty(o.SigningKey)) o.SigningKey = configuration[JwtOptions.SigningKeyFallback] ?? "";
            })
            .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= JwtOptions.MinKeyBytes,
                $"Jwt:SigningKey (or {JwtOptions.SigningKeyFallback}) must be at least {JwtOptions.MinKeyBytes} bytes.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience are required.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, TimeProvider>((o, jwt, clock) =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = TokenIssuer.SigningKey(jwt.Value),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RequireExpirationTime = true,
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    // IdentityModel's default lifetime check reads the system clock; use TimeProvider (ADR-0005 amendment).
                    LifetimeValidator = (notBefore, expires, _, _) =>
                    {
                        var now = clock.GetUtcNow().UtcDateTime;
                        return expires is { } exp && now <= exp + JwtOptions.ClockSkew
                            && (notBefore is not { } nbf || nbf - JwtOptions.ClockSkew <= now);
                    },
                };
            });

        return services
            .AddSingleton<TokenIssuer>()
            .AddScoped<IAuthService, IdentityAuthService>()
            .AddScoped<IUserAdminService, IdentityUserAdminService>();
    }
}
