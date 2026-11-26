# Delivery workspace (internal project management)

An internal, Jira-style project and software-delivery workspace inside the Admin area: projects, issues, Kanban and sprint
boards, backlog, sprints, My Work and a dashboard. It is built on the existing architecture and security model; nothing about
the public site changes.

> Named **Delivery** because `Projects` / `Work` already mean the public portfolio (CLAUDE.md §2.2, `Permissions.Projects.*`).

## What is implemented

| Area | Status |
|---|---|
| Projects | Create (workspace administrators), edit, status/priority/type, client vs internal (client name required), dates, repository link, unique immutable key (`AI`, `WEB`…) |
| Members & roles | Per-project roles Viewer < Contributor < Developer < Manager < Admin; add/change/remove; a project always keeps one Admin |
| Workflow | Default workflow per project (Backlog → To Do → In Progress → In Review → Testing → Done, with step-back, review/testing → In Progress, back to Backlog, reopen). Transitions are validated on the server. Advisory WIP limit per status |
| Issues | Epic, Story, Task, Bug, Subtask, Feature, Improvement, Technical debt; unique keys (`AI-1`, never reused); title, plain-text description, priority, reporter, assignee (project members only), labels, components, story points, estimate, start/due dates, epic, parent (subtasks), sprint, resolution time, manual repository/branch/PR/commit links |
| Hierarchy | Subtasks always have a standard parent; standard issues may belong to an epic; epics/subtasks keep their type; issues with children cannot be deleted |
| Activity | Issue history (creation, every field change, status transitions, sprint moves, comments, links) with names resolved for display; audit log entries for project, membership, sprint and delete actions |
| Comments | Threaded (one level), edit by author, delete by author or project admin (placeholder kept) |
| Links & watchers | Blocks / relates to / duplicates within a project (shown from both sides); watch/unwatch (reporter and commenters watch automatically) |
| Search | Text (key/title), project, status category, type, priority, label, epic, overdue, sort, pagination; only visible projects |
| Kanban / Scrum board | Columns per workflow status; with an active sprint the board shows that sprint, otherwise every issue. Drag and drop (mouse) and keyboard (Space, arrows, Space/Enter, Escape) with persisted order; invalid transitions are refused by the server and the card rolls back. Filters by text, assignee, type, priority, label |
| Backlog & sprints | Ranked backlog and open sprints; drag/keyboard reordering and moving between sprints, plus a no-JavaScript "move to" form; create, plan, start (one active per project, dates and ≥1 issue required), complete or cancel (unfinished issues go to another open sprint or the backlog); epics list; sprint report from recorded snapshots |
| Dashboard | Active projects, open issues, assigned to me, overdue, completed this week, active sprints, project progress, upcoming deadlines (14 days), recently updated, recent activity, workload — all counted from the database for the caller's visible projects |
| My Work | Open issues assigned to the caller, with the same filters |
| Workspace dashboard | KPI cards (projects, active, tasks, completed, pending, overdue, mine, team throughput) with a change against the previous period only where both periods are real counts, sparklines, status donut, created-vs-completed line (7/30/90 days, daily or weekly), project progress and health, team and workload bars, widgets that can be hidden and reordered (kept per browser) |
| Reports | Productivity, project reports, team performance and workload for every visible project or one, over 7/30/90 days |
| Calendar | Due dates by month or week, across projects or one project; drag an issue to another day (the start date moves with it) |
| Timeline & roadmap | Per-project Gantt (dated issues under their epics, epic progress, sprints as bands, project start/target as milestones, "blocks" links as connectors) and a cross-project roadmap of projects and epics |
| Sprints | Per-project sprint list with velocity (committed vs completed as recorded at start/close); sprint report with a burndown rebuilt from the issue history and a retrospective (went well / to improve / actions, Manager+) |
| Attachments | Images, PDF and UTF-8 text/CSV up to 10 MB, typed from the bytes, images stripped of metadata, stored under random keys and served only as downloads after the access check; uploader or project admin removes |
| Time tracking | Work logs (1 min–24 h per entry, not in the future) with the total against the estimate; Contributor+ logs, the author or a project admin removes |
| Issue list | Bulk assign / priority / add label / delete (each issue checked and saved on its own), column chooser, assignee filter, saved filters (private, up to 30 per person) |
| Shell | Command palette (Ctrl/⌘+K: pages, actions, issue search), "New" menu, light/dark/system theme, confirmation dialogs, toast notices |

### Analytics rules

- Epics group work and are not counted as tasks — on the dashboard, in reports, on project cards and on the project overview alike.
  "Completed" means currently in a Done status, dated by when it got there.
- Counts are written so they read correctly for any number ("Points done: 3 of 20", "النقاط المنجزة: 3 من 20"), and the Arabic
  UI uses one term for a sprint (الدورة) and for the backlog (قائمة الانتظار).
- Project health is a stated rule, not an estimate: Completed and On hold come from the project status; otherwise Delayed (past the
  target date), At risk (overdue open issues) or On track.
- Team figures are counts of issues assigned to each person now; there is no productivity score. Days are UTC.

## Architecture

```
Admin views (Areas/Admin/Views/Delivery) → DeliveryController → IAdminDeliveryApiClient
  → /api/v1/admin/delivery/{projects|issues|sprints|dashboard} (HasPermission Delivery.Access)
  → DeliveryProjectService · IssueService · SprintService · DeliveryQueryService (+ DeliveryAccess)
  → IDeliveryProjectRepository · IIssueRepository · ISprintRepository · IDeliveryReadModel
  → ApplicationDbContext (PostgreSQL)
```

- **Domain** `src/Onyx.Domain/Delivery/`: `DeliveryProject` (workflow, members, issue numbering), `Issue`, `IssueComment`,
  `IssueHistoryEntry`, `IssueLink`, `IssueWatcher`, `Sprint`, enums, `DeliveryText` (input rules).
- **Application** `src/Onyx.Application/Delivery/`: services, `DeliveryAccess` (resource-level authorization), `IssueRanking`.
- **Infrastructure**: `Persistence/Configurations/Delivery/DeliveryConfigurations.cs`, `Persistence/Repositories/Delivery/*`
  (the read model resolves names in SQL and filters by visible projects; no N+1).
- **API**: `Controllers/Admin/AdminDelivery{Projects,Issues,Sprints}Controller.cs`.
- **Web**: `Areas/Admin/Controllers/DeliveryController.cs`, `Areas/Admin/Delivery/*` (view models), `Areas/Admin/Views/Delivery/*`,
  `wwwroot/js/delivery-board.js`, the "Delivery workspace" section of `wwwroot/css/admin.css`.

### Security

- `Delivery.Access` opens the workspace; projects are visible only to their members. `Delivery.Administer` (and SuperAdmin)
  sees and manages every project and is the only one who creates projects.
- Project capabilities: Viewer = read; Contributor = + create issues, comment; Developer = + edit, transition, move, link;
  Manager = + sprints; Admin = + project settings, members, WIP limits, delete issues.
- Enforced in the Application services on every call. Invisible projects, their issues and sprints answer **404**.
- Optimistic concurrency (`xmin`) on projects, issues and sprints: stale edits get **409**. Issue numbers are allocated under the
  project's version, so concurrent creates cannot share a key (the database also enforces unique keys).
- Text is stored and rendered as plain text (Razor-encoded; no `Html.Raw`). Links accept only http(s) without credentials and are
  never fetched by the server. Drag-and-drop posts a normal antiforgery-protected form. No inline script or style (CSP unchanged).

### Ordering

One sparse `rank` per issue, shared by board columns, sprints and the backlog. A move takes the midpoint between its new
neighbours; when there is no gap the project is renumbered in the same transaction.

## Database migration

Migration `20261025052745_DeliveryWorkspace` adds ten tables (`delivery_*`), indexes (including a GIN index on labels) and a
partial unique index allowing one active sprint per project. It creates tables only; it does not alter existing ones.

Migration `DeliveryWorkspaceEnhancements` (2026-11-25) adds `delivery_issue_attachments`, `delivery_issue_work_logs`,
`delivery_saved_issue_filters` and nullable retrospective columns on `delivery_sprints`. Migration `BuiltInRolesDeliveryAccess`
(data only) gives the built-in roles `Delivery.Access`. Both are additive. When applying by hand, run them as the owner role so the
new tables get the same owner and `onyx_app` grants as the rest.

Apply it like every other migration, from the pipeline or as an idempotent script — never automatically at start-up:

```bash
dotnet ef migrations script --idempotent --project src/Onyx.Infrastructure -o onyx-schema-idempotent.sql
```

## Setup after deploying

1. Apply the migration.
2. In Admin → Roles, give `Delivery.Access` to the roles that should use the workspace, and `Delivery.Administer` to the people who
   create and manage all projects (SuperAdmins already hold both).
3. A workspace administrator creates a project (Admin → Delivery → Projects) and adds members with their project roles.

## Tests

- `tests/Onyx.Domain.Tests/Delivery/DeliveryDomainTests.cs` — keys, workflow, last admin, hierarchy, resolution, validation, sprint lifecycle, comments.
- `tests/Onyx.Application.Tests/Delivery/IssueRankingTests.cs` — ranking and role capabilities.
- `tests/Onyx.API.IntegrationTests/Delivery/DeliveryApiTests.cs` — real API + PostgreSQL: permissions and project isolation,
  keys, roles, invalid transitions, stale versions, comments and links, persistent board order with renumbering, hierarchy and
  delete rules, sprint lifecycle and snapshots, search/My Work/dashboard counts.
- `tests/Onyx.Web.Tests/AdminDeliveryTests.cs` — menu visibility, encoded rendering, capability-based controls, move forwarding.

## Known limitations

- Workflow statuses and transitions use the default set; statuses and transitions cannot yet be edited in the UI (WIP limits can).
- No releases, per-user notifications (assignment / resolution) or cycle-time report yet. Meetings, discussions, documents, team
  availability and a "productivity score" are deliberately not shown: there is no real data behind them.
- Attachments accept images, PDF and plain text/CSV only (no Office documents); the timeline is read-only (dates change on the issue
  or by dragging in the calendar).
- Repository, branch, pull-request and commit links are entered manually; nothing is synchronized with GitHub.
- Board filters are applied to the loaded board, and drag and drop is switched off while a filter is active.
- After a project renumber, other cards on an open page keep their old version until reload; moving one of them then reports a
  conflict and asks for a reload (no data is lost).
- No AI features; the services are ready for them behind `ITextGenerationService` but nothing calls a model.

## Remaining work (suggested order)

1. Per-user notifications (assignment, resolution, mentions) — the existing notification centre is permission-based, so this
   needs a user-targeted notification type.
2. Attachments (reusing the media upload validation and storage, with per-project authorization).
3. Releases and a roadmap (epic timelines, release progress from issue states).
4. Reports: burndown (needs daily snapshots recorded by the Worker), velocity trend, cycle and lead time from history.
5. Workflow editor (custom statuses and transitions) and saved filters.
6. Optional AI assistance (epic breakdown, acceptance criteria, discussion summaries) through `ITextGenerationService`, behind
   human review.
