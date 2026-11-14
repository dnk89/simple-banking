namespace Integration.Tests.Fixtures;

// Starts Postgres and Kafka once for the whole test assembly, in parallel.
public sealed class InfrastructureFixture : IAsyncLifetime
{
    public PostgresFixture Postgres { get; } = new();

    public KafkaFixture Kafka { get; } = new();

    public Task InitializeAsync() => Task.WhenAll(Postgres.InitializeAsync(), Kafka.InitializeAsync());

    public Task DisposeAsync() => Task.WhenAll(Postgres.DisposeAsync(), Kafka.DisposeAsync());
}
