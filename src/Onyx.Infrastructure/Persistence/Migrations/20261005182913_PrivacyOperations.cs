using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onyx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivacyOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "erased_at_utc",
                table: "leads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "erased_at_utc",
                table: "inquiries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "data_subject_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subject_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    subject_email_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    request_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    received_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verified_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    completed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_subject_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "retention_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    period_days = table.Column<int>(type: "integer", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_retention_policies", x => x.id);
                    table.CheckConstraint("ck_retention_policies_period", "period_days IS NULL OR (period_days BETWEEN 1 AND 36500)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_inquiries_normalized_email",
                table: "inquiries",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ix_data_subject_requests_status_received_at_utc",
                table: "data_subject_requests",
                columns: new[] { "status", "received_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_data_subject_requests_subject_email_hash",
                table: "data_subject_requests",
                column: "subject_email_hash");

            migrationBuilder.CreateIndex(
                name: "ix_retention_policies_category",
                table: "retention_policies",
                column: "category",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_subject_requests");

            migrationBuilder.DropTable(
                name: "retention_policies");

            migrationBuilder.DropIndex(
                name: "ix_inquiries_normalized_email",
                table: "inquiries");

            migrationBuilder.DropColumn(
                name: "erased_at_utc",
                table: "leads");

            migrationBuilder.DropColumn(
                name: "erased_at_utc",
                table: "inquiries");
        }
    }
}
