using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AppointmentScheduler.Domain.Doctors;
using AppointmentScheduler.Domain.Patients;
using AppointmentScheduler.Infrastructure.Auth;
using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace AppointmentScheduler.IntegrationTests;

/// <summary>Users created by <see cref="ApiFactory"/>; all test users share <see cref="Password"/>.</summary>
public static class TestUsers
{
    public const string Admin = "admin@test.local";
    public const string Receptionist = "reception@test.local";
    public const string Password = "Test-Password-123"; // meets the AC-004-12 policy
}

/// <summary>
/// Runs the real API against a throwaway PostgreSQL container (exclusion constraints need real Postgres).
/// The clock is fixed to Monday 2026-11-02 08:00 UTC (= 08:00 Europe/London, GMT), one hour before opening.
/// Tests authenticate through the real <c>POST /api/v1/auth/login</c> (no test-only token issuer).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly DateOnly Day = new(2026, 11, 2);
    public static DateTimeOffset At(int hour, int minute = 0, int dayOffset = 0) =>
        new(Day.AddDays(dayOffset).ToDateTime(new TimeOnly(hour, minute)), TimeSpan.Zero); // London == UTC in November

    public const string Issuer = "appointment-scheduler";
    public const string Audience = "appointment-scheduler-web";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));

    /// <summary>Random per run, never a real secret.</summary>
    public string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public LogCapture Logs { get; } = new();

    protected virtual string EnvironmentName => "Development"; // Development applies migrations on startup
    protected virtual bool SeedEnabled => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("ConnectionStrings:Scheduler", _db.GetConnectionString());
        builder.UseSetting("Seed:Enabled", SeedEnabled ? "true" : "false");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("BackgroundJobs:Enabled", "false"); // no Hangfire polling; jobs are run directly in tests
        builder.UseSetting("Observability:OtlpEndpoint", ""); // never export test traces, even if Jaeger runs locally
        builder.UseSetting("Messaging:RabbitMq:Enabled", "false"); // Wolverine runs on its PostgreSQL store only; never a local broker
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            // Test-only controllers (e.g. ErrorTestController) live in this assembly, outside /api/v1.
            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
        });
    }

    public virtual async Task InitializeAsync()
    {
        await StartAsync();
        await CreateUserAsync(TestUsers.Admin, "Admin");
        await CreateUserAsync(TestUsers.Receptionist, "Receptionist");
    }

    /// <summary>Starts the container and the host (which migrates in Development).</summary>
    protected async Task StartAsync()
    {
        await _db.StartAsync();
        _ = Server;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        // Host first: Wolverine's durability agent polls the database until the host stops; dropping the container first
        // made it fail and log into disposed loggers, which crashed the test host.
        await base.DisposeAsync();
        await _db.DisposeAsync();
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

    /// <summary>Creates a user directly through Identity (bypassing the API) and returns its e-mail.</summary>
    public async Task<string> CreateUserAsync(string? email = null, string role = "Receptionist", int? doctorId = null)
    {
        email ??= $"user-{Guid.NewGuid():N}@test.local";
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = email, Email = email, DoctorId = doctorId };
        Assert.True((await users.CreateAsync(user, TestUsers.Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return email;
    }

    /// <summary>A user whose only role is Doctor, linked to <paramref name="doctorId"/> (BR-7).</summary>
    public Task<string> CreateDoctorUserAsync(int doctorId) => CreateUserAsync(role: "Doctor", doctorId: doctorId);

    /// <summary>A client that logged in through the real <c>/auth/login</c> and sends the bearer token.</summary>
    public async Task<HttpClient> ClientForAsync(string email, string password = TestUsers.Password)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await client.LoginAsync(email, password);
        response.EnsureSuccessStatusCode();
        var token = (await response.DataAsync()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>A client without cookie handling: .NET's CookieContainer won't send <c>Secure</c> cookies over http.</summary>
    public HttpClient CreateAnonymousClient() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;

/// <summary>Captures every log message (incl. structured values) for the AC-004-25 PII assertions.</summary>
public sealed class LogCapture : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyCollection<string> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(_entries);

    public void Dispose() { }

    private sealed class CapturingLogger(ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> kv ? string.Join("|", kv.Select(p => $"{p.Key}={p.Value}")) : "";
            entries.Enqueue($"{formatter(state, exception)} {values} {exception}");
        }
    }
}

internal static class HttpExtensions
{
    public const string RefreshCookie = "refresh_token";

    public static Task<HttpResponseMessage> BookAsync(this HttpClient client, int doctorId, int patientId, DateTimeOffset startUtc, string reason = "Check-up") =>
        client.PostAsJsonAsync("/api/v1/appointments", new { doctorId, patientId, startUtc, reason });

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string? email, string? password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    /// <summary>POSTs to <paramref name="url"/> with the refresh cookie set explicitly (null = no cookie).</summary>
    public static Task<HttpResponseMessage> PostWithRefreshCookieAsync(this HttpClient client, string url, string? refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (refreshToken is not null) request.Headers.Add("Cookie", $"{RefreshCookie}={refreshToken}");
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string? refreshToken) =>
        client.PostWithRefreshCookieAsync("/api/v1/auth/refresh", refreshToken);

    /// <summary>The raw <c>Set-Cookie</c> header for the refresh cookie.</summary>
    public static string RefreshSetCookie(this HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith($"{RefreshCookie}=", StringComparison.Ordinal));

    /// <summary>The refresh cookie's value.</summary>
    public static string RefreshToken(this HttpResponseMessage response) =>
        response.RefreshSetCookie().Split(';')[0][(RefreshCookie.Length + 1)..];

    /// <summary>The <c>errorCode</c> of an <c>ApiResponse</c> error body (ADR-0015).</summary>
    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("errorCode", out var code) ? code.GetString() : null;
    }

    /// <summary>The whole <c>ApiResponse</c> envelope (success flag, message, errors, traceId…).</summary>
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    /// <summary>The envelope's <c>data</c>: the DTO the endpoint returned.</summary>
    public static async Task<JsonElement> DataAsync(this HttpResponseMessage response) =>
        (await response.JsonAsync()).GetProperty("data");
}

/// <summary>Role arrays for request bodies (static to satisfy CA1861).</summary>
internal static class RoleSets
{
    public static readonly string[] Admin = ["Admin"];
    public static readonly string[] Receptionist = ["Receptionist"];
    public static readonly string[] Doctor = ["Doctor"];
    public static readonly string[] AdminReceptionist = ["Admin", "Receptionist"];
}
