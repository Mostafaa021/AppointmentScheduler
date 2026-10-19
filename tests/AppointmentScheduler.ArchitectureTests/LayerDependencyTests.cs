using System.Reflection;
using AppointmentScheduler.Application.Common;
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
    private static readonly Assembly ApplicationAssembly = typeof(IAppDbContext).Assembly;
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
    public void Application_does_not_depend_on_infrastructure_hosts_or_a_db_provider()
    {
        var result = Types.InAssembly(ApplicationAssembly).ShouldNot()
            .HaveDependencyOnAny(Infrastructure, Api, "Npgsql", "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_api_host()
    {
        var result = Types.InAssembly(InfrastructureAssembly).ShouldNot().HaveDependencyOn(Api).GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
