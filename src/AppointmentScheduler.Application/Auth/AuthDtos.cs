namespace AppointmentScheduler.Application.Auth;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record UserDto(Guid Id, string Email, IReadOnlyList<string> Roles, int? DoctorId);

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

/// <summary>Login/refresh outcome: the body for the client plus the refresh token the Api puts in the cookie.</summary>
public sealed record AuthResult(AuthResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

/// <summary>Shared by the user-admin requests so <see cref="RolesRules"/> validates both.</summary>
public interface IRolesRequest
{
    IReadOnlyList<string>? Roles { get; }
    int? DoctorId { get; }
}

public sealed record CreateUserRequest(string? Email, string? Password, IReadOnlyList<string>? Roles, int? DoctorId) : IRolesRequest;

public sealed record SetUserRolesRequest(IReadOnlyList<string>? Roles, int? DoctorId) : IRolesRequest;
