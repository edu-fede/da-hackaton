# Hackathon 2026 — Context for Claude Code

## Project
Web application built for DataArt's Hackathon 2026. The experiment tests whether a developer can deliver a working application by directing AI agents rather than writing code directly (agentic development). The developer reviews and directs; the agent implements.

The task (see `docs/task.md`): a classic web-based online chat application with auth, public/private rooms, 1-to-1 messaging, friends/contacts, attachments, moderation, persistent history, and presence.

## Golden Rule (non-negotiable)
The application **must** start cleanly with:

```bash
git clone <repo> && cd <repo> && docker compose up
```

This is a **binary evaluation gate**. If `docker compose up` does not bring the app to a working state, the project scores zero — regardless of code quality or features. Verify this rule holds after every feature using `/verify-deployable`.

## Stack
- **Backend:** .NET 10, Minimal APIs, EF Core + Npgsql, Serilog, xUnit v3 + FluentAssertions
- **Real-time:** SignalR (WebSocket transport) for messaging, presence, notifications — runs on the same HTTP port as the API
- **In-process queue:** `System.Threading.Channels` for message buffering (no external broker)
- **Background processing:** `BackgroundService` / `IHostedService` hosted inside the API process (not a separate container)
- **Frontend:** React + Vite + TypeScript + Tailwind CSS, Vitest + Testing Library
- **SignalR client (web):** `@microsoft/signalr`
- **Database:** PostgreSQL 16
- **File storage:** local filesystem, exposed to the API container via a Docker volume
- **Logging:** Serilog with both console sink (→ `docker compose logs`) and file sink (→ volume-mounted `./logs/`)
- **Container:** Docker Compose (services: `db`, `api`, `web`)

## Architecture Constraints (from task briefing — NON-NEGOTIABLE)

These were called out explicitly by the task author during the kickoff. They are **design constraints**, not optimizations. Violating them will require rework and may cause the system to miss its performance targets.

### 1. Real-time messaging flow
Message path MUST be asynchronous with an in-memory queue between reception and persistence/broadcast:

```
Client sends message (SignalR) → Hub receives
  → push to in-memory Channel<MessageWorkItem>
  → ack to sender + broadcast to room (via SignalR)           [fast path]

Background consumer (BackgroundService):
  → read from Channel → persist to Postgres → log             [slow path]
```

The sender does NOT wait for the DB write before the message appears for receivers. The DB write is fire-and-forget from the user's perspective.

**Do NOT** implement "receive → INSERT → SELECT → broadcast" as a single synchronous chain. That pattern will not meet the <3s delivery target under concurrent load.

**Nature of the queue — read carefully:** the `Channel<T>` is a **transient processing buffer**, not a per-user mailbox. A message enters the channel when received and exits within milliseconds as soon as the consumer processes it. The channel NEVER holds messages addressed to offline users. What offline users eventually read comes from the **database**, fetched on reconnect via the watermark resync (see point 3). This is why the queue cannot grow unboundedly: it holds at-most-one-burst of in-flight processing, not historical backlog.

### 2. Presence tracking
User online/AFK/offline state is held in an **in-memory structure** on the server:

```
ConcurrentDictionary<userId, PresenceInfo> in the SignalR Hub
  - populated on OnConnectedAsync
  - updated on client heartbeat / activity signal
  - removed on OnDisconnectedAsync
  - changes broadcast over SignalR to affected rooms/contacts
```

**Do NOT** store presence as a queryable column in the database (e.g., `Users.Status`, `Users.LastSeenAt`). Do NOT poll the database for presence. Presence is ephemeral, lives in RAM, and is recomputed from active connections.

Activity detection (AFK rule):

- The client sends heartbeats to the Hub when there is user activity (mouse move, keypress, touch, scroll). Throttle to at most one heartbeat every 10–15 seconds; do not send one per event.
- The server stores lastHeartbeat per user in the presence dictionary. A PeriodicTimer transitions users to AFK when now - lastHeartbeat > 60s (the 1-minute rule from the task).
- Do NOT rely on the client to report inactivity. Browsers hibernate inactive tabs; JavaScript stops running in hibernated tabs. Therefore the client cannot reliably emit an "I am now AFK" signal. Inactivity must be inferred server-side by absence of heartbeat.
- The client CAN reliably emit "I am active" signals while the tab is foregrounded. That is what heartbeats are.
- Multi-tab rule (from task 2.2.3): any active tab for a given user counts as activity for that user globally. Track heartbeats per-connection (each tab = one SignalR connection), and the user is AFK only when ALL their connections have stale heartbeats.

### 3. Chat watermarks (message history integrity)
Every message in every chat gets a per-chat incremental sequence number (a "watermark"). Clients track the last watermark they have seen per chat. On reconnect or on demand:

```
Client → Server: "my watermarks are room-abc-123:5, room-xyz-789:12"
Server → Client: for each room, return any messages with sequence > client's value
```

This guarantees history integrity without maintaining a per-user offline message queue. If the client's watermark is behind the server's latest, there is a gap → re-fetch from the DB.

**Keying — important:** watermarks are keyed by **room ID** (the stable DB identifier — int or UUID), NOT by room name. Room names can be released and reused when a room is deleted; IDs are never reused. A client holding a stale watermark for a deleted room's ID will get "not found / not a member" from the server and can discard it. There is no possibility of cross-room contamination.

**Implementation sketch:**
- `Message` table includes `SequenceInRoom` (int, monotonically increasing per room).
- On insert, compute as `COALESCE(MAX(SequenceInRoom), 0) + 1` scoped to the room. Use pessimistic lock or rely on a unique constraint `(RoomId, SequenceInRoom)` to prevent duplicates under concurrency.
- Client maintains a `Map<RoomId, LastSeenSequence>` in memory and persists it to localStorage/IndexedDB.
- Personal 1-to-1 dialogs are modeled as rooms with exactly 2 members, so they use the same watermark mechanism.

### 4. REST vs WebSocket — what goes where
- **REST (HTTP):** registration, login, logout, password change, profile, account deletion, listing rooms, listing contacts, creating rooms, managing friends, file upload/download, admin actions (ban/kick/invite), history fetch for watermark resync.
- **SignalR (WebSocket):** sending/receiving messages in real time, presence updates, unread indicator updates, message edit/delete notifications, real-time room member changes.

If in doubt: is it request-response? → REST. Is it push/streaming? → SignalR.

### 5. Deployment topology
One API process runs **everything** backend-side: HTTP endpoints (Minimal APIs), SignalR Hub, the in-memory Channel, and the BackgroundService consumer. No separate container for the worker.

```
Container `api`:
  ├── Kestrel (HTTP endpoints + SignalR Hub)    ← receives messages
  ├── Channel<MessageWorkItem> (in-process)     ← the buffer
  └── MessageProcessorService : BackgroundService  ← drains the buffer
```

**Why not separate the worker:**
- `Channel<T>` is in-memory. A separate container would require an out-of-process broker (Redis, RabbitMQ), adding a new service and complexity for zero benefit at this scale.
- For 300 concurrent users on a single server, one process is more than enough.
- If the container crashes, the watermark-based resync handles recovery: clients reconnect, send their watermarks, and get any missing messages from the DB.

### 6. Scale targets (for sanity checks; not a load test target)
- 300 concurrent users.
- 1000 users per room.
- 10K+ messages per room with smooth infinite scroll → **cursor-based pagination**, never OFFSET.
- Message delivery <3s end-to-end, presence updates <2s.

## Project Structure
```
da-hackaton/
├── src/
│   ├── Api/              # .NET Minimal API + SignalR Hub + message queue consumer
│   └── Web/              # React + Vite + SignalR client
├── tests/
│   └── Api.Tests/        # xUnit v3 (Web tests colocated in src/Web)
├── docs/
│   ├── task.md           # Original task description
│   ├── requirements.md   # Structured requirements extracted from task
│   ├── stories.md        # Implementable stories (Jira-compatible format)
│   └── journal.md        # Development log — THE key deliverable per task author
├── logs/                 # Serilog file sink output (mounted into api container; gitignored)
├── .claude/
│   └── commands/         # Custom commands (/process-task, /add-feature, /checkpoint, /verify-deployable, /journal-note)
├── docker-compose.yml
├── CLAUDE.md             # This file
└── README.md
```

## Conventions
- English naming everywhere
- C#: PascalCase for types/methods, camelCase for variables/parameters, standard .NET conventions
- TypeScript: camelCase for variables, PascalCase for components, kebab-case for file names
- Structured logging (Serilog) in backend, with correlation IDs where flows span boundaries
- No hardcoded credentials, connection strings, or URLs — use environment variables exclusively
- Configuration via `appsettings.json` + environment overrides (backend), Vite env vars (frontend)

## Workflow Orchestration

### Plan Mode Default
Enter Plan Mode for any non-trivial task (3+ steps or architectural decisions). Propose a plan before executing structural work; wait for developer approval on significant changes. If something goes sideways, **stop and re-plan — don't push through**.

### Harness Engineering (core discipline)
- **Never commit red.** If any test fails, fix it before moving on.
- For every feature: write a failing test → implement minimal code → verify green → commit.
- Run tests after every meaningful change (not just at the end).
- Lint errors are hard errors, not warnings.
- The tight feedback loop (edit → test → error → fix) is what makes the AI effective. Don't skip it.

### Verification Before Done
No task is complete until proven:
1. Unit tests pass (`dotnet test` for backend, `npm test -- --run` for frontend).
2. Build succeeds.
3. `docker compose up -d --build` brings the system to a working state.
4. The feature actually does what the acceptance criteria say (exercise it manually or via Playwright MCP).

If a fix feels hacky, pause and ask — don't paper over it.

### Checkpoint Pattern
After each feature, invoke `/checkpoint`. This runs tests, commits if green, and appends progress to `docs/journal.md`. **Do not skip** — the journal is **half of the evaluation** per the task author's explicit guidance: "the most valuable output from this is your feedback".

### Task Management
- Read `docs/stories.md` for story priority and status.
- Work one story at a time, fully verified before moving to the next.
- Update acceptance criteria checkboxes in `docs/stories.md` as they are met.
- If the story is unclear or ambiguous, ask before assuming.

### Self-Improvement Loop
After any correction from the developer, update `CLAUDE.md` or the relevant command file with the corrected pattern. Apply the same correction to similar existing cases. Goal: zero repeated mistakes across the session.

## Code Comments
- Do NOT describe what the code does — the code should be self-explanatory.
- DO explain WHY a non-obvious decision was made.
- DO add XML doc comments (`///`) on public API contracts and shared types.

## Error Handling
- **API:** return RFC 9457 `ProblemDetails` for HTTP errors — no custom formats.
- **SignalR:** use `HubException` with meaningful messages; let the client handle reconnect logic.
- **Logging:** structured with Serilog, include correlation IDs and user IDs where available. Never swallow errors silently.
- **Frontend:** user-visible errors must be actionable; log technical detail to console in dev.

## UI Guidance
The task appendix includes ASCII wireframes as a **reference**, not a binding spec. Per the task author: "make UI concise and usable". Follow the general layout (top menu, central message area, side lists) but do not pixel-chase the wireframes. Tailwind defaults are fine.

## Known Constraints
- **Evaluation window:** submission by Monday 12:00 UTC (Saturday kickoff, work split across 3 days).
- **Agentic mode:** the developer directs and reviews; code is written by the agent. Direct code edits are allowed for small corrections but should be rare.
- **Graders run `docker compose up` with no context.** Anything that requires manual setup, local paths, or "works on my machine" assumptions is a failure.
- **Jabber/XMPP federation (section 6 of task):** explicitly marked OPTIONAL and advanced. Do NOT attempt until all core requirements are green and verified.

## ADLC Intent
This project is structured to demonstrate Agentic Development Life Cycle (ADLC):
- Task (`docs/task.md`) → Requirements (`docs/requirements.md`) → Stories (`docs/stories.md`) → Implementation.
- `/process-task` automates the task-to-stories transformation.
- Stories use a Jira-compatible markdown format, enabling downstream MCP integration (Atlassian Rovo / Jira MCP) to create real tickets from specs.
- `/add-feature` implements one story with plan → test → implement → verify → commit discipline.
- `/checkpoint` automates per-feature verification and documentation in `docs/journal.md`.
- `/verify-deployable` simulates the grader's environment (clean `docker compose up`).
- `/journal-note` captures mid-feature observations (friction, decisions, aha moments) without requiring a checkpoint.
- `docs/journal.md` is the narrative record of the development process — decisions, blockers, reflections, lessons.

The goal is to prove that a well-designed pipeline of agent-facing artifacts (rules, commands, context docs) produces higher-quality output than raw prompting.