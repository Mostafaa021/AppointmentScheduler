using AppointmentScheduler.Application.Auth;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Doctors;
using AppointmentScheduler.Domain.Patients;
using AppointmentScheduler.Domain.Scheduling;
using AppointmentScheduler.Infrastructure.Auth;
using AppointmentScheduler.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppointmentScheduler.Infrastructure.Persistence.Seed;

/// <summary>Fictional sample data. Appointments are placed relative to "today" so the demo always has a populated schedule.</summary>
internal static class DbSeeder
{
    // AC-004-26 demo users (Development only, fictional; documented in README). All share this password.
    public const string DemoPassword = "DemoClinic2026";
    public const string DemoAdmin = "admin@clinic.test";
    public const string DemoReceptionist = "reception@clinic.test";
    public const string DemoDoctor = "doctor@clinic.test"; // linked to the first seeded doctor (Dr. Amelia Hart)

    public static async Task SeedAsync(DbContext db, DaySchedule schedule, DateTimeOffset nowUtc, bool includeDemoUsers, CancellationToken ct)
    {
        if (!await db.Set<Doctor>().AnyAsync(ct)) await SeedScheduleAsync(db, schedule, nowUtc, ct);
        if (includeDemoUsers && !await db.Set<AppUser>().AnyAsync(ct)) await SeedDemoUsersAsync(db, ct);
    }

    private static async Task SeedScheduleAsync(DbContext db, DaySchedule schedule, DateTimeOffset nowUtc, CancellationToken ct)
    {
        Doctor[] doctors =
        [
            new("Dr. Amelia Hart", "General Practice"),
            new("Dr. Omar Rahman", "Cardiology"),
            new("Dr. Sofia Lindqvist", "Dermatology"),
            new("Dr. Kenji Watanabe", "Pediatrics"),
        ];
        Patient[] patients =
        [
            new("MRN-0001", "Liam Carter", new DateOnly(1985, 3, 14), "+44 20 7946 0001"),
            new("MRN-0002", "Nora Bennett", new DateOnly(1992, 7, 2), "+44 20 7946 0002"),
            new("MRN-0003", "Yusuf Haddad", new DateOnly(1978, 11, 23), "+44 20 7946 0003"),
            new("MRN-0004", "Chloe Dubois", new DateOnly(2001, 1, 30), "+44 20 7946 0004"),
            new("MRN-0005", "Mateo Alvarez", new DateOnly(1969, 9, 9), "+44 20 7946 0005"),
            new("MRN-0006", "Priya Nair", new DateOnly(1995, 5, 18), "+44 20 7946 0006"),
            new("MRN-0007", "Ethan Brooks", new DateOnly(2015, 12, 1), "+44 20 7946 0007"),
            new("MRN-0008", "Hana Sato", new DateOnly(1988, 4, 27), "+44 20 7946 0008"),
            new("MRN-0009", "Lucas Moreau", new DateOnly(1974, 6, 6), "+44 20 7946 0009"),
            new("MRN-0010", "Aisha Okafor", new DateOnly(1999, 10, 12), "+44 20 7946 0010"),
            new("MRN-0011", "Freya Johansson", new DateOnly(2010, 2, 20), "+44 20 7946 0011"),
            new("MRN-0012", "Daniel Kim", new DateOnly(1963, 8, 15), "+44 20 7946 0012"),
        ];
        db.AddRange(doctors);
        db.AddRange(patients);
        await db.SaveChangesAsync(ct);

        string[] reasons = ["Annual check-up", "Follow-up visit", "Blood pressure review", "Skin rash", "Vaccination", "Chest pain review"];
        var today = schedule.LocalDateOf(nowUtc);
        var p = 0;
        foreach (var date in new[] { today, today.AddDays(1) })
        {
            var futureSlots = schedule.SlotsFor(date).Where(s => s.StartUtc > nowUtc).ToList();
            foreach (var (doctor, i) in doctors.Select((d, i) => (d, i)))
            {
                // Two appointments per doctor per day at different slots; patients rotate so nobody overlaps (BR-2).
                foreach (var slot in futureSlots.Where((_, idx) => idx % 5 == i).Take(2))
                {
                    var patient = patients[p++ % patients.Length];
                    db.Add(Appointment.Schedule(doctor.Id, patient.Id, slot.StartUtc, reasons[p % reasons.Length], schedule, nowUtc));
                }
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedDemoUsersAsync(DbContext db, CancellationToken ct)
    {
        var doctorId = await db.Set<Doctor>().OrderBy(d => d.Id).Select(d => d.Id).FirstAsync(ct);
        var hasher = new PasswordHasher<AppUser>();
        foreach (var (email, role, linkedDoctor) in new[]
                 {
                     (DemoAdmin, Roles.Admin, (int?)null),
                     (DemoReceptionist, Roles.Receptionist, null),
                     (DemoDoctor, Roles.Doctor, doctorId),
                 })
        {
            // Same normalisation as Identity's UpperInvariantLookupNormalizer, so FindByEmailAsync finds them.
            var user = new AppUser
            {
                Id = Guid.CreateVersion7(),
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                LockoutEnabled = true,
                DoctorId = linkedDoctor,
            };
            user.PasswordHash = hasher.HashPassword(user, DemoPassword);
            db.Add(user);
            db.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = IdentityConfiguration.RoleIds[role] });
        }
        await db.SaveChangesAsync(ct);
    }
}
