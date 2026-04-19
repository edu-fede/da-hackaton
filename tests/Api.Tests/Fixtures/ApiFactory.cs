using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace Hackaton.Api.Tests.Fixtures;

/// <summary>
/// Spins up a transient Postgres container and hosts the API in-memory against it.
/// One instance per test class via <see cref="IClassFixture{T}"/>.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("hackaton")
        .WithUsername("hackaton")
        .WithPassword("hackaton")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        _ = Server;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Pauses the Postgres container (docker pause) so tests can assert Hub behavior when the DB is unreachable.</summary>
    public Task PausePostgresAsync() => RunDockerAsync("pause", _postgres.Id);

    public Task UnpausePostgresAsync() => RunDockerAsync("unpause", _postgres.Id);

    private static async Task RunDockerAsync(string command, string containerId)
    {
        var info = new ProcessStartInfo("docker", $"{command} {containerId}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Failed to spawn 'docker {command}'.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"'docker {command} {containerId}' exited {process.ExitCode}: {stderr}");
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
            });
        });
    }
}
