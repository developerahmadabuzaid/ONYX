using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Onyx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NewsHub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "news_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    keywords = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_news_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "news_category_translations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    news_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_news_category_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_news_category_translations_news_categories_news_category_id",
                        column: x => x.news_category_id,
                        principalTable: "news_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "news_sources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    homepage_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    feed_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    feed_format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    default_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    licensing_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    auto_publish = table.Column<bool>(type: "boolean", nullable: false),
                    fetch_interval_minutes = table.Column<int>(type: "integer", nullable: false),
                    next_fetch_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_fetched_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_succeeded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_news_sources", x => x.id);
                    table.ForeignKey(
                        name: "fk_news_sources_news_categories_default_category_id",
                        column: x => x.default_category_id,
                        principalTable: "news_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "news_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    original_url_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    original_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    publisher = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    source_language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    source_published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ingested_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    similar_to_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    hold_reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    summary_origin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    summary_provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    auto_published = table.Column<bool>(type: "boolean", nullable: false),
                    indexable = table.Column<bool>(type: "boolean", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_news_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_news_items_news_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "news_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_news_items_news_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "news_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "news_item_translations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    news_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    summary = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_news_item_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_news_item_translations_news_items_news_item_id",
                        column: x => x.news_item_id,
                        principalTable: "news_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "news_categories",
                columns: new[] { "id", "created_at_utc", "created_by_id", "key", "keywords", "sort_order", "updated_at_utc", "updated_by_id" },
                values: new object[,]
                {
                    { new Guid("0192f0c0-0000-7000-8000-000000000001"), new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "artificial-intelligence", "[]", 1, null, null },
                    { new Guid("0192f0c0-0000-7000-8000-000000000002"), new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "software-technology", "[]", 2, null, null },
                    { new Guid("0192f0c0-0000-7000-8000-000000000003"), new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "digital-marketing", "[]", 3, null, null },
                    { new Guid("0192f0c0-0000-7000-8000-000000000004"), new DateTimeOffset(new DateTime(2026, 10, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "cybersecurity", "[]", 4, null, null }
                });

            migrationBuilder.InsertData(
                table: "news_category_translations",
                columns: new[] { "id", "language_code", "name", "news_category_id", "slug" },
                values: new object[,]
                {
                    { new Guid("0192f0c0-0001-7000-8000-000000000001"), "ar", "الذكاء الاصطناعي", new Guid("0192f0c0-0000-7000-8000-000000000001"), "الذكاء-الاصطناعي" },
                    { new Guid("0192f0c0-0001-7000-8000-000000000002"), "en", "Artificial Intelligence", new Guid("0192f0c0-0000-7000-8000-000000000001"), "artificial-intelligence" },
                    { new Guid("0192f0c0-0001-7000-8000-000000000003"), "ar", "البرمجيات والتقنية", new Guid("0192f0c0-0000-7000-8000-000000000002"), "البرمجيات-والتقنية" },
                    { new Guid("0192f0c0-0001-7000-8000-000000000004"), "en", "Software & Technology", new Guid("0192f0c0-0000-7000-8000-000000000002"), "software-technology" },
                    { new Guid("0192f0c0-0001-7000-8000-000000000005"), "ar", "التسويق الرقمي", new Guid("0192f0c0-0000-7000-8000-000000000003"), "التسويق-الرقمي" },
                    { new Guid("0192f0c0-0001-7000-8000-000000000006"), "en", "Digital Marketing", new Guid("0192f0c0-0000-7000-8000-000000000003"), "digital-marketing" },
                    { new Guid("0192f0c0-0001-7000-8000-000000000007"), "ar", "الأمن السيبراني", new Guid("0192f0c0-0000-7000-8000-000000000004"), "الأمن-السيبراني" },
                    { new Guid("0192f0c0-0001-7000-8000-000000000008"), "en", "Cybersecurity", new Guid("0192f0c0-0000-7000-8000-000000000004"), "cybersecurity" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_news_categories_key",
                table: "news_categories",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_news_category_translations_language_code_slug",
                table: "news_category_translations",
                columns: new[] { "language_code", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_news_category_translations_news_category_id_language_code",
                table: "news_category_translations",
                columns: new[] { "news_category_id", "language_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_news_item_translations_language_code_slug",
                table: "news_item_translations",
                columns: new[] { "language_code", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_news_item_translations_news_item_id_language_code",
                table: "news_item_translations",
                columns: new[] { "news_item_id", "language_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_news_items_category_id",
                table: "news_items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_news_items_original_url_hash",
                table: "news_items",
                column: "original_url_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_news_items_source_id_ingested_at_utc",
                table: "news_items",
                columns: new[] { "source_id", "ingested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_news_items_source_language_code_cluster_key_ingested_at_utc",
                table: "news_items",
                columns: new[] { "source_language_code", "cluster_key", "ingested_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_news_items_status_published_at_utc",
                table: "news_items",
                columns: new[] { "status", "published_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_news_sources_default_category_id",
                table: "news_sources",
                column: "default_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_news_sources_feed_url",
                table: "news_sources",
                column: "feed_url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_news_sources_status_next_fetch_at_utc",
                table: "news_sources",
                columns: new[] { "status", "next_fetch_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "news_category_translations");

            migrationBuilder.DropTable(
                name: "news_item_translations");

            migrationBuilder.DropTable(
                name: "news_items");

            migrationBuilder.DropTable(
                name: "news_sources");

            migrationBuilder.DropTable(
                name: "news_categories");
        }
    }
}
