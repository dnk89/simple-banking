using System.Net;
using Integration.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using MockBank;
using Payments.Api;

namespace Integration.Tests.Health;

[Collection(InfrastructureCollection.Name)]
public sealed class HealthEndpointTests(InfrastructureFixture infra) : IAsyncLifetime
{
    private string _connectionString = "";

    public async Task InitializeAsync() => _connectionString = await infra.Postgres.CreateDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // Reserved, non-routable address. Connections fail fast without depending on a real host.
    private const string UnreachableConnectionString =
        "Host=10.255.255.1;Port=5432;Database=payments;Username=payments;Password=payments;Timeout=1";

    [Fact]
    public async Task Live_ReturnsHealthy_WithoutDatabase()
    {
        await using var factory = new PaymentsFactory(UnreachableConnectionString, infra.Kafka.BootstrapServers, migrateOnStartup: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_ReturnsUnavailable_WhenDatabaseIsDown()
    {
        await using var factory = new PaymentsFactory(UnreachableConnectionString, infra.Kafka.BootstrapServers, migrateOnStartup: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Ready_ReturnsHealthy_WhenDatabaseIsUp()
    {
        await using var factory = new PaymentsFactory(_connectionString, infra.Kafka.BootstrapServers, migrateOnStartup: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MockBank_Live_And_Ready_ReturnHealthy()
    {
        await using var factory = new WebApplicationFactory<MockBankMarker>();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    private sealed class PaymentsFactory(string connectionString, string bootstrapServers, bool migrateOnStartup)
        : WebApplicationFactory<PaymentsApiMarker>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // In-memory only: the referenced projects copy their appsettings.json into one output directory.
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Payments"] = connectionString,
                ["Database:MigrateOnStartup"] = migrateOnStartup.ToString(),
                ["Kafka:BootstrapServers"] = bootstrapServers,
                // Unique per factory so each test's consumer group is isolated.
                ["Kafka:GroupIdSuffix"] = Guid.NewGuid().ToString("N"),
                ["Auth:ApiKeys:0:ClientId"] = "health-test",
                ["Auth:ApiKeys:0:Key"] = "health-test-key",
            }));
        }
    }
}
