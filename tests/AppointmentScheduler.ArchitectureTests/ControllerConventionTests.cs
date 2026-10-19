using System.Reflection;
using AppointmentScheduler.Api.Controllers;

namespace AppointmentScheduler.ArchitectureTests;

/// <summary>
/// Controllers hold endpoints only: no private/protected helper methods or constants. Logic belongs in an Application
/// service; HTTP-only helpers (cookies, headers) in an Api helper class such as <c>Api/Auth/RefreshTokenCookie</c>.
/// </summary>
public class ControllerConventionTests
{
    private static readonly Type[] Controllers = typeof(ApiControllerBase).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(ApiControllerBase)))
        .ToArray();

    [Fact]
    public void Controllers_were_found() => Assert.NotEmpty(Controllers);

    [Fact]
    public void Controllers_declare_only_public_endpoint_methods()
    {
        var helpers = Controllers
            .SelectMany(c => c.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName) // property accessors
                .Select(m => $"{c.Name}.{m.Name}"))
            .ToList();

        Assert.True(helpers.Count == 0, "Move these out of the controller: " + string.Join(", ", helpers));
    }

    [Fact]
    public void Controllers_declare_their_api_version()
    {
        // ADR-0017: [ApiVersion] is not inherited from ApiControllerBase, so each controller must state it.
        var unversioned = Controllers.Where(c => !c.IsDefined(typeof(Asp.Versioning.ApiVersionAttribute), inherit: false))
            .Select(c => c.Name).ToList();

        Assert.True(unversioned.Count == 0, "Add [ApiVersion(...)] to: " + string.Join(", ", unversioned));
    }

    [Fact]
    public void Controllers_declare_no_constants()
    {
        var constants = Controllers
            .SelectMany(c => c.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(f => f.IsLiteral)
                .Select(f => $"{c.Name}.{f.Name}"))
            .ToList();

        Assert.True(constants.Count == 0, "Move these out of the controller: " + string.Join(", ", constants));
    }
}
