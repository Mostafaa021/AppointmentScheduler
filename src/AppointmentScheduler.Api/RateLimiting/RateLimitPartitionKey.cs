using System.Security.Claims;

namespace AppointmentScheduler.Api.RateLimiting;

/// <summary>Who a request counts against: the signed-in user (<c>sub</c>), else the client IP. Auth endpoints always use the IP.</summary>
public static class RateLimitPartitionKey
{
    public static string For(HttpContext http, string policy)
    {
        var userId = http.User.FindFirstValue("sub");
        return policy != RateLimitPolicies.Auth && !string.IsNullOrEmpty(userId)
            ? $"user:{userId}"
            // shortcut: RemoteIpAddress is the proxy's address behind a reverse proxy; add ForwardedHeaders with the
            // container setup (M6, S-61) so every client doesn't share one partition.
            : $"ip:{http.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
