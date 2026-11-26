using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Onyx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "delivery_projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    classification = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    client_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    target_date = table.Column<DateOnly>(type: "date", nullable: true),
                    repository_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    next_issue_number = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_projects", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_projects_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_project_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    added_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_project_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_project_members_delivery_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "delivery_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_project_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_sprints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    goal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    committed_issue_count = table.Column<int>(type: "integer", nullable: true),
                    committed_points = table.Column<int>(type: "integer", nullable: true),
                    completed_issue_count = table.Column<int>(type: "integer", nullable: true),
                    completed_points = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_sprints", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_sprints_delivery_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "delivery_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_workflow_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    system_key = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    wip_limit = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_workflow_statuses", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_workflow_statuses_delivery_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "delivery_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_issues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    key = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    epic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sprint_id = table.Column<Guid>(type: "uuid", nullable: true),
                    story_points = table.Column<int>(type: "integer", nullable: true),
                    estimate_hours = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    labels = table.Column<List<string>>(type: "text[]", nullable: false),
                    components = table.Column<List<string>>(type: "text[]", nullable: false),
                    rank = table.Column<long>(type: "bigint", nullable: false),
                    resolved_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    repository_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    branch_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pull_request_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    commit_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_issues", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_issues_delivery_issues_epic_id",
                        column: x => x.epic_id,
                        principalTable: "delivery_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_issues_delivery_issues_parent_id",
                        column: x => x.parent_id,
                        principalTable: "delivery_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_issues_delivery_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "delivery_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_issues_delivery_sprints_sprint_id",
                        column: x => x.sprint_id,
                        principalTable: "delivery_sprints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_delivery_issues_delivery_workflow_statuses_status_id",
                        column: x => x.status_id,
                        principalTable: "delivery_workflow_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_issues_users_assignee_id",
                        column: x => x.assignee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_delivery_issues_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_workflow_transitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_status_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_workflow_transitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_workflow_transitions_delivery_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "delivery_projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_workflow_transitions_delivery_workflow_statuses_fr~",
                        column: x => x.from_status_id,
                        principalTable: "delivery_workflow_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_workflow_transitions_delivery_workflow_statuses_to~",
                        column: x => x.to_status_id,
                        principalTable: "delivery_workflow_statuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_issue_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    edited_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_issue_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_issue_comments_delivery_issue_comments_parent_comm~",
                        column: x => x.parent_comment_id,
                        principalTable: "delivery_issue_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_issue_comments_delivery_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "delivery_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_issue_comments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_issue_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    field = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    old_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    new_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_issue_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_issue_history_delivery_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "delivery_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_issue_history_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_issue_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_issue_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_issue_links_delivery_issues_source_issue_id",
                        column: x => x.source_issue_id,
                        principalTable: "delivery_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_issue_links_delivery_issues_target_issue_id",
                        column: x => x.target_issue_id,
                        principalTable: "delivery_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_issue_links_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_issue_watchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_issue_watchers", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_issue_watchers_delivery_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "delivery_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_delivery_issue_watchers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_comments_author_id",
                table: "delivery_issue_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_comments_issue_id",
                table: "delivery_issue_comments",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_comments_parent_comment_id",
                table: "delivery_issue_comments",
                column: "parent_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_history_actor_id",
                table: "delivery_issue_history",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_history_issue_id_at_utc",
                table: "delivery_issue_history",
                columns: new[] { "issue_id", "at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_history_project_id_at_utc",
                table: "delivery_issue_history",
                columns: new[] { "project_id", "at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_links_created_by_id",
                table: "delivery_issue_links",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_links_source_issue_id_target_issue_id_type",
                table: "delivery_issue_links",
                columns: new[] { "source_issue_id", "target_issue_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_links_target_issue_id",
                table: "delivery_issue_links",
                column: "target_issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_watchers_issue_id_user_id",
                table: "delivery_issue_watchers",
                columns: new[] { "issue_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issue_watchers_user_id",
                table: "delivery_issue_watchers",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issues_assignee_id",
                table: "delivery_issues",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issues_due_date",
                table: "delivery_issues",
                column: "due_date");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issues_epic_id",
                table: "delivery_issues",
                column: "epic_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issues_key",
                table: "delivery_issues",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issues_labels",
                table: "delivery_issues",
                column: "labels")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issues_parent_id",
                table: "delivery_issues",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issues_project_id_number",
                table: "delivery_issues",
                columns: new[] { "project_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issues_project_id_sprint_id_rank",
                table: "delivery_issues",
                columns: new[] { "project_id", "sprint_id", "rank" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issues_project_id_status_id_rank",
                table: "delivery_issues",
                columns: new[] { "project_id", "status_id", "rank" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issues_reporter_id",
                table: "delivery_issues",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issues_sprint_id",
                table: "delivery_issues",
                column: "sprint_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_issues_status_id",
                table: "delivery_issues",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_project_members_project_id_user_id",
                table: "delivery_project_members",
                columns: new[] { "project_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_project_members_user_id",
                table: "delivery_project_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_projects_key",
                table: "delivery_projects",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_projects_owner_id",
                table: "delivery_projects",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_projects_status",
                table: "delivery_projects",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_sprints_project_id_state",
                table: "delivery_sprints",
                columns: new[] { "project_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ux_delivery_sprints_one_active_per_project",
                table: "delivery_sprints",
                column: "project_id",
                unique: true,
                filter: "state = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_workflow_statuses_project_id_order",
                table: "delivery_workflow_statuses",
                columns: new[] { "project_id", "order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_workflow_transitions_from_status_id",
                table: "delivery_workflow_transitions",
                column: "from_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_workflow_transitions_project_id_from_status_id_to_~",
                table: "delivery_workflow_transitions",
                columns: new[] { "project_id", "from_status_id", "to_status_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_workflow_transitions_to_status_id",
                table: "delivery_workflow_transitions",
                column: "to_status_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_issue_comments");

            migrationBuilder.DropTable(
                name: "delivery_issue_history");

            migrationBuilder.DropTable(
                name: "delivery_issue_links");

            migrationBuilder.DropTable(
                name: "delivery_issue_watchers");

            migrationBuilder.DropTable(
                name: "delivery_project_members");

            migrationBuilder.DropTable(
                name: "delivery_workflow_transitions");

            migrationBuilder.DropTable(
                name: "delivery_issues");

            migrationBuilder.DropTable(
                name: "delivery_sprints");

            migrationBuilder.DropTable(
                name: "delivery_workflow_statuses");

            migrationBuilder.DropTable(
                name: "delivery_projects");
        }
    }
}
