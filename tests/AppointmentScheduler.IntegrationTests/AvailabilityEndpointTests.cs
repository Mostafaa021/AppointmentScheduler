using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static AppointmentScheduler.IntegrationTests.ApiFactory;

namespace AppointmentScheduler.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class AvailabilityEndpointTests(ApiFactory api) : IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() => _client = await api.ClientForAsync(TestUsers.Receptionist);

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<JsonElement[]> SlotsAsync(int doctorId, DateOnly date, int? patientId = null)
    {
        var url = $"/api/v1/doctors/{doctorId}/availability?date={date:yyyy-MM-dd}" + (patientId is null ? "" : $"&patientId={patientId}");
        var body = await (await _client.GetAsync(url)).DataAsync();
        Assert.Equal("Europe/London", body.GetProperty("timeZone").GetString());
        return body.GetProperty("slots").EnumerateArray().ToArray();
    }

    private static string StatusAt(JsonElement[] slots, string localStart) =>
        slots.Single(s => s.GetProperty("localStart").GetString() == localStart).GetProperty("status").GetString()!;

    [Fact]
    public async Task AC_001_1_Returns_16_free_slots_for_an_empty_future_day()
    {
        var (d, _) = await api.CreatePeopleAsync();

        var slots = await SlotsAsync(d[0], Day);

        Assert.Equal(16, slots.Length);
        Assert.All(slots, s => Assert.Equal("Free", s.GetProperty("status").GetString()));
        Assert.Equal(At(9), slots[0].GetProperty("startUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task AC_001_2_and_3_Booked_slot_is_Booked_and_frees_after_cancel()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var id = (await (await _client.BookAsync(d[0], p[0], At(9, 30))).DataAsync()).GetProperty("id").GetGuid();

        var booked = (await SlotsAsync(d[0], Day)).Single(s => s.GetProperty("localStart").GetString() == "09:30");
        Assert.Equal("Booked", booked.GetProperty("status").GetString());
        Assert.Equal(id, booked.GetProperty("appointmentId").GetGuid());

        await _client.PostAsync($"/api/v1/appointments/{id}/cancel", JsonContent.Create(new { reason = "called" }));
        Assert.Equal("Free", StatusAt(await SlotsAsync(d[0], Day), "09:30"));
    }

    [Fact]
    public async Task AC_001_4_Slots_before_now_are_Past()
    {
        var (d, _) = await api.CreatePeopleAsync();

        var yesterday = await SlotsAsync(d[0], Day.AddDays(-1));

        Assert.All(yesterday, s => Assert.Equal("Past", s.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task AC_001_5_PatientBusy_when_patient_is_booked_with_another_doctor()
    {
        var (d, p) = await api.CreatePeopleAsync();
        await _client.BookAsync(d[1], p[0], At(14));

        var slots = await SlotsAsync(d[0], Day, p[0]);

        Assert.Equal("PatientBusy", StatusAt(slots, "14:00"));
        Assert.Equal("Free", StatusAt(slots, "14:30"));
    }

    [Fact]
    public async Task AC_001_7_Unknown_doctor_404_and_bad_date_400()
    {
        var notFound = await _client.GetAsync($"/api/v1/doctors/{int.MaxValue}/availability?date=2026-11-02");
        var badDate = await _client.GetAsync("/api/v1/doctors/1/availability?date=not-a-date");

        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal(ErrorCodes.DoctorNotFound, await notFound.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, badDate.StatusCode);
    }
}
