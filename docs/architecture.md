# QA Platform Architecture

## Goals

The platform is designed for small game QA teams (roughly 10 testers, a few developers, and 2 admins) while supporting multiple concurrent projects.

Core principles:

1. Tester UX must be extremely simple.
2. Hugging Face credentials never ship inside tester/admin executables.
3. Video/build binaries live in Hugging Face Storage Buckets.
4. Live metadata and state live in a transactional database, not GitHub JSON files.
5. Every meaningful state change is appended to an event/audit history.
6. A task may be shared by multiple testers; shared state is identical for every assignee while personal actions remain user-specific.
7. Bug progress represents the state of known issues, never an estimate of how much of a mission/game has been tested.
8. Project archival and destructive purge are separate concepts.

## Components

### Tester Client

Windows desktop application for average PC users.

Responsibilities:
- First-run name/device enrollment.
- Show projects assigned to the tester.
- Show shared tasks and deadlines.
- Show latest approved test build for each project.
- Download builds and display installation instructions/changelog.
- Submit bug reports and videos.
- Receive retest requests.
- Submit retest results and optional evidence video.
- Receive live notifications and mandatory announcements.
- Show personal reports plus shared task/project issue status.
- Self-update through a stable bootstrap executable.

### Admin/Developer Client

One desktop codebase with role-based screens.

Developer capabilities:
- Upload build candidates.
- Add build title, description, installation guide, changelog and fix candidates.
- Inspect bugs relevant to builds.

Admin capabilities:
- Everything developers can do.
- Publish the current test build.
- Create projects, sections and shared tasks.
- Assign testers and deadlines.
- Request retests.
- Review/classify bugs and set root cause.
- Send normal/mandatory notifications.
- View analytics.
- Archive builds.
- Close projects.
- Request/approve permanent project purge.

### Backend API

Runs as a Docker service (target: Hugging Face Space).

Responsibilities:
- Authentication/device enrollment.
- Authorization and roles.
- Project/task/build/bug/retest lifecycle.
- Analytics queries.
- Event stream/audit log.
- Notification fan-out via WebSocket with polling fallback.
- Secure Hugging Face bucket operations.
- Release manifest for desktop client updates.
- Destructive purge orchestration.

### Database

Production target: PostgreSQL.

SQLite may be used for local development only.

The database stores metadata and relationships, not large videos/build archives.

### Hugging Face Storage

Binary storage is namespaced by project.

Current bucket:

`xykeskin/dmc-turkish-dub-qa-archive`

Recommended logical prefixes:

```text
projects/{project_id}/
  reports/{report_id}/
    evidence/{asset_id}.mp4
  retests/{retest_id}/
    evidence/{asset_id}.mp4
  builds/
    active/{build_id}/{filename}
    archived/{build_id}/{filename}
```

Moving a build from active to archived must preserve the same build identity in the database.

A separate private control bucket may be used for small operational artifacts if needed, but database state remains authoritative.

## Domain Hierarchy

```text
Project
  ├─ Section (Mission / Chapter / Level / Episode / custom)
  ├─ Build
  ├─ Shared Task
  │    └─ Task Assignees
  ├─ Bug Report
  │    ├─ Evidence Assets
  │    ├─ Bug Events
  │    └─ Retest Requests
  │          └─ Retest Results
  ├─ Notifications
  └─ Analytics (derived)
```

## Bug Lifecycle

Primary path:

```text
NEW
 -> ACKNOWLEDGED
 -> IN_PROGRESS
 -> RETEST_REQUIRED
 -> RESOLVED
```

Retest failure returns the issue to `IN_PROGRESS`.

Side states:
- `ON_HOLD`
- `DUPLICATE`
- `NOT_A_BUG`
- `WONT_FIX`
- `REOPENED`

Status changes must append an immutable event containing actor, timestamp, old state, new state and optional note/build reference.

## Progress Metrics

Never show "Mission X is 70% tested" unless a future explicit checklist proves that value.

The progress bar means only known issue resolution state.

Example denominator:
- Include confirmed/active/resolved known issues.
- Exclude duplicate and not-a-bug reports.

Suggested segments:
- Resolved
- In progress
- Retest required
- New/unstarted
- On hold

## Shared Tasks

A shared task has one task ID and multiple assignees.

All assignees see the same:
- Task title/description.
- Required build.
- Deadline.
- Shared issue counts.
- Shared task activity.

Each tester additionally sees personal actions:
- Their reports.
- Retests assigned to them.
- Their acknowledgement/completion state.

## Builds

Build states:

```text
UPLOADING -> CANDIDATE -> CURRENT -> SUPERSEDED -> ARCHIVED
```

Only admins can publish `CURRENT` or archive builds.

Developer uploads do not automatically become visible as the current tester build.

Every bug report records `reported_build_id`.
Every retest result records `tested_build_id`.
Every fix candidate relates a bug to a target build.

The client displays the latest admin-approved `CURRENT` build, not simply the most recently uploaded file.

## Client Updates

The tester keeps one stable launcher executable.

The launcher checks a release manifest before starting the UI payload.

Release channels:
- stable
- beta

Manifest includes:
- version
- download artifact
- SHA-256
- release notes
- mandatory/minimum version

The launcher validates the downloaded payload hash before switching versions.

## Notifications

Notification targets:
- All users.
- Project members.
- Task assignees.
- Selected users.
- Single user.

Severity:
- info
- warning
- critical
- must_read

Must-read announcements require acknowledgement and expose acknowledgement analytics to admins.

## Project Closure and Purge

### Close

`ACTIVE -> CLOSED`

Closing:
- Blocks new reports/tasks/build publication for that project.
- Keeps all files and history accessible to admins.
- Does not delete data.

### Permanent Purge

Purge is intentionally difficult.

Requirements:
1. Project must already be CLOSED.
2. Show exact counts and estimated bytes to be deleted.
3. Require exact project-name confirmation.
4. Require a generated destructive confirmation code.
5. Require a second distinct admin approval.
6. Delete only paths scoped to the immutable project ID.
7. Remove transactional project data after storage deletion succeeds (or record a recoverable purge failure state).
8. Keep a minimal non-content audit tombstone unless super-admin explicitly chooses total audit destruction.

## Security

- Never embed HF write tokens in executables.
- Backend secrets only via environment/secret manager.
- Device enrollment uses generated device credentials, not a reusable global password.
- Server performs all authorization checks; UI hiding is not authorization.
- Build/update artifacts should be hashed and ideally code-signed.
- Every destructive/admin action is audited.
