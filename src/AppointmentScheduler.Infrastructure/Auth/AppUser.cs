using Microsoft.AspNetCore.Identity;

namespace AppointmentScheduler.Infrastructure.Auth;

/// <summary>Identity user (<c>identity.users</c>); <see cref="IdentityUser{TKey}.UserName"/> is the e-mail.</summary>
public sealed class AppUser : IdentityUser<Guid>
{
    /// <summary>Linked doctor for users with role Doctor (BR-7), otherwise null.</summary>
    public int? DoctorId { get; set; }
}
