using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Payments.Api.Data;

// Used by `dotnet ef` at design time only; the running API builds its context through DI.
public sealed class PaymentsDbContextFactory : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Database=payments;Username=payments;Password=payments";

    public PaymentsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Payments")
            ?? DesignTimeConnectionString;

        var builder = new DbContextOptionsBuilder<PaymentsDbContext>();
        PaymentsDbContext.ConfigureDatabase(builder, connectionString);
        return new PaymentsDbContext(builder.Options);
    }
}
