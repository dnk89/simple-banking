using Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Payments.Api.Data;
using Payments.Api.Data.Entities;
using Payments.Api.Domain;

namespace Integration.Tests.Data;

public sealed class MigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task PaymentsContext_MigratesOnEmptyDatabase()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var tables = await PaymentsSchemaTables();

        Assert.Contains("payments", tables);
        Assert.Contains("idempotency_keys", tables);
        Assert.Contains("__EFMigrationsHistory", tables);
    }

    [Fact]
    public async Task Payment_RoundTrips_WithXminConcurrencyToken()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            ClientId = "client-1",
            Status = PaymentStatus.Received,
            DebtorIban = "LV80BANK0000435195001",
            DebtorName = "Acme Holdings SIA",
            CreditorName = "Example SIA",
            CreditorIban = "DE89370400440532013000",
            Amount = 125.50m,
            Currency = "EUR",
            RequestedExecutionDate = new DateOnly(2026, 10, 5),
            EndToEndId = "INV-2026-104",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        await using var reader = CreateContext();
        var loaded = await reader.Payments.SingleAsync(p => p.Id == payment.Id);

        Assert.Equal(PaymentStatus.Received, loaded.Status);
        Assert.Equal(125.50m, loaded.Amount);
    }

    private PaymentsDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<PaymentsDbContext>();
        PaymentsDbContext.ConfigureDatabase(builder, postgres.ConnectionString);
        return new PaymentsDbContext(builder.Options);
    }

    private async Task<HashSet<string>> PaymentsSchemaTables()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'payments'", connection);
        await using var reader = await command.ExecuteReaderAsync();

        var tables = new HashSet<string>();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }
}
