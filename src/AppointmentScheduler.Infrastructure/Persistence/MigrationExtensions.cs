using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AppointmentScheduler.Infrastructure.Persistence;

public static class MigrationExtensions
{
    /// <summary>
    /// Applies pending migrations (and the seed) in Development only.
    /// shortcut: auto-migrate in Development only; containers use the migration bundle (M6).
    /// </summary>
    public static async Task ApplyMigrationsAsync(this IHost app)
    {
        if (!app.Services.GetRequiredService<IHostEnvironment>().IsDevelopment()) return;

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }
}
