using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BankGateway.Worker.Data;

// Used by `dotnet ef` at design time only; the running worker builds its context through DI.
public sealed class BankGatewayDbContextFactory : IDesignTimeDbContextFactory<BankGatewayDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Database=payments;Username=payments;Password=payments";

    public BankGatewayDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__BankGateway")
            ?? DesignTimeConnectionString;

        var builder = new DbContextOptionsBuilder<BankGatewayDbContext>();
        BankGatewayDbContext.ConfigureDatabase(builder, connectionString);
        return new BankGatewayDbContext(builder.Options);
    }
}
