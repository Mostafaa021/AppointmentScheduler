using System.Net;
using System.Net.Http.Json;
using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppointmentScheduler.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class UserAdminEndpointTests(ApiFactory api) : IAsyncLifetime
{
    private const string Password = "New-User-Pass-42";
    private HttpClient _admin = null!;

    public async Task InitializeAsync() => _admin = await api.ClientForAsync(TestUsers.Admin);

    public Task DisposeAsync() => Task.CompletedTask;

    private static string NewEmail() => $"created-{Guid.NewGuid():N}@test.local";

    private Task<HttpResponseMessage> CreateAsync(string? email, string? password = Password, string[]? roles = null, int? doctorId = null) =>
        _admin.PostAsJsonAsync("/api/v1/users", new { email, password, roles = roles ?? ["Receptionist"], doctorId });

    private async Task<bool> UserExistsAsync(string email)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.AnyAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
    }

    [Fact]
    public async Task AC_004_11_Admin_creates_a_user_who_can_log_in()
    {
        var email = NewEmail();

        var response = await CreateAsync(email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var body = await response.DataAsync();
        Assert.NotEqual(Guid.Empty, body.GetProperty("id").GetGuid());
        Assert.Equal(email, body.GetProperty("email").GetString());
        Assert.Equal(["Receptionist"], body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(HttpStatusCode.OK, (await api.CreateAnonymousClient().LoginAsync(email, Password)).StatusCode);
    }

    [Fact]
    public async Task AC_004_11_Doctor_user_is_linked_to_the_doctor()
    {
        var (d, _) = await api.CreatePeopleAsync(doctors: 1, patients: 0);
        var email = NewEmail();

        var body = await (await CreateAsync(email, roles: ["Doctor"], doctorId: d[0])).DataAsync();
        var login = await (await api.CreateAnonymousClient().LoginAsync(email, Password)).DataAsync();

        Assert.Equal(d[0], body.GetProperty("doctorId").GetInt32());
        Assert.Equal(d[0], login.GetProperty("user").GetProperty("doctorId").GetInt32());
    }

    [Theory]
    [InlineData("not-an-email", Password, new[] { "Receptionist" }, null, "email")]
    [InlineData(null, "short", new[] { "Receptionist" }, null, "password")]
    [InlineData(null, "alllowercase123", new[] { "Receptionist" }, null, "password")]
    [InlineData(null, Password, new string[0], null, "roles")]
    [InlineData(null, Password, new[] { "Janitor" }, null, "roles")]
    [InlineData(null, Password, new[] { "Doctor" }, null, "doctorId")]
    [InlineData(null, Password, new[] { "Receptionist" }, 1, "doctorId")]
    public async Task AC_004_12_Invalid_user_returns_400_and_creates_nothing(string? email, string password, string[] roles, int? doctorId, string field)
    {
        email ??= NewEmail();

        var response = await CreateAsync(email, password, roles, doctorId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await response.ErrorCodeAsync());
        var errors = (await response.JsonAsync()).GetProperty("errors");
        Assert.Equal([field], errors.EnumerateObject().Select(p => p.Name));
        Assert.False(await UserExistsAsync(email));
    }

    [Fact]
    public async Task AC_004_13_Unknown_doctor_is_404_and_taken_email_is_409_case_insensitive()
    {
        var email = NewEmail();
        var noDoctor = await CreateAsync(email, roles: ["Doctor"], doctorId: int.MaxValue);
        Assert.Equal(HttpStatusCode.Created, (await CreateAsync(email)).StatusCode);

        var taken = await CreateAsync(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.NotFound, noDoctor.StatusCode);
        Assert.Equal(ErrorCodes.DoctorNotFound, await noDoctor.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(ErrorCodes.EmailTaken, await taken.ErrorCodeAsync());
    }

    [Fact]
    public async Task AC_004_14_Set_roles_replaces_roles_and_doctor_link()
    {
        var (d, _) = await api.CreatePeopleAsync(doctors: 1, patients: 0);
        var id = (await (await CreateAsync(NewEmail(), roles: ["Doctor"], doctorId: d[0])).DataAsync()).GetProperty("id").GetGuid();

        var response = await _admin.PutAsJsonAsync($"/api/v1/users/{id}/roles", new { roles = RoleSets.AdminReceptionist });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.DataAsync();
        Assert.Equal(["Admin", "Receptionist"], body.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, body.GetProperty("doctorId").ValueKind);
        using var scope = api.Services.CreateScope();
        Assert.Null((await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Id == id)).DoctorId);
    }

    [Fact]
    public async Task AC_004_14_Set_roles_errors()
    {
        var id = (await (await CreateAsync(NewEmail())).DataAsync()).GetProperty("id").GetGuid();

        var unknownUser = await _admin.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}/roles", new { roles = RoleSets.Admin });
        var unknownDoctor = await _admin.PutAsJsonAsync($"/api/v1/users/{id}/roles", new { roles = RoleSets.Doctor, doctorId = int.MaxValue });
        var invalid = await _admin.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}/roles", new { roles = Array.Empty<string>() });

        Assert.Equal(ErrorCodes.UserNotFound, await unknownUser.ErrorCodeAsync());
        Assert.Equal(ErrorCodes.DoctorNotFound, await unknownDoctor.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); // validation before lookups
        Assert.Equal(ErrorCodes.ValidationFailed, await invalid.ErrorCodeAsync());
    }
}
