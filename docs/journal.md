# Development Journal

Log of features completed, decisions made, and issues encountered during the hackathon. Updated automatically via `/checkpoint` after each feature.

---

## [2026-04-18 00:44] — Scaffold minimum working stack

**What:** Walking-skeleton scaffold so `docker compose up` brings the full stack online: .NET 10 Minimal API with `/health` (EF Core + Npgsql + Serilog + CORS), React 19 + Vite 6 + TS + Tailwind v4 frontend that fetches `/health` on mount, Postgres 16, and an xUnit v3 + FluentAssertions test project. `/health` is wired to `Database.CanConnectAsync()`, the page renders the live result, and the golden rule was verified end-to-end through containers (incl. Playwright).

**Story:** none — scaffold predates `docs/stories.md` content (still empty stub awaiting `/process-task`).

**Decisions:**
- `tests/Api.Tests/` (per CLAUDE.md) over `src/Api.Tests/`; updated `/checkpoint` command path to match.
- Tailwind v4 with `@tailwindcss/vite` plugin (no JS config) — simpler than v3.
- `xunit.v3` package family and `.slnx` (the .NET 10 default solution format) over the legacy `.sln`.
- `/health` always returns 200 (with `status:"degraded"`/`database:"down"` in the body when DB is unreachable) so `curl -f` in `/verify-deployable` sees a live API, and the body carries the DB signal.
- `VITE_API_BASE_URL` passed as a build-arg in the web Dockerfile and baked into the bundle: the browser runs on the host, so it must call `localhost:8080`, not the in-network `api:8080`.
- API runtime image installs `curl` so the compose healthcheck can hit `/health`.
- API CORS policy whitelists `http://localhost:3000` only (not `*`).
- No EF Core entities or migrations yet — `/health` only pings the connection. Entities arrive with the first feature story.
- `Program.cs` ends with `public partial class Program;` so `WebApplicationFactory<Program>` integration tests can be added later without touching the API project.

**Blockers/Issues:**
- `dotnet new sln` on .NET 10.0.201 creates `.slnx` (XML format) by default; the chained `dotnet sln DataArtHackaton.sln add ...` failed because the file is `DataArtHackaton.slnx`. Recovered by re-running `dotnet sln DataArtHackaton.slnx add ...`. Side effect: a batch of parallel `Write` calls was cancelled and had to be replayed.
- Docker Desktop daemon was not running on the host. Launched via `Start-Process` from PowerShell and polled `docker info` via `Monitor` until ready (~95s).

**Tokens/Time (rough):** ~45 min wall clock from start-of-plan to fully verified deployable; one full plan/explore pass plus one cold `/verify-deployable` run.

**Next:** Add `docs/task.md` (the hackathon brief) so `/process-task` can generate `docs/requirements.md` and `docs/stories.md`. After that, run `/add-feature 1.1` to implement the first real story on top of this scaffold.

---
