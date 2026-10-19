using AppointmentScheduler.Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppointmentScheduler.Infrastructure.Persistence.Configurations;

internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    // Created with raw SQL in the InitialCreate migration (EF cannot model exclusion constraints).
    public const string DoctorOverlapConstraint = "ex_appointments_doctor_overlap";
    public const string PatientOverlapConstraint = "ex_appointments_patient_overlap";

    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.Property(a => a.Reason).HasMaxLength(Appointment.ReasonMaxLength).IsRequired();
        b.Property(a => a.CancellationReason).HasMaxLength(Appointment.ReasonMaxLength);
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.Version).IsRowVersion(); // maps to PostgreSQL xmin

        b.HasOne(a => a.Doctor).WithMany().HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(a => a.Patient).WithMany().HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(a => new { a.DoctorId, a.StartUtc });
        b.HasIndex(a => new { a.PatientId, a.StartUtc });
        b.ToTable(t => t.HasCheckConstraint("ck_appointments_end_after_start", "end_utc > start_utc"));
    }
}

internal sealed class DoctorConfiguration : IEntityTypeConfiguration<Domain.Doctors.Doctor>
{
    public void Configure(EntityTypeBuilder<Domain.Doctors.Doctor> b)
    {
        b.Property(d => d.FullName).HasMaxLength(200).IsRequired();
        b.Property(d => d.Specialty).HasMaxLength(100).IsRequired();
    }
}

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Domain.Patients.Patient>
{
    public void Configure(EntityTypeBuilder<Domain.Patients.Patient> b)
    {
        b.Property(p => p.Mrn).HasMaxLength(20).IsRequired();
        b.HasIndex(p => p.Mrn).IsUnique();
        b.Property(p => p.FullName).HasMaxLength(200).IsRequired();
        b.Property(p => p.Phone).HasMaxLength(30).IsRequired();
    }
}
