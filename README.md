# ![DataArt](./docs/images/logo.png) DataArt - Hackaton 2026

DataArt Hackathon 2026 submission — an experiment in Agentic Development Life Cycle (ADLC).

> **Scope note.** This submission delivers an MVP with the core chat functionality (auth, rooms, real-time messaging, presence, message edit/delete/reply). Several task features are intentionally out of scope for this submission — see **Feature coverage** below for the per-requirement status. The focus of the experiment was the ADLC methodology, not exhaustive feature completion.

## Running the app

```bash
git clone https://github.com/edu-fede/da-hackaton.git
cd da-hackaton
docker compose up
```

That is the only supported way to run this application. No `dotnet run` dance, no `npm install` dance, no environment variables to set. Clone, compose up, go.

The web frontend will be available at `http://localhost:3000` and the API at `http://localhost:8080`

(change in `docker-compose.yml` if necessary).

## Repository guide

| Path | Purpose |
|---|---|
| `CLAUDE.md` | Context and conventions for Claude Code (the AI agent driving development) |
| `.claude/commands/` | Custom commands — the automation backbone of the ADLC workflow |
| `docs/task.md` | Original hackathon task description |
| `docs/requirements.md` | Structured requirements extracted from the task |
| `docs/stories.md` | Implementation-ready stories in Jira-compatible format |
| `docs/journal.md` | Structured per-feature checkpoint log |
| `docs/journal-notes.md` | Methodological notes captured during the work |
| `docs/reflections.md` | Personal observations about the experience |
| `src/Api/` | .NET backend (Minimal API) |
| `src/Web/` | React frontend (Vite + TypeScript + Tailwind) |

## Records of this weekend

Three complementary documents capture the full ADLC cycle — from spec authoring through implementation to methodological reflection:

- **`docs/journal.md`** — feature-by-feature development log. One structured
  entry per completed story: decisions, blockers, verification, time
  metrics. Generated via `/checkpoint` after each story landed.
- **`docs/journal-notes.md`** — methodological observations captured
  during the work: workflow patterns, tool usage findings, friction
  points, self-observed habits. Chronological, each note contextualized
  against the story in progress when it was captured.
- **`docs/reflections.md`** — personal, discursive observations about
  the experience. Written in a more informal voice, including raw
  weekend notes at the end.

Read in any order. For a quick sense of *what was built*, start with
the journal. For *what was learned about the workflow*, the notes.
For *what it was like*, the reflections.

## Findings

Four observations from running this experiment that are worth surfacing upfront (expanded in `docs/journal-notes.md` and `docs/reflections.md`):

**1. Dual-loop development emerged naturally.** A pattern formed without being designed for: a fast loop (human ↔ Claude Code) for implementation, and a slow loop (human ↔ Claude.ai) for architectural review of risky plans before execution. The fast loop is where the code gets written. The slow loop catches proposals that are locally correct but globally suboptimal — exactly the class of decision where an executor deep in the codebase tends to minimize disruption to current state rather than propose the right thing.

**2. Cross-flow bugs surface only in manual multi-actor smoke.** Three bugs appeared during the weekend (owner-can't-send-messages, joiner-membership-gap, F5 connection race). All three passed the automated test suite — unit and integration, backend and frontend, 100% green. All three were caught by opening two browsers with different users and exercising the real interaction. Automated tests verify what's specified per component; the interactions *between* components, roles, or async chains only break under real multi-actor use. Multi-browser smoke after every milestone isn't optional.

**3. Decisions upstream save downstream confusion.** Eight open questions were resolved at the `/process-task` stage (password policy, rate-limiting scope, session metadata sourcing, ban semantics, and others) — before any feature code was written. Over the 15+ stories that followed, not one required a "wait, should I…" interruption on those topics. The cost of resolving them upfront (~30 minutes) paid back across the entire implementation phase.

**4. Within-session methodological self-learning is observable.** During the Story 1.15 fix cycle, after an earlier tsc regression had slipped past the test runner, Claude Code unprompted added `npm run build` to its verification protocol and explicitly cited the earlier incident in the same session as the reason. The agent modified its own methodology based on a lesson from a previous task in the same session. That's a meaningful line between "a faster way to type" and "a collaborator that learns on the job."

## ADLC approach

This project deliberately implements a pipeline of AI-facing artifacts rather than treating the agent as an autocomplete tool. **Every line of code and every test in this repository was generated by Claude Code under human direction; no line was typed by hand.** Human contribution went into design, architectural constraints (`CLAUDE.md`), plan review at story boundaries, and end-to-end verification. The intent is to show that investing in orchestration produces better results than raw prompting — this is the hypothesis the hackathon is testing.

**The pipeline:**

```
task.md  →  /process-task  →  requirements.md + stories.md  →  /add-feature (per story)  →  /checkpoint (per feature)  →  journal.md
                                                                                                             ↓
                                                                                                     /verify-deployable (periodic)
```

**Custom commands:**
- `/process-task` — reads `docs/task.md` and produces structured requirements (53 FR + 16 NFR + 4 EXT items with traceable IDs) and a Jira-compatible story backlog (~30 stories prioritized for MVP-first delivery) in one pass, typically in ~5 minutes.
- `/add-feature [story-id]` — implements a single story with test-first discipline (plan → test → implement → verify → commit).
- `/checkpoint` — runs tests, commits if green, appends a dated entry to `docs/journal.md`.
- `/journal-note` — captures a methodological observation mid-task, categorized as Meta / Insight / Friction / Decision / Blocker, appended to `docs/journal-notes.md`.
- `/verify-deployable` — simulates a grader running `git clone && docker compose up` to catch environment drift early.

**Jira compatibility:** `docs/stories.md` is written in a format (Summary / Description / Acceptance Criteria / Priority / Labels / Story Points) compatible with Jira import via the Atlassian MCP. The design closes the loop spec → ticket → implementation, even though actual ticket creation is out of scope for this event.

## Standing rules during development

- **Golden rule:** if `docker compose up` does not produce a working app, nothing else matters. Verified via `/verify-deployable`.
- **Harness engineering:** tests before implementation, never commit red, tight edit-test-fix loops.
- **Plan before acting:** non-trivial changes go through Plan Mode.
- **Journal everything:** decisions, blockers, and learnings are captured in `docs/journal.md` (per-feature) and `docs/journal-notes.md` (methodological observations) as they happen.

## Monitoring

Once the stack is up, you can observe the system in two ways:

**Live logs (streamed):**
```bash
docker compose logs -f api      # API + SignalR + background worker
docker compose logs -f web      # nginx access log
docker compose logs -f db       # Postgres
```

**Persistent logs:** the API writes structured logs to `./logs/api-YYYYMMDD.log` (volume-mounted from the container). These survive `docker compose down`.

**Service health:**
```bash
docker compose ps               # current status of all services
curl -s http://localhost:8080/health | jq    # API health + DB connectivity
```

## Stack

- .NET 10, Minimal APIs, EF Core + Npgsql, Serilog, xUnit v3 + FluentAssertions
- React 19 + Vite 6 + TypeScript + Tailwind v4, Vitest + Testing Library
- PostgreSQL 16
- SignalR for real-time messaging and presence
- Docker Compose

## Feature coverage

Per-feature status against the task requirements. MVP bar (core chat functionality) is green; several non-MVP features are intentionally deferred.

| Feature | Status | Notes |
|---|---|---|
| User registration & login | ✅ | Email + username unique, password policy enforced |
| Session-based auth | ✅ | HttpOnly cookie, multi-session, per-session logout |
| Password change | ⏭️ | |
| Account deletion (soft-delete + cascade) | ⏭️ | Out of scope for this submission |
| Public rooms (create / list / join / leave) | ✅ | Catalog with substring search |
| Private rooms | ✅ | Invisible from public catalog; members see them |
| Real-time messaging | ✅ | SignalR + in-memory `Channel<T>` + BackgroundService persistence |
| Message history (infinite scroll) | ✅ | Cursor pagination, <200ms at 10K messages |
| Reconnect with watermark resync | ✅ | Per-room sequence watermarks stored in localStorage |
| Message edit / delete / reply | ⚠️ | Backend + broadcasts working; UI hover interaction pending verification |
| Presence (online / AFK / offline) | ✅ | Server-authoritative AFK inference, multi-tab aware |
| Members list with role & status | ✅ | Initial snapshot + live `PresenceChanged` deltas |
| Friend requests / contacts | ⏭️ | Out of scope for this submission |
| 1-to-1 direct messages | ⏭️ | Out of scope (modeled as "rooms of 2"; wiring deferred) |
| Room moderation (admin ban/kick) | ⏭️ | Out of scope |
| File attachments | ⏭️ | Out of scope |
| Unread message indicators | ⏭️ | Out of scope |
| Jabber/XMPP federation | ⏭️ | Out of scope (task-waivable extension) |

**Legend:** ✅ working end-to-end · ⚠️ partial or verification pending · ⏭️ deliberately out of scope · ❌ broken

## Known limitations (production gaps)

- **No rate limiting on auth endpoints.** `/api/auth/register` and `/api/auth/login` accept unbounded traffic from any caller. Production deployment would add per-IP throttling via ASP.NET Core's built-in `AddRateLimiter`. Out of scope for the hackathon by explicit decision (see §6 of the decisions block in `docs/stories.md`).
- **Extended browser-tab hibernation edge case on presence.** If a tab backgrounds for hours and the browser fully hibernates the JS runtime, the heartbeat loop stops emitting; the user is correctly marked AFK server-side. On wake, the user remains visible as AFK to peers until the next activity event (mouse/key/scroll) nudges the heartbeat. Root cause is structural (browsers hibernate tabs, client cannot self-report "I'm back"). Accepted tradeoff for MVP.

## Notes

The `CLAUDE.md` file is the authoritative context document for the AI agent. The commands in `.claude/commands/` are the executable workflow primitives. Together they form the ADLC this project demonstrates.