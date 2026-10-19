using System.Net;
using System.Net.Http.Json;
using System.Text;
using AppointmentScheduler.Api.Idempotency;
using AppointmentScheduler.Infrastructure.Idempotency;
using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static AppointmentScheduler.IntegrationTests.ApiFactory;

namespace AppointmentScheduler.IntegrationTests;

/// <summary>Test-only writes for the idempotency cases a real endpoint can't produce on demand (5xx, a slow handler).</summary>
[ApiController]
[Authorize]
[Route("test/idempotency")]
public sealed class IdempotencyTestController : ControllerBase
{
    private static int _failCalls;

    public static int FailCalls => Volatile.Read(ref _failCalls);

    public static TaskCompletionSource SlowStarted { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public static TaskCompletionSource SlowRelease { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static void ResetSlow()
    {
        SlowStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        SlowRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [HttpPost("fail")]
    public IActionResult Fail()
    {
        Interlocked.Increment(ref _failCalls);
        throw new InvalidOperationException("boom");
    }

    [HttpPost("slow")]
    public async Task<IActionResult> Slow()
    {
        SlowStarted.TrySetResult();
        await SlowRelease.Task.WaitAsync(TimeSpan.FromSeconds(10));
        return StatusCode(201, new { done = true });
    }

    [HttpPost("echo")]
    public IActionResult Echo() => StatusCode(201, new { echoed = true });
}

/// <summary>Spec 009 (AC-009-1…8): idempotent writes through the MVC resource filter and the idempotency_records table.</summary>
[Collection(nameof(ApiCollection))]
public class IdempotencyTests(ApiFactory api) : IAsyncLifetime
{
    private const string EchoPath = "/test/idempotency/echo";
    private static readonly byte[] EchoBody = "{}"u8.ToArray();

    private HttpClient _client = null!; // sends exactly the headers each test sets

    public async Task InitializeAsync() =>
        _client = await api.ClientForAsync(TestUsers.Receptionist, addIdempotencyKeys: false);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AC_009_1_Write_without_key_is_400_and_changes_nothing()
    {
        var (d, p) = await api.CreatePeopleAsync();

        var response = await _client.BookAsync(d[0], p[0], At(9));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ApiErrorCodes.IdempotencyKeyRequired, await response.ErrorCodeAsync());
        Assert.Equal(0, await AppointmentsOfDoctorAsync(d[0]));
    }

    [Fact]
    public async Task AC_009_1_Key_longer_than_100_characters_is_rejected()
    {
        var response = await PostEchoAsync(new string('k', 101));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ApiErrorCodes.IdempotencyKeyRequired, await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task AC_009_1_Auth_endpoints_need_no_key()
    {
        var login = await api.CreateAnonymousClient().LoginAsync(TestUsers.Receptionist, TestUsers.Password);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task AC_009_2_and_3_Replay_returns_the_stored_response_byte_for_byte_and_books_once()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var key = NewKey();

        var first = await BookWithKeyAsync(key, d[0], p[0], At(9));
        var second = await BookWithKeyAsync(key, d[0], p[0], At(9));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(first.Headers.Contains(IdempotencyHeaders.Replayed));
        Assert.Equal("true", Assert.Single(second.Headers.GetValues(IdempotencyHeaders.Replayed)));
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Equal(1, await AppointmentsOfDoctorAsync(d[0])); // the handler ran once

        var record = await RecordAsync(key);
        Assert.Equal(IdempotencyStatus.Completed, record.Status);
        Assert.Equal(201, record.ResponseStatusCode);
        Assert.Contains("Location", record.ResponseHeaders!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC_009_4_Same_key_with_a_different_body_is_422()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var key = NewKey();
        await BookWithKeyAsync(key, d[0], p[0], At(10));

        var reused = await BookWithKeyAsync(key, d[0], p[0], At(11));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        Assert.Equal(ApiErrorCodes.IdempotencyKeyReused, await reused.ErrorCodeAsync());
        Assert.Equal(1, await AppointmentsOfDoctorAsync(d[0]));
    }

    [Fact]
    public async Task AC_009_5_Same_key_while_the_first_request_runs_is_409_with_retry_after()
    {
        IdempotencyTestController.ResetSlow();
        var key = NewKey();

        var first = PostAsync("/test/idempotency/slow", key);
        await IdempotencyTestController.SlowStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = await PostAsync("/test/idempotency/slow", key);
        IdempotencyTestController.SlowRelease.TrySetResult();

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ApiErrorCodes.IdempotencyRequestInProgress, await second.ErrorCodeAsync());
        Assert.Equal("1", second.Headers.RetryAfter?.ToString());
        Assert.Equal(HttpStatusCode.Created, (await first).StatusCode);
    }

    [Fact]
    public async Task AC_009_5_A_stale_in_progress_record_is_taken_over()
    {
        var key = NewKey();
        await SeedAsync(key, EchoPath, IdempotencyStatus.InProgress,
            createdAt: api.Clock.GetUtcNow().AddMinutes(-5), expiresAt: api.Clock.GetUtcNow().AddHours(1));

        var response = await PostEchoAsync(key);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(IdempotencyStatus.Completed, (await RecordAsync(key)).Status);
    }

    [Fact]
    public async Task AC_009_6_5xx_is_not_stored_so_the_same_key_can_retry()
    {
        var key = NewKey();
        var before = IdempotencyTestController.FailCalls;

        var first = await PostAsync("/test/idempotency/fail", key);
        var retry = await PostAsync("/test/idempotency/fail", key);

        Assert.Equal(HttpStatusCode.InternalServerError, first.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, retry.StatusCode);
        Assert.False(retry.Headers.Contains(IdempotencyHeaders.Replayed));
        Assert.Equal(before + 2, IdempotencyTestController.FailCalls); // the handler ran again
        Assert.Null(await FindRecordAsync(key));
    }

    [Fact]
    public async Task AC_009_6_4xx_is_stored_and_replayed()
    {
        var (d, p) = await api.CreatePeopleAsync();
        await BookWithKeyAsync(NewKey(), d[0], p[0], At(12));
        var key = NewKey();

        var conflict = await BookWithKeyAsync(key, d[0], p[1], At(12));
        var replay = await BookWithKeyAsync(key, d[0], p[1], At(12));

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(ErrorCodes.DoctorConflict, await conflict.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues(IdempotencyHeaders.Replayed)));
        Assert.Equal(await conflict.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AC_009_7_Expired_records_are_purged_and_an_expired_key_is_free_again()
    {
        var now = api.Clock.GetUtcNow();
        var expiredKey = NewKey();
        var liveKey = NewKey();
        await SeedAsync(expiredKey, EchoPath, IdempotencyStatus.Completed, createdAt: now.AddHours(-25), expiresAt: now.AddHours(-1));
        await SeedAsync(liveKey, EchoPath, IdempotencyStatus.Completed, createdAt: now, expiresAt: now.AddHours(23));

        using (var scope = api.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PurgeExpiredIdempotencyRecordsJob>().RunAsync(CancellationToken.None);

        Assert.Null(await FindRecordAsync(expiredKey));
        Assert.NotNull(await FindRecordAsync(liveKey));

        // An expired record the job hasn't reached yet doesn't replay either: the key starts over.
        var lateKey = NewKey();
        await SeedAsync(lateKey, EchoPath, IdempotencyStatus.Completed, createdAt: now.AddHours(-25), expiresAt: now.AddMinutes(-1));
        var response = await PostEchoAsync(lateKey);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(response.Headers.Contains(IdempotencyHeaders.Replayed));
    }

    [Fact]
    public async Task AC_009_8_Keys_are_scoped_per_user()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var other = await api.ClientForAsync(await api.CreateUserAsync(), addIdempotencyKeys: false);
        var key = NewKey();

        var mine = await BookWithKeyAsync(key, d[0], p[0], At(14));
        var theirs = await Send(other, HttpMethod.Post, "/api/v1/appointments", key,
            JsonContent.Create(new { doctorId = d[1], patientId = p[1], startUtc = At(14), reason = "Check-up" }));

        Assert.Equal(HttpStatusCode.Created, mine.StatusCode);
        Assert.Equal(HttpStatusCode.Created, theirs.StatusCode);
        Assert.False(theirs.Headers.Contains(IdempotencyHeaders.Replayed));
    }

    private static string NewKey() => Guid.NewGuid().ToString();

    private Task<HttpResponseMessage> BookWithKeyAsync(string key, int doctorId, int patientId, DateTimeOffset startUtc) =>
        Send(_client, HttpMethod.Post, "/api/v1/appointments", key,
            JsonContent.Create(new { doctorId, patientId, startUtc, reason = "Check-up" }));

    private Task<HttpResponseMessage> PostEchoAsync(string key) =>
        Send(_client, HttpMethod.Post, EchoPath, key, new ByteArrayContent(EchoBody) { Headers = { { "Content-Type", "application/json" } } });

    private Task<HttpResponseMessage> PostAsync(string path, string key) =>
        Send(_client, HttpMethod.Post, path, key, new StringContent("{}", Encoding.UTF8, "application/json"));

    private static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string key, HttpContent content)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        request.Headers.Add(IdempotencyHeaders.Key, key);
        return client.SendAsync(request);
    }

    private async Task<Guid> ReceptionistIdAsync() =>
        Guid.Parse((await (await _client.GetAsync("/api/v1/me")).DataAsync()).GetProperty("id").GetString()!);

    private async Task SeedAsync(string key, string path, IdempotencyStatus status, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        var userId = await ReceptionistIdAsync();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(), UserId = userId, Key = key, Method = "POST", Path = path,
            RequestHash = RequestFingerprint.Compute("POST", path, EchoBody), Status = status,
            ResponseStatusCode = status == IdempotencyStatus.Completed ? 201 : null,
            ResponseBody = status == IdempotencyStatus.Completed ? "{\"seeded\":true}" : null,
            CreatedAtUtc = createdAt, ExpiresAtUtc = expiresAt,
        });
        await db.SaveChangesAsync();
    }

    private async Task<IdempotencyRecord?> FindRecordAsync(string key)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key);
    }

    private async Task<IdempotencyRecord> RecordAsync(string key) =>
        await FindRecordAsync(key) ?? throw new InvalidOperationException($"no record for {key}");

    private async Task<int> AppointmentsOfDoctorAsync(int doctorId)
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Appointments.CountAsync(a => a.DoctorId == doctorId);
    }
}
