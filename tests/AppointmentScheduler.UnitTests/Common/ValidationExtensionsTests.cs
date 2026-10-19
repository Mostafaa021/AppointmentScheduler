using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Common;
using FluentValidation;
using ValidationException = AppointmentScheduler.Domain.Common.ValidationException;

namespace AppointmentScheduler.UnitTests.Common;

public class ValidationExtensionsTests
{
    private sealed record Sample(string? FirstName, IReadOnlyList<string> Tags);

    private sealed class SampleValidator : AbstractValidator<Sample>
    {
        public SampleValidator()
        {
            RuleFor(s => s.FirstName).NotEmpty().MinimumLength(3);
            RuleForEach(s => s.Tags).NotEmpty();
        }
    }

    [Fact]
    public void AC_004_22_Failures_are_grouped_by_camelCase_top_level_property()
    {
        var ex = Assert.Throws<ValidationException>(() => new SampleValidator().ValidateOrThrow(new Sample("", ["ok", "", " "])));

        Assert.Equal("validation_failed", ex.Code);
        Assert.Equal(["firstName", "tags"], ex.Errors.Keys.Order());
        Assert.Equal(2, ex.Errors["firstName"].Length); // NotEmpty + MinimumLength, both reported
        Assert.Single(ex.Errors["tags"]);               // Tags[1] and Tags[2] -> "tags", same message once
    }

    [Fact]
    public void Valid_request_does_not_throw()
    {
        new SampleValidator().ValidateOrThrow(new Sample("Ada", ["x"]));
    }
}
