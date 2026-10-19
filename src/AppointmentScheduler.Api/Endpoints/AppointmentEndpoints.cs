using AppointmentScheduler.Api.Auth;
using AppointmentScheduler.Application.Appointments;

namespace AppointmentScheduler.Api.Endpoints;

internal static class AppointmentEndpoints
{
    public static RouteGroupBuilder MapAppointmentEndpoints(this RouteGroupBuilder api)
    {
        // Policies AND-combine: the POSTs need ScheduleReader and FrontDesk, so a Doctor gets 403 (AC-004-8).
        var group = api.MapGroup("/appointments").WithTags("Appointments").RequireAuthorization(Policies.ScheduleReader);

        group.MapGet("/", (int doctorId, DateOnly date, AppointmentService service, CancellationToken ct) =>
                service.GetDayScheduleAsync(doctorId, date, ct))
            .WithSummary("Day schedule of a doctor (clinic-local date), including cancelled appointments.");

        group.MapGet("/{id:guid}", (Guid id, AppointmentService service, CancellationToken ct) => service.GetAsync(id, ct))
            .WithName("GetAppointment");

        group.MapPost("/", async (BookAppointmentRequest request, AppointmentService service, CancellationToken ct) =>
            {
                var created = await service.BookAsync(request, ct);
                return TypedResults.CreatedAtRoute(created, "GetAppointment", new { id = created.Id });
            })
            .RequireAuthorization(Policies.FrontDesk)
            .WithSummary("Book a free slot. 409 doctor_conflict / patient_conflict, 422 appointment_in_past / invalid_slot.");

        group.MapPost("/{id:guid}/cancel", (Guid id, CancelAppointmentRequest request, AppointmentService service, CancellationToken ct) =>
            service.CancelAsync(id, request, ct))
            .RequireAuthorization(Policies.FrontDesk);

        group.MapPost("/{id:guid}/reschedule", (Guid id, RescheduleAppointmentRequest request, AppointmentService service, CancellationToken ct) =>
            service.RescheduleAsync(id, request, ct))
            .RequireAuthorization(Policies.FrontDesk);

        return api;
    }
}
