using BankGateway.Worker.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BankGateway.Worker.Data;

public sealed class BankGatewayDbContext(DbContextOptions<BankGatewayDbContext> options) : DbContext(options)
{
    public const string Schema = "bankgateway";

    public DbSet<SentMessage> SentMessages => Set<SentMessage>();

    public DbSet<ReceivedStatus> ReceivedStatuses => Set<ReceivedStatus>();

    /// <summary>
    /// Pins this context's migration history to its own schema. Shares a database with the API's
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

        modelBuilder.Entity<SentMessage>(entity =>
        {
            entity.ToTable("sent_messages");
            entity.HasKey(m => m.PaymentId);

            entity.Property(m => m.PaymentId).HasColumnName("payment_id");
            entity.Property(m => m.MessageId).HasColumnName("message_id").HasMaxLength(35);
            entity.Property(m => m.PaymentInfoId).HasColumnName("payment_info_id").HasMaxLength(35);
            entity.Property(m => m.InstructionId).HasColumnName("instruction_id").HasMaxLength(35);
            entity.Property(m => m.Xml).HasColumnName("xml");
            entity.Property(m => m.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(16);
            entity.Property(m => m.SentAt).HasColumnName("sent_at");
            entity.Property(m => m.CreatedAt).HasColumnName("created_at");
            // Optimistic concurrency via Postgres' xmin system column; there is no CLR property for it.
            entity.Property<uint>("Xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            entity.HasIndex(m => m.MessageId)
                .IsUnique()
                .HasDatabaseName("ux_sent_messages_message_id");
        });

        modelBuilder.Entity<ReceivedStatus>(entity =>
        {
            entity.ToTable("received_statuses");
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            entity.Property(s => s.MessageId).HasColumnName("message_id").HasMaxLength(35);
            entity.Property(s => s.InstructionId).HasColumnName("instruction_id").HasMaxLength(35);
            entity.Property(s => s.RawXml).HasColumnName("raw_xml");
            entity.Property(s => s.Status).HasColumnName("status").HasMaxLength(16);
            entity.Property(s => s.ReceivedAt).HasColumnName("received_at");

            entity.HasIndex(s => new { s.MessageId, s.InstructionId })
                .HasDatabaseName("ix_received_statuses_message_instruction");
        });
    }
}
