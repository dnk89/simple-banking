using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Messaging.Data;

public static class DatabaseMigrator
{
    // Fixed key for migrations only. The outbox relays use 727001 (API) and 727002 (worker).
    private const long MigrationLockKey = 727000;

    /// <summary>
    /// Applies pending migrations for <typeparamref name="TContext"/>. Two instances starting at once
    /// queue on a transaction-scoped advisory lock, so the second one finds nothing left to apply.
    /// </summary>
    public static async Task MigrateAsync<TContext>(IServiceProvider services, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();

        // The lock sits on its own connection and transaction, so EF's migration transaction is untouched.
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)", connection, transaction))
        {
            command.Parameters.AddWithValue("key", MigrationLockKey);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await db.Database.MigrateAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
