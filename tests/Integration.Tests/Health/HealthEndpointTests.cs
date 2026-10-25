using System.Net;
using Integration.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using MockBank;
using Payments.Api;

namespace Integration.Tests.Health;

public sealed class HealthEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    // Reserved, non-routable address. Connections fail fast without depending on a real host.
    private const string UnreachableConnectionString =
        "Host=10.255.255.1;Port=5432;Database=payments;Username=payments;Password=payments;Timeout=1";

    [Fact]
    public async Task Live_ReturnsHealthy_WithoutDatabase()
    {
        await using var factory = new PaymentsFactory(UnreachableConnectionString, migrateOnStartup: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_ReturnsUnavailable_WhenDatabaseIsDown()
    {
        await using var factory = new PaymentsFactory(UnreachableConnectionString, migrateOnStartup: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Ready_ReturnsHealthy_WhenDatabaseIsUp()
    {
        await using var factory = new PaymentsFactory(postgres.ConnectionString, migrateOnStartup: true);
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

    private sealed class PaymentsFactory(string connectionString, bool migrateOnStartup)
        : WebApplicationFactory<PaymentsApiMarker>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Payments", connectionString);
            builder.UseSetting("Database:MigrateOnStartup", migrateOnStartup.ToString());
            builder.UseSetting("Kafka:BootstrapServers", "localhost:9092");
            builder.UseSetting("Auth:ApiKeys:0:ClientId", "health-test");
            builder.UseSetting("Auth:ApiKeys:0:Key", "health-test-key");
        }
    }
}
