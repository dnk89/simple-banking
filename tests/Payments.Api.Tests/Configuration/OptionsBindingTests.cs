using Messaging.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Payments.Api.Tests.Configuration;

public sealed class OptionsBindingTests
{
    [Fact]
    public async Task MissingBootstrapServers_FailsOnStart()
    {
        // HostBuilder instead of CreateDefaultBuilder: no appsettings files, no environment variables.
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddKafkaOptions(new ConfigurationBuilder().Build()))
            .Build();

        var ex = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("BootstrapServers", ex.Message);
    }
}
