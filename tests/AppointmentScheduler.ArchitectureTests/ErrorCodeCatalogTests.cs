using System.Reflection;
using System.Text.RegularExpressions;
using AppointmentScheduler.Api.Errors;
using AppointmentScheduler.Domain.Common;

namespace AppointmentScheduler.ArchitectureTests;

/// <summary>
/// ADR-0015: business codes live in Domain <see cref="ErrorCodes"/>, transport codes in Api <see cref="ApiErrorCodes"/>.
/// Together they are one catalogue clients branch on, so no value may mean two things. The only overlap allowed is the
/// declared shared codes, which must be identical in both classes.
/// </summary>
public partial class ErrorCodeCatalogTests
{
    private static readonly string[] Shared = [nameof(ApiErrorCodes.Unauthorized), nameof(ApiErrorCodes.Forbidden)];

    [Fact]
    public void Api_codes_are_upper_snake_case_and_unique()
    {
        var codes = Codes(typeof(ApiErrorCodes));

        Assert.All(codes.Values, code => Assert.Matches(UpperSnake(), code));
        Assert.Equal(codes.Count, codes.Values.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Shared_codes_have_identical_values_in_both_classes()
    {
        var api = Codes(typeof(ApiErrorCodes));
        var domain = Codes(typeof(ErrorCodes));

        Assert.All(Shared, name => Assert.Equal(domain[name], api[name]));
    }

    [Fact]
    public void Api_and_domain_codes_do_not_collide_except_the_shared_ones()
    {
        var api = Codes(typeof(ApiErrorCodes)).Where(c => !Shared.Contains(c.Key)).Select(c => c.Value);
        var domain = Codes(typeof(ErrorCodes)).Values;

        Assert.Empty(api.Intersect(domain, StringComparer.Ordinal));
    }

    private static Dictionary<string, string> Codes(Type type) => type
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);

    [GeneratedRegex("^[A-Z]+(_[A-Z]+)*$")]
    private static partial Regex UpperSnake();
}
