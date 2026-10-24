using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BankGateway.Worker.Migrations
{
    /// <inheritdoc />
    public partial class InitialBankGateway : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bankgateway");

            migrationBuilder.CreateTable(
                name: "received_statuses",
                schema: "bankgateway",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    message_id = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    instruction_id = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    raw_xml = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_received_statuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sent_messages",
                schema: "bankgateway",
                columns: table => new
                {
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    payment_info_id = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    instruction_id = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    xml = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sent_messages", x => x.payment_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_received_statuses_message_instruction",
                schema: "bankgateway",
                table: "received_statuses",
                columns: new[] { "message_id", "instruction_id" });

            migrationBuilder.CreateIndex(
                name: "ux_sent_messages_message_id",
                schema: "bankgateway",
                table: "sent_messages",
                column: "message_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "received_statuses",
                schema: "bankgateway");

            migrationBuilder.DropTable(
                name: "sent_messages",
                schema: "bankgateway");
        }
    }
}
