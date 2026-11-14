using Testcontainers.Kafka;

namespace Integration.Tests.Fixtures;

// One Kafka broker per test assembly (see InfrastructureFixture). Tests isolate themselves with unique group IDs.
public sealed class KafkaFixture : IAsyncLifetime
{
    private readonly KafkaContainer _container = new KafkaBuilder("apache/kafka:3.9.0").Build();

    public string BootstrapServers => _container.GetBootstrapAddress();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
