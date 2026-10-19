namespace AppointmentScheduler.Api.Auth;

/// <summary>AC-004-8 role matrix. <c>RequireRole</c> = any of the roles, i.e. the union of a user's roles.</summary>
internal static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string FrontDesk = nameof(FrontDesk);
    public const string ScheduleReader = nameof(ScheduleReader);
}
