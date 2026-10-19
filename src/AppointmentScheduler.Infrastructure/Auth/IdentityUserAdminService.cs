using AppointmentScheduler.Application.Auth;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AppointmentScheduler.Infrastructure.Auth;

/// <summary>AC-004-11..14. Order: validate → lookups (404) → uniqueness (409) → one transaction incl. the audit row.</summary>
internal sealed class IdentityUserAdminService(
    UserManager<AppUser> users,
    AppDbContext db,
    AuditLog audit,
    ICurrentUser currentUser,
    IValidator<CreateUserRequest> createValidator,
    IValidator<SetUserRolesRequest> setRolesValidator,
    ILogger<IdentityUserAdminService> logger) : IUserAdminService
{
    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        createValidator.ValidateOrThrow(request);
        await EnsureDoctorExistsAsync(request.DoctorId, ct);
        if (await users.FindByEmailAsync(request.Email!) is not null) throw EmailTaken();

        var user = new AppUser { Id = Guid.CreateVersion7(), UserName = request.Email, Email = request.Email, DoctorId = request.DoctorId };
        var roles = request.Roles!.Distinct().ToList();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        Check(await users.CreateAsync(user, request.Password!));
        Check(await users.AddToRolesAsync(user, roles));
        audit.Record(AuditActions.UserCreated, "User", user.Id.ToString());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("User {UserId} created by {ActorId}", user.Id, currentUser.UserId);
        return IdentityAuthService.ToDto(user, roles);
    }

    public async Task<UserDto> SetRolesAsync(Guid id, SetUserRolesRequest request, CancellationToken ct)
    {
        setRolesValidator.ValidateOrThrow(request);
        var user = await users.FindByIdAsync(id.ToString())
            ?? throw new NotFoundException("user_not_found", $"User {id} was not found.");
        await EnsureDoctorExistsAsync(request.DoctorId, ct);

        var target = request.Roles!.Distinct().ToList();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await users.GetRolesAsync(user);
        Check(await users.RemoveFromRolesAsync(user, current.Except(target)));
        Check(await users.AddToRolesAsync(user, target.Except(current)));
        user.DoctorId = request.DoctorId; // the validator guarantees null when role Doctor is absent
        Check(await users.UpdateAsync(user));
        audit.Record(AuditActions.UserRolesChanged, "User", user.Id.ToString());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        logger.LogInformation("Roles of user {UserId} changed by {ActorId}", user.Id, currentUser.UserId);
        return IdentityAuthService.ToDto(user, target);
    }

    private async Task EnsureDoctorExistsAsync(int? doctorId, CancellationToken ct)
    {
        if (doctorId is { } d && !await db.Doctors.AnyAsync(x => x.Id == d, ct))
            throw new NotFoundException("doctor_not_found", $"Doctor {d} was not found.");
    }

    /// <summary>Duplicate e-mail → 409; any other Identity failure means our validator and Identity disagree → 500.</summary>
    private static void Check(IdentityResult result)
    {
        if (result.Succeeded) return;
        var codes = result.Errors.Select(e => e.Code).ToList(); // codes only: descriptions may echo the e-mail
        if (codes.Contains(nameof(IdentityErrorDescriber.DuplicateUserName)) || codes.Contains(nameof(IdentityErrorDescriber.DuplicateEmail)))
            throw EmailTaken();
        throw new InvalidOperationException($"Identity rejected the change: {string.Join(", ", codes)}");
    }

    internal static ConflictException EmailTaken() => new("email_taken", "A user with this e-mail already exists.");
}
