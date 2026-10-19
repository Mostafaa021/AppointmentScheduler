namespace AppointmentScheduler.Application.Auth;

/// <summary>Login, refresh-token rotation, logout and "me" (spec 004, ADR-0005). Implemented in Infrastructure with Identity.</summary>
public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken ct);
    Task LogoutAsync(string? refreshToken, CancellationToken ct);
    Task<UserDto> GetMeAsync(CancellationToken ct);
}
