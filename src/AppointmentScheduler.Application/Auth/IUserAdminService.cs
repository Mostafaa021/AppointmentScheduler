namespace AppointmentScheduler.Application.Auth;

/// <summary>Admin user management (AC-004-11..14). Implemented in Infrastructure with Identity.</summary>
public interface IUserAdminService
{
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserDto> SetRolesAsync(Guid id, SetUserRolesRequest request, CancellationToken ct);
}
