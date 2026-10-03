using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onyx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CookieConsentReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cookie_consent_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    policy_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    analytics = table.Column<bool>(type: "boolean", nullable: false),
                    marketing = table.Column<bool>(type: "boolean", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ip_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cookie_consent_receipts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cookie_consent_receipts_consent_id",
                table: "cookie_consent_receipts",
                column: "consent_id");

            migrationBuilder.CreateIndex(
                name: "ix_cookie_consent_receipts_recorded_at_utc",
                table: "cookie_consent_receipts",
                column: "recorded_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cookie_consent_receipts");
        }
    }
}
