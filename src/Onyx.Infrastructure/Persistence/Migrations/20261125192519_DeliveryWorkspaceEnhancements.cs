using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onyx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryWorkspaceEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "retro_action_items",
                table: "delivery_sprints",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "retro_to_improve",
                table: "delivery_sprints",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "retro_updated_at_utc",
                table: "delivery_sprints",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "retro_updated_by_id",
                table: "delivery_sprints",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "retro_went_well",
                table: "delivery_sprints",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "delivery_issue_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    extension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    uploaded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_issue_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_issue_attachments_delivery_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "delivery_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_issue_attachments_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_issue_work_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    minutes = table.Column<int>(type: "integer", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_issue_work_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_issue_work_logs_delivery_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "delivery_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_issue_work_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_saved_issue_filters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    query = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_saved_issue_filters", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_saved_issue_filters_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_sprints_retro_updated_by_id",
                table: "delivery_sprints",
                column: "retro_updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_attachments_issue_id",
                table: "delivery_issue_attachments",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_attachments_storage_key",
                table: "delivery_issue_attachments",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_attachments_uploaded_by_id",
                table: "delivery_issue_attachments",
                column: "uploaded_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_work_logs_issue_id_work_date",
                table: "delivery_issue_work_logs",
                columns: new[] { "issue_id", "work_date" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_work_logs_user_id_work_date",
                table: "delivery_issue_work_logs",
                columns: new[] { "user_id", "work_date" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_saved_issue_filters_owner_id_created_at_utc",
                table: "delivery_saved_issue_filters",
                columns: new[] { "owner_id", "created_at_utc" });

            migrationBuilder.AddForeignKey(
                name: "fk_delivery_sprints_users_retro_updated_by_id",
                table: "delivery_sprints",
                column: "retro_updated_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_delivery_sprints_users_retro_updated_by_id",
                table: "delivery_sprints");

            migrationBuilder.DropTable(
                name: "delivery_issue_attachments");

            migrationBuilder.DropTable(
                name: "delivery_issue_work_logs");

            migrationBuilder.DropTable(
                name: "delivery_saved_issue_filters");

            migrationBuilder.DropIndex(
                name: "ix_delivery_sprints_retro_updated_by_id",
                table: "delivery_sprints");

            migrationBuilder.DropColumn(
                name: "retro_action_items",
                table: "delivery_sprints");

            migrationBuilder.DropColumn(
                name: "retro_to_improve",
                table: "delivery_sprints");

            migrationBuilder.DropColumn(
                name: "retro_updated_at_utc",
                table: "delivery_sprints");

            migrationBuilder.DropColumn(
                name: "retro_updated_by_id",
                table: "delivery_sprints");

            migrationBuilder.DropColumn(
                name: "retro_went_well",
                table: "delivery_sprints");
        }
    }
}
