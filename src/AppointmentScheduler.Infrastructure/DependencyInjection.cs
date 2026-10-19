using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Infrastructure.Persistence;
using AppointmentScheduler.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AppointmentScheduler.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Scheduler")
            ?? throw new InvalidOperationException("Connection string 'Scheduler' is missing.");
        var seed = configuration.GetValue("Seed:Enabled", true);

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
            if (!seed) return;

            var schedule = sp.GetRequiredService<IOptions<ClinicOptions>>().Value.ToDaySchedule();
            var clock = sp.GetRequiredService<TimeProvider>();
            options.UseAsyncSeeding((db, _, ct) => DbSeeder.SeedAsync(db, schedule, clock.GetUtcNow(), ct));
            options.UseSeeding((db, _) => DbSeeder.SeedAsync(db, schedule, clock.GetUtcNow(), CancellationToken.None).GetAwaiter().GetResult());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        return services;
    }
}
