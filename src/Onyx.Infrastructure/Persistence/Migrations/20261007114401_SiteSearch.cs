using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Onyx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SiteSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "search_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    content_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    group_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    live_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    indexed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    title_normalized = table.Column<string>(type: "text", nullable: false),
                    summary_normalized = table.Column<string>(type: "text", nullable: false),
                    body_normalized = table.Column<string>(type: "text", nullable: false),
                    names_normalized = table.Column<string>(type: "text", nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "setweight(to_tsvector(CASE WHEN language_code = 'ar' THEN 'arabic'::regconfig ELSE 'english'::regconfig END, title_normalized), 'A') || setweight(to_tsvector(CASE WHEN language_code = 'ar' THEN 'arabic'::regconfig ELSE 'english'::regconfig END, summary_normalized), 'B') || setweight(to_tsvector(CASE WHEN language_code = 'ar' THEN 'arabic'::regconfig ELSE 'english'::regconfig END, body_normalized), 'C')", stored: true),
                    names_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('simple'::regconfig, names_normalized)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_search_documents_content_item_id_language_code",
                table: "search_documents",
                columns: new[] { "content_item_id", "language_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_search_documents_names_vector",
                table: "search_documents",
                column: "names_vector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_search_documents_path",
                table: "search_documents",
                column: "path");

            migrationBuilder.CreateIndex(
                name: "ix_search_documents_search_vector",
                table: "search_documents",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_search_documents_title_normalized",
                table: "search_documents",
                column: "title_normalized")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_documents");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");
        }
    }
}
