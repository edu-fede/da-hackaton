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

### [2026-04-18 13:58 ART] Note — Meta

Auto mode is trickier than I expected. The instinct is to stay out of the loop so I don't become the bottleneck, but I'm finding the human checkpoint is needed more often than I'd hoped. Concrete example: automatic updates to `CLAUDE.md`. Not only do those deserve my review on their own merits — they also quietly **evict me from the shared context** if I skim the generated plan instead of reading it carefully. Plan mode (my default for every task) helps, but only if I actually read what's been done, not just what's proposed next.

The deeper question I'm parking for later: do I even need to stay abreast of the full context, or is that a losing battle? For `CLAUDE.md` specifically the answer is clearly yes — it's the contract that shapes every future turn. For other artifacts it's less obvious, and "how much context the human keeps" might be the real ADLC design knob.

---

### [2026-04-18 14:12 ART] Note — Insight

Mind note: **importance of Plan Mode — verified, several times over.** Building on the earlier Meta note: the cost-benefit math is lopsided in plan mode's favor. A few seconds spent reading a proposed plan routinely saves me from unwanted actions (wrong file touched, over-eager refactor, a command I'd have vetoed if asked). Even on trivial tasks, where the overhead feels unnecessary, the downside of *not* planning dominates the tiny overhead of reading.

Conclusion for this project: **plan mode stays always-on.** I'd rather pay the overhead tax on trivial tasks than re-learn the "I should have checked" lesson on a non-trivial one. The asymmetry is the point.

---

### [2026-04-18 14:18 ART] Note — Insight

Companion to the previous note, on the *other* dial: **auto-accept.** It's a velocity multiplier for repetitive mechanical work (scaffolding, edits across many files, test boilerplate) and a liability for architecture work, where I actually want to see the plan and the concrete file changes before anything lands.

Working rule I'm settling on:
- **Default: auto-accept ON + plan mode ON.** Plan mode is the contingency net — I review intent before execution, then let the mechanical work fly.
- **Auto-accept OFF for high-blast-radius changes:** `CLAUDE.md`, `docker-compose.yml`, DB migrations, structural refactors, anything that silently rewires how future turns behave. For these, I want a per-tool confirmation, not just a plan review.

The two dials aren't redundant: plan mode gates the *strategy*, auto-accept gates the *execution*. Turning the right one off at the right moment is the actual skill.

---

### [2026-04-18 14:26 ART] Note — Friction

Workspace organization note: I'm running Claude Code in **two surfaces simultaneously** inside VS Code — the integrated terminal CLI *and* the Claude Code extension panel. Two reasons:

1. **Parallelism.** I can have the CLI chewing on a feature implementation while I use the extension for side work like these journal notes, without stepping on the same session.
2. **Input ergonomics.** The CLI's text input is genuinely painful for anything more than a short prompt — no real multi-line editing, no easy paste-and-edit, awkward cursor behavior. For composing longer messages (journal notes, detailed directions, pasted specs) the extension's editor-backed input is dramatically better.

Worth flagging as a process observation: the "one session, one terminal" mental model doesn't match how I actually work. Running two surfaces against the same repo has been the pragmatic answer, with the extension effectively acting as the "writing desk" and the CLI as the "workbench."

---

### [2026-04-18 14:34 ART] Note — Insight

`/process-task` output was, frankly, **incredible** — the single biggest "this is why ADLC works" moment so far.

- **~5 minutes wall clock** from invocation to a structured `docs/requirements.md` + `docs/stories.md` pair. No human on Earth produces that volume at that fidelity in that window.
- **Coverage:** 53 FRs + 16 NFRs + 4 EXT items, all with traceable IDs cross-referencing the task sections. Organized by domain, not by order-of-appearance in the source.
- **Discipline I didn't prompt for explicitly:** an explicit **Out of Scope** section (including the task's own waivers like XMPP federation being optional), and a **Open Questions** section flagging genuine ambiguities that deserved human judgement rather than being silently decided.
- **Review cost:** I read the full output and could not find anything material to change. That's the part that surprised me — usually AI output is a *starting point* you edit. Here it was closer to a *finished artifact* you sign off on.

The leverage ratio (input prompt → structured spec) on this command is the strongest argument I have for investing in good slash commands up front. The command file is a couple hundred lines of guidance; it produced a spec I'd normally spend half a day on.

---

### [2026-04-18 14:48 ART] Note — Meta

Process observation: I'm running a **three-party loop** and it's become the dominant shape of my workflow, well before any code generation has started.

- **claude.ai session** — holds the hackathon's *general context*. This is where high-level thinking, architecture debates, approach comparisons, and (especially) **methodology** for the tooling itself get worked out. It's also, quietly, the main author of the prompts I then hand to Claude Code.
- **Claude Code (CLI + extension)** — the *executor*. It consumes specs/prompts (often drafted in claude.ai), produces plans and artifacts, and operates directly on the repo.
- **Me** — the reviewer in the middle, ferrying artifacts back and forth, catching anything that looks off, but more often asking questions to *understand* what was produced rather than to correct it.

The loop in practice:
1. Think-aloud with claude.ai → get a well-shaped brief/prompt.
2. Feed brief to Claude Code → get plan + artifacts.
3. Review the Claude Code output, flag anything surprising.
4. Paste Claude Code's output back into claude.ai → ask "what does this actually do? is this what I want?"
5. claude.ai explains / critiques / refines → loop back to step 2.

Important: this is **still pre-code**. The loop is running on task generation, command design, and process setup.

The open question I'm sitting with: how much of step 4 (round-tripping Claude Code output through claude.ai to "understand" it) is genuinely necessary versus a comfort habit? Probably not one answer — for high-blast-radius artifacts (CLAUDE.md, architecture choices) the round-trip seems worth it; for mechanical output it's probably overhead I could drop. Worth revisiting once coding starts and the artifact volume jumps.

---

### [2026-04-18 14:55 ART] Note — Insight

Concrete evidence for the previous note: in reviewing a Claude Code plan built from a spec that claude.ai had largely drafted, **Claude Code flagged important missing details in the spec itself.** I caught it while reading the plan (plan mode earning its keep, again). When I took that finding back to claude.ai, claude.ai acknowledged the omission as its own error.

Two takeaways worth logging:

1. **Careful human review is still necessary — even when both ends of the loop are the same underlying model family.** "Same LLM on both sides" does not mean the inputs are self-consistent. Different sessions, different context windows, different framings produce different blind spots. Treating claude.ai output as pre-vetted just because "it's Claude" is wrong.
2. **A strange but productive iteration emerges: claude.ai ↔ Claude Code as mutual critics.** Claude Code caught what claude.ai missed; claude.ai, shown the finding, corrected itself cleanly. I'm effectively using the two surfaces to cross-check each other, with me as the courier. Not a workflow I'd have predicted, but it's been useful.

Caveat for future-me: this probably reflects my current tool-usage skill level. A more experienced operator might structure prompts well enough that these gaps don't appear in the first place. For now, the cross-check is load-bearing.

---

### [2026-04-18 15:04 ART] Note — Meta

Amusing recursion: even `/journal-note` — the command whose entire purpose is capturing the process — had to be reviewed, revised, and iterated on before I trusted it. The tool built to document the work was itself work.

Not a complaint, just worth naming:

- **Authoring slash commands is slow, and that's okay.** They're reusable, and once they're right the payback compounds across every future invocation. The front-loaded cost is the investment; the per-use cost is near zero.
- **"Right" is slippery for soft/process commands.** `/journal-note` has no tests, no deterministic "does it compile" gate. What makes it *good* is whether the entries it produces are useful to a reader months from now — that's a judgement call, not a pass/fail. So iteration is more about calibration ("does this category scheme catch the things I actually want to record?") than about correctness.
- **Contrast with technical commands:** `/process-task` or `/add-feature` have clearer success signals (spec produced, tests green, feature works). `/journal-note`, `/checkpoint`'s narrative portion, and CLAUDE.md live in the fuzzier category where "good" is an editorial judgement.

Lesson for the rest of the hackathon and beyond: budget real time for the fuzzy commands up front, don't expect them to feel "done" as quickly as the technical ones, and accept that their value compounds silently over many future uses.

---

### [2026-04-18 15:12 ART] Note — Friction

Honest self-observation: I'm **struggling with control.** I opt in to auto-everything, then catch myself scrutinizing every generated plan, second-guessing edits to `CLAUDE.md` that I'd explicitly authorized, and generally behaving as if I hadn't just delegated the thing I delegated.

This is a *me* friction, not a tool friction. The tool is doing exactly what I told it to; I'm the one who hasn't reconciled "delegate" with actually letting go. A few patterns I notice:

- Auto-accept ON, then reading every diff as if I'd asked for a PR review. That's plan-mode behavior dressed up as auto-mode.
- Authorizing `CLAUDE.md` self-updates, then pushing back on the self-update because it *feels* too autonomous even though it's within the scope I granted.
- Treating every surprise as something to audit, instead of choosing which classes of surprise I actually care about.

What I think this is really about: I haven't decided, per category of change, **where on the autonomy dial I actually want to sit.** Without that decision, I default to "let it run *and* watch everything", which is the worst of both — I pay the autonomy's blast-radius risk *and* the manual review's time cost.

Action item (not a fix, just a direction): next time I flip a dial, write down — even one line — *what I'm agreeing to stop checking.* Otherwise "auto" is a lie I'm telling myself.

---

### [2026-04-18 18:19 ART] Note — Meta

A lot of my wall clock is going into *reading* the agent's output — both the streaming Claude Code narration and the final diffs — rather than steering or correcting it. Most of that reading is technically skippable if I were willing to trade oversight for speed, and in some cases that trade is probably fine. Right now I'm deliberately keeping it because the reading is doing double duty: it's how I learn what the agent is actually doing under the hood, and occasionally it catches something that needs a redirect before it compounds.

Worth noting, though: the time I spend *correcting* input/output is minimal so far. That's the interesting signal. If reading is mostly for comprehension and rarely for intervention, then at some point the honest move is to stop reading and trust more — same tension as the autonomy-dial note from 15:12. Parking it for now; I'll keep tracking the ratio of (read-to-understand) vs (read-to-correct) and revisit once I have enough data to know whether the oversight is earning its cost.

---

### [2026-04-18 21:48 ART] Note — Decision

Consolidated the initial migration rather than shipping an additive `AddUniqueIndexes` migration on top of it. Acceptable here because no production environment has the first migration applied yet — the schema has never been deployed anywhere persistent, so there is no deployed history to preserve.

This pattern (remove a migration and re-add a modified version) **must not** be used once the schema has been deployed anywhere persistent. After that point, the only safe path is an additive forward migration; rewriting prior migrations retroactively breaks environments that have already recorded them in `__EFMigrationsHistory`.

Noted for future reference — if we hit a second migration, promote this rule into `CLAUDE.md` so the agent stops reaching for the "just collapse them" shortcut once the schema goes live.

---

### [2026-04-18 21:49 ART] Note — Decision

Story 1.3 enforces user uniqueness **case-sensitively at the DB level by design**. Case-insensitive semantics (so that `Juan@x.com` and `juan@x.com` are treated as the same identity) are deferred to the Story 1.4 service layer, where registration/login normalize inputs before hitting the DB.

**Risk:** if normalization is inconsistent across endpoints — e.g., registration lowercases but another write path doesn't — two rows differing only in case could coexist, since the DB constraint won't catch them. This is a latent data-integrity bug that stays invisible until someone tries to log in with the "other" casing.

**Mitigation plan:** Story 1.4 tests must include an explicit case-conflict assertion: register `Juan@x.com`, then attempt to register `juan@x.com` — expect a conflict response. Same shape of test for username. This pins the normalization contract at the service boundary and will fail loudly if any future endpoint skips the normalization step.

---

## [2026-04-18 18:52 ART] — User/Session schema + initial EF migration

**Story:** Story 1.3 — User & Session data model + initial EF migration
**Commit:** `59248cb` — feat(api,db): add User/Session schema with initial migration

### What was built
The persistence foundation for auth. Two POCO entities (`User`, `Session`) with fluent configuration in `AppDbContext` — unique indexes on `Email` and `Username`, a cascade FK from `Session.UserId` to `User.Id`, and sensible column length caps (email 320, username 32, password hash 512, UA 512, IP 45). A local `dotnet-ef 10.0.0` tool manifest and a `DesignTimeDbContextFactory` make `dotnet ef migrations add …` reproducible without global tool or env-var setup. The generated `InitialSchema` migration auto-applies on API startup when `app.Environment.IsDevelopment()` — the compose file already sets that, so the grader's first `docker compose up` creates the schema. A `Testcontainers.PostgreSql` class fixture runs the migration once per test class and exposes a `CreateContext()` factory, and three xUnit v3 tests prove round-trip, duplicate-email rejection, and duplicate-username rejection via real Npgsql SQLSTATE 23505.

### ADLC traceability
- **Requirements satisfied:** FR-1 (registration model fields), FR-2 (unique email — unique index), FR-3 (unique, immutable username — unique index; immutability is a service-layer contract for Story 1.4), FR-9 (PasswordHash column sized for Argon2id/Identity output), FR-15 (Session fields for the future sessions screen). NFR-16 is extended via Serilog lines logging start and end of the migration step on startup.
- **AC status:** all 5 acceptance criteria in `docs/stories.md` §Story 1.3 now `[x]`. `**Status:** Done (commit 59248cb)` appended.
- **No reinterpretation.** AC wording called for "a round-trip test"; I delivered three tests (round-trip + two unique-constraint tests), flagged on the plan file before approval. No silent expansion.

### Non-obvious decisions
- **Decision:** Spin up real Postgres via `Testcontainers.PostgreSql` for integration tests instead of `Microsoft.EntityFrameworkCore.InMemory` or the SQLite in-memory provider.
  **Alternatives considered:** EF Core InMemory, SQLite in-memory, or connecting to the docker-compose Postgres.
  **Why:** InMemory and SQLite don't honor Postgres unique-index semantics the same way (InMemory uses `Dictionary` semantics; SQLite's collation is different), so a broken constraint could pass silently. Sharing the compose DB would couple tests to an out-of-band process. Testcontainers gives fidelity at ~3s/class overhead — the cost proved worth it within minutes.
- **Decision:** Single consolidated `InitialSchema` migration rather than shipping an additive follow-up once the unique indexes were added.
  **Alternatives considered:** generate the basic schema first, then a second migration `AddUniqueIndexes`.
  **Why:** nothing is deployed anywhere persistent yet, so squashing is safe and produces cleaner history for graders reading the repo. Explicitly noted (earlier in this journal at 21:48) that this is a one-time allowance — once the schema is live, the only safe path is additive migrations.
- **Decision:** Case-sensitive uniqueness at the DB layer; case-insensitive normalization deferred to the service layer (Story 1.4).
  **Alternatives considered:** `citext` column type, or a computed unique index on `lower(Email)`.
  **Why:** citext is a Postgres extension that would need to be enabled in the `db` image init scripts; `lower()` indexes work but require raw SQL in the migration. Service-layer normalization is simpler and — as flagged in the earlier 21:49 note — also already requires a Story 1.4 test to prove the contract holds. Accepted risk documented there.
- **Decision:** Pin `dotnet-ef` in a local tool manifest (`.config/dotnet-tools.json`) rather than assuming a global install.
  **Alternatives considered:** assume `dotnet ef` is globally installed on contributor machines; or bake it into the `api` Dockerfile's build stage.
  **Why:** the tool manifest is self-contained — `dotnet tool restore` makes `dotnet ef` work for any cloner on day one, and pinning the version to `10.0.0` matches `Microsoft.EntityFrameworkCore 10.0.0` in `Api.csproj` so there's no scaffold/runtime mismatch.
- **Decision:** `Guid` primary keys for both entities.
  **Alternatives considered:** `int` identity columns.
  **Why:** `Session.Id` doubles as the opaque session token in Story 1.5; `int` would leak creation order and allow enumeration. `Guid` also aligns with the "int or UUID" latitude CLAUDE.md §3 already grants for room IDs, so it's a consistent project-wide default.

### Friction and blockers
- **Test data hit the username length constraint.** First test run failed with `value too long for type character varying(32)` because I seeded usernames as `alice-{Guid.ToString("N")}` — that's 38 characters, two chars over the 32 cap I'd just defined. The good news: the test caught my own schema decision doing exactly what it's supposed to do. Fixed by trimming the suffix to 8 chars. Lesson: fit your test fixtures to your constraints, don't assume a random Guid "just works" as a username.
- **xUnit v3 analyzer complaints.** The `xUnit1051` rule flags any async DB call that isn't passed `TestContext.Current.CancellationToken`. Trivial fix — one-line add per call — but worth noting for future tests so I don't introduce the warning again. This is v3-specific; the project is on `xunit.v3 1.0.0`.
- **No real blockers.** The plan file set up during plan-mode covered everything, and execution was mostly mechanical. If there was drift it was spending ~5 minutes second-guessing whether to name the migration `InitialSchema` vs `AuthBaseline` — settled on `InitialSchema` because it matches the AC wording and there's no reason to invent a synonym.

### Verification evidence
- Tests: **5 passing** (4 backend: 1 sanity + 3 PersistenceTests; 1 frontend: App.test).
- Build: ✅ `dotnet build DataArtHackaton.slnx` → 0 warnings, 0 errors.
- `docker compose up`: ✅ — teardown with `-v` + fresh build, all three services healthy in <15s.
- End-to-end check:
  - `curl http://localhost:8080/health` → `200 {"status":"healthy","database":"up"}`.
  - `docker exec hackaton-db psql -c "\dt"` lists `Users`, `Sessions`, `__EFMigrationsHistory`.
  - `\d "Users"` shows both unique indexes and the `ON DELETE CASCADE` FK inbound from Sessions.
  - `docker logs hackaton-api` contains `Applying migration '20260418214803_InitialSchema'.` followed by `EF Core migrations applied.` — so on a fresh grader environment the schema materializes before the first request lands.
  - Playwright GET `/` → React app loads, fires `GET /health` → 200. UI path unchanged but proven intact.

### Reflection
The plan-mode discipline paid off again: every design decision was logged on the plan file before any code was written, so execution was uninterrupted by "wait, should I use citext instead?" The only surprise was self-inflicted (test data vs column length), and the test suite caught it in under a minute — a lived example of why Testcontainers beats in-memory providers. Pattern to keep reusing: **one `PostgresFixture` per test class, migrations run once per container**; Story 1.4 (register/login) should build on the same fixture without changes. If there's a lesson for later stories it's to always ask "does my test fixture respect the invariants I'm about to declare?" before writing the assertions — had I seeded data first and constrained second, this would have been seamless.

### Cost so far (rough)
- Wall clock for Story 1.3 end-to-end (plan approval → commit): ~25 minutes. Breakdown roughly: 2 min tool manifest, 4 min entities + DbContext + DesignTimeFactory, 6 min fixtures + tests, 2 min generate migration, 1 min test failure diagnosis + fix, 4 min auto-apply + full compose teardown/rebuild, 3 min DB/psql inspection + Playwright smoke, 3 min commit + doc updates.
- Story was estimated at 2 points (≈1 hour); actual was inside that.

### Next
- **Story 1.4 — Register & login endpoints (REST).** Schema is now in place; next is the first real user-facing feature (POST `/api/auth/register`, POST `/api/auth/login`). Prerequisites resolved: Decision §5 (password policy: ≥8 chars, ≥1 letter + 1 digit, server-side), Decision §6 (rate-limiting out of scope — add README note). Open risk already flagged at 21:49: Story 1.4 tests MUST include the case-conflict assertion (register `Juan@x.com` then `juan@x.com` → 409) to pin the normalization contract that the DB is not enforcing.

---
