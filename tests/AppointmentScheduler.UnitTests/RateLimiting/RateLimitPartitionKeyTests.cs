using System.Net;
using System.Security.Claims;
using AppointmentScheduler.Api.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace AppointmentScheduler.UnitTests.RateLimiting;

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
