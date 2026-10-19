using System.Net;
using System.Net.Http.Json;
using System.Text;
using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AppointmentScheduler.IntegrationTests.ApiFactory;

namespace AppointmentScheduler.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class ValidationAndHeadersTests(ApiFactory api) : IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() => _client = await api.ClientForAsync(TestUsers.Receptionist);

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<int> AuditCountAsync()
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditRecords.CountAsync();
    }

    [Fact]
    public async Task AC_004_22_Book_reports_all_failures_at_once_before_lookups_and_records_nothing()
    {
        var before = await AuditCountAsync();

        // Unknown doctor/patient would be 404 — validation runs first; missing startUtc is no longer a misleading 422.
        var response = await _client.PostAsJsonAsync("/api/v1/appointments", new { doctorId = 0, patientId = 0, reason = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.False(body.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(400, body.GetProperty("statusCode").GetInt32());
        Assert.Equal(ErrorCodes.ValidationFailed, body.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("message").GetString()));
        Assert.Equal(["doctorId", "patientId", "reason", "startUtc"],
            body.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(before, await AuditCountAsync());
    }

    [Fact]
    public async Task AC_004_22_Cancel_and_reschedule_are_validated_before_the_lookup()
    {
        var unknown = Guid.NewGuid();

        var cancel = await _client.PostAsJsonAsync($"/api/v1/appointments/{unknown}/cancel", new { reason = new string('x', 501) });
        var reschedule = await _client.PostAsJsonAsync($"/api/v1/appointments/{unknown}/reschedule", new { });

        Assert.Equal(HttpStatusCode.BadRequest, cancel.StatusCode);
        Assert.True((await cancel.JsonAsync()).GetProperty("errors").TryGetProperty("reason", out _));
        Assert.Equal(HttpStatusCode.BadRequest, reschedule.StatusCode);
        Assert.True((await reschedule.JsonAsync()).GetProperty("errors").TryGetProperty("startUtc", out _));
    }

    [Fact]
    public async Task AC_004_22_Unbindable_input_stays_400_bad_request()
    {
        var json = await _client.PostAsync("/api/v1/appointments", new StringContent("{ nope", Encoding.UTF8, "application/json"));
        var types = await _client.PostAsync("/api/v1/appointments",
            new StringContent("""{"doctorId":"one","patientId":1,"startUtc":"2026-11-02T10:00:00Z","reason":"x"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, json.StatusCode);
        Assert.Equal(ErrorCodes.BadRequest, await json.ErrorCodeAsync());
        Assert.Equal(ErrorCodes.BadRequest, await types.ErrorCodeAsync());
    }

    private static void AssertBaseHeaders(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.False(response.Headers.Contains("Strict-Transport-Security")); // Development
    }

    [Fact]
    public async Task AC_004_23_Security_headers_on_success_error_401_403_and_health()
    {
        var (d, _) = await api.CreatePeopleAsync(doctors: 1, patients: 0);
        var doctor = await api.ClientForAsync(await api.CreateDoctorUserAsync(d[0]));
        var https = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://api.clinic.test") });

        HttpResponseMessage[] apiResponses =
        [
            await _client.GetAsync("/api/v1/doctors"),                                         // 200
            await _client.GetAsync($"/api/v1/appointments/{Guid.NewGuid()}"),                    // 404
            await api.CreateAnonymousClient().GetAsync("/api/v1/me"),                            // 401
            await doctor.GetAsync("/api/v1/patients"),                                           // 403
            await https.GetAsync("/api/v1/me"),                                                  // https in Development
        ];
        var health = await api.CreateClient().GetAsync("/health/live");

        foreach (var response in apiResponses)
        {
            AssertBaseHeaders(response);
            Assert.Equal("default-src 'none'; frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        }
        AssertBaseHeaders(health);
        Assert.False(health.Headers.Contains("Content-Security-Policy"));
    }
}
