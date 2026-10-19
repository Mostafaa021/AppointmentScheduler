using System.Text;
using AppointmentScheduler.Api.Errors;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Application.Idempotency;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AppointmentScheduler.Api.Idempotency;

/// <summary>
/// Spec 009 / ADR-0006, as an MVC resource filter (it wraps model binding, the action and the result, so the bytes it stores
/// are exactly what the client got). Every POST/PUT/PATCH/DELETE needs an <c>Idempotency-Key</c>, except on
/// <see cref="SkipIdempotencyAttribute"/> controllers:
/// <list type="bullet">
/// <item>first use → the key is claimed, the handler runs, its response is stored (2xx and 4xx) or the key released (5xx);</item>
/// <item>same key + same request → the stored response, byte-for-byte, with <c>Idempotent-Replayed: true</c>;</item>
/// <item>same key + different request → 422; still running → 409 + <c>Retry-After: 1</c>.</item>
/// </list>
/// Exceptions from the handler are written here by the global <see cref="IExceptionHandler"/> (the one error writer), so a
/// 409/422 from a rule is stored and replayed like any other 4xx.
/// </summary>
internal sealed class IdempotencyFilter(
    IIdempotencyStore store,
    ICurrentUser currentUser,
    IEnumerable<IExceptionHandler> exceptionHandlers) : IAsyncResourceFilter
{
    // Headers a replay must reproduce; everything else (Date, trace headers, security headers) is per-response.
    private static readonly string[] KeptHeaders = ["Location", "ETag", "Content-Type"];

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (!IsWrite(http.Request.Method) || context.ActionDescriptor.EndpointMetadata.OfType<SkipIdempotencyAttribute>().Any())
        {
            await next();
            return;
        }

        var key = http.Request.Headers[IdempotencyHeaders.Key].ToString().Trim();
        if (key.Length is 0 or > IdempotencyHeaders.MaxKeyLength)
        {
            context.Result = Error(http, StatusCodes.Status400BadRequest, ApiErrorCodes.IdempotencyKeyRequired,
                $"Send an {IdempotencyHeaders.Key} header (1–{IdempotencyHeaders.MaxKeyLength} characters, a UUID per user action) with every write.");
            return;
        }

        // Writes are authorized (ApiControllerBase), so a missing user here is a wiring bug, not a client error.
        var userId = currentUser.UserId
            ?? throw new InvalidOperationException($"{nameof(IdempotencyFilter)} ran for an anonymous write; add [SkipIdempotency] or [Authorize].");

        var pathAndQuery = http.Request.Path + http.Request.QueryString;
        var hash = RequestFingerprint.Compute(http.Request.Method, pathAndQuery, await ReadBodyAsync(http.Request));
        var begin = await store.TryBeginAsync(new IdempotencyRequest(userId, key, http.Request.Method, pathAndQuery, hash),
            http.RequestAborted);

        switch (begin.Outcome)
        {
            case IdempotencyOutcome.Replay:
                await ReplayAsync(http, begin.Response!);
                context.Result = new EmptyResult(); // the response is already written
                return;
            case IdempotencyOutcome.KeyReused:
                context.Result = Error(http, StatusCodes.Status422UnprocessableEntity, ApiErrorCodes.IdempotencyKeyReused,
                    $"This {IdempotencyHeaders.Key} was already used for a different request. Use a new key for a new action.");
                return;
            case IdempotencyOutcome.InProgress:
                http.Response.Headers.RetryAfter = "1";
                context.Result = Error(http, StatusCodes.Status409Conflict, ApiErrorCodes.IdempotencyRequestInProgress,
                    "The first request with this key is still being processed. Retry shortly.");
                return;
        }

        await RunAndRecordAsync(context, next, begin.RecordId!.Value);
    }

    private async Task RunAndRecordAsync(ResourceExecutingContext context, ResourceExecutionDelegate next, Guid recordId)
    {
        var http = context.HttpContext;
        var realBody = http.Response.Body;
        await using var buffer = new MemoryStream();
        http.Response.Body = buffer;
        try
        {
            var executed = await next();
            if (executed.Exception is { } exception && !executed.ExceptionHandled)
            {
                if (exception is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
                {
                    await store.ReleaseAsync(recordId, CancellationToken.None); // client left; let it retry with the same key
                    return; // the framework answers the abort itself
                }

                await WriteErrorAsync(http, exception);
                executed.ExceptionHandled = true;
            }
        }
        catch
        {
            await store.ReleaseAsync(recordId, CancellationToken.None);
            throw;
        }
        finally
        {
            http.Response.Body = realBody;
        }

        // AC-009-6: 5xx are not stored, so the client may retry with the same key; 2xx/4xx are the answer for this key.
        if (http.Response.StatusCode >= 500)
            await store.ReleaseAsync(recordId, CancellationToken.None);
        else
            await store.CompleteAsync(recordId, Snapshot(http.Response, buffer), CancellationToken.None);

        buffer.Position = 0;
        await buffer.CopyToAsync(realBody, http.RequestAborted);
    }

    private async Task WriteErrorAsync(HttpContext http, Exception exception)
    {
        foreach (var handler in exceptionHandlers)
            if (await handler.TryHandleAsync(http, exception, http.RequestAborted)) return;
        throw exception; // no handler took it — cannot happen with GlobalExceptionHandler registered
    }

    private static async Task ReplayAsync(HttpContext http, StoredResponse stored)
    {
        http.Response.StatusCode = stored.StatusCode;
        foreach (var (name, value) in stored.Headers) http.Response.Headers[name] = value;
        http.Response.Headers[IdempotencyHeaders.Replayed] = "true";
        await http.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(stored.Body), http.RequestAborted);
    }

    private static StoredResponse Snapshot(HttpResponse response, MemoryStream buffer)
    {
        var headers = KeptHeaders
            .Where(h => response.Headers.ContainsKey(h))
            .ToDictionary(h => h, h => response.Headers[h].ToString(), StringComparer.OrdinalIgnoreCase);
        return new StoredResponse(response.StatusCode, Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length), headers);
    }

    private static async Task<byte[]> ReadBodyAsync(HttpRequest request)
    {
        request.EnableBuffering(); // model binding reads the body again after us
        using var copy = new MemoryStream();
        await request.Body.CopyToAsync(copy, request.HttpContext.RequestAborted);
        request.Body.Position = 0;
        return copy.ToArray();
    }

    private static ObjectResult Error(HttpContext http, int status, string code, string message) =>
        new(ApiErrorWriter.Body(http, status, code, message)) { StatusCode = status };

    internal static bool IsWrite(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);
}
