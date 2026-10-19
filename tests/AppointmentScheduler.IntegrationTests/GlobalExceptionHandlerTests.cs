using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public async Task Unexpected_exception_returns_generic_500_problem_without_internals()
    {
        var response = await api.CreateClient().GetAsync("/test/errors/unexpected");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("internal_error", await response.ProblemCodeAsync());
        Assert.Contains("traceId", body);
        Assert.DoesNotContain("hunter2", body);
        Assert.DoesNotContain("InvalidOperationException", body);
        Assert.DoesNotContain(" at ", body); // no stack trace
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
