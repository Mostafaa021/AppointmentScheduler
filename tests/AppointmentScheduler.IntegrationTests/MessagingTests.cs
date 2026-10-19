using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using AppointmentScheduler.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wolverine;

namespace AppointmentScheduler.IntegrationTests;

/// <summary>ADR-0007: Wolverine runs in the Api host on the PostgreSQL store; without a broker the host still serves HTTP.</summary>
[Collection(nameof(ApiCollection))]
public class MessagingTests(ApiFactory api)
{
    [Fact]
    public void Wolverine_is_hosted_and_runs_without_rabbitmq_in_tests()
    {
        using var scope = api.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetService<IMessageBus>());
        var state = api.Services.GetRequiredService<RabbitMqState>();
        Assert.False(state.IsAvailable);
        Assert.Contains("disabled", state.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Message_store_tables_live_in_the_wolverine_schema()
    {
        api.CreateClient(); // make sure the host (and Wolverine's storage migration) has started

        await using var connection = new NpgsqlConnection(api.Services.GetRequiredService<IConfiguration>()
            .GetConnectionString("Scheduler"));
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select count(*) from information_schema.tables where table_schema = @schema", connection);
        command.Parameters.AddWithValue("schema", MessagingSetup.WolverineSchema);

        Assert.True((long)(await command.ExecuteScalarAsync())! > 0, "expected Wolverine inbox/outbox tables");
    }

    [Fact]
    public void Probe_reports_an_unreachable_broker_within_the_timeout()
    {
        var port = FreePort(); // nothing listens there
        var stopwatch = Stopwatch.StartNew();

        var state = RabbitMqAvailability.Probe(new RabbitMqOptions { Host = "127.0.0.1", Port = port, ProbeTimeoutSeconds = 2 });

        Assert.False(state.IsAvailable);
        Assert.Contains($"127.0.0.1:{port}", state.Reason, StringComparison.Ordinal);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"probe took {stopwatch.Elapsed}");
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
