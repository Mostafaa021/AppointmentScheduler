using System.Net;
using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppointmentScheduler.IntegrationTests;

/// <summary>Development with the normal seed (demo data + demo users).</summary>
public sealed class SeededDevelopmentFactory : ApiFactory
{
    protected override bool SeedEnabled => true;
}

/// <summary>A non-Development environment with Seed:Enabled. It does not auto-migrate, so the test host migrates once.</summary>
public sealed class StagingFactory : ApiFactory
{
    protected override string EnvironmentName => "Staging";
    protected override bool SeedEnabled => true;

    public override async Task InitializeAsync()
    {
        await StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(); // runs the seeder too
    }
}

public class DevelopmentSeedTests(SeededDevelopmentFactory api) : IClassFixture<SeededDevelopmentFactory>
{
    [Theory]
    [InlineData("admin@clinic.test", "Admin")]
    [InlineData("reception@clinic.test", "Receptionist")]
    [InlineData("doctor@clinic.test", "Doctor")]
    public async Task AC_004_26_Demo_users_can_log_in_in_Development(string email, string role)
    {
        var response = await api.CreateAnonymousClient().LoginAsync(email, "DemoClinic2026");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = (await response.DataAsync()).GetProperty("user");
        Assert.Equal([role], user.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        if (role == "Doctor")
        {
            using var scope = api.Services.CreateScope();
            var firstDoctor = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Doctors.MinAsync(d => d.Id);
            Assert.Equal(firstDoctor, user.GetProperty("doctorId").GetInt32());
        }
    }
}

public class StagingEnvironmentTests(StagingFactory api) : IClassFixture<StagingFactory>
{
    [Fact]
    public async Task AC_004_26_No_demo_users_outside_Development()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.True(await db.Doctors.AnyAsync()); // the seed ran…
        Assert.False(await db.Users.AnyAsync());  // …without demo users
    }

    [Fact]
    public async Task Unexpected_exception_leaks_no_details_outside_Development()
    {
        var response = await api.CreateClient().GetAsync("/test/errors/unexpected");

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var body = await response.JsonAsync();
        Assert.Equal(ErrorCodes.InternalError, body.GetProperty("errorCode").GetString());
        Assert.False(body.TryGetProperty("errorDetails", out _));
        Assert.DoesNotContain("hunter2", raw);
        Assert.DoesNotContain("InvalidOperationException", raw);
    }

    [Fact]
    public async Task AC_004_23_Hsts_outside_Development_over_https()
    {
        var https = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://api.clinic.test") });

        var response = await https.GetAsync("/health/live");

        var hsts = Assert.Single(response.Headers.GetValues("Strict-Transport-Security"));
        Assert.True(int.Parse(hsts.Split(';')[0].Split('=')[1]) >= 31536000, hsts);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }
}
