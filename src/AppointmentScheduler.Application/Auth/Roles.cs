namespace AppointmentScheduler.Application.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Receptionist = "Receptionist";
    public const string Doctor = "Doctor";

    public static readonly IReadOnlyList<string> All = [Admin, Receptionist, Doctor];
}
