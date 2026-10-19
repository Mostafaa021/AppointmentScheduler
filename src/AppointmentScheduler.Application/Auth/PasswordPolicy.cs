namespace AppointmentScheduler.Application.Auth;

/// <summary>ADR-0005 amendment: 12–128 chars, at least one lowercase, one uppercase and one digit; no symbol required.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128; // bounds hashing cost; enforced by the validator only
}
