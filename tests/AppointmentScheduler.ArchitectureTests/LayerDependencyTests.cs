using System.Reflection;
using AppointmentScheduler.Api.Controllers;
using AppointmentScheduler.Application.Appointments;
using AppointmentScheduler.Domain.Appointments;
using AppointmentScheduler.Domain.Common;
using AppointmentScheduler.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace AppointmentScheduler.ArchitectureTests;

/// <summary>Enforces ADR-0001: Domain ← Application ← Infrastructure ← Api.</summary>
public class LayerDependencyTests
{
    private const string Domain = "AppointmentScheduler.Domain";
    private const string Application = "AppointmentScheduler.Application";
    private const string Infrastructure = "AppointmentScheduler.Infrastructure";
    private const string Api = "AppointmentScheduler.Api";

    private static readonly Assembly DomainAssembly = typeof(Appointment).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(AppointmentService).Assembly;
    private static readonly Assembly ApiAssembly = typeof(ApiControllerBase).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer_or_framework()
    {
        var result = Types.InAssembly(DomainAssembly).ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Api, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "FluentValidation")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Application_does_not_depend_on_infrastructure_hosts_or_ef_core()
    {
        // ADR-0016: data access goes through repository + unit-of-work interfaces, so Application needs no EF Core at all.
        var result = Types.InAssembly(ApplicationAssembly).ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api, "Npgsql", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_api_host()
    {
        var result = Types.InAssembly(InfrastructureAssembly).ShouldNot().HaveDependencyOn(Api).GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Api_does_not_use_ef_core_directly()
    {
        // Controllers call Application services; only Infrastructure (repositories, migrations) talks to EF Core.
        var result = Types.InAssembly(ApiAssembly).ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore").GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Api_does_not_depend_on_domain()
    {
        // The Api talks to Application (services, DTOs, ApplicationError) and Infrastructure (composition) only. Domain
        // arrives transitively through those project references, so this test is what keeps it out of Api code.
        var result = Types.InAssembly(ApiAssembly).ShouldNot().HaveDependencyOn(Domain).GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Repository_implementations_are_internal_to_infrastructure()
    {
        var result = Types.InAssembly(InfrastructureAssembly).That().HaveNameEndingWith("Repository").Or().HaveName("UnitOfWork")
            .Should().NotBePublic().GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Repositories_are_reached_only_through_the_unit_of_work()
    {
        // ADR-0016: services inject IUnitOfWork and use unitOfWork.Appointments etc., so every repository and the commit share
        // one DbContext. UnitOfWork creates its repositories itself, so no constructor anywhere takes one.
        var violations = new[] { ApplicationAssembly, InfrastructureAssembly }
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(c => c.GetParameters().Any(p => p.ParameterType.Name.EndsWith("Repository", StringComparison.Ordinal))))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(violations.Count == 0, "Inject IUnitOfWork instead of a repository: " + string.Join(", ", violations));
    }

    [Fact]
    public void Repository_contracts_extend_the_generic_repository()
    {
        // Plan 013: every aggregate's contract inherits Add / GetById / GetByIdReadOnly / Exists from IRepository<TEntity, TKey>.
        var contracts = DomainAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Repository", StringComparison.Ordinal) && !t.IsGenericTypeDefinition)
            .ToList();
        var violations = contracts
            .Where(t => !t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRepository<,>)))
            .Select(t => t.FullName)
            .ToList();

        Assert.NotEmpty(contracts); // not vacuous
        Assert.True(violations.Count == 0, "Extend IRepository<TEntity, TKey>: " + string.Join(", ", violations));
    }

    [Fact]
    public void Repository_implementations_inherit_the_generic_base()
    {
        // The base is internal, so it is matched by its generic definition's name and namespace.
        static bool InheritsBase(Type t) =>
            t.BaseType is { IsGenericType: true } b && b.GetGenericTypeDefinition() is var d
            && d.Name == "Repository`2" && d.Namespace == $"{Infrastructure}.Persistence.Repositories";

        var implementations = InfrastructureAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Repository", StringComparison.Ordinal))
            .ToList();
        var violations = implementations
            .Where(t => !InheritsBase(t))
            .Select(t => t.FullName)
            .ToList();

        Assert.NotEmpty(implementations); // not vacuous
        Assert.True(violations.Count == 0, "Inherit Repository<TEntity, TKey>: " + string.Join(", ", violations));
    }

    private static string Describe(TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
