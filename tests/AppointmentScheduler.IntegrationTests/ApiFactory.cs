using System.Net.Http.Json;
using System.Text.Json;
using AppointmentScheduler.Domain.Doctors;
using AppointmentScheduler.Domain.Patients;
using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace AppointmentScheduler.IntegrationTests;

/// <summary>
/// Runs the real API against a throwaway PostgreSQL container (exclusion constraints need real Postgres).
/// The clock is fixed to Monday 2026-11-02 08:00 UTC (= 08:00 Europe/London, GMT), one hour before opening.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly DateOnly Day = new(2026, 11, 2);
    public static DateTimeOffset At(int hour, int minute = 0, int dayOffset = 0) =>
        new(Day.AddDays(dayOffset).ToDateTime(new TimeOnly(hour, minute)), TimeSpan.Zero); // London == UTC in November

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development"); // applies migrations on startup
        builder.UseSetting("ConnectionStrings:Scheduler", _db.GetConnectionString());
        builder.UseSetting("Seed:Enabled", "false");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        _ = Server; // start the host so migrations run once
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>Fresh doctors and patients per test, so tests never share schedules.</summary>
    public async Task<(int[] Doctors, int[] Patients)> CreatePeopleAsync(int doctors = 2, int patients = 2)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var d = Enumerable.Range(0, doctors).Select(i => new Doctor($"Dr Test {Guid.NewGuid():N}", "GP")).ToArray();
        var p = Enumerable.Range(0, patients).Select(i => new Patient($"T-{Guid.NewGuid():N}"[..20], "Test Patient", new DateOnly(1990, 1, 1), "000")).ToArray();
        db.AddRange(d);
        db.AddRange(p);
        await db.SaveChangesAsync();
        return (d.Select(x => x.Id).ToArray(), p.Select(x => x.Id).ToArray());
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;

internal static class HttpExtensions
{
    public static Task<HttpResponseMessage> BookAsync(this HttpClient client, int doctorId, int patientId, DateTimeOffset startUtc, string reason = "Check-up") =>
        client.PostAsJsonAsync("/api/v1/appointments", new { doctorId, patientId, startUtc, reason });

    /// <summary>The ProblemDetails <c>code</c> extension of an error response.</summary>
    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
}
