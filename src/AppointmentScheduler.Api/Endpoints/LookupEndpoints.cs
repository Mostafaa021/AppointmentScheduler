using AppointmentScheduler.Application.Availability;
using AppointmentScheduler.Application.Lookups;

namespace AppointmentScheduler.Api.Endpoints;

internal static class LookupEndpoints
{
    public static RouteGroupBuilder MapLookupEndpoints(this RouteGroupBuilder api)
    {
        var doctors = api.MapGroup("/doctors").WithTags("Doctors");
        doctors.MapGet("/", (LookupQueries queries, CancellationToken ct) => queries.ListDoctorsAsync(ct));
        doctors.MapGet("/{id:int}/availability", (int id, DateOnly date, int? patientId, AvailabilityService service, CancellationToken ct) =>
                service.GetAsync(id, date, patientId, ct))
            .WithSummary("Slots of a doctor's day: Free, Booked, Past or PatientBusy (when patientId is given).");

        api.MapGroup("/patients").WithTags("Patients")
            .MapGet("/", (string? search, int? page, int? pageSize, LookupQueries queries, CancellationToken ct) =>
                queries.SearchPatientsAsync(search, page ?? 1, pageSize ?? 20, ct));

        return api;
    }
}
