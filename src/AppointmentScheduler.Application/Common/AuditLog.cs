using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Application.Common;

public static class AuditActions
{
    public const string LoginSucceeded = "auth.login_succeeded";
    public const string LoginFailed = "auth.login_failed";
    public const string Logout = "auth.logout";
    public const string RefreshTokenReused = "auth.refresh_token_reused";
    public const string AppointmentBooked = "appointment.booked";
    public const string AppointmentCancelled = "appointment.cancelled";
    public const string AppointmentRescheduled = "appointment.rescheduled";
    public const string UserCreated = "user.created";
    public const string UserRolesChanged = "user.roles_changed";
}

/// <summary>
/// ADR-0011: stages an audit row in the current unit of work; the caller's single <c>IUnitOfWork.SaveChangesAsync</c> (or, in
/// Infrastructure, <c>AppDbContext.SaveChangesAsync</c>) commits it together with the change, so a rejected change leaves no record.
/// </summary>
public sealed class AuditLog(IUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider clock)
{
    public void Record(string action, string entityType, string? entityId) =>
        Record(action, entityType, entityId, currentUser.UserId);

    /// <summary>For events without an authenticated caller (login, refresh-token reuse).</summary>
    public void Record(string action, string entityType, string? entityId, Guid? actingUserId) =>
        unitOfWork.AuditRecords.Add(new AuditRecord(actingUserId, action, entityType, entityId, clock.GetUtcNow(), currentUser.TraceId));
}
