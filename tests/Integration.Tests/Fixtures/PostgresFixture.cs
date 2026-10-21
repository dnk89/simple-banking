using Testcontainers.PostgreSql;

namespace Integration.Tests.Fixtures;

// One Postgres container per test class. The shared harness (Kafka, fresh databases) arrives in M0.10.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
