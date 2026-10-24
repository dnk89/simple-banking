using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Messaging.Data;

/// <summary>
/// Migrates <typeparamref name="TContext"/> before the host starts serving, so no request or message
/// is handled against a schema that is not yet in place.
/// </summary>
public sealed class DatabaseMigrationService<TContext>(
    IServiceProvider services,
    IOptions<DatabaseOptions> options) : IHostedService
    where TContext : DbContext
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return options.Value.MigrateOnStartup
            ? DatabaseMigrator.MigrateAsync<TContext>(services, cancellationToken)
            : Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
