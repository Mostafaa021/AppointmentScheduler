using AppointmentScheduler.Api.Auth;
using AppointmentScheduler.Application.Auth;

namespace AppointmentScheduler.Api.Endpoints;

internal static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users").WithTags("Users").RequireAuthorization(Policies.AdminOnly);

        // 201 without Location: there is no GET /users/{id} until S-43.
        users.MapPost("/", async (CreateUserRequest request, IUserAdminService service, CancellationToken ct) =>
                TypedResults.Created((string?)null, await service.CreateAsync(request, ct)))
            .WithSummary("Create a user. 404 doctor_not_found, 409 email_taken.");

        users.MapPut("/{id:guid}/roles", (Guid id, SetUserRolesRequest request, IUserAdminService service, CancellationToken ct) =>
                service.SetRolesAsync(id, request, ct))
            .WithSummary("Replace a user's roles and doctor link; applies at their next login or refresh.");

        return api;
    }
}
