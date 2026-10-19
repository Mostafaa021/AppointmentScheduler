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
    public async Task Unexpected_exception_returns_generic_500_without_internals()
    {
        var response = await api.CreateClient().GetAsync("/test/errors/unexpected");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var raw = await response.Content.ReadAsStringAsync();
        var body = await response.JsonAsync();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(500, body.GetProperty("statusCode").GetInt32());
        Assert.Equal(ErrorCodes.InternalError, body.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.False(body.TryGetProperty("data", out _));
        Assert.DoesNotContain("hunter2", raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
        Assert.DoesNotContain(" at ", raw); // no stack trace
    }

    [Fact]
    public async Task Unknown_route_returns_404_api_response()
    {
        var response = await api.CreateClient().GetAsync("/api/v1/no-such-endpoint");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(ErrorCodes.RouteNotFound, body.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Non_json_body_returns_415_api_response()
    {
        var response = await api.CreateClient().PostAsync("/api/v1/auth/login", new StringContent("email=x", System.Text.Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(ErrorCodes.UnsupportedMediaType, await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task Wrong_method_returns_405_api_response()
    {
        var response = await api.CreateClient().DeleteAsync("/api/v1/auth/login");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal(ErrorCodes.MethodNotAllowed, await response.ErrorCodeAsync());
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
