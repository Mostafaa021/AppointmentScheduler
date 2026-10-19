namespace AppointmentScheduler.Api.Idempotency;

/// <summary>
/// Exempts a controller or a single action from <see cref="IdempotencyFilter"/> (which guards every POST). Two uses:
/// <list type="bullet">
/// <item>on <c>AuthController</c>: sign-in, refresh and logout are not resource writes and must work before the caller has a
/// user id to scope keys to (AC-009-1);</item>
/// <item>on an action that is a POST only because its criteria travel in the body, e.g. a search
/// (<c>[HttpPost("search")] [SkipIdempotency]</c>): it changes nothing, so there is nothing to deduplicate.</item>
/// </list>
/// Never put it on a POST that creates or changes data.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipIdempotencyAttribute : Attribute;
