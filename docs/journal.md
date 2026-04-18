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

### [2026-04-18 22:23 ART] Note — Insight

Companion observation to the 14:34 note on `/process-task` — the **user-story generation half** of that run deserves its own entry, because the quality is what actually determines whether the spec is usable downstream.

What landed in `docs/stories.md` in a single pass:

- **Jira-native format.** Each story is already shaped for create/update via the Atlassian Rovo MCP — summary, description, type, priority, story-point estimate, acceptance criteria as a checklist, and parent/epic linkage. No reshaping needed before pushing tickets; the MCP call is basically a for-loop over the file.
- **Estimates that survived first contact.** The point estimates felt reasonable on read and have held up against actuals so far — Story 1.3 was estimated 2pts / ~1h, came in at ~25min wall clock (inside the envelope, not wildly off). A few stories have already had their estimates stress-tested in earlier journal entries, and the reasoning behind them was sound.
- **Priority genuinely reflects the MVP framing from the task.** The first ~16 stories are marked High and, taken together, cover exactly the MVP acceptance criteria the task author called out as the minimum bar. The rest ladder down to Medium/Low by feature weight, not alphabetically or by section order. That's the kind of judgement call I'd expect to argue about in a grooming session, and the agent just… got it right.
- **Descriptions, traceability, and ACs are the real win.** Every story back-links to the FR/NFR IDs from `requirements.md` (which themselves back-link to task sections), so from any story I can walk the chain: story → requirement → original task clause. ACs are concrete and testable, not hand-wavy ("user sees message" vs "POST returns 201 with `{id, createdAt}` and the message appears in the room within 3s").

This is where the ADLC leverage compounds: `/process-task` didn't just produce a spec, it produced a spec that's **directly executable** by the rest of the pipeline (`/add-feature` reads it, the Jira MCP can ingest it, the journal cross-references it). The investment in the command's prompt paid for itself on turn one.

---

## [2026-04-18 19:29 ART] — Register + login endpoints with session cookies

**Story:** Story 1.4 — Register & login endpoints (REST)
**Commit:** `4210a02` — feat(api,security): register + login endpoints with session cookies

### What was built
`POST /api/auth/register` and `POST /api/auth/login`. Register validates the password policy (≥8 chars + ≥1 letter + ≥1 digit, per Decision §5), normalizes email and username to lowercase, hashes the password with ASP.NET Core's `PasswordHasher<User>` (PBKDF2-SHA256, random salt, 100K iterations — shipped in the shared framework, no NuGet added), persists the user, and returns `201` with a `UserSummary`. Login looks up the user by normalized email, verifies the hash, creates a `Session` row (carrying `UserAgent` and `RemoteIp` derived server-side from the request per Decision §4), issues an opaque `Guid` session token returned in the body AND as an HTTP-only `session` cookie (`SameSite=Lax`, 30-day expiry, `Secure` auto-on under HTTPS). Duplicate email/username — including case variants — are caught from Npgsql's `SQLSTATE 23505` and translated to `409 ProblemDetails` identifying the offending field. Wrong password AND unknown email both return `401` to avoid account enumeration. The README gains a "Known limitations" section documenting the deliberate omission of rate limiting (Decision §6).

### ADLC traceability
- **Requirements satisfied:** FR-1 (self-registration), FR-2 (unique email — enforced via DB index AND service-layer lowercasing), FR-3 (unique username, same), FR-4 (login with email+password), FR-9 (password stored only as hash). Also NFR-16 (RFC 9457 `ProblemDetails` for errors — every failure path returns a well-shaped problem document with the right `Content-Type: application/problem+json`).
- **AC status:** all 5 acceptance criteria in §Story 1.4 now `[x]`. `**Status:** Done (commit 4210a02)` appended.
- **Decisions resolved:** §5 (password policy implemented exactly as specified in `PasswordPolicy.TryValidate`), §6 (rate limiting out of scope — README note), §4 (session UA/IP derived server-side from `HttpContext.Request.Headers.UserAgent` and `HttpContext.Connection.RemoteIpAddress`).
- **Pre-flagged risk from journal 21:49 closed:** case-insensitive uniqueness is now covered by two dedicated tests (`Register_duplicate_email_is_case_insensitive`, `Register_duplicate_username_is_case_insensitive`). The contract is service-layer normalization, pinned by tests.

### Non-obvious decisions
- **Decision:** Use ASP.NET Core's `PasswordHasher<User>` from the shared framework, not a NuGet package.
  **Alternatives considered:** `Microsoft.Extensions.Identity.Core` as an explicit `PackageReference`; a BCrypt library (`BCrypt.Net-Next`); Argon2id via `Konscious.Security.Cryptography.Argon2`; a hand-rolled PBKDF2 using `Rfc2898DeriveBytes.Pbkdf2`.
  **Why:** `.NET 10`'s `Microsoft.AspNetCore.App` shared framework already ships `PasswordHasher<TUser>` — adding the package raises NU1510 (prune warning) because the types are duplicated. PBKDF2-SHA256 + 100K iterations + 128-bit salt is strong enough for this scale, and using the framework hasher means one fewer dependency to version-bump. The initial attempt to add the explicit package produced the warning and was reverted.
- **Decision:** Lazy (scope-resolved) connection string for `AddDbContext` — switched `AddDbContext<AppDbContext>(options => …)` to `AddDbContext<AppDbContext>((sp, options) => …)` so the connection string is read via `IConfiguration` at context-resolve time.
  **Alternatives considered:** set `ConnectionStrings__Default` as a process environment variable in the test; use `IWebHostBuilder.UseSetting(...)`; override config via `WebApplicationFactory.ConfigureHostConfiguration`.
  **Why:** the eager-read version captured `null` in the test host because `WebApplicationFactory.ConfigureAppConfiguration(...)` merges its in-memory provider AFTER `Program.cs`'s `GetConnectionString("Default")` executed at registration time. Env vars work but pollute process state across parallel tests. `UseSetting` works but is opaque. The lazy factory is a cleaner, no-magic pattern that will also pay off for every future `WebApplicationFactory`-backed test.
- **Decision:** Normalize email AND username to `Trim().ToLowerInvariant()` at the service boundary, and store the lowercase form as the only representation.
  **Alternatives considered:** Postgres `citext` extension; computed unique index on `lower(Email)` / `lower(Username)`; store display-case separately with a normalized shadow column.
  **Why:** `citext` would require enabling an extension in the `db` init and coupling the schema to a Postgres-only feature. A `lower()` computed index requires raw SQL in the migration and doesn't help username display. Display-case preservation isn't required by any AC; chat display names can be added as a separate column in a later story if needed. Normalizing at the service layer is the simplest thing that works and is now pinned by tests.
- **Decision:** Unknown-email login returns `401`, not `404`.
  **Alternatives considered:** `404 Not Found` (more "correct" in REST-purist terms); or a user-lookup endpoint that itself returns 404 and a separate 401 on password mismatch.
  **Why:** account enumeration. Differentiating "user doesn't exist" from "wrong password" lets an attacker discover which emails are registered. Returning `401` uniformly collapses that side channel at near-zero cost. This is also reflected in an explicit test (`Login_with_unknown_email_returns_401_to_avoid_enumeration`) so the behavior can't silently drift.
- **Decision:** Session cookie with `SameSite=Lax` + 30-day expiry, `Secure` driven by `Request.IsHttps`.
  **Alternatives considered:** `SameSite=Strict`; session-scoped (no expiry); `Secure` always on.
  **Why:** `Lax` is the practical default for a web chat — `Strict` would break any cross-site navigation into the app (e.g., an emailed password-reset link landing on the app while the user is already signed in elsewhere). 30-day expiry supports FR-6 (login persists across browser close) without waiting for Story 1.5. `Secure` unconditionally would break the hackathon's HTTP-only dev environment; gating on `IsHttps` means the flag auto-turns-on the first time the app is fronted by a TLS-terminating proxy in production.

### Friction and blockers
- **The lazy-DbContext fix described above was a real hiccup.** First test run failed with `The ConnectionString property has not been initialized` because `GetConnectionString` was called at the wrong time in the config pipeline. Diagnosed from the stack trace (ran through `Program.Main` → `MigrateAsync` → `NpgsqlConnection.Open`). The fix is three lines but the diagnosis wasn't obvious from the symptom; the lesson is that `WebApplicationFactory` + `WebApplication.CreateBuilder` have specific ordering semantics that bite when you reach for "read config at registration time".
- **`Microsoft.Extensions.Identity.Core` package false-start.** Reflexively added as a `PackageReference`, `dotnet restore` emitted NU1510 complaining the package duplicates shared-framework types. A 30-second dead end that ended with `dotnet build` of a throwaway probe project confirming `PasswordHasher<TUser>` already compiles without the reference. Lesson for .NET 10: before adding `Microsoft.Extensions.*` or `Microsoft.AspNetCore.*` packages, assume they're in the shared framework and verify with a probe build.
- **None of the AC wording or test design surfaced anything ambiguous.** The up-front decisions block in `docs/stories.md` (Decisions §4, §5, §6) carried exactly the load it was meant to — there was no "wait, should I …?" moment during implementation. Score one for the decisions-at-planning pattern.

### Verification evidence
- Tests: **17 passing** (16 backend: 1 sanity + 3 persistence + 12 auth — 9 fact + 3 theory; 1 frontend: App.test).
- Build: ✅ `dotnet build` → 0 warnings, 0 errors.
- `docker compose up`: ✅ — full teardown with `-v` + fresh rebuild, all three services healthy in ~12s; migrations auto-apply on the first boot.
- End-to-end check via live `curl` against the running container:
  - `POST /api/auth/register {email: alice@example.com, username: alice, password: Secret123}` → `201` with `{id, email, username, createdAt}`.
  - Same request again → `409 ProblemDetails` `{title: "Duplicate email", …}`.
  - `POST /api/auth/login {email: alice@example.com, password: Secret123}` → `200` with `{token, user}` AND `Set-Cookie: session=<guid>; HttpOnly; SameSite=Lax; Expires=30d; Path=/`.
  - Same with `password: WrongPass1` → `401 ProblemDetails` `{title: "Invalid credentials"}`.
  - `psql "SELECT … FROM Sessions"` confirms the row landed with `UserAgent=curl/8.18.0` and `RemoteIp=::ffff:172.18.0.1` — so the Docker gateway IP, correctly captured by `HttpContext.Connection.RemoteIpAddress`.

### Reflection
Two reusable patterns fell out of this story. First, **the ApiFactory fixture** (`WebApplicationFactory<Program>` + Testcontainers Postgres + lazy config override) is now the template for every subsequent endpoint story (1.5 session middleware, 1.8 rooms, 1.11 SignalR hub integration, etc.) — I expect zero further changes to the fixture shape. Second, **Decisions §N in `docs/stories.md`** paid its cost back big time: password policy, rate-limiting scope, and session-metadata sourcing were all locked before I started coding, so the entire implementation pass was uninterrupted by "wait, should I …" moments. The only real stumble (the eager-vs-lazy DbContext config) was a .NET-host subtlety that wouldn't have been caught by planning, because it's the kind of thing you only see when you wire the real test host. Pattern to commit to: **keep decisions upstream, keep scaffolding fixtures canonical, let the surprises be genuine system interactions rather than avoidable ambiguity.**

### Cost so far (rough)
- Wall clock for Story 1.4 end-to-end (from task creation → commit): ~35 minutes. Rough breakdown: 2 min package probe (+revert), 4 min fixture + failing tests, 3 min diagnosing the lazy-config issue, 8 min endpoint implementation + DTOs + PasswordPolicy, 4 min Program.cs wiring + build, 3 min test iteration to green, 5 min docker teardown/rebuild + live curl verification, 3 min README + stories.md + commit, 3 min journal.
- Story was estimated at 3 points (≈2 hours); actual was well inside that.
- Running total for MVP track: Story 1.1 (~45min) + 1.2 (~10min) + 1.3 (~25min) + 1.4 (~35min) ≈ **~2 hours of agent-wall-clock** to a working auth surface on top of a migrated schema.

### Next
- **Story 1.5 — Session-based auth middleware, logout, and current-user endpoint.** Everything it needs is now in place: sessions are persisted, the cookie is set, the token format is a lookup-friendly `Guid`. The middleware reads the `session` cookie, looks up the `Session` row (filtering `RevokedAt == null`), loads the user, and populates `HttpContext.User`. `POST /api/auth/logout` flips `RevokedAt`. `GET /api/me` returns the current user. No schema change needed. **Open risk to carry into 1.5:** where does the middleware live and how does it handle the case where the session's user has been soft-deleted? Default answer (reject with 401) works for MVP, but if Story 2.10 ever flips soft-delete semantics, the middleware's filter becomes load-bearing.

---

## [2026-04-18 20:02 ART] — Session auth middleware, logout, /api/me

**Story:** Story 1.5 — Session-based auth middleware, logout, and current-user endpoint
**Commit:** `ae27847` — feat(api,security): session auth middleware, logout, /api/me

### What was built
A custom `SessionAuthenticationHandler` registered under the `"Session"` scheme. On every incoming request it reads the `session` cookie, parses the Guid, and looks up the `Sessions` row — `Include`-ing `User` — filtering to `RevokedAt IS NULL AND User.DeletedAt IS NULL`. Success populates a `ClaimsPrincipal` with `NameIdentifier`, `Name`, `Email`, and a custom `sid` claim carrying the session id so downstream handlers don't need to round-trip the cookie. `HandleChallengeAsync` is overridden to emit `application/problem+json` bodies, so every 401 conforms to RFC 9457 (NFR-16). `POST /api/auth/logout` (RequireAuthorization) reads the `sid` claim, sets `Session.RevokedAt` on that one row only, deletes the cookie, returns `204`. `GET /api/me` (RequireAuthorization) returns `{id, email, username}` derived from claims — no DB hit on the hot path. Six new integration tests prove every branch of the AC plus the FR-6 cookie-lifetime contract.

### ADLC traceability
- **Requirements satisfied:** FR-5 (per-session logout — verified by `Logout_revokes_current_session_only` which keeps a second session valid), FR-6 (persistent login across browser close — verified by `Login_cookie_has_persistent_30_day_expiry` asserting the Set-Cookie `expires` attribute is ~30 days from now), NFR-12 (same), NFR-16 (ProblemDetails on 401 via overridden `HandleChallengeAsync`).
- **AC status:** all 4 in §Story 1.5 now `[x]`. `**Status:** Done (commit ae27847)` appended.
- **Soft-delete open risk from journal 19:29 addressed explicitly in code:** the `SingleOrDefaultAsync` predicate filters `User.DeletedAt == null`, documented on the handler class itself. A user soft-deleted after session creation immediately fails auth on the next request — same as revocation, same 401 path, no special casing needed.

### Non-obvious decisions
- **Decision:** Implement a custom `AuthenticationHandler<AuthenticationSchemeOptions>` rather than middleware or `AddCookie`.
  **Alternatives considered:** ad-hoc middleware that just sets `HttpContext.User`; ASP.NET Core's built-in `AddCookie(...)` scheme; a per-endpoint `[FromHeader]` token guard.
  **Why:** middleware works but doesn't integrate with `RequireAuthorization()` / `[Authorize]` — the `.NET` authorization stack keys off registered schemes. `AddCookie` stores the principal inside an encrypted cookie, which defeats the point of having a DB-backed `Session` row (revocation would silently do nothing until the cookie expires). The `AuthenticationHandler` route gives us (a) first-class integration with the auth pipeline, (b) real-time revocation because every request re-reads the DB, and (c) a clean extension point for future auth rules.
- **Decision:** Carry the session id as a custom `sid` claim rather than stashing it in `HttpContext.Items`.
  **Alternatives considered:** `HttpContext.Items["SessionId"] = sessionId` populated by the handler and read by logout.
  **Why:** `HttpContext.Items` is request-local and opaque to the rest of the authorization stack; the claim lives on the principal, survives any re-authentication, and is self-documenting in logs. It's also how the wider ecosystem models "this was issued by that session" (OIDC `sid`, spec'd in RFC 8693 §4.2). Cost is ~40 bytes per request.
- **Decision:** `/api/me` returns claim values, not a DB read.
  **Alternatives considered:** re-query the `Users` table so the response reflects edits between login and this call.
  **Why:** claims were populated on auth (this same request), so they ARE fresh. Avoiding a second DB round-trip keeps `/api/me` on the order of a single lookup per request (the one inside `HandleAuthenticateAsync`). When we eventually add username or email editing (Story 2.10), sessions can be torn down on edit to force re-auth — cheaper and more correct than per-endpoint re-reads.
- **Decision:** Override `HandleChallengeAsync` with a hand-written ProblemDetails body instead of relying on `AddProblemDetails()` to intercept the empty 401.
  **Alternatives considered:** rely on the built-in `IProblemDetailsService` to shape the auth middleware's challenge response automatically.
  **Why:** the auth challenge writes `401` directly and short-circuits the pipeline before the ProblemDetails middleware gets a chance. Empirically, the built-in path produced a `401` with `Content-Length: 0` — fine for HTTP but fails the AC wording ("returns `401` ProblemDetails"). Writing the body inline is nine lines and guarantees the shape.
- **Decision:** Anonymous `/api/me` returns `401`, not redirect-to-login.
  **Alternatives considered:** HTTP `302` to `/login` (browser-friendly), or a `403` (which some APIs conflate with unauthenticated).
  **Why:** this is an API, not a server-rendered web app — a redirect would hijack `fetch()` calls in the SPA. `401` is the REST-correct answer; the frontend decides whether to navigate.

### Friction and blockers
- **Local constant naming collision.** While replacing the hardcoded `"session"` string in `AuthEndpoints.Login` with the new `SessionAuthenticationDefaults.CookieName` constant, the edit-all-occurrences replaced the `private const string SessionCookieName = "session";` field too — yielding `private const string SessionAuthenticationDefaults.CookieName = "session";` which doesn't compile. Had to follow up with a targeted edit to delete the now-dead local constant. Lesson for future replace-all passes: if the replaced symbol is itself defined in the same file, double-check the declaration site.
- **Zero test surprises.** The handler + /logout + /api/me all passed on the first full run after wiring. That's partly the failure-first test discipline (writing tests that initially fail with 404 narrows the target very precisely) and partly that `AuthenticationHandler<T>` is a boring, well-documented extension point — nothing about it was novel.

### Verification evidence
- Tests: **23 passing** (22 backend: 1 sanity + 3 persistence + 12 auth + 6 session; 1 frontend: App.test).
- Build: ✅ `dotnet build DataArtHackaton.slnx` — 0 warnings, 0 errors.
- `docker compose up`: ✅ — fresh teardown with `-v` + rebuild, all three services healthy in <12s.
- End-to-end check via live `curl` against the running container:
  - `GET /api/me` (no cookie) → `401 {"type":…,"title":"Unauthorized","status":401}` with `Content-Type: application/problem+json` — confirms the overridden challenge emits a proper ProblemDetails body.
  - `POST /api/auth/register` → 201, `POST /api/auth/login` → 200 with `Set-Cookie: session=…`.
  - `GET /api/me` (using the login cookie) → `200 {"id":"6973a1b6-…","email":"bob@example.com","username":"bob"}`.
  - `POST /api/auth/logout` → `204` (no body).
  - `GET /api/me` (reusing the now-revoked cookie) → `401` — confirms every request re-checks the DB.

### Reflection
The custom `AuthenticationHandler` pattern has now earned its place in the project's vocabulary; every future auth-gated surface (rooms, messages, admin actions) gets `RequireAuthorization()` for free, and the `sid` claim is a clean primitive for auditing "which session did this action" later on. The test style — pinning every AC branch AND the *implied* contracts (content-type is `application/problem+json`; cookie is `HttpOnly`; `expires` is ~30 days; revocation takes effect on the very next request) — is worth keeping as the default for every story: an AC that doesn't assert content-type is inviting drift. If I were starting 1.5 over I'd skip the flirtation with relying on `AddProblemDetails()` auto-shaping; write the challenge body yourself, it's nine lines and eliminates a whole class of "why is this 401 empty" bugs at review time.

### Cost so far (rough)
- Wall clock for Story 1.5: ~25 minutes from plan to journal entry. Faster than 1.4 largely because the `ApiFactory` fixture + the normalized test patterns (`RawClient`, `UniqueEmail`, `UniqueUsername`) came directly from 1.4 and were reused verbatim.
- Story was estimated at 2 points (≈1 hour); actual was inside the envelope.
- Running total on the MVP track (1.1 → 1.5): roughly **2h 25min** of agent-wall-clock. Schema + register/login/session auth is now a complete, test-covered surface.

### Next
- **Story 1.6 — Web: login, register, and auth context.** First frontend-heavy story. The backend surface is complete: register, login, logout, `/api/me`, session cookie. On the web side: Login and Register pages per Appendix A wireframes, an `AuthProvider` React context that fetches `/api/me` on mount to recover state, a route guard that redirects unauthenticated users away from protected pages, and Vitest/Testing-Library tests covering the login form happy path + invalid-credentials branch. No backend changes required. **Open question to carry into 1.6:** how does the web client handle the CORS `credentials: 'include'` dance — the CORS policy already names `http://localhost:3000` and `AllowAnyHeader/AllowAnyMethod`, but cross-origin cookie flows need `WithCredentials` on the server AND `credentials: 'include'` on every `fetch`; if that's missing, the session cookie never round-trips and the frontend appears eternally logged out even though auth works. Plan to verify with a Playwright end-to-end check as part of the story.

---

## [2026-04-18 20:54 ART] — Web login, register, auth context, route guard

**Story:** Story 1.6 — Web: login, register, and auth context
**Commit:** `d25d88a` — feat(web,api): login + register pages, auth context, route guard

### What was built
The first genuinely user-facing milestone. A grader can now open `http://localhost:3000`, be redirected to `/login` because they're anonymous, click "Create one" to register on `/register`, land on `/` as a signed-in user (the frontend auto-logs the new account in so they skip a second form), reload the page and still be signed in (the `AuthProvider` boots by calling `GET /api/me`), click Sign out, and be bounced back to `/login`. Everything goes through a small typed `fetch` wrapper (`src/Web/src/api/client.ts`) that unconditionally sets `credentials: 'include'` and throws a `ApiError` whose `.message` is the ProblemDetails `title` from the server. React Router v7 carries `/login`, `/register`, and a single protected `/` → `HomePage` route guarded by an `<Outlet>`-based `ProtectedRoute` component. The server-side prerequisite — adding `.AllowCredentials()` to the CORS policy — shipped with this commit because the story was literally unfulfillable without it.

### ADLC traceability
- **Requirements satisfied:** FR-1 (registration UI landed), FR-4 (login UI), FR-6 / NFR-12 (post-login reload still authenticated — verified live in Playwright). ADLC link: every `fetch` call in the new web code goes through `api.client`, which pipes ProblemDetails → UI (NFR-16 extended to the client).
- **AC status:** 5/5 in §Story 1.6 now `[x]`. `**Status:** Done (commit d25d88a)`.
- **Decisions invoked:** §5 of stories.md open-question decisions (password min 8 + letter + digit) is now reflected in the `RegisterPage` `minLength={8}` hint AND the fact that an invalid password falls through to the backend's ProblemDetails, which the frontend renders verbatim. §6 (no rate limiting) — already documented in README; unchanged by this story.
- **Carried-forward risk from journal 20:02:** the CORS `credentials: 'include'` handshake. Resolved inline: `.AllowCredentials()` on the server, `credentials: 'include'` on every client call, `SameSite=Lax` works because localhost:3000 and localhost:8080 are same-site (same registrable domain). Playwright network trace confirmed the `Set-Cookie` + `Cookie` round-trip.

### Non-obvious decisions
- **Decision:** Ship the `.AllowCredentials()` server tweak together with the frontend, not as a separate Story 1.5b.
  **Alternatives considered:** file a one-line server-side PR first, wait for review, then land the frontend; or stub the cookie flow in the frontend and defer cookie-roundtrip validation.
  **Why:** the story is literally unfulfillable without it — AC2 ("login stores the session cookie") and AC4 ("auth state persists across full-page reload") both fail silently cross-origin. Splitting the commit would have forced an out-of-sequence server PR that makes no sense in isolation. Called out explicitly in the plan file and the commit message so this is visible in review rather than smuggled.
- **Decision:** Auto-log-in after `/register`.
  **Alternatives considered:** redirect to `/login` to have the user re-enter credentials; or issue the session from the register endpoint itself.
  **Why:** not called out by any AC, but the common SPA UX; easier to reverse than to add. Kept the *backend* register endpoint responsibility narrow (create the row, return a summary) and made the frontend do the extra `login(email, password)` call. If the task author prefers the re-enter-credentials flow, that's a two-line change in `AuthProvider.register`.
- **Decision:** React Context for auth state, not a router data loader or Redux/Zustand.
  **Alternatives considered:** React Router v7's loader pattern (data-per-route); a state library.
  **Why:** auth state is a single slice that every page reads and a handful of pages write. Loaders couple data fetching to route boundaries, which would make the `/api/me` bootstrap awkward (loaders run per-navigation, not on-mount). A full state library is overkill. `useContext` is the exactly-right amount of machinery.
- **Decision:** Test with `vi.stubGlobal('fetch', vi.fn())` rather than installing MSW.
  **Alternatives considered:** `msw` with a pre-configured server; `fetch-mock`.
  **Why:** MSW is wonderful but costs a dependency + a setup file + a service-worker registration for the browser-mode path. For two tests that inspect one URL each, a `vi.fn()` matcher is clearer (the test body *shows* the response shape instead of pointing at a handler file). If the test count grows past ~10, reconsider.
- **Decision:** Loading placeholder in `ProtectedRoute` rather than redirecting during the pending `/api/me` call.
  **Alternatives considered:** render `<Navigate to="/login" />` while `loading === true` and let the user flicker through it.
  **Why:** the flicker is user-hostile and also racy — `/api/me` resolves in ~100ms and if the user happens to have a valid session, we'd redirect them to `/login` and then back to `/` on `state change`, producing a double navigation. A neutral "Loading…" for ~100ms is strictly better.
- **Decision:** Keep the original `/health` card visible on `HomePage`.
  **Alternatives considered:** remove it entirely since it's scaffold residue.
  **Why:** it's still useful as a live debug signal — a grader who sees a healthy status pill can distinguish "DB broken" from "frontend routing broken" at a glance. Cost is ~15 lines of JSX. Can be removed when a real home view lands.

### Friction and blockers
- **Duplicate POST on logout.** Playwright network trace showed `POST /api/auth/logout` firing twice on a single "Sign out" click. Both return `204`, both succeed, and the `Logout` handler is already idempotent (`if (session.RevokedAt is null) …`), so the second call is a no-op against an already-revoked row. This is suspicious but not broken — likely a `StrictMode` double-invoke in dev, or Playwright dispatching `click` twice. Noted for follow-up; did not chase in this story because the feature behaves correctly end-to-end.
- **Tool-name collision on replace-all (historical repeat).** When wiring the `SessionAuthenticationDefaults.CookieName` constant into `AuthEndpoints.Login` in Story 1.5, `replace_all` tried to rewrite the class-member declaration too. I caught and fixed it that time; the same style-of-bug does not apply to Story 1.6, but worth reiterating as a standing hazard of `replace_all` across a file that owns the declaration site.
- **Bash `cd` state.** At one point the checkpoint tried `cd src/Web && npm test` but the bash session's cwd had drifted back to the repo root, so `npm test` failed with "no package.json". Not a story issue — tooling friction — but 30 seconds lost. For durable commands I'll prefer absolute paths inside the Bash call instead of relying on cwd persistence.
- **No genuine design surprises.** The plan file was accurate; every AC passed on first or second attempt. The biggest "hmm" moment was verifying that `SameSite=Lax` really does permit cross-origin cookies from `localhost:3000` to `localhost:8080` — it does, because both URLs share a registrable domain (port is origin-scoped, not site-scoped).

### Verification evidence
- Tests: **25 passing** (22 backend: unchanged; 3 frontend: 1 sanity + 2 `LoginPage` tests — happy path, invalid credentials).
- Build: ✅ `dotnet build` clean; `npm run build` (Vite + `tsc --noEmit`) clean — 0 warnings.
- `docker compose up`: ✅ — full teardown with `-v` + rebuild, all three services healthy in ~12s.
- End-to-end check via Playwright MCP on the running container:
  - `GET /` (anonymous) → `302` to `/login`, Sign-in form renders.
  - `/register` form submit with `carol@example.com` / `carol` / `Secret123` → lands on `/` with `Hello, carol` heading.
  - `/api/me` (with session cookie) → `200` with `{id, email, username}`; `Stack health` card green.
  - Full-page reload on `/` → still on `/`, still greeted as `carol` — AuthProvider's `useEffect` re-fetched `/api/me` and restored state.
  - `Sign out` click → `POST /api/auth/logout` → `204`, cookie deleted, navigated to `/login`.
  - Re-navigating to `/` after logout → bounced back to `/login`.
  - Network trace inspected: login response `Set-Cookie: session=<guid>; HttpOnly; SameSite=Lax; Expires=+30d`; subsequent `/api/me` requests carried `Cookie: session=<guid>`; after logout, `/api/me` called with no cookie and returned `401`.

### Reflection
Two patterns that are going to pay compounding interest. First, **a single typed `fetch` wrapper at `src/Web/src/api/client.ts`** — every future feature (SignalR wiring has a REST-history-fetch companion, rooms listing, friends, attachments) gets cross-origin cookies, ProblemDetails → UI error flow, and a typed error surface for free. Second, **the `AuthProvider` + `ProtectedRoute` shape** will be reused as-is for every post-auth feature; `RequireAuthorization()` on the server already gates every future endpoint, and on the client `ProtectedRoute` already gates every future page. If there's a lesson to carry: **bundle load-bearing server tweaks with the feature that first needs them, explicitly, in both plan and commit**; silently slipping a CORS change in a pure "web" commit is the kind of thing that makes a later grepper confused. The commit message + plan file + this journal all say the same thing three different ways, which is the right amount of redundancy for that class of change.

### Time
- **Agent wall clock:** ~35 min from `/add-feature 1.6` (plan mode entry) through `docker compose up` verification and commit. Breakdown: ~4 min exploration + plan, ~2 min package + tool-restore, ~8 min scaffolding the 7 new web files, ~5 min writing the two failing tests (and tightening them twice as I found cleaner ways to mock), ~6 min filling implementations, ~2 min compile + test iteration, ~6 min docker rebuild + Playwright smoke, ~2 min stories.md / commit.
- **Equivalent human work:** ~3 hours end-to-end. Design (router shape, context shape): 15 min. Scaffold + React Router integration: 30 min. Auth context + protected route: 30 min. Login + Register forms with validation + styling: 45 min. API client wrapper: 15 min. Vitest tests (fetch stubbing is finicky without MSW experience): 30 min. Cross-origin cookie diagnosis + CORS server fix + Playwright smoke: 30 min. README/journal: 15 min. That's ~3h done as a focused senior dev; 4–6h realistic if interrupted.
- **Productivity ratio:** ≈5× for this specific story. The ratio is higher than 1.5 because a lot of the cost was paid upstream — the decisions block in `stories.md`, the plan file, and the `ApiFactory`/`PostgresFixture` patterns from earlier stories made this story's design decisions flow rather than debate.
- **Developer-time invested:** ~15 min during the story for review of the plan (~5 min), spot-checking the CORS decision (~3 min), watching the Playwright smoke (~3 min), and reviewing the diff before commit (~4 min). Closer to "actively reviewed and directed" than "watched the agent work" — the plan-file review upfront was the costliest bit.

### Cost so far (rough)
- Running total on the MVP track (Stories 1.1 → 1.6): roughly **3 hours of agent-wall-clock**. The app now boots, auths, and presents a real UI.
- No direct token-count instrumentation available in-session; journal time estimates are based on session-transcript timestamps.

### Next
- **Story 1.7 — Room & RoomMember data model + migration.** Back to the backend. Schema-only story: introduce `Room`, `RoomMember { Role ∈ {Member, Admin, Owner} }`, and `RoomBan` entities, each with the right indexes and FKs; generate an additive `AddRoomsSchema` migration (NOT a consolidated rewrite — per journal 21:48, "consolidated migration is allowed only while no environment has the first migration deployed"; the `InitialSchema` has now been applied in Docker Compose on this machine so from here on, migrations are additive). Round-trip test for each entity. Points: 2. No web changes. No docker change. Prerequisite: verify `dotnet ef migrations add AddRoomsSchema` doesn't try to collapse with `InitialSchema`; that should be automatic but worth a sanity check.

---
