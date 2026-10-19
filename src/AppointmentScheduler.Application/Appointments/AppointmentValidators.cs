using AppointmentScheduler.Domain.Appointments;
using FluentValidation;

namespace AppointmentScheduler.Application.Appointments;

public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentRequestValidator()
    {
        RuleFor(r => r.DoctorId).GreaterThan(0);
        RuleFor(r => r.PatientId).GreaterThan(0);
        RuleFor(r => r.StartUtc).NotNull();
        RuleFor(r => r.Reason).Must(r => r?.Trim().Length is >= 1 and <= Appointment.ReasonMaxLength)
            .WithMessage($"Reason is required and must be at most {Appointment.ReasonMaxLength} characters.");
    }
}

public sealed class CancelAppointmentRequestValidator : AbstractValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator() => RuleFor(r => r.Reason).MaximumLength(Appointment.ReasonMaxLength);
}

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator() => RuleFor(r => r.StartUtc).NotNull();
}
