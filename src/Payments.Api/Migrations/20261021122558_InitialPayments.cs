using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payments.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payments");

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                schema: "payments",
                columns: table => new
                {
                    client_id = table.Column<string>(type: "text", nullable: false),
                    key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    request_fingerprint = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => new { x.client_id, x.key });
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    debtor_iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    debtor_name = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    creditor_name = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: false),
                    creditor_iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    creditor_bic = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    remittance_info = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    requested_execution_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_to_end_id = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    reject_reason_code = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                    // xmin is deliberately not created: it is a Postgres system column, and EF's
                    // concurrency token reads it from every row. The snapshot still lists it.
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payments_client_status_created",
                schema: "payments",
                table: "payments",
                columns: new[] { "client_id", "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_keys",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "payments");
        }
    }
}
