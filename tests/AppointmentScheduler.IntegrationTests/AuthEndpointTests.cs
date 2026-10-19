using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AppointmentScheduler.Infrastructure.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AppointmentScheduler.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class AuthEndpointTests(ApiFactory api)
{
    private readonly HttpClient _anon = api.CreateAnonymousClient();

    private async Task<(string Email, HttpResponseMessage Login)> LoginNewUserAsync(string role = "Receptionist")
    {
        var email = await api.CreateUserAsync(role: role);
        return (email, await _anon.LoginAsync(email, TestUsers.Password));
    }

    private string Token(string? key = null, string issuer = ApiFactory.Issuer, string audience = ApiFactory.Audience,
        DateTimeOffset? expires = null, string sub = "00000000-0000-0000-0000-000000000001")
    {
        var exp = expires ?? api.Clock.GetUtcNow().AddMinutes(15);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = new Dictionary<string, object> { ["sub"] = sub, ["role"] = RoleSets.Admin },
            IssuedAt = exp.AddMinutes(-15).UtcDateTime,
            NotBefore = exp.AddMinutes(-15).UtcDateTime,
            Expires = exp.UtcDateTime,
            SigningCredentials = new SigningCredentials(TokenIssuer.SigningKey(new JwtOptions { SigningKey = key ?? api.SigningKey }),
                SecurityAlgorithms.HmacSha256),
        });
    }

    private Task<HttpResponseMessage> MeAsync(string? bearer)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        if (bearer is not null) request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearer}");
        return _anon.SendAsync(request);
    }

    private static async Task AssertUnauthorizedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
        Assert.Equal(ErrorCodes.Unauthorized, await response.ErrorCodeAsync());
    }

    [Fact]
    public async Task AC_004_1_Login_returns_token_user_and_refresh_cookie()
    {
        var (email, response) = await LoginNewUserAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.DataAsync();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.Equal(api.Clock.GetUtcNow().AddMinutes(15), body.GetProperty("expiresAt").GetDateTimeOffset());
        var user = body.GetProperty("user");
        Assert.NotEqual(Guid.Empty, user.GetProperty("id").GetGuid());
        Assert.Equal(email, user.GetProperty("email").GetString());
        Assert.Equal(["Receptionist"], user.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));

        var cookie = response.RefreshSetCookie().ToLowerInvariant();
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/v1/auth", cookie);
        Assert.NotEmpty(response.RefreshToken());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(" ", "")]
    public async Task AC_004_1_Missing_email_or_password_returns_400(string? email, string? password)
    {
        var response = await _anon.LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, await response.ErrorCodeAsync());
        var errors = (await response.JsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task AC_004_2_Unknown_email_and_wrong_password_return_identical_401()
    {
        var email = await api.CreateUserAsync();

        var unknown = await _anon.LoginAsync($"nobody-{Guid.NewGuid():N}@test.local", TestUsers.Password);
        var wrong = await _anon.LoginAsync(email, "Wrong-Password-999");

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, await unknown.ErrorCodeAsync());
        Assert.Equal(await WithoutPerRequestFieldsAsync(unknown), await WithoutPerRequestFieldsAsync(wrong));
    }

    private static async Task<string> WithoutPerRequestFieldsAsync(HttpResponseMessage response)
    {
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        json.Remove("traceId");   // per-request correlation id, not response content
        json.Remove("timestamp"); // when the body was built, not what it says
        return json.ToJsonString();
    }

    [Theory]
    [InlineData("GET", "/api/v1/me")]
    [InlineData("POST", "/api/v1/auth/logout")]
    [InlineData("GET", "/api/v1/doctors")]
    [InlineData("GET", "/api/v1/doctors/1/availability?date=2026-11-02")]
    [InlineData("GET", "/api/v1/patients")]
    [InlineData("GET", "/api/v1/appointments?doctorId=1&date=2026-11-02")]
    [InlineData("GET", "/api/v1/appointments/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/appointments")]
    [InlineData("POST", "/api/v1/appointments/00000000-0000-0000-0000-000000000001/cancel")]
    [InlineData("POST", "/api/v1/appointments/00000000-0000-0000-0000-000000000001/reschedule")]
    [InlineData("POST", "/api/v1/users")]
    [InlineData("PUT", "/api/v1/users/00000000-0000-0000-0000-000000000001/roles")]
    public async Task AC_004_4_Every_protected_route_without_token_returns_401(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method != "GET") request.Content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        await AssertUnauthorizedAsync(await _anon.SendAsync(request)); // 401 before binding/validation (AC-004-8)
    }

    [Fact]
    public async Task AC_004_4_Malformed_bad_signature_issuer_audience_or_expired_token_returns_401()
    {
        var adminId = (await (await _anon.LoginAsync(TestUsers.Admin, TestUsers.Password)).DataAsync())
            .GetProperty("user").GetProperty("id").GetString()!;
        var now = api.Clock.GetUtcNow();

        Assert.Equal(HttpStatusCode.OK, (await MeAsync(Token(sub: adminId))).StatusCode); // control: crafted valid token
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(Token(sub: adminId, expires: now.AddSeconds(-29)))).StatusCode); // 30 s skew

        await AssertUnauthorizedAsync(await MeAsync(null));
        await AssertUnauthorizedAsync(await MeAsync("not-a-jwt"));
        await AssertUnauthorizedAsync(await MeAsync(Token(key: Convert.ToBase64String(new byte[48]), sub: adminId)));
        await AssertUnauthorizedAsync(await MeAsync(Token(issuer: "someone-else", sub: adminId)));
        await AssertUnauthorizedAsync(await MeAsync(Token(audience: "someone-else", sub: adminId)));
        await AssertUnauthorizedAsync(await MeAsync(Token(sub: adminId, expires: now.AddSeconds(-31))));
    }

    [Fact]
    public async Task AC_004_5_Refresh_rotates_the_cookie_and_returns_the_login_shape()
    {
        var (email, login) = await LoginNewUserAsync();
        var first = login.RefreshToken();

        var refreshed = await _anon.RefreshAsync(first);

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var body = await refreshed.DataAsync();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.Equal(email, body.GetProperty("user").GetProperty("email").GetString());
        var second = refreshed.RefreshToken();
        Assert.NotEqual(first, second);
        Assert.Contains("path=/api/v1/auth", refreshed.RefreshSetCookie().ToLowerInvariant());
        Assert.Equal(HttpStatusCode.OK, (await _anon.RefreshAsync(second)).StatusCode);
    }

    [Fact]
    public async Task AC_004_5_Missing_or_unknown_refresh_token_returns_401()
    {
        var none = await _anon.RefreshAsync(null);
        var unknown = await _anon.RefreshAsync("not-a-real-token");

        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, await none.ErrorCodeAsync());
        Assert.Equal(ErrorCodes.InvalidRefreshToken, await unknown.ErrorCodeAsync());
    }

    [Fact]
    public async Task AC_004_5_and_14_Refresh_reads_the_users_current_roles_and_doctor()
    {
        var (d, _) = await api.CreatePeopleAsync(doctors: 1, patients: 0);
        var (_, login) = await LoginNewUserAsync();
        var userId = (await login.DataAsync()).GetProperty("user").GetProperty("id").GetGuid();
        var admin = await api.ClientForAsync(TestUsers.Admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/users/{userId}/roles", new { roles = RoleSets.Doctor, doctorId = d[0] })).StatusCode);

        var user = (await (await _anon.RefreshAsync(login.RefreshToken())).DataAsync()).GetProperty("user");

        Assert.Equal(["Doctor"], user.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(d[0], user.GetProperty("doctorId").GetInt32());
    }

    [Fact]
    public async Task AC_004_6_Reused_refresh_token_revokes_family()
    {
        var (_, login) = await LoginNewUserAsync();
        var first = login.RefreshToken();
        var second = (await _anon.RefreshAsync(first)).RefreshToken();

        var reuse = await _anon.RefreshAsync(first);
        var newest = await _anon.RefreshAsync(second);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(ErrorCodes.RefreshTokenReused, await reuse.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, newest.StatusCode);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, await newest.ErrorCodeAsync());
    }

    [Fact]
    public async Task AC_004_6_Concurrent_refreshes_of_one_token_let_exactly_one_succeed()
    {
        var (_, login) = await LoginNewUserAsync();
        var token = login.RefreshToken();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => _anon.RefreshAsync(token)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }

    [Fact]
    public async Task AC_004_7_Logout_revokes_the_refresh_token_and_clears_the_cookie()
    {
        var (_, login) = await LoginNewUserAsync();
        var accessToken = (await login.DataAsync()).GetProperty("accessToken").GetString()!;
        var refreshToken = login.RefreshToken();
        _anon.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

        var logout = await _anon.PostWithRefreshCookieAsync("/api/v1/auth/logout", refreshToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var cleared = logout.RefreshSetCookie().ToLowerInvariant();
        Assert.Contains("expires=thu, 01 jan 1970", cleared);
        Assert.Contains("path=/api/v1/auth", cleared);
        _anon.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(ErrorCodes.InvalidRefreshToken, await (await _anon.RefreshAsync(refreshToken)).ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(accessToken)).StatusCode); // access token lives until it expires
    }

    [Fact]
    public async Task AC_004_7_Logout_without_cookie_is_204_and_without_bearer_is_401()
    {
        var client = await api.ClientForAsync(await api.CreateUserAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        await AssertUnauthorizedAsync(await _anon.PostWithRefreshCookieAsync("/api/v1/auth/logout", "whatever"));
    }

    [Fact]
    public async Task AC_004_10_Me_returns_the_caller()
    {
        var (d, _) = await api.CreatePeopleAsync(doctors: 1, patients: 0);
        var email = await api.CreateDoctorUserAsync(d[0]);
        var client = await api.ClientForAsync(email);

        var me = await (await client.GetAsync("/api/v1/me")).DataAsync();

        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal(["Doctor"], me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(d[0], me.GetProperty("doctorId").GetInt32());
        Assert.NotEqual(Guid.Empty, me.GetProperty("id").GetGuid());
    }
}
