using System.Security.Cryptography;
using AppointmentScheduler.Infrastructure.Auth;
using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppointmentScheduler.IntegrationTests;

[Collection(nameof(ApiCollection))]
public class PurgeExpiredRefreshTokensJobTests(ApiFactory api)
{
    [Fact]
    public async Task Deletes_only_tokens_expired_longer_than_the_grace_period()
    {
        var now = api.Clock.GetUtcNow();
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.Email == TestUsers.Admin).Select(u => u.Id).SingleAsync();

        RefreshToken Token(DateTimeOffset expiresAt) => new()
        {
            Id = Guid.CreateVersion7(), UserId = userId, FamilyId = Guid.NewGuid(), TokenHash = RandomNumberGenerator.GetBytes(32),
            CreatedAtUtc = expiresAt - TimeSpan.FromDays(7), ExpiresAtUtc = expiresAt,
        };
        var longExpired = Token(now - TimeSpan.FromDays(3));
        var justExpired = Token(now - TimeSpan.FromHours(2)); // inside the 1-day grace
        var valid = Token(now + TimeSpan.FromDays(5));
        db.RefreshTokens.AddRange(longExpired, justExpired, valid);
        await db.SaveChangesAsync();

        var deleted = await scope.ServiceProvider.GetRequiredService<PurgeExpiredRefreshTokensJob>().RunAsync(CancellationToken.None);

        var remaining = await db.RefreshTokens.AsNoTracking().Where(t => t.UserId == userId).Select(t => t.Id).ToListAsync();
        Assert.True(deleted >= 1);
        Assert.DoesNotContain(longExpired.Id, remaining);
        Assert.Contains(justExpired.Id, remaining);
        Assert.Contains(valid.Id, remaining);
    }
}
