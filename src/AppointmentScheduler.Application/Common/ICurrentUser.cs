namespace AppointmentScheduler.Application.Common;

/// <summary>The authenticated caller of the current request (implemented in Api from the JWT claims).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    IReadOnlyCollection<string> Roles { get; }
    int? DoctorId { get; }
    string? TraceId { get; }
}
