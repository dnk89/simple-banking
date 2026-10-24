using Messaging.Data;
using Messaging.Kafka;
using Messaging.Outbox;
using Messaging.Retry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Messaging.Configuration;

public static class MessagingOptionsExtensions
{
    /// <summary>
    /// Binds a configuration section and fails host startup if its data annotations are violated.
    /// </summary>
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        return services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    public static IServiceCollection AddKafkaOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<KafkaOptions>(configuration, KafkaOptions.SectionName);
        return services;
    }

    public static IServiceCollection AddOutboxOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<OutboxOptions>(configuration, OutboxOptions.SectionName);
        return services;
    }

    public static IServiceCollection AddRetryOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<RetryOptions>(configuration, RetryOptions.SectionName);
        return services;
    }

    public static IServiceCollection AddDatabaseOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);
        return services;
    }

    /// <summary>
    /// Runs <typeparamref name="TContext"/>'s migrations at host start, gated on <c>Database:MigrateOnStartup</c>.
    /// </summary>
    public static IServiceCollection AddDatabaseMigrations<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddHostedService<DatabaseMigrationService<TContext>>();
        return services;
    }
}
