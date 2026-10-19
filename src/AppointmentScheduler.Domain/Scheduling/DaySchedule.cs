using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.Domain.Scheduling;

/// <summary>One bookable slot. <see cref="LocalStart"/> is wall-clock time in the clinic's zone.</summary>
public sealed record Slot(DateTimeOffset StartUtc, DateTimeOffset EndUtc, TimeOnly LocalStart);

/// <summary>
/// Working hours of a day in clinic wall-clock time (ADR-0003). Generates slots and validates requested starts (BR-4).
/// shortcut: one open/close range for every day; per-doctor ranges, breaks and holidays arrive in M4 (spec 006/007).
/// </summary>
public sealed record DaySchedule(TimeZoneInfo TimeZone, TimeOnly Open, TimeOnly Close, TimeSpan SlotLength)
{
    public IReadOnlyList<Slot> SlotsFor(DateOnly localDate)
    {
        var slots = new List<Slot>();
        for (var local = localDate.ToDateTime(Open); local.Add(SlotLength) <= localDate.ToDateTime(Close); local = local.Add(SlotLength))
        {
            if (TimeZone.IsInvalidTime(local)) continue; // DST gap: this wall-clock time does not exist
            var startUtc = ToUtc(local);
            slots.Add(new Slot(startUtc, startUtc + SlotLength, TimeOnly.FromDateTime(local)));
        }
        return slots;
    }

    /// <summary>Clinic-local date of an instant.</summary>
    public DateOnly LocalDateOf(DateTimeOffset utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, TimeZone).DateTime);

    /// <summary>Clinic-local wall-clock time of an instant.</summary>
    public TimeOnly LocalTimeOf(DateTimeOffset utc) => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, TimeZone).DateTime);

    /// <summary>UTC window [start, end) covering the whole clinic-local day.</summary>
    public (DateTimeOffset StartUtc, DateTimeOffset EndUtc) UtcWindowOf(DateOnly localDate) =>
        (ToUtc(localDate.ToDateTime(TimeOnly.MinValue)), ToUtc(localDate.AddDays(1).ToDateTime(TimeOnly.MinValue)));

    /// <summary>BR-4: the start must be exactly one of the day's generated slot starts.</summary>
    public Slot EnsureValidSlot(DateTimeOffset startUtc) =>
        SlotsFor(LocalDateOf(startUtc)).FirstOrDefault(s => s.StartUtc == startUtc)
        ?? throw new BusinessRuleException("invalid_slot",
            $"Appointments must start on a {SlotLength.TotalMinutes:0}-minute slot between {Open:HH\\:mm} and {Close:HH\\:mm} ({TimeZone.Id}).");

    // Ambiguous local times (DST fall-back) resolve to the standard offset, which GetUtcOffset returns.
    private DateTimeOffset ToUtc(DateTime local) => new DateTimeOffset(local, TimeZone.GetUtcOffset(local)).ToUniversalTime();
}
