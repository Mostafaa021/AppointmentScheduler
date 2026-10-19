using FluentValidation;

namespace AppointmentScheduler.Application.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty();
        RuleFor(r => r.Password).NotEmpty();
    }
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        // ASCII classes match Identity's PasswordValidator, the backstop configured from the same policy.
        RuleFor(r => r.Password).NotEmpty()
            .MinimumLength(PasswordPolicy.MinLength)
            .MaximumLength(PasswordPolicy.MaxLength)
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
        Include(new RolesRules());
    }
}

public sealed class SetUserRolesRequestValidator : AbstractValidator<SetUserRolesRequest>
{
    public SetUserRolesRequestValidator() => Include(new RolesRules());
}

/// <summary>AC-004-12: roles non-empty ⊆ <see cref="Roles.All"/>; role Doctor ⇔ <c>doctorId</c> (error on <c>doctorId</c>).</summary>
public sealed class RolesRules : AbstractValidator<IRolesRequest>
{
    public RolesRules()
    {
        RuleFor(r => r.Roles).NotEmpty()
            .Must(roles => roles is null || roles.All(Roles.All.Contains))
            .WithMessage($"Roles must be any of: {string.Join(", ", Roles.All)}.");
        RuleFor(r => r.DoctorId).NotNull().When(HasDoctorRole).WithMessage("doctorId is required for role Doctor.");
        RuleFor(r => r.DoctorId).Null().Unless(HasDoctorRole).WithMessage("doctorId is only allowed with role Doctor.");
        RuleFor(r => r.DoctorId).GreaterThan(0);
    }

    private static bool HasDoctorRole(IRolesRequest r) => r.Roles?.Contains(Roles.Doctor) == true;
}
