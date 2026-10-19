namespace AppointmentScheduler.Infrastructure.Auth;

/// <summary>
/// Section <c>Jwt</c>. <see cref="SigningKey"/> comes from <c>Jwt:SigningKey</c> (env <c>Jwt__SigningKey</c>) or, if unset,
/// <c>JWT_SIGNING_KEY</c>. Lifetimes are fixed by ADR-0005, not configuration.
/// </summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public const string SigningKeyFallback = "JWT_SIGNING_KEY";
    public const int MinKeyBytes = 32;

    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKey { get; set; } = "";
}
