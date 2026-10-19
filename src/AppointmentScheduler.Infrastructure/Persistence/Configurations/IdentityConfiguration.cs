using AppointmentScheduler.Application.Auth;
using AppointmentScheduler.Domain.Doctors;
using AppointmentScheduler.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Infrastructure.Persistence.Configurations;

/// <summary>Identity tables in schema <c>identity</c> with short names; roles seeded with fixed ids (spec 004 Data changes).</summary>
internal static class IdentityConfiguration
{
    public const string Schema = "identity";
    public const string UserNameIndex = "ix_users_normalized_user_name";

    public static readonly IReadOnlyDictionary<string, Guid> RoleIds = new Dictionary<string, Guid>
    {
        [Roles.Admin] = new("0199f0a0-0000-7000-8000-000000000001"),
        [Roles.Receptionist] = new("0199f0a0-0000-7000-8000-000000000002"),
        [Roles.Doctor] = new("0199f0a0-0000-7000-8000-000000000003"),
    };

    public static void Apply(ModelBuilder b)
    {
        b.Entity<AppUser>(u =>
        {
            u.ToTable("users", Schema);
            u.HasIndex(x => x.NormalizedUserName).HasDatabaseName(UserNameIndex).IsUnique();
            u.HasOne<Doctor>().WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<IdentityRole<Guid>>(r =>
        {
            r.ToTable("roles", Schema);
            r.HasData(RoleIds.Select(role => new IdentityRole<Guid>
            {
                Id = role.Value,
                Name = role.Key,
                NormalizedName = role.Key.ToUpperInvariant(),
                ConcurrencyStamp = role.Value.ToString(), // fixed, so the model snapshot stays stable
            }));
        });
        b.Entity<IdentityUserRole<Guid>>().ToTable("user_roles", Schema);
        b.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims", Schema);
        b.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins", Schema);
        b.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens", Schema);
        b.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims", Schema);
    }
}
