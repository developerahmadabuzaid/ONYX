using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onyx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SiteContentDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "site_content_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    draft_json = table.Column<string>(type: "jsonb", nullable: true),
                    draft_media_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    draft_updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    draft_updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_json = table.Column<string>(type: "jsonb", nullable: true),
                    published_media_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_revision = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_content_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "site_content_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    json = table.Column<string>(type: "jsonb", nullable: false),
                    media_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_content_revisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_site_content_revisions_site_content_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "site_content_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_site_content_documents_key",
                table: "site_content_documents",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_site_content_revisions_document_id_number",
                table: "site_content_revisions",
                columns: new[] { "document_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_content_revisions");

            migrationBuilder.DropTable(
                name: "site_content_documents");
        }
    }
}
