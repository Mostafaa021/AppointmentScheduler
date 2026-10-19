using System.Net;
using System.Net.Http.Json;
using static AppointmentScheduler.IntegrationTests.ApiFactory;

namespace AppointmentScheduler.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class BookingEndpointTests(ApiFactory api) : IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() => _client = await api.ClientForAsync(TestUsers.Receptionist);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AC_002_1_Books_a_free_slot()
    {
        var (d, p) = await api.CreatePeopleAsync();

        var response = await _client.BookAsync(d[0], p[0], At(10), "  Annual check-up ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.Equal("Scheduled", body.GetProperty("status").GetString());
        Assert.Equal("10:00", body.GetProperty("localStart").GetString());
        Assert.Equal("10:30", body.GetProperty("localEnd").GetString());
        Assert.Equal("Annual check-up", body.GetProperty("reason").GetString());
        Assert.EndsWith($"/api/v1/appointments/{body.GetProperty("id").GetGuid()}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AC_002_2_Doctor_overlap_returns_409_doctor_conflict()
    {
        var (d, p) = await api.CreatePeopleAsync();
        await _client.BookAsync(d[0], p[0], At(11));

        var response = await _client.BookAsync(d[0], p[1], At(11));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("doctor_conflict", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task AC_002_3_Patient_overlap_with_another_doctor_returns_409_patient_conflict()
    {
        var (d, p) = await api.CreatePeopleAsync();
        await _client.BookAsync(d[0], p[0], At(12));

        var response = await _client.BookAsync(d[1], p[0], At(12));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("patient_conflict", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Back_to_back_slots_do_not_conflict()
    {
        var (d, p) = await api.CreatePeopleAsync();

        Assert.Equal(HttpStatusCode.Created, (await _client.BookAsync(d[0], p[0], At(13))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _client.BookAsync(d[0], p[0], At(13, 30))).StatusCode);
    }

    [Fact]
    public async Task AC_002_4_Past_slot_returns_422()
    {
        var (d, p) = await api.CreatePeopleAsync();

        var response = await _client.BookAsync(d[0], p[0], At(9, dayOffset: -1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("appointment_in_past", await response.ProblemCodeAsync());
    }

    [Theory]
    [InlineData(10, 15)]
    [InlineData(17, 0)]
    public async Task AC_002_5_Invalid_slot_returns_422(int hour, int minute)
    {
        var (d, p) = await api.CreatePeopleAsync();

        var response = await _client.BookAsync(d[0], p[0], At(hour, minute));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid_slot", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task AC_002_6_Missing_reason_returns_400_validation_problem()
    {
        var (d, p) = await api.CreatePeopleAsync();

        var response = await _client.BookAsync(d[0], p[0], At(14), reason: " ");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.JsonAsync()).GetProperty("errors").TryGetProperty("reason", out _));
    }

    [Fact]
    public async Task AC_002_7_Unknown_doctor_or_patient_returns_404()
    {
        var (d, p) = await api.CreatePeopleAsync();

        var noDoctor = await _client.BookAsync(int.MaxValue, p[0], At(15));
        var noPatient = await _client.BookAsync(d[0], int.MaxValue, At(15));

        Assert.Equal(HttpStatusCode.NotFound, noDoctor.StatusCode);
        Assert.Equal("doctor_not_found", await noDoctor.ProblemCodeAsync());
        Assert.Equal("patient_not_found", await noPatient.ProblemCodeAsync());
    }

    [Fact]
    public async Task AC_002_8_Parallel_bookings_of_one_slot_yield_exactly_one_201()
    {
        var (d, p) = await api.CreatePeopleAsync(doctors: 1, patients: 10);

        // Different patients, same doctor + slot, fired concurrently: the pre-check races, the DB constraint decides.
        var responses = await Task.WhenAll(p.Select(patient => _client.BookAsync(d[0], patient, At(16))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        var schedule = await _client.GetFromJsonAsync<List<object>>($"/api/v1/appointments?doctorId={d[0]}&date={Day:yyyy-MM-dd}");
        Assert.Single(schedule!);
    }
}
