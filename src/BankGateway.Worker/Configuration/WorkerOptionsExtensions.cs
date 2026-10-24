using Messaging.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BankGateway.Worker.Configuration;

public static class WorkerOptionsExtensions
{
    public static IServiceCollection AddWorkerOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<ConnectionStringsOptions>(configuration, ConnectionStringsOptions.SectionName);
        services.AddValidatedOptions<MockBankOptions>(configuration, MockBankOptions.SectionName);
        services.AddValidatedOptions<SigningOptions>(configuration, SigningOptions.SectionName);
        services.AddValidatedOptions<Iso20022Options>(configuration, Iso20022Options.SectionName);
        services.AddDatabaseOptions(configuration);
        return services;
    }
}
