using Integration.Tests.Fixtures;
using Messaging.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Payments.Api.Data;

namespace Integration.Tests.Data;

[Collection(InfrastructureCollection.Name)]
public sealed class DatabaseMigratorTests(InfrastructureFixture infra) : IAsyncLifetime
{

    private string _connectionString = "";

    public async Task InitializeAsync() => _connectionString = await infra.Postgres.CreateDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConcurrentMigrators_BothComplete_AndNothingIsLeftPending()
    {
        // Two providers stand in for two app instances starting against the same database.
        var first = BuildProvider();
        var second = BuildProvider();

        await Task.WhenAll(
            DatabaseMigrator.MigrateAsync<PaymentsDbContext>(first, CancellationToken.None),
            DatabaseMigrator.MigrateAsync<PaymentsDbContext>(second, CancellationToken.None));

        await using var scope = first.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<PaymentsDbContext>(options =>
            PaymentsDbContext.ConfigureDatabase(options, _connectionString));
        return services.BuildServiceProvider();
    }
}
