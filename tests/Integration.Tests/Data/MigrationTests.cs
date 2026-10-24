using Integration.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using BankGateway.Worker.Data;
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

        var tables = await SchemaTables("payments");

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

    [Fact]
    public async Task BankGatewayContext_MigratesOnEmptyDatabase()
    {
        await using var db = CreateBankGatewayContext();
        await db.Database.MigrateAsync();

        var tables = await SchemaTables("bankgateway");

        Assert.Contains("sent_messages", tables);
        Assert.Contains("received_statuses", tables);
        Assert.Contains("__EFMigrationsHistory", tables);
    }

    [Fact]
    public async Task BothContexts_MigrateIntoOneDatabase_WithSeparateHistoryTables()
    {
        await using var payments = CreateContext();
        await using var bankGateway = CreateBankGatewayContext();

        await payments.Database.MigrateAsync();
        await bankGateway.Database.MigrateAsync();

        var paymentsTables = await SchemaTables("payments");
        var bankGatewayTables = await SchemaTables("bankgateway");

        Assert.Contains("payments", paymentsTables);
        Assert.Contains("sent_messages", bankGatewayTables);
        Assert.DoesNotContain("sent_messages", paymentsTables);
        Assert.DoesNotContain("payments", bankGatewayTables);
    }

    private PaymentsDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<PaymentsDbContext>();
        PaymentsDbContext.ConfigureDatabase(builder, postgres.ConnectionString);
        return new PaymentsDbContext(builder.Options);
    }

    private BankGatewayDbContext CreateBankGatewayContext()
    {
        var builder = new DbContextOptionsBuilder<BankGatewayDbContext>();
        BankGatewayDbContext.ConfigureDatabase(builder, postgres.ConnectionString);
        return new BankGatewayDbContext(builder.Options);
    }

    private async Task<HashSet<string>> SchemaTables(string schema)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            $"SELECT table_name FROM information_schema.tables WHERE table_schema = '{schema}'", connection);
        await using var reader = await command.ExecuteReaderAsync();

        var tables = new HashSet<string>();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }
}
