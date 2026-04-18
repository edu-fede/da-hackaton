# Hackathon 2026 — Context for Claude Code

## Project
Web application built for DataArt's Hackathon 2026. The experiment tests whether a developer can deliver a working application by directing AI agents rather than writing code directly (agentic development). The developer reviews and directs; the agent implements.

## Golden Rule (non-negotiable)
The application **must** start cleanly with:

```bash
git clone <repo> && cd <repo> && docker compose up
```

This is a **binary evaluation gate**. If `docker compose up` does not bring the app to a working state, the project scores zero — regardless of code quality or features. Verify this rule holds after every feature using `/verify-deployable`.

## Stack
- **Backend:** .NET 10, Minimal APIs, EF Core + Npgsql, Serilog, xUnit v3 + FluentAssertions
- **Frontend:** React + Vite + TypeScript + Tailwind CSS, Vitest + Testing Library
- **Database:** PostgreSQL 16
- **Container:** Docker Compose (services: `db`, `api`, `web`)
- **Real-time (if required):** SignalR

## Project Structure
```
da-hackaton/
├── src/
│   ├── Api/              # .NET Minimal API
│   └── Web/              # React + Vite
├── tests/
│   └── Api.Tests/        # xUnit v3 (Web tests colocated in src/Web)
├── docs/
│   ├── task.md           # Original task description (converted from .docx if needed)
│   ├── requirements.md   # Structured requirements extracted from task
│   ├── stories.md        # Implementable stories (Jira-compatible format)
│   └── journal.md        # Development log (updated via /checkpoint)
├── .claude/
│   └── commands/         # Custom commands (/checkpoint, /process-task, /add-feature, /verify-deployable)
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
Enter Plan Mode for any non-trivial task (3+ steps or architectural decisions). 
Propose a plan before executing structural work; wait for developer approval on significant changes. 
If something goes sideways, **stop and re-plan — don't push through**.

### Harness Engineering (core discipline)
- **Never commit red.** If any test fails, fix it before moving on.
- For every feature: write a failing test → implement minimal code → verify green → commit.
- Run tests after every meaningful change (not just at the end).
- Lint errors are hard errors, not warnings.
- The tight feedback loop (edit → test → error → fix) is what makes the AI effective. Don't skip it.

### Verification Before Done
No task is complete until proven:
1. Unit tests pass (`dotnet test` for backend, `npm test` for frontend).
2. Build succeeds.
3. `docker compose up -d --build` brings the system to a working state.
4. The feature actually does what the acceptance criteria say (exercise it manually or via Playwright MCP).

If a fix feels hacky, pause and ask — don't paper over it.

### Checkpoint Pattern
After each feature, invoke `/checkpoint`. This runs tests, commits if green, and appends progress to `docs/journal.md`. 
Do not skip — the journal is part of the deliverable.

### Task Management
- Read `docs/stories.md` for story priority and status.
- Work one story at a time, fully verified before moving to the next.
- Update acceptance criteria checkboxes in `docs/stories.md` as they are met.
- If the story is unclear or ambiguous, ask before assuming.

### Self-Improvement Loop
After any correction from the developer, update `CLAUDE.md` or the relevant command file with the corrected pattern. 
Apply the same correction to similar existing cases. 
Goal: zero repeated mistakes across the session.

## Code Comments
- Do NOT describe what the code does — the code should be self-explanatory.
- DO explain WHY a non-obvious decision was made.
- DO add XML doc comments (`///`) on public API contracts and shared types.

## Error Handling
- **API:** return RFC 9457 `ProblemDetails` for errors — no custom formats.
- **Logging:** structured with Serilog, include correlation IDs where available. Never swallow errors silently.
- **Frontend:** user-visible errors must be actionable; log technical detail to console in dev.

## Known Constraints
- **Evaluation window:** 8 working hours on hackathon day; deadline is hard.
- **Agentic mode:** the developer directs and reviews; code is written by the agent. Direct code edits are allowed for small corrections but should be rare.
- **Graders run `docker compose up` with no context.** Anything that requires manual setup, local paths, or "works on my machine" assumptions is a failure.

## ADLC Intent
This project is structured to demonstrate Agentic Development Life Cycle (ADLC):
- Task (`docs/task.md`) → Requirements (`docs/requirements.md`) → Stories (`docs/stories.md`) → Implementation.
- `/process-task` automates the task-to-stories transformation.
- Stories use a Jira-compatible markdown format, enabling downstream MCP integration (Atlassian Rovo / Jira MCP) to create real tickets from specs.
- `/checkpoint` automates per-feature verification and documentation.
- `/verify-deployable` simulates the grader's environment.
- `docs/journal.md` captures decisions and learnings for post-hoc review.

The goal is to prove that a well-designed pipeline of agent-facing artifacts (rules, commands, skills, context docs) produces higher-quality output than raw prompting.