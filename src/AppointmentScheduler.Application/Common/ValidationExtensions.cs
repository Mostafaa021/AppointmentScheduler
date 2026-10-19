using System.Text.Json;
using FluentValidation;
using ValidationException = AppointmentScheduler.Domain.Common.ValidationException;

namespace AppointmentScheduler.Application.Common;

public static class ValidationExtensions
{
    /// <summary>
    /// ADR-0013: runs the (pure) validator and throws the domain <see cref="ValidationException"/>, keyed by the camelCase
    /// top-level property (<c>Roles[1]</c> → <c>roles</c>), so the 400 response shape stays unchanged.
    /// </summary>
    public static void ValidateOrThrow<T>(this IValidator<T> validator, T request)
    {
        var result = validator.Validate(request);
        if (result.IsValid) return;

        throw new ValidationException(result.Errors
            .GroupBy(e => FieldName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()));
    }

    private static string FieldName(string propertyName)
    {
        var end = propertyName.IndexOfAny(['[', '.']);
        return JsonNamingPolicy.CamelCase.ConvertName(end < 0 ? propertyName : propertyName[..end]);
    }
}
