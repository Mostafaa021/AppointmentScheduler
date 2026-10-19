using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static AppointmentScheduler.IntegrationTests.ApiFactory;

namespace AppointmentScheduler.IntegrationTests;

/// <summary>
/// ADR-0002: the database alone rejects overlaps, even when the application pre-check is skipped
/// (which is what happens to the loser of a race).
/// </summary>
[Collection(nameof(ApiCollection))]
public class ExclusionConstraintTests(ApiFactory api)
{
    private async Task SaveDirectlyAsync(params Appointment[] appointments)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Appointments.AddRange(appointments);
        await db.SaveChangesAsync();
    }

    private Appointment New(int doctorId, int patientId, int hour, int minute = 0)
    {
        using var scope = api.Services.CreateScope();
        var schedule = scope.ServiceProvider.GetRequiredService<IOptions<ClinicOptions>>().Value.ToDaySchedule();
        return Appointment.Schedule(doctorId, patientId, At(hour, minute), "x", schedule, api.Clock.GetUtcNow());
    }

    [Fact]
    public async Task BR_1_Db_rejects_doctor_overlap_with_doctor_conflict()
    {
        var (d, p) = await api.CreatePeopleAsync();
        await SaveDirectlyAsync(New(d[0], p[0], 10));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => SaveDirectlyAsync(New(d[0], p[1], 10)));

        Assert.Equal("doctor_conflict", ex.Code);
    }

    [Fact]
    public async Task BR_2_Db_rejects_patient_overlap_with_patient_conflict()
    {
        var (d, p) = await api.CreatePeopleAsync();
        await SaveDirectlyAsync(New(d[0], p[0], 11));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => SaveDirectlyAsync(New(d[1], p[0], 11)));

        Assert.Equal("patient_conflict", ex.Code);
    }

    [Fact]
    public async Task Db_allows_back_to_back_and_cancelled_overlaps()
    {
        var (d, p) = await api.CreatePeopleAsync();
        var cancelled = New(d[0], p[0], 12);
        cancelled.Cancel(null, api.Clock.GetUtcNow());

        await SaveDirectlyAsync(cancelled, New(d[0], p[1], 12), New(d[0], p[1], 12, 30));
    }
}
