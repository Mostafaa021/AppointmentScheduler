using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using static AppointmentScheduler.IntegrationTests.ApiFactory;

namespace AppointmentScheduler.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class AuthorizationTests(ApiFactory api)
{
    private static readonly string[] Anonymous = ["api/v1/auth/login", "api/v1/auth/refresh"];

    [Fact]
    public void AC_004_8_Every_api_endpoint_except_login_and_refresh_requires_authorization()
    {
        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            // Controller route templates have no leading slash; normalize so the sweep is routing-style agnostic.
            .Where(e => e.RoutePattern.RawText?.TrimStart('/').StartsWith("api/v1", StringComparison.Ordinal) == true)
            .ToList();

        Assert.True(endpoints.Count >= 14, $"expected all /api/v1 endpoints, found {endpoints.Count}");
        Assert.All(endpoints, e =>
        {
            var anonymous = e.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var authorized = e.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;
            if (Anonymous.Contains(e.RoutePattern.RawText!.TrimStart('/'))) Assert.True(anonymous, e.DisplayName);
            else Assert.True(authorized && !anonymous, $"{e.DisplayName} must require authorization");
        });
    }

    [Fact]
    public async Task AC_004_8_Doctor_is_forbidden_from_front_desk_and_admin_routes_and_nothing_changes()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var doctor = await api.ClientForAsync(await api.CreateDoctorUserAsync(d[0]));
        var reception = await api.ClientForAsync(TestUsers.Receptionist);
        var id = (await (await reception.BookAsync(d[0], p[0], At(10))).DataAsync()).GetProperty("id").GetGuid();

        HttpResponseMessage[] responses =
        [
            await doctor.GetAsync("/api/v1/doctors"),
            await doctor.GetAsync($"/api/v1/doctors/{d[0]}/availability?date={Day:yyyy-MM-dd}"),
            await doctor.GetAsync("/api/v1/patients"),
            await doctor.BookAsync(d[0], p[1], At(11)),
            await doctor.PostAsJsonAsync($"/api/v1/appointments/{id}/cancel", new { reason = "x" }),
            await doctor.PostAsJsonAsync($"/api/v1/appointments/{id}/reschedule", new { startUtc = At(12) }),
            await doctor.PostAsJsonAsync("/api/v1/users", new { }),
            await doctor.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}/roles", new { }),
        ];

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(ErrorCodes.Forbidden, await response.ErrorCodeAsync());
        }
        var schedule = await (await reception.GetAsync($"/api/v1/appointments?doctorId={d[0]}&date={Day:yyyy-MM-dd}")).DataAsync();
        var only = Assert.Single(schedule.EnumerateArray());
        Assert.Equal("Scheduled", only.GetProperty("status").GetString());
        Assert.Equal("10:00", only.GetProperty("localStart").GetString());
    }

    [Fact]
    public async Task AC_004_8_Receptionist_is_forbidden_from_user_admin_and_role_check_precedes_validation()
    {
        var reception = await api.ClientForAsync(TestUsers.Receptionist);

        var create = await reception.PostAsJsonAsync("/api/v1/users", new { email = "bad" }); // invalid body → still 403
        var roles = await reception.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}/roles", new { roles = RoleSets.Admin });

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(ErrorCodes.Forbidden, await create.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Forbidden, roles.StatusCode);
    }

    [Fact]
    public async Task AC_004_8_Admin_can_use_front_desk_routes()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var admin = await api.ClientForAsync(TestUsers.Admin);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/doctors")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.BookAsync(d[0], p[0], At(13))).StatusCode);
    }

    [Fact]
    public async Task AC_004_9_Doctor_reads_only_own_appointments()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var reception = await api.ClientForAsync(TestUsers.Receptionist);
        var own = (await (await reception.BookAsync(d[0], p[0], At(14))).DataAsync()).GetProperty("id").GetGuid();
        var other = (await (await reception.BookAsync(d[1], p[1], At(14))).DataAsync()).GetProperty("id").GetGuid();
        var doctor = await api.ClientForAsync(await api.CreateDoctorUserAsync(d[0]));

        var ownDay = await doctor.GetAsync($"/api/v1/appointments?doctorId={d[0]}&date={Day:yyyy-MM-dd}");
        var otherDay = await doctor.GetAsync($"/api/v1/appointments?doctorId={d[1]}&date={Day:yyyy-MM-dd}");
        var ownById = await doctor.GetAsync($"/api/v1/appointments/{own}");
        var otherById = await doctor.GetAsync($"/api/v1/appointments/{other}");
        var unknown = await doctor.GetAsync($"/api/v1/appointments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.OK, ownDay.StatusCode);
        Assert.Equal(own, Assert.Single((await ownDay.DataAsync()).EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Forbidden, otherDay.StatusCode);
        Assert.Equal(ErrorCodes.Forbidden, await otherDay.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, ownById.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherById.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(ErrorCodes.AppointmentNotFound, await unknown.ErrorCodeAsync());
    }
}
