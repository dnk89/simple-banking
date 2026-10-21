using Messaging.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Payments.Api.Configuration;

public static class PaymentsOptionsExtensions
{
    public static IServiceCollection AddPaymentsOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<ConnectionStringsOptions>(configuration, ConnectionStringsOptions.SectionName);
        services.AddValidatedOptions<AuthOptions>(configuration, AuthOptions.SectionName);
        services.AddValidatedOptions<IdempotencyOptions>(configuration, IdempotencyOptions.SectionName);
        services.AddValidatedOptions<DatabaseOptions>(configuration, DatabaseOptions.SectionName);
        return services;
    }
}
