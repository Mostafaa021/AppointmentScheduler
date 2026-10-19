namespace AppointmentScheduler.Api.Idempotency;

/// <summary>
/// Exempts a controller or action from <see cref="IdempotencyFilter"/>. Only <c>AuthController</c> carries it: sign-in, refresh
/// and logout are not resource writes and must work before the caller has a user id to scope keys to (AC-009-1).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipIdempotencyAttribute : Attribute;
