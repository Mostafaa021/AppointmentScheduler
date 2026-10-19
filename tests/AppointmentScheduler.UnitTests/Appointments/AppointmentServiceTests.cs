using AppointmentScheduler.Application.Appointments;
using AppointmentScheduler.Application.Auth;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Doctors;
using AppointmentScheduler.Domain.Patients;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AppointmentScheduler.UnitTests.Appointments;

/// <summary>
/// ADR-0016: with repositories + unit of work behind interfaces, the use-case order (validate → lookups → pre-check →
/// one commit) is testable without a database. Real DB behaviour stays covered by the integration tests.
/// </summary>
public class AppointmentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 5, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TomorrowNine = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero); // GMT in November

    private readonly FakeAppointments _appointments = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditRecords _audit = new();

    private AppointmentService Service(int[]? doctorIds = null, int[]? patientIds = null)
    {
        var clock = new FakeTimeProvider(Now);
        var user = new Receptionist();
        return new AppointmentService(
            _appointments, new FakeDoctors(doctorIds ?? [1]), new FakePatients(patientIds ?? [2]), _unitOfWork,
            clock, Options.Create(new ClinicOptions()), user, new AuditLog(_audit, user, clock),
            new BookAppointmentRequestValidator(), new CancelAppointmentRequestValidator(), new RescheduleAppointmentRequestValidator());
    }

    [Fact]
    public async Task AC_002_2_Doctor_overlap_pre_check_rejects_before_anything_is_staged_or_committed()
    {
        _appointments.DoctorOverlap = true;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Service().BookAsync(new BookAppointmentRequest(1, 2, TomorrowNine, "Check-up"), CancellationToken.None));

        Assert.Equal(ErrorCodes.DoctorConflict, ex.ErrorCode);
        Assert.Equal("pre-check", ex.LogDetail);
        Assert.Empty(_appointments.Added);
        Assert.Empty(_audit.Added);
        Assert.Equal(0, _unitOfWork.Commits);
    }

    [Fact]
    public async Task AC_002_7_Unknown_doctor_is_404_and_commits_nothing()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            Service(doctorIds: []).BookAsync(new BookAppointmentRequest(1, 2, TomorrowNine, "Check-up"), CancellationToken.None));

        Assert.Equal(ErrorCodes.DoctorNotFound, ex.ErrorCode);
        Assert.Equal(0, _unitOfWork.Commits);
    }

    [Fact]
    public async Task AC_004_22_Invalid_request_fails_validation_before_any_repository_call()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(doctorIds: []).BookAsync(new BookAppointmentRequest(1, 2, null, " "), CancellationToken.None));

        Assert.Equal(ErrorCodes.ValidationFailed, ex.ErrorCode); // not DOCTOR_NOT_FOUND: validation runs first
        Assert.Equal(0, _appointments.Calls);
        Assert.Equal(0, _unitOfWork.Commits);
    }

    private sealed class Receptionist : ICurrentUser
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public IReadOnlyCollection<string> Roles { get; } = [Application.Auth.Roles.Receptionist];
        public int? DoctorId => null;
        public string? TraceId => null;
    }

    private sealed class FakeDoctors(int[] ids) : IDoctorRepository
    {
        public Task<bool> ExistsAsync(int id, CancellationToken ct) => Task.FromResult(ids.Contains(id));
        public Task<IReadOnlyList<Doctor>> ListAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakePatients(int[] ids) : IPatientRepository
    {
        public Task<bool> ExistsAsync(int id, CancellationToken ct) => Task.FromResult(ids.Contains(id));
        public Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(string? term, int page, int pageSize, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class FakeAppointments : IAppointmentRepository
    {
        public bool DoctorOverlap { get; set; }
        public List<Appointment> Added { get; } = [];
        public int Calls { get; private set; }

        public void Add(Appointment appointment) => Added.Add(appointment);
        public Task<bool> DoctorHasOverlapAsync(Appointment appointment, CancellationToken ct) => Count(DoctorOverlap);
        public Task<bool> PatientHasOverlapAsync(Appointment appointment, CancellationToken ct) => Count(false);
        public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Appointment?> GetByIdReadOnlyAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Appointment>> ListDayScheduleAsync(int doctorId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<Appointment>> ListScheduledForDoctorAsync(int doctorId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<Appointment>> ListScheduledForPatientAsync(int patientId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) =>
            throw new NotSupportedException();

        private Task<bool> Count(bool result)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Commits { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Commits++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeAuditRecords : IAuditRecordRepository
    {
        public List<AuditRecord> Added { get; } = [];
        public void Add(AuditRecord record) => Added.Add(record);
    }
}
