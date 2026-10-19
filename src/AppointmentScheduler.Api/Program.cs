using System.Text.Json.Serialization;
using AppointmentScheduler.Api.Endpoints;
using AppointmentScheduler.Api.Errors;
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

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    // shortcut: auto-migrate in Development only; containers use the migration bundle (M6).
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.MapGet("/health/live", () => TypedResults.Ok("Healthy")).ExcludeFromDescription();

app.MapGroup("/api/v1")
    .MapLookupEndpoints()
    .MapAppointmentEndpoints();

await app.RunAsync();

public partial class Program; // for WebApplicationFactory in integration tests
