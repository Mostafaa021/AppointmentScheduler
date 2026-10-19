using System.Net;
using System.Net.Http.Json;
using static AppointmentScheduler.IntegrationTests.ApiFactory;

namespace AppointmentScheduler.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class ManageScheduleEndpointTests(ApiFactory api) : IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() => _client = await api.ClientForAsync(TestUsers.Receptionist);

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<Guid> BookIdAsync(int doctorId, int patientId, DateTimeOffset start) =>
        (await (await _client.BookAsync(doctorId, patientId, start)).DataAsync()).GetProperty("id").GetGuid();

    private Task<HttpResponseMessage> RescheduleAsync(Guid id, DateTimeOffset startUtc) =>
        _client.PostAsJsonAsync($"/api/v1/appointments/{id}/reschedule", new { startUtc });

    private Task<HttpResponseMessage> CancelAsync(Guid id) =>
        _client.PostAsJsonAsync($"/api/v1/appointments/{id}/cancel", new { reason = "Patient request" });

    [Fact]
    public async Task AC_003_1_Day_schedule_is_ordered_and_includes_cancelled()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var late = await BookIdAsync(d[0], p[0], At(15));
        var early = await BookIdAsync(d[0], p[1], At(9));
        await CancelAsync(late);

        var schedule = (await (await _client.GetAsync($"/api/v1/appointments?doctorId={d[0]}&date={Day:yyyy-MM-dd}")).DataAsync())
            .EnumerateArray().ToArray();

        Assert.Equal([early, late], schedule.Select(a => a.GetProperty("id").GetGuid()));
        Assert.Equal("Cancelled", schedule[1].GetProperty("status").GetString());
        Assert.Equal("Test Patient", schedule[0].GetProperty("patientName").GetString());
    }

    [Fact]
    public async Task AC_003_2_and_3_Cancel_once_then_422()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var id = await BookIdAsync(d[0], p[0], At(10));

        var first = await CancelAsync(id);
        var second = await CancelAsync(id);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("Cancelled", (await first.DataAsync()).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Equal(ErrorCodes.AppointmentNotModifiable, await second.ErrorCodeAsync());
    }

    [Fact]
    public async Task AC_003_4_Reschedule_moves_and_frees_old_slot()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var id = await BookIdAsync(d[0], p[0], At(11));

        var response = await RescheduleAsync(id, At(14, 30, dayOffset: 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("14:30", (await response.DataAsync()).GetProperty("localStart").GetString());
        Assert.Equal(HttpStatusCode.Created, (await _client.BookAsync(d[0], p[1], At(11))).StatusCode);
    }

    [Fact]
    public async Task AC_003_5_Reschedule_into_conflict_409_but_own_slot_is_not_a_conflict()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var id = await BookIdAsync(d[0], p[0], At(12));
        await BookIdAsync(d[0], p[1], At(13));          // doctor busy at 13:00
        await BookIdAsync(d[1], p[0], At(15));          // patient busy at 15:00 with another doctor

        var doctorClash = await RescheduleAsync(id, At(13));
        var patientClash = await RescheduleAsync(id, At(15));
        var sameSlot = await RescheduleAsync(id, At(12));

        Assert.Equal(ErrorCodes.DoctorConflict, await doctorClash.ErrorCodeAsync());
        Assert.Equal(ErrorCodes.PatientConflict, await patientClash.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, sameSlot.StatusCode);
    }

    [Fact]
    public async Task AC_003_6_Reschedule_into_past_or_invalid_slot_422()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var id = await BookIdAsync(d[0], p[0], At(16));

        Assert.Equal(ErrorCodes.AppointmentInPast, await (await RescheduleAsync(id, At(7))).ErrorCodeAsync());
        Assert.Equal(ErrorCodes.InvalidSlot, await (await RescheduleAsync(id, At(16, 45))).ErrorCodeAsync());
    }

    [Fact]
    public async Task AC_003_7_Unknown_appointment_404()
    {
        var response = await CancelAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.AppointmentNotFound, await response.ErrorCodeAsync());
    }
}
