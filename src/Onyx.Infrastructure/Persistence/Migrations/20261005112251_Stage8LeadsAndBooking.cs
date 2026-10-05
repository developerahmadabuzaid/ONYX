using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onyx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stage8LeadsAndBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attachments",
                table: "email_messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "availability_exceptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_availability_exceptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "availability_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_availability_rules", x => x.id);
                    table.CheckConstraint("ck_availability_rules_window", "end_time > start_time");
                });

            migrationBuilder.CreateTable(
                name: "booking_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slot_minutes = table.Column<int>(type: "integer", nullable: false),
                    buffer_minutes = table.Column<int>(type: "integer", nullable: false),
                    minimum_notice_hours = table.Column<int>(type: "integer", nullable: false),
                    horizon_days = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    pillar_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    priority = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    score_reasons = table.Column<string>(type: "jsonb", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    inquiry_count = table.Column<int>(type: "integer", nullable: false),
                    first_inquiry_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_inquiry_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leads", x => x.id);
                    table.ForeignKey(
                        name: "fk_leads_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "processed_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_processed_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inquiries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    answers = table.Column<string>(type: "jsonb", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    call_to_action_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attribution = table.Column<string>(type: "jsonb", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inquiries", x => x.id);
                    table.ForeignKey(
                        name: "fk_inquiries_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lead_activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    detail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    related_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lead_activities", x => x.id);
                    table.ForeignKey(
                        name: "fk_lead_activities_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lead_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lead_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_lead_notes_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consent_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inquiry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_consent = table.Column<bool>(type: "boolean", nullable: false),
                    marketing_consent = table.Column<bool>(type: "boolean", nullable: false),
                    consent_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    given_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    marketing_withdrawn_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    marketing_withdrawn_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_consent_records_inquiries_inquiry_id",
                        column: x => x.inquiry_id,
                        principalTable: "inquiries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_consent_records_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "consultation_bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    lead_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inquiry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    manage_token_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    manage_token_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rescheduled_from_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rescheduled_to_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_visitor = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consultation_bookings", x => x.id);
                    table.CheckConstraint("ck_consultation_bookings_range", "end_utc > start_utc");
                    table.ForeignKey(
                        name: "fk_consultation_bookings_inquiries_inquiry_id",
                        column: x => x.inquiry_id,
                        principalTable: "inquiries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_consultation_bookings_leads_lead_id",
                        column: x => x.lead_id,
                        principalTable: "leads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_availability_exceptions_date",
                table: "availability_exceptions",
                column: "date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_availability_rules_day_of_week_start_time",
                table: "availability_rules",
                columns: new[] { "day_of_week", "start_time" });

            migrationBuilder.CreateIndex(
                name: "ix_booking_settings_key",
                table: "booking_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_inquiry_id",
                table: "consent_records",
                column: "inquiry_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_consent_records_lead_id",
                table: "consent_records",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_consultation_bookings_confirmed_slot",
                table: "consultation_bookings",
                column: "start_utc",
                unique: true,
                filter: "status = 'Confirmed'");

            migrationBuilder.CreateIndex(
                name: "ix_consultation_bookings_inquiry_id",
                table: "consultation_bookings",
                column: "inquiry_id");

            migrationBuilder.CreateIndex(
                name: "ix_consultation_bookings_lead_id",
                table: "consultation_bookings",
                column: "lead_id");

            migrationBuilder.CreateIndex(
                name: "ix_consultation_bookings_manage_token_hash",
                table: "consultation_bookings",
                column: "manage_token_hash",
                filter: "manage_token_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_consultation_bookings_status_start_utc",
                table: "consultation_bookings",
                columns: new[] { "status", "start_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_inquiries_lead_id_submitted_at_utc",
                table: "inquiries",
                columns: new[] { "lead_id", "submitted_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_inquiries_submitted_at_utc",
                table: "inquiries",
                column: "submitted_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_lead_activities_lead_id_occurred_at_utc",
                table: "lead_activities",
                columns: new[] { "lead_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_lead_notes_lead_id_created_at_utc",
                table: "lead_notes",
                columns: new[] { "lead_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_normalized_email_stage",
                table: "leads",
                columns: new[] { "normalized_email", "stage" });

            migrationBuilder.CreateIndex(
                name: "ix_leads_owner_id",
                table: "leads",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_leads_stage_last_inquiry_at_utc",
                table: "leads",
                columns: new[] { "stage", "last_inquiry_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_processed_events_key",
                table: "processed_events",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "availability_exceptions");

            migrationBuilder.DropTable(
                name: "availability_rules");

            migrationBuilder.DropTable(
                name: "booking_settings");

            migrationBuilder.DropTable(
                name: "consent_records");

            migrationBuilder.DropTable(
                name: "consultation_bookings");

            migrationBuilder.DropTable(
                name: "lead_activities");

            migrationBuilder.DropTable(
                name: "lead_notes");

            migrationBuilder.DropTable(
                name: "processed_events");

            migrationBuilder.DropTable(
                name: "inquiries");

            migrationBuilder.DropTable(
                name: "leads");

            migrationBuilder.DropColumn(
                name: "attachments",
                table: "email_messages");
        }
    }
}
