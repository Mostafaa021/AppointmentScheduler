using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using AppointmentScheduler.Api.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace AppointmentScheduler.IntegrationTests;

/// <summary>Low limits so tests can reach them: 5 logins/min per IP, 5 reads/min and a burst of 3 writes per user.</summary>
public sealed class RateLimitedFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "5");
        builder.UseSetting("RateLimiting:Auth:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:Reads:PermitLimit", "5");
        builder.UseSetting("RateLimiting:Reads:WindowSeconds", "60");
        builder.UseSetting("RateLimiting:Writes:TokenLimit", "3");
        builder.UseSetting("RateLimiting:Writes:TokensPerPeriod", "1");
        builder.UseSetting("RateLimiting:Writes:ReplenishmentSeconds", "60");
    }
}

/// <summary>NFR-6 / ADR-0009: the login limit per IP. Own fixture, because it uses up the IP's login budget.</summary>
public class AuthRateLimitTests(RateLimitedFactory api) : IClassFixture<RateLimitedFactory>
{
    [Fact]
    public async Task Sixth_login_in_a_minute_is_429_with_retry_after_and_the_envelope()
    {
        var email = await api.CreateUserAsync();
        var client = api.CreateAnonymousClient();

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.LoginAsync(email, TestUsers.Password)).StatusCode);
        var sixth = await client.LoginAsync(email, TestUsers.Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.True(sixth.Headers.RetryAfter?.Delta >= TimeSpan.FromSeconds(1), $"Retry-After: {sixth.Headers.RetryAfter}");
        var body = await sixth.JsonAsync();
        Assert.False(body.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(ApiErrorCodes.RateLimited, body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Health_is_never_limited()
    {
        var client = api.CreateAnonymousClient();

        for (var i = 0; i < 20; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
}

/// <summary>NFR-6 / ADR-0009: reads and writes are limited per user, so one busy user never blocks another.</summary>
public class UserRateLimitTests(RateLimitedFactory api) : IClassFixture<RateLimitedFactory>
{
    [Fact]
    public async Task Writes_beyond_the_burst_are_429_and_another_user_is_unaffected()
    {
        var busy = await api.ClientForAsync(await api.CreateUserAsync());
        var other = await api.ClientForAsync(await api.CreateUserAsync());

        // Unknown appointment: each write is a 404, which still spends a token.
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.NotFound, (await CancelUnknownAsync(busy)).StatusCode);
        var limited = await CancelUnknownAsync(busy);
        var otherUser = await CancelUnknownAsync(other);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(ApiErrorCodes.RateLimited, await limited.ErrorCodeAsync());
        Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.NotFound, otherUser.StatusCode);
    }

    [Fact]
    public async Task Reads_beyond_the_window_limit_are_429()
    {
        var client = await api.ClientForAsync(await api.CreateUserAsync());

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/doctors")).StatusCode);
        var sixth = await client.GetAsync("/api/v1/doctors");

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        Assert.Equal(ApiErrorCodes.RateLimited, await sixth.ErrorCodeAsync());
    }

    private static Task<HttpResponseMessage> CancelUnknownAsync(HttpClient client) =>
        client.PostAsJsonAsync($"/api/v1/appointments/{Guid.NewGuid()}/cancel", new { reason = "x" });
}

/// <summary>Who a request counts against (ADR-0009).</summary>
public class RateLimitPartitionKeyTests
{
    [Fact]
    public void Signed_in_requests_count_per_user()
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u-1")], "test")) };

        Assert.Equal("user:u-1", RateLimitPartitionKey.For(http, RateLimitPolicies.Writes));
    }

    [Fact]
    public void Anonymous_requests_and_auth_endpoints_count_per_ip()
    {
        var signedIn = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u-1")], "test")) };
        signedIn.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.7");
        var anonymous = new DefaultHttpContext();
        anonymous.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.8");

        Assert.Equal("ip:10.0.0.7", RateLimitPartitionKey.For(signedIn, RateLimitPolicies.Auth)); // login ignores the user
        Assert.Equal("ip:10.0.0.8", RateLimitPartitionKey.For(anonymous, RateLimitPolicies.Reads));
    }
}
