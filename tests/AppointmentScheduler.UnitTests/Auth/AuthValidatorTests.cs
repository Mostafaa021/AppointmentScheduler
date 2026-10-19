using AppointmentScheduler.Application.Auth;
using AppointmentScheduler.Application.Common;
using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.UnitTests.Auth;

public class AuthValidatorTests
{
    private const string GoodPassword = "Correct-Horse-42";

    private static IReadOnlyDictionary<string, string[]> Errors<T>(FluentValidation.IValidator<T> validator, T request) =>
        Assert.Throws<ValidationException>(() => validator.ValidateOrThrow(request)).Errors;

    private static CreateUserRequest User(string? email = "new.user@clinic.test", string? password = GoodPassword,
        string[]? roles = null, int? doctorId = null) => new(email, password, roles ?? [Roles.Receptionist], doctorId);

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("  ", " ")]
    public void AC_004_1_Login_requires_non_blank_email_and_password(string? email, string? password)
    {
        var errors = Errors(new LoginRequestValidator(), new LoginRequest(email, password));

        Assert.Equal(["email", "password"], errors.Keys.Order());
    }

    [Fact]
    public void AC_004_1_Login_with_both_fields_is_valid()
    {
        new LoginRequestValidator().ValidateOrThrow(new LoginRequest("a@clinic.test", "x"));
    }

    [Fact]
    public void AC_004_11_Valid_create_user_requests_pass()
    {
        var validator = new CreateUserRequestValidator();

        validator.ValidateOrThrow(User());
        validator.ValidateOrThrow(User(roles: [Roles.Doctor], doctorId: 3));
        validator.ValidateOrThrow(User(roles: [Roles.Admin, Roles.Doctor], doctorId: 3));
        validator.ValidateOrThrow(User(password: "abcdefghijK1"));            // exactly 12
        validator.ValidateOrThrow(User(password: "aB1" + new string('x', 125))); // exactly 128
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    [InlineData(null)]
    public void AC_004_12_Invalid_email_is_rejected(string? email)
    {
        Assert.Equal(["email"], Errors(new CreateUserRequestValidator(), User(email: email)).Keys);
    }

    [Theory]
    [InlineData("Short1a")]                 // < 12
    [InlineData("alllowercase123")]         // no uppercase
    [InlineData("ALLUPPERCASE123")]         // no lowercase
    [InlineData("NoDigitsAtAllHere")]       // no digit
    [InlineData("")]
    [InlineData(null)]
    public void AC_004_12_Password_violating_the_policy_is_rejected(string? password)
    {
        Assert.Equal(["password"], Errors(new CreateUserRequestValidator(), User(password: password)).Keys);
    }

    [Fact]
    public void AC_004_12_Password_longer_than_128_is_rejected()
    {
        var errors = Errors(new CreateUserRequestValidator(), User(password: "aB1" + new string('x', 126)));

        Assert.Equal(["password"], errors.Keys);
    }

    [Fact]
    public void AC_004_12_All_password_violations_are_reported_at_once()
    {
        var errors = Errors(new CreateUserRequestValidator(), User(password: "short"));

        Assert.True(errors["password"].Length >= 3); // length, uppercase, digit
    }

    [Fact]
    public void AC_004_12_Empty_or_unknown_roles_are_rejected()
    {
        var validator = new CreateUserRequestValidator();

        Assert.Equal(["roles"], Errors(validator, User(roles: [])).Keys);
        Assert.Equal(["roles"], Errors(validator, new CreateUserRequest("a@clinic.test", GoodPassword, null, null)).Keys);
        Assert.Equal(["roles"], Errors(validator, User(roles: [Roles.Receptionist, "SuperUser"])).Keys);
        Assert.Equal(["roles"], Errors(validator, User(roles: ["admin"])).Keys); // role names are exact
    }

    [Fact]
    public void AC_004_12_Doctor_role_requires_doctorId_and_doctorId_requires_Doctor_role()
    {
        var validator = new CreateUserRequestValidator();

        Assert.Equal(["doctorId"], Errors(validator, User(roles: [Roles.Doctor])).Keys);
        Assert.Equal(["doctorId"], Errors(validator, User(roles: [Roles.Receptionist], doctorId: 3)).Keys);
        Assert.Equal(["doctorId"], Errors(validator, User(roles: [Roles.Doctor], doctorId: 0)).Keys);
    }

    [Fact]
    public void AC_004_12_Every_failing_field_is_reported_at_once()
    {
        var errors = Errors(new CreateUserRequestValidator(), new CreateUserRequest("bad", "bad", ["Nope"], 5));

        Assert.Equal(["doctorId", "email", "password", "roles"], errors.Keys.Order());
    }

    [Fact]
    public void AC_004_14_Set_roles_applies_the_same_role_rules()
    {
        var validator = new SetUserRolesRequestValidator();

        validator.ValidateOrThrow(new SetUserRolesRequest([Roles.Doctor], 2));
        validator.ValidateOrThrow(new SetUserRolesRequest([Roles.Admin], null));
        Assert.Equal(["roles"], Errors(validator, new SetUserRolesRequest([], null)).Keys);
        Assert.Equal(["doctorId"], Errors(validator, new SetUserRolesRequest([Roles.Doctor], null)).Keys);
        Assert.Equal(["doctorId"], Errors(validator, new SetUserRolesRequest([Roles.Admin], 2)).Keys);
    }
}
