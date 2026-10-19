using System.Net;

namespace AppointmentScheduler.IntegrationTests;

/// <summary>Own host + DB: these tests advance the clock, which would expire other tests' tokens in the shared factory.</summary>
public sealed class ClockApiFactory : ApiFactory;

[CollectionDefinition(nameof(ClockCollection))]
public sealed class ClockCollection : ICollectionFixture<ClockApiFactory>;

[Collection(nameof(ClockCollection))]
public class ClockDependentAuthTests(ClockApiFactory api)
{
    private const string Wrong = "Wrong-Password-999";
    private readonly HttpClient _anon = api.CreateAnonymousClient();

    private async Task<HttpStatusCode[]> LoginTimesAsync(string email, string password, int times)
    {
        var codes = new HttpStatusCode[times];
        for (var i = 0; i < times; i++) codes[i] = (await _anon.LoginAsync(email, password)).StatusCode;
        return codes;
    }

    [Fact]
    public async Task AC_004_3_Fifth_failure_locks_for_15_minutes()
    {
        var email = await api.CreateUserAsync();

        Assert.All(await LoginTimesAsync(email, Wrong, 4), c => Assert.Equal(HttpStatusCode.Unauthorized, c));
        var fifth = await _anon.LoginAsync(email, Wrong);
        var correctWhileLocked = await _anon.LoginAsync(email, TestUsers.Password);
        api.Clock.Advance(TimeSpan.FromMinutes(14));
        var stillLocked = await _anon.LoginAsync(email, TestUsers.Password);
        api.Clock.Advance(TimeSpan.FromMinutes(1));
        var unlocked = await _anon.LoginAsync(email, TestUsers.Password);

        Assert.Equal((HttpStatusCode)423, fifth.StatusCode);
        Assert.Equal(ErrorCodes.AccountLocked, await fifth.ErrorCodeAsync());
        Assert.Equal((HttpStatusCode)423, correctWhileLocked.StatusCode);
        Assert.Equal(ErrorCodes.AccountLocked, await correctWhileLocked.ErrorCodeAsync());
        Assert.Equal((HttpStatusCode)423, stillLocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);
    }

    [Fact]
    public async Task AC_004_3_Successful_login_resets_the_failure_count()
    {
        var email = await api.CreateUserAsync();

        await LoginTimesAsync(email, Wrong, 4);
        Assert.Equal(HttpStatusCode.OK, (await _anon.LoginAsync(email, TestUsers.Password)).StatusCode);

        Assert.All(await LoginTimesAsync(email, Wrong, 4), c => Assert.Equal(HttpStatusCode.Unauthorized, c));
    }

    [Fact]
    public async Task AC_004_3_Parallel_guesses_are_capped_at_five_per_lockout()
    {
        var email = await api.CreateUserAsync();

        var codes = (await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => _anon.LoginAsync(email, Wrong)))).Select(r => r.StatusCode).ToList();

        Assert.True(codes.Count(c => c == HttpStatusCode.Unauthorized) <= 4, string.Join(",", codes));
        Assert.All(codes, c => Assert.True(c is HttpStatusCode.Unauthorized or (HttpStatusCode)423, c.ToString()));
        Assert.Equal((HttpStatusCode)423, (await _anon.LoginAsync(email, TestUsers.Password)).StatusCode);
    }

    [Fact]
    public async Task AC_004_4_and_10_Access_token_expires_15_minutes_after_issue_with_30s_skew()
    {
        var client = await api.ClientForAsync(await api.CreateUserAsync());

        api.Clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(29));
        var withinSkew = await client.GetAsync("/api/v1/me");
        api.Clock.Advance(TimeSpan.FromSeconds(2));
        var expired = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, withinSkew.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal(ErrorCodes.Unauthorized, await expired.ErrorCodeAsync());
        Assert.Equal("Bearer", Assert.Single(expired.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task AC_004_5_Refresh_token_expires_after_7_days()
    {
        var email = await api.CreateUserAsync();
        var first = (await _anon.LoginAsync(email, TestUsers.Password)).RefreshToken();
        var second = (await _anon.LoginAsync(email, TestUsers.Password)).RefreshToken(); // issued at the same instant

        api.Clock.Advance(TimeSpan.FromDays(7) - TimeSpan.FromSeconds(1));
        var beforeExpiry = await _anon.RefreshAsync(first);
        api.Clock.Advance(TimeSpan.FromSeconds(1));
        var expired = await _anon.RefreshAsync(second);

        Assert.Equal(HttpStatusCode.OK, beforeExpiry.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Equal(ErrorCodes.InvalidRefreshToken, await expired.ErrorCodeAsync());
    }
}
