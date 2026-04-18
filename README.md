# ![DataArt](./docs/images/logo.png) DataArt - Hackaton 2026

DataArt Hackathon 2026 submission — an experiment in Agentic Development Life Cycle (ADLC).

## Running the app

```bash
git clone https://github.com/edu-fede/da-hackaton.git
cd da-hackaton
docker compose up
```

That is the only supported way to run this application. 
The web frontend will be available at `http://localhost:3000` and the API at `http://localhost:8080` (see `docker-compose.yml` for exact ports).

## Repository guide

| Path | Purpose |
|---|---|
| `CLAUDE.md` | Context and conventions for Claude Code (the AI agent driving development) |
| `.claude/commands/` | Custom commands — the automation backbone of the ADLC workflow |
| `docs/task.md` | Original hackathon task description |
| `docs/requirements.md` | Structured requirements extracted from the task |
| `docs/stories.md` | Implementation-ready stories in Jira-compatible format |
| `docs/journal.md` | Running log of decisions, blockers, and progress |
| `src/Api/` | .NET backend (Minimal API) |
| `src/Web/` | React frontend (Vite + TypeScript + Tailwind) |

## ADLC approach

This project deliberately implements a pipeline of AI-facing artifacts rather than treating the agent as an autocomplete tool. The intent is to show that investing in orchestration produces better results than raw prompting — this is the hypothesis the hackathon is testing.

**The pipeline:**

```
task.md  →  /process-task  →  requirements.md + stories.md  →  /add-feature (per story)  →  /checkpoint (per feature)  →  journal.md
                                                                                                             ↓
                                                                                                     /verify-deployable (periodic)
```

**Custom commands:**
- `/process-task` — reads `docs/task.md` and produces structured requirements and stories.
- `/add-feature [story-id]` — implements a single story with test-first discipline (plan → test → implement → verify → commit).
- `/checkpoint` — runs tests, commits if green, appends a dated entry to `docs/journal.md`.
- `/verify-deployable` — simulates a grader running `git clone && docker compose up` to catch environment drift early.

**Jira compatibility:** `docs/stories.md` is written in a format (Summary / Description / Acceptance Criteria / Priority / Labels / Story Points) compatible with Jira import via the Atlassian MCP. The design closes the loop spec → ticket → implementation, even though actual ticket creation is out of scope for this event.

## Standing rules during development

- **Golden rule:** if `docker compose up` does not produce a working app, nothing else matters. Verified via `/verify-deployable`.
- **Harness engineering:** tests before implementation, never commit red, tight edit-test-fix loops.
- **Plan before acting:** non-trivial changes go through Plan Mode.
- **Journal everything:** decisions, blockers, and learnings are captured in `docs/journal.md` as they happen.

## Stack

- .NET 10, Minimal APIs, EF Core + Npgsql, Serilog, xUnit v3 + FluentAssertions
- React + Vite + TypeScript + Tailwind CSS, Vitest + Testing Library
- PostgreSQL 16
- Docker Compose
- SignalR (if required by the task)

## Notes

The `CLAUDE.md` file is the authoritative context document for the AI agent. The commands in `.claude/commands/` are the executable workflow primitives. Together they form the ADLC this project demonstrates.