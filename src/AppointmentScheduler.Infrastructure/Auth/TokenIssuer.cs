using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AppointmentScheduler.Infrastructure.Auth;

/// <summary>Issues HS256 access tokens (times from <see cref="TimeProvider"/>) and random refresh tokens.</summary>
public sealed class TokenIssuer(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credentials = new(SigningKey(options.Value), SecurityAlgorithms.HmacSha256);

    public static SymmetricSecurityKey SigningKey(JwtOptions o) => new(Encoding.UTF8.GetBytes(o.SigningKey));

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(AppUser user, IReadOnlyCollection<string> roles)
    {
        var now = clock.GetUtcNow();
        var expiresAt = now + JwtOptions.AccessTokenLifetime;
        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            ["role"] = roles.ToArray(),
        };
        if (user.DoctorId is { } doctorId) claims["doctor_id"] = doctorId;

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Value.Issuer,
            Audience = options.Value.Audience,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
        });
        return (token, expiresAt);
    }

    /// <summary>256 random bits, base64url (cookie-safe).</summary>
    public static string NewRefreshToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static byte[] Hash(string refreshToken) => SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
}
