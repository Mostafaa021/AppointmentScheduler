using AppointmentScheduler.Api.Idempotency;

namespace AppointmentScheduler.UnitTests.Idempotency;

/// <summary>Spec 009 / plan 012: which requests need an <c>Idempotency-Key</c>.</summary>
public class IdempotencyScopeTests
{
    [Fact]
    public void AC_009_1_Post_needs_a_key()
    {
        Assert.True(IdempotencyFilter.Applies("POST", []));
    }

    [Theory] // idempotent by HTTP design, or reads
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("GET")]
    public void Other_verbs_need_no_key(string method)
    {
        Assert.False(IdempotencyFilter.Applies(method, []));
    }

    [Fact]
    public void A_post_marked_SkipIdempotency_needs_no_key()
    {
        // MVC puts controller and action attributes into the endpoint metadata, so one list covers both placements.
        Assert.False(IdempotencyFilter.Applies("POST", [new SkipIdempotencyAttribute()]));
    }
}
