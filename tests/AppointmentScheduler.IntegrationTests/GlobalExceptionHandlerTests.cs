using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AppointmentScheduler.IntegrationTests;

/// <summary>Throws on purpose so the global handler's fallback paths can be exercised. Registered only by <see cref="ApiFactory"/>.</summary>
[ApiController]
[AllowAnonymous]
[Route("test/errors")]
public sealed class ErrorTestController : ControllerBase
{
    public const string SecretMessage = "connection string Host=db;Password=hunter2 for patient Jane Roe";

    [HttpGet("unexpected")]
    public IActionResult Unexpected() => throw new InvalidOperationException(SecretMessage);

    // Npgsql marks a failure transient when its cause is I/O or a timeout (broken connection, database down).
    [HttpGet("database-down")]
    public IActionResult DatabaseDown() => throw new NpgsqlException(SecretMessage, new TimeoutException());

    // SaveChanges wraps the provider exception (GET only to keep this test free of an Idempotency-Key).
    [HttpGet("save-failed")]
    public IActionResult SaveFailed() =>
        throw new DbUpdateException("save failed", new NpgsqlException(SecretMessage, new System.IO.IOException("reset")));

    [HttpGet("database-bug")]
    public IActionResult DatabaseBug() => throw new NpgsqlException(SecretMessage); // not transient

    /// <summary>Completes once the server has observed a client abort on <c>slow</c>.</summary>
    public static TaskCompletionSource SlowAborted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [HttpGet("slow")]
    public async Task<IActionResult> Slow()
    {
        try
        {
            await Task.Delay(Timeout.Infinite, HttpContext.RequestAborted); // throws when the client goes away
            return Ok();
        }
        finally
        {
            SlowAborted.TrySetResult();
        }
    }
}

[Collection(nameof(ApiCollection))]
public class GlobalExceptionHandlerTests(ApiFactory api)
{
    [Fact]
    public async Task Unexpected_exception_returns_generic_500_with_error_details_in_development_only()
    {
        // ApiFactory runs in Development; StagingEnvironmentTests asserts the same request leaks nothing outside it.
        var response = await api.CreateClient().GetAsync("/test/errors/unexpected");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var raw = await response.Content.ReadAsStringAsync();
        var body = await response.JsonAsync();
        Assert.False(body.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(500, body.GetProperty("statusCode").GetInt32());
        Assert.Equal(ApiErrorCodes.InternalError, body.GetProperty("errorCode").GetString());
        Assert.DoesNotContain("hunter2", body.GetProperty("message").GetString()); // the message itself stays generic
        Assert.StartsWith("InvalidOperationException: ", body.GetProperty("errorDetails").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.False(body.TryGetProperty("data", out _));
        Assert.DoesNotContain(" at ", raw); // never a stack trace
    }

    [Theory] // ADR-0008: no retry; the unit of work rolled back, so the client retries after Retry-After
    [InlineData("/test/errors/database-down")]
    [InlineData("/test/errors/save-failed")]
    public async Task Transient_database_failure_returns_503_with_retry_after(string path)
    {
        var response = await api.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(5), response.Headers.RetryAfter?.Delta);
        var raw = await response.Content.ReadAsStringAsync();
        var body = await response.JsonAsync();
        Assert.False(body.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(ApiErrorCodes.ServiceUnavailable, body.GetProperty("errorCode").GetString());
        Assert.DoesNotContain("hunter2", raw); // not even errorDetails: the provider message stays in no response
        Assert.Contains(api.Logs.Entries, e => e.Contains($"Database unavailable for GET {path}") && !e.Contains("hunter2"));
    }

    [Fact]
    public async Task Non_transient_database_failure_stays_a_generic_500()
    {
        var response = await api.CreateClient().GetAsync("/test/errors/database-bug");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ApiErrorCodes.InternalError, await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Unknown_route_returns_404_api_response()
    {
        var response = await api.CreateClient().GetAsync("/api/v1/no-such-endpoint");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.False(body.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(ApiErrorCodes.RouteNotFound, body.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Non_json_body_returns_415_api_response()
    {
        var response = await api.CreateClient().PostAsync("/api/v1/auth/login", new StringContent("email=x", System.Text.Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(ApiErrorCodes.UnsupportedMediaType, await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Wrong_method_returns_405_api_response()
    {
        var response = await api.CreateClient().DeleteAsync("/api/v1/auth/login");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(ApiErrorCodes.MethodNotAllowed, await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Unexpected_exception_is_logged_with_details_for_operators()
    {
        await api.CreateClient().GetAsync("/test/errors/unexpected");

        Assert.Contains(api.Logs.Entries, e => e.Contains("Unhandled exception for GET /test/errors/unexpected") && e.Contains("hunter2"));
    }

    [Fact]
    public async Task Client_abort_is_not_reported_as_an_unhandled_error()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.CreateClient().GetAsync("/test/errors/slow", cts.Token));

        await ErrorTestController.SlowAborted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(200); // let the exception propagate through the middleware

        Assert.DoesNotContain(api.Logs.Entries, e => e.Contains("Unhandled exception for GET /test/errors/slow"));
    }
}
