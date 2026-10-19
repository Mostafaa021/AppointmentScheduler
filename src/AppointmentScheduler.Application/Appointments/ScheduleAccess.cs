using AppointmentScheduler.Application.Auth;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Application.Appointments;

/// <summary>BR-7 (ADR-0012): a user whose only role is Doctor reads only their own doctor's appointments.</summary>
public static class ScheduleAccess
{
    public static void EnsureCanRead(this ICurrentUser user, int doctorId)
    {
        var doctorOnly = user.Roles.Count > 0 && user.Roles.All(r => r == Roles.Doctor);
        if (doctorOnly && user.DoctorId != doctorId) throw new ForbiddenException(); // null DoctorId fails closed
    }
}
