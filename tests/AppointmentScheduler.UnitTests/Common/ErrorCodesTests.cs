using System.Reflection;
using System.Text.RegularExpressions;

namespace AppointmentScheduler.UnitTests.Common;

/// <summary>ADR-0015: clients branch on error codes, so every code is UPPER_SNAKE and unique.</summary>
public partial class ErrorCodesTests
{
    private static readonly string[] Codes = typeof(ErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    [Fact]
    public void Every_code_is_upper_snake_case() =>
        Assert.All(Codes, code => Assert.Matches(UpperSnake(), code));

    [Fact]
    public void Codes_are_unique() =>
        Assert.Equal(Codes.Length, Codes.Distinct(StringComparer.Ordinal).Count());

    [GeneratedRegex("^[A-Z]+(_[A-Z]+)*$")]
    private static partial Regex UpperSnake();
}
