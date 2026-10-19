using System.Diagnostics;
using System.Security.Claims;
using AppointmentScheduler.Application.Common;

namespace AppointmentScheduler.Api.Auth;

/// <summary><see cref="ICurrentUser"/> from the validated JWT (<c>sub</c>, <c>role</c>, <c>doctor_id</c>; inbound claim mapping is off).</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(User?.FindFirstValue("sub"), out var id) ? id : null;

    public IReadOnlyCollection<string> Roles => User?.FindAll("role").Select(c => c.Value).ToArray() ?? [];

    public int? DoctorId => int.TryParse(User?.FindFirstValue("doctor_id"), out var id) ? id : null;

    /// <summary>W3C trace id, the same one in logs and Jaeger (NFR-8).</summary>
    public string? TraceId => Activity.Current?.TraceId.ToHexString() ?? accessor.HttpContext?.TraceIdentifier;
}
