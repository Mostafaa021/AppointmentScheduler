using System.Text.Json.Serialization;
using AppointmentScheduler.Api.Auth;
using AppointmentScheduler.Api.Errors;
using AppointmentScheduler.Api.Security;
using AppointmentScheduler.Application;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Infrastructure;
using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ClinicOptions>().Bind(builder.Configuration.GetSection(ClinicOptions.Section))
    .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Clinic:TimeZone must be a valid IANA zone.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiAuth();

builder.Services.AddControllers(o =>
        // Required-ness is checked by FluentValidation in the Application layer (400 validation_failed, ADR-0013),
        // so MVC must not reject missing non-nullable members first.
        o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = BadRequestResponse.FromModelState);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(365));
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Order matters (docs/architecture.md): headers first so error/401/403 responses carry them; auth before endpoints (AC-004-8).
app.UseSecurityHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    // shortcut: auto-migrate in Development only; containers use the migration bundle (M6).
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.MapGet("/health/live", () => TypedResults.Ok("Healthy")).ExcludeFromDescription();

// Controllers are authenticated by default (ApiControllerBase); login/refresh opt out, role policies narrow access.
app.MapControllers();

await app.RunAsync();

public partial class Program; // for WebApplicationFactory in integration tests
