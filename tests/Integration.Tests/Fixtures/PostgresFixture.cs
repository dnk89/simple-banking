using Npgsql;
using Testcontainers.PostgreSql;

namespace Integration.Tests.Fixtures;

// One Postgres server per test assembly (see InfrastructureFixture). Each test class gets its own database on it.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    private string _serverConnectionString = "";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _serverConnectionString = _container.GetConnectionString();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Creates an empty database with a unique name and returns a connection string to it.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"test_{Guid.NewGuid():N}";

        await using var connection = new NpgsqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(_serverConnectionString) { Database = name }.ConnectionString;
    }
}
