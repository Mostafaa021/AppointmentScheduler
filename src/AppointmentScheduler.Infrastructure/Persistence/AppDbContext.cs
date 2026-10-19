using AppointmentScheduler.Application.Appointments;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Domain.Doctors;
using AppointmentScheduler.Domain.Patients;
using AppointmentScheduler.Infrastructure.Auth;
using AppointmentScheduler.Infrastructure.Idempotency;
using AppointmentScheduler.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AppointmentScheduler.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Identity model first, then our schema/table overrides
        builder.HasPostgresExtension("btree_gist");
        IdentityConfiguration.Apply(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    /// <summary>Translates DB-level guarantees into domain errors (ADR-0002, ADR-0004).</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw AppointmentConflicts.Concurrency();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation } pg)
        {
            var detail = $"exclusion constraint {pg.ConstraintName}";
            throw pg.ConstraintName == AppointmentConfiguration.PatientOverlapConstraint
                ? AppointmentConflicts.Patient(detail)
                : AppointmentConflicts.Doctor(detail);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           {
                                               SqlState: PostgresErrorCodes.UniqueViolation,
                                               ConstraintName: IdentityConfiguration.UserNameIndex,
                                           })
        {
            throw IdentityUserAdminService.EmailTaken(); // race backstop for AC-004-13
        }
        catch (Exception ex) when (IsDeadlock(ex))
        {
            // Safety net for a write path that skipped LockScheduleAsync: conflicting inserts on an exclusion constraint can
            // deadlock; PostgreSQL aborts one (40P01). That is a concurrency conflict (409), not an outage (503) — SPEC §5.
            throw new ConflictException(ErrorCodes.ConcurrencyConflict,
                "Someone else changed this schedule at the same moment. Reload and try again.") { LogDetail = "deadlock 40P01" };
        }
    }

    // EF wraps the provider error (DbUpdateException, or InvalidOperationException for "transient" errors), so walk the chain.
    private static bool IsDeadlock(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected })
                return true;
        }

        return false;
    }
}
