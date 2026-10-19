using System.Net;

namespace AppointmentScheduler.IntegrationTests;

/// <summary>ADR-0017: URL-segment versioning (v1), reported versions, and unknown versions as an ApiResponse error.</summary>
[Collection(nameof(ApiCollection))]
public class ApiVersioningTests(ApiFactory api) : IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() => _client = await api.ClientForAsync(TestUsers.Receptionist);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task V1_responses_report_the_supported_versions()
    {
        var response = await _client.GetAsync("/api/v1/doctors");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("1.0", Assert.Single(response.Headers.GetValues("api-supported-versions")));
    }

    [Fact]
    public async Task Unknown_version_in_the_url_returns_404_unsupported_api_version()
    {
        var response = await _client.GetAsync("/api/v2/doctors");

        // 404 is Asp.Versioning's answer for URL-segment versions (that path does not exist); the code says why.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.JsonAsync();
        Assert.False(body.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(ErrorCodes.UnsupportedApiVersion, body.GetProperty("errorCode").GetString());
        Assert.Contains("1.0", body.GetProperty("message").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Matching_version_header_is_accepted()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/doctors");
        request.Headers.Add("X-Api-Version", "1.0");

        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Conflicting_url_and_header_versions_are_rejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/doctors");
        request.Headers.Add("X-Api-Version", "2.0");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.UnsupportedApiVersion, await response.ErrorCodeAsync());
    }
}
