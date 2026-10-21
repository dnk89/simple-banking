using Microsoft.EntityFrameworkCore;
using Payments.Api.Data.Entities;
using Payments.Api.Domain;

namespace Payments.Api.Data;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : DbContext(options)
{
    public const string Schema = "payments";

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<IdempotencyRecord> IdempotencyKeys => Set<IdempotencyRecord>();

    /// <summary>
    /// Pins this context's migration history to its own schema. Shares a database with the worker's
    /// context, so the default __EFMigrationsHistory table would collide (hazard 4).
    /// </summary>
    public static DbContextOptionsBuilder ConfigureDatabase(DbContextOptionsBuilder builder, string connectionString)
    {
        return builder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", Schema));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Id).HasColumnName("id");
            entity.Property(p => p.ClientId).HasColumnName("client_id");
            entity.Property(p => p.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(16);
            entity.Property(p => p.DebtorIban).HasColumnName("debtor_iban").HasMaxLength(34);
            entity.Property(p => p.DebtorName).HasColumnName("debtor_name").HasMaxLength(70);
            entity.Property(p => p.CreditorName).HasColumnName("creditor_name").HasMaxLength(70);
            entity.Property(p => p.CreditorIban).HasColumnName("creditor_iban").HasMaxLength(34);
            entity.Property(p => p.CreditorBic).HasColumnName("creditor_bic").HasMaxLength(11);
            entity.Property(p => p.Amount).HasColumnName("amount").HasPrecision(18, 2);
            entity.Property(p => p.Currency).HasColumnName("currency").IsFixedLength().HasMaxLength(3);
            entity.Property(p => p.RemittanceInfo).HasColumnName("remittance_info").HasMaxLength(140);
            entity.Property(p => p.RequestedExecutionDate).HasColumnName("requested_execution_date");
            entity.Property(p => p.EndToEndId).HasColumnName("end_to_end_id").HasMaxLength(35);
            entity.Property(p => p.RejectReasonCode).HasColumnName("reject_reason_code").HasMaxLength(35);
            entity.Property(p => p.FailureReason).HasColumnName("failure_reason");
            entity.Property(p => p.CreatedAt).HasColumnName("created_at");
            entity.Property(p => p.UpdatedAt).HasColumnName("updated_at");
            // Optimistic concurrency via Postgres' xmin system column; there is no CLR property for it.
            entity.Property<uint>("Xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            entity.HasIndex(p => new { p.ClientId, p.Status, p.CreatedAt })
                .HasDatabaseName("ix_payments_client_status_created");
        });

        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("idempotency_keys");
            entity.HasKey(r => new { r.ClientId, r.Key });

            entity.Property(r => r.ClientId).HasColumnName("client_id");
            entity.Property(r => r.Key).HasColumnName("key").HasMaxLength(255);
            entity.Property(r => r.RequestFingerprint).HasColumnName("request_fingerprint").IsFixedLength().HasMaxLength(64);
            entity.Property(r => r.PaymentId).HasColumnName("payment_id");
            entity.Property(r => r.CreatedAt).HasColumnName("created_at");
        });
    }
}
