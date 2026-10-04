using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onyx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CmsContentFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "call_to_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    texts = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_call_to_actions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "competitor_references",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    markets = table.Column<string>(type: "jsonb", nullable: false),
                    languages = table.Column<string>(type: "jsonb", nullable: false),
                    topics = table.Column<string>(type: "jsonb", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competitor_references", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "content_clusters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_clusters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_template_copies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    heading = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    paragraphs = table.Column<string>(type: "jsonb", nullable: false),
                    action_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    footer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_template_copies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    statements = table.Column<string>(type: "jsonb", nullable: false),
                    source_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    verified_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "media_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    extension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    texts = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cta_defaults",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    call_to_action_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cta_defaults", x => x.id);
                    table.ForeignKey(
                        name: "fk_cta_defaults_call_to_actions_call_to_action_id",
                        column: x => x.call_to_action_id,
                        principalTable: "call_to_actions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "brand_kits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    official_logo_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    website_url = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    company_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    colors = table.Column<string>(type: "jsonb", nullable: false),
                    typefaces = table.Column<string>(type: "jsonb", nullable: false),
                    allow_branding_on_featured_images = table.Column<bool>(type: "boolean", nullable: false),
                    allow_branding_on_social_images = table.Column<bool>(type: "boolean", nullable: false),
                    image_generation_notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_brand_kits", x => x.id);
                    table.ForeignKey(
                        name: "fk_brand_kits_media_assets_official_logo_media_id",
                        column: x => x.official_logo_media_id,
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "content_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    presentation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    primary_cta_id = table.Column<Guid>(type: "uuid", nullable: true),
                    featured_image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    live_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    scheduled_publish_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    scheduled_unpublish_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    schedule_time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    first_published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    offline_resolution = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    offline_redirect_to_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_revision_number = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_items_call_to_actions_primary_cta_id",
                        column: x => x.primary_cta_id,
                        principalTable: "call_to_actions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_content_items_content_items_offline_redirect_to_id",
                        column: x => x.offline_redirect_to_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_content_items_content_items_parent_id",
                        column: x => x.parent_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_content_items_media_assets_featured_image_id",
                        column: x => x.featured_image_id,
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "content_calendar_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    target_market = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    target_service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: true),
                    keyword = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    search_intent = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    supporting_content_ids = table.Column<string>(type: "jsonb", nullable: false),
                    proposed_publish_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    timing_basis = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    evidence = table.Column<string>(type: "jsonb", nullable: false),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    linked_content_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_calendar_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_calendar_entries_content_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "content_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_content_calendar_entries_content_items_linked_content_item_~",
                        column: x => x.linked_content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_content_calendar_entries_content_items_target_service_id",
                        column: x => x.target_service_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "content_cluster_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_cluster_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_cluster_members_content_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "content_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_content_cluster_members_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_paths",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    retired_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_paths", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_paths_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_performance_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    period_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    period_end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    metrics = table.Column<string>(type: "jsonb", nullable: false),
                    collected_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_performance_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_performance_snapshots_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_preview_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    token_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_preview_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_preview_tokens_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_relations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_relations", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_relations_content_items_from_content_item_id",
                        column: x => x.from_content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_content_relations_content_items_to_content_item_id",
                        column: x => x.to_content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    content_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    restored_from_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_revisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_revisions_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_translations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    slug_scope = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    body = table.Column<string>(type: "jsonb", nullable: false),
                    faqs = table.Column<string>(type: "jsonb", nullable: false),
                    primary_keyword = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    primary_keyword_normalized = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    secondary_keywords = table.Column<string>(type: "jsonb", nullable: false),
                    search_intent = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    meta_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    meta_description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    canonical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    robots_index = table.Column<bool>(type: "boolean", nullable: false),
                    robots_follow = table.Column<bool>(type: "boolean", nullable: false),
                    structured_data = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    open_graph_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    open_graph_description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    open_graph_image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_market = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    seo_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    seo_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_translations", x => x.id);
                    table.ForeignKey(
                        name: "fk_content_translations_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_content_translations_media_assets_open_graph_image_id",
                        column: x => x.open_graph_image_id,
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "information_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    context = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    knowledge_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    resolved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_information_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_information_requests_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_information_requests_knowledge_entries_knowledge_entry_id",
                        column: x => x.knowledge_entry_id,
                        principalTable: "knowledge_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "keyword_targets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    keyword = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_keyword = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    target_market = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    search_intent = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_content_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_keyword_targets", x => x.id);
                    table.ForeignKey(
                        name: "fk_keyword_targets_content_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "content_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_keyword_targets_content_items_target_content_item_id",
                        column: x => x.target_content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "media_usages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    context = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_live = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_usages", x => x.id);
                    table.ForeignKey(
                        name: "fk_media_usages_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_media_usages_media_assets_media_asset_id",
                        column: x => x.media_asset_id,
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "redirects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    from_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    target_content_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_redirects", x => x.id);
                    table.ForeignKey(
                        name: "fk_redirects_content_items_target_content_item_id",
                        column: x => x.target_content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seo_recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    cluster_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    origin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    basis = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    detected_issue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    proposed_changes = table.Column<string>(type: "jsonb", nullable: false),
                    reasoning = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    evidence = table.Column<string>(type: "jsonb", nullable: false),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    applied_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    applied_revision_number = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seo_recommendations", x => x.id);
                    table.ForeignKey(
                        name: "fk_seo_recommendations_content_clusters_cluster_id",
                        column: x => x.cluster_id,
                        principalTable: "content_clusters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_seo_recommendations_content_items_content_item_id",
                        column: x => x.content_item_id,
                        principalTable: "content_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "seo_change_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recommendation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    evidence = table.Column<string>(type: "jsonb", nullable: false),
                    assessment = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seo_change_observations", x => x.id);
                    table.ForeignKey(
                        name: "fk_seo_change_observations_seo_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "seo_recommendations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_brand_kits_official_logo_media_id",
                table: "brand_kits",
                column: "official_logo_media_id");

            migrationBuilder.CreateIndex(
                name: "ix_call_to_actions_key",
                table: "call_to_actions",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_competitor_references_domain",
                table: "competitor_references",
                column: "domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_calendar_entries_cluster_id",
                table: "content_calendar_entries",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_calendar_entries_linked_content_item_id",
                table: "content_calendar_entries",
                column: "linked_content_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_calendar_entries_status_proposed_publish_at_utc",
                table: "content_calendar_entries",
                columns: new[] { "status", "proposed_publish_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_content_calendar_entries_target_service_id",
                table: "content_calendar_entries",
                column: "target_service_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_cluster_members_cluster_id_content_item_id",
                table: "content_cluster_members",
                columns: new[] { "cluster_id", "content_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_cluster_members_content_item_id",
                table: "content_cluster_members",
                column: "content_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_items_featured_image_id",
                table: "content_items",
                column: "featured_image_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_items_key",
                table: "content_items",
                column: "key",
                unique: true,
                filter: "key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_content_items_live_revision_id",
                table: "content_items",
                column: "live_revision_id",
                filter: "live_revision_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_content_items_offline_redirect_to_id",
                table: "content_items",
                column: "offline_redirect_to_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_items_offline_resolution",
                table: "content_items",
                column: "offline_resolution",
                filter: "offline_resolution = 'PendingDecision'");

            migrationBuilder.CreateIndex(
                name: "ix_content_items_parent_id",
                table: "content_items",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_items_primary_cta_id",
                table: "content_items",
                column: "primary_cta_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_items_scheduled_unpublish_at_utc",
                table: "content_items",
                column: "scheduled_unpublish_at_utc",
                filter: "scheduled_unpublish_at_utc IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_content_items_status_scheduled_publish_at_utc",
                table: "content_items",
                columns: new[] { "status", "scheduled_publish_at_utc" },
                filter: "scheduled_publish_at_utc IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_content_items_type_status",
                table: "content_items",
                columns: new[] { "type", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_content_paths_content_item_id_language_code",
                table: "content_paths",
                columns: new[] { "content_item_id", "language_code" },
                unique: true,
                filter: "is_current");

            migrationBuilder.CreateIndex(
                name: "ix_content_paths_language_code_path",
                table: "content_paths",
                columns: new[] { "language_code", "path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_performance_snapshots_content_item_id_language_code~",
                table: "content_performance_snapshots",
                columns: new[] { "content_item_id", "language_code", "period_end_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_content_preview_tokens_content_item_id",
                table: "content_preview_tokens",
                column: "content_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_preview_tokens_expires_at_utc",
                table: "content_preview_tokens",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_content_preview_tokens_token_hash",
                table: "content_preview_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_relations_from_content_item_id_to_content_item_id_k~",
                table: "content_relations",
                columns: new[] { "from_content_item_id", "to_content_item_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_relations_to_content_item_id",
                table: "content_relations",
                column: "to_content_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_revisions_content_item_id_number",
                table: "content_revisions",
                columns: new[] { "content_item_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_translations_content_item_id_language_code",
                table: "content_translations",
                columns: new[] { "content_item_id", "language_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_translations_language_code_meta_title",
                table: "content_translations",
                columns: new[] { "language_code", "meta_title" },
                filter: "meta_title IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_content_translations_language_code_primary_keyword_normaliz~",
                table: "content_translations",
                columns: new[] { "language_code", "primary_keyword_normalized" },
                filter: "primary_keyword_normalized IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_content_translations_language_code_slug_scope_slug",
                table: "content_translations",
                columns: new[] { "language_code", "slug_scope", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_translations_open_graph_image_id",
                table: "content_translations",
                column: "open_graph_image_id");

            migrationBuilder.CreateIndex(
                name: "ix_cta_defaults_call_to_action_id",
                table: "cta_defaults",
                column: "call_to_action_id");

            migrationBuilder.CreateIndex(
                name: "ix_cta_defaults_content_type",
                table: "cta_defaults",
                column: "content_type",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_email_template_copies_template_key_language_code",
                table: "email_template_copies",
                columns: new[] { "template_key", "language_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_information_requests_content_item_id",
                table: "information_requests",
                column: "content_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_information_requests_knowledge_entry_id",
                table: "information_requests",
                column: "knowledge_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_information_requests_status",
                table: "information_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_keyword_targets_cluster_id",
                table: "keyword_targets",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "ix_keyword_targets_language_code_normalized_keyword_target_mar~",
                table: "keyword_targets",
                columns: new[] { "language_code", "normalized_keyword", "target_market" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_keyword_targets_target_content_item_id",
                table: "keyword_targets",
                column: "target_content_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_entries_status_category",
                table: "knowledge_entries",
                columns: new[] { "status", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_created_at_utc",
                table: "media_assets",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_sha256",
                table: "media_assets",
                column: "sha256",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_storage_key",
                table: "media_assets",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_usages_content_item_id",
                table: "media_usages",
                column: "content_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_media_usages_media_asset_id_content_item_id_language_code_c~",
                table: "media_usages",
                columns: new[] { "media_asset_id", "content_item_id", "language_code", "context", "is_live" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_redirects_language_code_from_path",
                table: "redirects",
                columns: new[] { "language_code", "from_path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_redirects_target_content_item_id",
                table: "redirects",
                column: "target_content_item_id",
                filter: "target_content_item_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_seo_change_observations_recommendation_id_recorded_at_utc",
                table: "seo_change_observations",
                columns: new[] { "recommendation_id", "recorded_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_seo_recommendations_cluster_id",
                table: "seo_recommendations",
                column: "cluster_id");

            migrationBuilder.CreateIndex(
                name: "ix_seo_recommendations_content_item_id_kind_language_code",
                table: "seo_recommendations",
                columns: new[] { "content_item_id", "kind", "language_code" });

            migrationBuilder.CreateIndex(
                name: "ix_seo_recommendations_status_created_at_utc",
                table: "seo_recommendations",
                columns: new[] { "status", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brand_kits");

            migrationBuilder.DropTable(
                name: "competitor_references");

            migrationBuilder.DropTable(
                name: "content_calendar_entries");

            migrationBuilder.DropTable(
                name: "content_cluster_members");

            migrationBuilder.DropTable(
                name: "content_paths");

            migrationBuilder.DropTable(
                name: "content_performance_snapshots");

            migrationBuilder.DropTable(
                name: "content_preview_tokens");

            migrationBuilder.DropTable(
                name: "content_relations");

            migrationBuilder.DropTable(
                name: "content_revisions");

            migrationBuilder.DropTable(
                name: "content_translations");

            migrationBuilder.DropTable(
                name: "cta_defaults");

            migrationBuilder.DropTable(
                name: "email_template_copies");

            migrationBuilder.DropTable(
                name: "information_requests");

            migrationBuilder.DropTable(
                name: "keyword_targets");

            migrationBuilder.DropTable(
                name: "media_usages");

            migrationBuilder.DropTable(
                name: "redirects");

            migrationBuilder.DropTable(
                name: "seo_change_observations");

            migrationBuilder.DropTable(
                name: "knowledge_entries");

            migrationBuilder.DropTable(
                name: "seo_recommendations");

            migrationBuilder.DropTable(
                name: "content_clusters");

            migrationBuilder.DropTable(
                name: "content_items");

            migrationBuilder.DropTable(
                name: "call_to_actions");

            migrationBuilder.DropTable(
                name: "media_assets");
        }
    }
}
