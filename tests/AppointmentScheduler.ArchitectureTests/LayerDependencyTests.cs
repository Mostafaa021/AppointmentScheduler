using System.Reflection;
using AppointmentScheduler.Api.Controllers;
using AppointmentScheduler.Application.Appointments;
using AppointmentScheduler.Domain.Appointments;
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
    public void Repository_implementations_are_internal_to_infrastructure()
    {
        var result = Types.InAssembly(InfrastructureAssembly).That().HaveNameEndingWith("Repository").Or().HaveName("UnitOfWork")
            .Should().NotBePublic().GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
