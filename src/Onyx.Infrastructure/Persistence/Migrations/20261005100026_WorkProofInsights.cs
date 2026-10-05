using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onyx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkProofInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "author_id",
                table: "content_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "insight_kind",
                table: "content_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Existing content is human-written: backfill with the enum name the model stores.
            migrationBuilder.AddColumn<string>(
                name: "origin",
                table: "content_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Human");

            migrationBuilder.AddColumn<string>(
                name: "proof_verification",
                table: "content_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "authors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    names = table.Column<string>(type: "jsonb", nullable: false),
                    consent_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "featured_projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    featured_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    featured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_featured_projects", x => x.id);
                    table.CheckConstraint("ck_featured_projects_slot", "slot BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "fk_featured_projects_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "proof_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    texts = table.Column<string>(type: "jsonb", nullable: false),
                    value = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    as_of = table.Column<DateOnly>(type: "date", nullable: true),
                    attribution_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attribution_role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attribution_organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    verification = table.Column<string>(type: "jsonb", nullable: true),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proof_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_proof_items_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_content_items_author_id",
                table: "content_items",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_featured_projects_content_item_id",
                table: "featured_projects",
                column: "content_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_featured_projects_slot",
                table: "featured_projects",
                column: "slot",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_proof_items_content_item_id_status_sort_order",
                table: "proof_items",
                columns: new[] { "content_item_id", "status", "sort_order" });

            migrationBuilder.AddForeignKey(
                name: "fk_content_items_authors_author_id",
                table: "content_items",
                column: "author_id",
                principalTable: "authors",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_content_items_authors_author_id",
                table: "content_items");

            migrationBuilder.DropTable(
                name: "authors");

            migrationBuilder.DropTable(
                name: "featured_projects");

            migrationBuilder.DropTable(
                name: "proof_items");

            migrationBuilder.DropIndex(
                name: "ix_content_items_author_id",
                table: "content_items");

            migrationBuilder.DropColumn(
                name: "author_id",
                table: "content_items");

            migrationBuilder.DropColumn(
                name: "insight_kind",
                table: "content_items");

            migrationBuilder.DropColumn(
                name: "origin",
                table: "content_items");

            migrationBuilder.DropColumn(
                name: "proof_verification",
                table: "content_items");
        }
    }
}
