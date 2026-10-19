namespace AppointmentScheduler.Infrastructure.Auth;

/// <summary>
/// Rotating refresh token (<c>identity.refresh_tokens</c>, ADR-0005). Only the SHA-256 hash of the cookie value is stored.
/// A rotated token has <see cref="ReplacedById"/> set; presenting it again revokes the whole <see cref="FamilyId"/>.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public Guid FamilyId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public Guid? ReplacedById { get; set; }
}
