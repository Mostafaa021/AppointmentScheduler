using AppointmentScheduler.Application.Auth;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AppointmentScheduler.Infrastructure.Auth;

/// <summary>
/// Spec 004 flows (AC-004-1..7, 10). Lockout uses Identity's columns but atomic conditional UPDATEs timed by
/// <see cref="TimeProvider"/> (ADR-0005 amendment), not <c>UserManager.AccessFailedAsync</c>.
/// </summary>
internal sealed class IdentityAuthService(
    UserManager<AppUser> users,
    IPasswordHasher<AppUser> hasher,
    AppDbContext db,
    TokenIssuer tokens,
    AuditLog audit,
    TimeProvider clock,
    ICurrentUser currentUser,
    IValidator<LoginRequest> loginValidator,
    ILogger<IdentityAuthService> logger) : IAuthService
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Verified against for unknown e-mails so both 401 paths cost one hash check (AC-004-2).
    private static readonly string DummyHash = new PasswordHasher<AppUser>().HashPassword(new AppUser(), "timing-equaliser-not-a-password");

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        loginValidator.ValidateOrThrow(request);

        var user = await users.FindByEmailAsync(request.Email!);
        if (user is null)
        {
            hasher.VerifyHashedPassword(new AppUser(), DummyHash, request.Password!);
            throw await LoginFailedAsync(null, InvalidCredentials(), ct);
        }

        // Reserve an attempt atomically: caps a parallel burst at MaxFailedAttempts guesses per lockout window.
        var now = clock.GetUtcNow();
        var reserved = await db.Users
            .Where(u => u.Id == user.Id && u.AccessFailedCount < MaxFailedAttempts && (u.LockoutEnd == null || u.LockoutEnd <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.AccessFailedCount, u => u.AccessFailedCount + 1), ct);
        if (reserved == 0) throw await LoginFailedAsync(user.Id, AccountLocked(), ct);

        // Not CheckPasswordAsync: its rehash path calls UpdateAsync and would write the stale tracked counter back.
        if (user.PasswordHash is null
            || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!) == PasswordVerificationResult.Failed)
        {
            var failures = await db.Users.Where(u => u.Id == user.Id).Select(u => u.AccessFailedCount).SingleAsync(ct);
            if (failures < MaxFailedAttempts) throw await LoginFailedAsync(user.Id, InvalidCredentials(), ct);

            await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s
                .SetProperty(u => u.LockoutEnd, now + LockoutDuration)
                .SetProperty(u => u.AccessFailedCount, 0), ct);
            logger.LogWarning("Account {UserId} locked after {Failures} failed logins", user.Id, failures);
            throw await LoginFailedAsync(user.Id, AccountLocked(), ct);
        }

        await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.AccessFailedCount, 0)
            .SetProperty(u => u.LockoutEnd, (DateTimeOffset?)null), ct);
        var result = await IssueAsync(user, familyId: Guid.CreateVersion7(), Guid.CreateVersion7(), now, ct);
        audit.Record(AuditActions.LoginSucceeded, "User", user.Id.ToString(), user.Id);
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(refreshToken)) throw InvalidRefreshToken();

        var hash = TokenIssuer.Hash(refreshToken);
        var token = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, ct)
            ?? throw InvalidRefreshToken();
        if (token.ReplacedById is not null) throw await RevokeFamilyAsync(token, ct);

        var now = clock.GetUtcNow();
        if (token.RevokedAtUtc is not null || token.ExpiresAtUtc <= now) throw InvalidRefreshToken();
        var user = await users.FindByIdAsync(token.UserId.ToString()) ?? throw InvalidRefreshToken();

        var successorId = Guid.CreateVersion7();
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var claimed = await db.RefreshTokens
                .Where(t => t.Id == token.Id && t.RevokedAtUtc == null && t.ReplacedById == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now).SetProperty(t => t.ReplacedById, successorId), ct);
            if (claimed == 1)
            {
                var result = await IssueAsync(user, token.FamilyId, successorId, now, ct); // roles/doctorId re-read (AC-004-5)
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return result;
            }
            await tx.RollbackAsync(ct);
        }
        throw await RevokeFamilyAsync(token, ct); // lost a concurrent rotation of the same token
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw Unauthenticated();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var hash = TokenIssuer.Hash(refreshToken);
            var now = clock.GetUtcNow();
            await db.RefreshTokens.Where(t => t.TokenHash == hash && t.UserId == userId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
        }
        audit.Record(AuditActions.Logout, "User", userId.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> GetMeAsync(CancellationToken ct)
    {
        var user = currentUser.UserId is { } id ? await users.FindByIdAsync(id.ToString()) : null;
        if (user is null) throw Unauthenticated();
        return ToDto(user, await users.GetRolesAsync(user));
    }

    internal static UserDto ToDto(AppUser user, IEnumerable<string> roles) =>
        new(user.Id, user.Email!, roles.Order(StringComparer.Ordinal).ToList(), user.DoctorId);

    private async Task<AuthResult> IssueAsync(AppUser user, Guid familyId, Guid tokenId, DateTimeOffset now, CancellationToken ct)
    {
        var roles = await users.GetRolesAsync(user);
        var (accessToken, expiresAt) = tokens.CreateAccessToken(user, roles.ToList());
        var refreshToken = TokenIssuer.NewRefreshToken();
        var entity = new RefreshToken
        {
            Id = tokenId,
            UserId = user.Id,
            TokenHash = TokenIssuer.Hash(refreshToken),
            FamilyId = familyId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + JwtOptions.RefreshTokenLifetime,
        };
        db.RefreshTokens.Add(entity);
        return new AuthResult(new AuthResponse(accessToken, expiresAt, ToDto(user, roles)), refreshToken, entity.ExpiresAtUtc);
    }

    private async Task<SchedulerException> LoginFailedAsync(Guid? userId, SchedulerException error, CancellationToken ct)
    {
        audit.Record(AuditActions.LoginFailed, "User", userId?.ToString(), userId);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Login failed ({Code}) for user {UserId}", error.ErrorCode, userId);
        return error;
    }

    /// <summary>AC-004-6: a rotated token was presented again — revoke every live token of its family.</summary>
    private async Task<SchedulerException> RevokeFamilyAsync(RefreshToken token, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.RefreshTokens.Where(t => t.FamilyId == token.FamilyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
        audit.Record(AuditActions.RefreshTokenReused, "RefreshTokenFamily", token.FamilyId.ToString(), token.UserId);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogWarning("Refresh token reuse for user {UserId}; family {FamilyId} revoked", token.UserId, token.FamilyId);
        return new UnauthorizedException(ErrorCodes.RefreshTokenReused, "The refresh token was already used. Please sign in again.")
        {
            LogDetail = $"family {token.FamilyId} revoked for user {token.UserId}",
        };
    }

    private static UnauthorizedException InvalidCredentials() =>
        new(ErrorCodes.InvalidCredentials, "The e-mail or password is incorrect.");

    private static LockedException AccountLocked() =>
        new(ErrorCodes.AccountLocked, "The account is temporarily locked after too many failed sign-in attempts. Try again later.");

    private static UnauthorizedException InvalidRefreshToken() =>
        new(ErrorCodes.InvalidRefreshToken, "The refresh token is missing, expired or revoked. Please sign in again.");

    private static UnauthorizedException Unauthenticated() =>
        new(ErrorCodes.Unauthorized, "Authentication is required.");
}
