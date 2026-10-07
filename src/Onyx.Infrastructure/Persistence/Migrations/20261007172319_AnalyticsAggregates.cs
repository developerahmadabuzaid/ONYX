using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onyx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnalyticsAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analytics_sync_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_success_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_failure_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    data_from_date = table.Column<DateOnly>(type: "date", nullable: true),
                    data_through_date = table.Column<DateOnly>(type: "date", nullable: true),
                    last_row_count = table.Column<int>(type: "integer", nullable: false),
                    last_run_truncated = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_analytics_sync_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "search_performance_daily_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    page = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    query = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dimension_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    clicks = table.Column<long>(type: "bigint", nullable: false),
                    impressions = table.Column<long>(type: "bigint", nullable: false),
                    average_position = table.Column<double>(type: "double precision", nullable: false),
                    collected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_performance_daily_metrics", x => x.id);
                    table.ForeignKey(
                        name: "fk_search_performance_daily_metrics_content_items_content_item_~",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "web_analytics_daily_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    landing_page = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    medium = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    campaign = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    device_category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    event_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dimension_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    active_users = table.Column<long>(type: "bigint", nullable: true),
                    sessions = table.Column<long>(type: "bigint", nullable: false),
                    engaged_sessions = table.Column<long>(type: "bigint", nullable: false),
                    page_views = table.Column<long>(type: "bigint", nullable: false),
                    event_count = table.Column<long>(type: "bigint", nullable: false),
                    collected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_web_analytics_daily_metrics", x => x.id);
                    table.ForeignKey(
                        name: "fk_web_analytics_daily_metrics_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_analytics_sync_states_provider_key",
                table: "analytics_sync_states",
                column: "provider_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_search_performance_daily_metrics_content_item_id",
                table: "search_performance_daily_metrics",
                column: "content_item_id",
                filter: "content_item_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_search_performance_daily_metrics_provider_key_date_kind_dim~",
                table: "search_performance_daily_metrics",
                columns: new[] { "provider_key", "date", "kind", "dimension_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_search_performance_daily_metrics_provider_key_kind_date",
                table: "search_performance_daily_metrics",
                columns: new[] { "provider_key", "kind", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_web_analytics_daily_metrics_content_item_id",
                table: "web_analytics_daily_metrics",
                column: "content_item_id",
                filter: "content_item_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_web_analytics_daily_metrics_provider_key_date_kind_dimensio~",
                table: "web_analytics_daily_metrics",
                columns: new[] { "provider_key", "date", "kind", "dimension_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_web_analytics_daily_metrics_provider_key_kind_date",
                table: "web_analytics_daily_metrics",
                columns: new[] { "provider_key", "kind", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_sync_states");

            migrationBuilder.DropTable(
                name: "search_performance_daily_metrics");

            migrationBuilder.DropTable(
                name: "web_analytics_daily_metrics");
        }
    }
}
