using System.Security.Cryptography;
using System.Text;

namespace AppointmentScheduler.Api.Idempotency;

/// <summary>
/// The SHA-256 (lower-case hex) of method + path-and-query + body (AC-009-3/4): the same key with a different fingerprint is a
/// reuse. Public so tests can seed records that match a request.
/// </summary>
public static class RequestFingerprint
{
    public static string Compute(string method, string pathAndQuery, ReadOnlySpan<byte> body)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(Encoding.UTF8.GetBytes($"{method.ToUpperInvariant()}\n{pathAndQuery}\n"));
        sha.AppendData(body);
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }
}
