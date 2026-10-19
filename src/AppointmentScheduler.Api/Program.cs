using AppointmentScheduler.Api.Configuration;
using AppointmentScheduler.Api.Security;
using AppointmentScheduler.Infrastructure.BackgroundJobs;
using AppointmentScheduler.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder
    .AddApiConfiguration()
    .AddSecurityConfiguration()
    .AddOpenApiConfiguration();

var app = builder.Build();

await app.ApplyMigrationsAsync(); // Development only

// Order matters (docs/architecture.md): headers first so error/401/403 responses carry them; auth before endpoints (AC-004-8).
app.UseSecurityHeaders();
app.UseApiExceptionHandling();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment()) 
    app.MapOpenApiDocs();

// Controllers are authenticated by default (ApiControllerBase); login/refresh opt out, role policies narrow access.
app.MapControllers();
app.MapHealthChecks("/health/live");

// ─── Hangfire ──────────────────────────────────────────────────────────────
// Runs in this process (ADR-0014): recurring jobs + Development-only dashboard at /hangfire.
app.UseBackgroundJobs(app.Configuration, app.Environment.IsDevelopment());

await app.RunAsync();

public partial class Program; // for WebApplicationFactory in integration tests
