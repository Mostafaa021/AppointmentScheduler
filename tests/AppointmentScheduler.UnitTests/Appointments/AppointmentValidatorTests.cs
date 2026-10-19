using AppointmentScheduler.Application.Appointments;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Common;
using FluentValidation;
using ValidationException = AppointmentScheduler.Domain.Common.ValidationException;

namespace AppointmentScheduler.UnitTests.Appointments;

public class AppointmentValidatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

    private static IReadOnlyDictionary<string, string[]> Errors<T>(IValidator<T> validator, T request) =>
        Assert.Throws<ValidationException>(() => validator.ValidateOrThrow(request)).Errors;

    [Fact]
    public void AC_004_22_Valid_book_request_passes()
    {
        new BookAppointmentRequestValidator().ValidateOrThrow(new BookAppointmentRequest(1, 2, Start, " Check-up "));
    }

    [Fact]
    public void AC_004_22_Book_reports_all_failing_fields_at_once()
    {
        var errors = Errors(new BookAppointmentRequestValidator(), new BookAppointmentRequest(0, -1, null, "   "));

        Assert.Equal(["doctorId", "patientId", "reason", "startUtc"], errors.Keys.Order());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AC_002_6_Book_requires_a_reason(string? reason)
    {
        Assert.Equal(["reason"], Errors(new BookAppointmentRequestValidator(), new BookAppointmentRequest(1, 2, Start, reason)).Keys);
    }

    [Fact]
    public void AC_002_6_Book_reason_is_at_most_500_after_trim()
    {
        var validator = new BookAppointmentRequestValidator();

        validator.ValidateOrThrow(new BookAppointmentRequest(1, 2, Start, "  " + new string('x', 500) + "  "));
        Assert.Equal(["reason"], Errors(validator, new BookAppointmentRequest(1, 2, Start, new string('x', 501))).Keys);
    }

    [Fact]
    public void AC_004_22_Cancel_reason_is_optional_and_at_most_500()
    {
        var validator = new CancelAppointmentRequestValidator();

        validator.ValidateOrThrow(new CancelAppointmentRequest(null));
        validator.ValidateOrThrow(new CancelAppointmentRequest(new string('x', 500)));
        Assert.Equal(["reason"], Errors(validator, new CancelAppointmentRequest(new string('x', 501))).Keys);
    }

    [Fact]
    public void AC_004_22_Reschedule_requires_startUtc()
    {
        var validator = new RescheduleAppointmentRequestValidator();

        validator.ValidateOrThrow(new RescheduleAppointmentRequest(Start));
        Assert.Equal(["startUtc"], Errors(validator, new RescheduleAppointmentRequest(null)).Keys);
    }
}
