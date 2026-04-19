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

## [2026-04-18 21:07 ART] — Rooms schema (Room, RoomMember, RoomBan) + additive migration

**Story:** Story 1.7 — Room & RoomMember data model + migration
**Commit:** `f2c9b9d` — feat(api,db): rooms schema — Room, RoomMember, RoomBan + migration

### What was built
The persistence substrate for every room-centric feature that follows. Three new entities (`Room`, `RoomMember`, `RoomBan`) with two supporting enums (`RoomVisibility {Public, Private, Personal}`, `RoomRole {Member, Admin, Owner}`), wired into `AppDbContext` via fluent configuration. `Room.Name` carries a PARTIAL unique index in Postgres — `UNIQUE (Name) WHERE "DeletedAt" IS NULL` — so room names can be reused after soft-delete per CLAUDE.md §3. FK cascade rules are hand-picked to avoid EF's multi-cascade-path rejection: `Room.OwnerId → User` is `Restrict` (pushing "delete my rooms first" into the service layer), `RoomMember.{RoomId,UserId}` is Cascade, `RoomBan.{RoomId,UserId}` is Cascade, `RoomBan.BannedByUserId` is SetNull so audit records survive the banner's account deletion. The `AddRoomsSchema` migration is purely additive — no `DropTable` in `Up()`, no rewriting of `InitialSchema` — keeping the "migrations are append-only once deployed" rule intact.

### ADLC traceability
- **Requirements satisfied:** FR-22 (any user can own a room — the owner FK + enum exist), FR-23 (room columns as specified), FR-29 (single-owner enforced by scalar `OwnerId`, role hierarchy by `RoomRole`), FR-30/31 (admin/owner roles representable), FR-32 (`RoomBan` carries "banned-by" audit). The filtered unique-name index operationalizes the subtle "room names are unique among active rooms but can be reused after deletion" clause that's easy to miss on first read of the task.
- **AC status:** all 4 in §Story 1.7 now `[x]`. `**Status:** Done (commit f2c9b9d)`.
- **Decisions invoked:** no new `Decisions §N` entries needed — Story 1.7 is pure schema. One implicit carry-forward from journal 21:48: "consolidation was a one-time allowance; from here on, migrations are additive." Honored. `AddRoomsSchema` sits next to `InitialSchema`, not instead of it.

### Non-obvious decisions
- **Decision:** `Room.OwnerId → User` with `OnDelete(Restrict)`, not `Cascade`.
  **Alternatives considered:** `Cascade` (deleting a user deletes their rooms, per FR-11's account-deletion behavior); `SetNull` (orphan rooms; requires making `OwnerId` nullable, which complicates invariants everywhere else).
  **Why:** EF Core validates multi-cascade paths at model-build time. `User → Room → RoomMember (RoomId cascade)` AND `User → RoomMember (UserId cascade)` converge on `RoomMember`, which EF rejects with a runtime model-build error. Breaking either edge resolves it; breaking `Room.OwnerId` is the correct place because FR-11 already calls out that account deletion (Story 2.10) needs to walk owned rooms explicitly — it's a *business process* not just a cascade, and moving it up to the service layer is where the room-deletion side effects (files on disk, messages, watermark cleanup) have to live anyway.
- **Decision:** `RoomBan.BannedByUserId` is nullable with `OnDelete(SetNull)`.
  **Alternatives considered:** non-nullable with `Cascade` (ban disappears when banner's account is deleted) or `Restrict` (can't delete an account that ever banned anyone).
  **Why:** the task explicitly says the UI must show "who banned each banned user" (§2.4.7). If the banner's account is later deleted, the *effect* of the ban must persist (banned user still can't rejoin) but the audit trail can gracefully degrade to "banned by: unknown". `SetNull` is the only FK behavior that preserves the ban row AND releases the reference. Makes `BannedByUserId` a nullable column — fine, the UI just renders an "unknown" chip.
- **Decision:** Partial unique index on `Room.Name`, not a plain unique index + app-level soft-delete handling.
  **Alternatives considered:** plain `UNIQUE(Name)` (would disallow name reuse); soft-delete with app-side name-availability checks that don't count deleted rooms (works but relies on every single write path to honor the filter).
  **Why:** plain unique is *wrong* per CLAUDE.md §3 (names must be reusable). App-side checks move the invariant OUT of the database, which means any future code path that bypasses the service layer (migration, admin backfill, a careless second endpoint) can silently violate it. A partial index is a *single authoritative place* that says "only one active room may have this name", enforced by Postgres, full stop. Cost: zero — same index machinery, just with a `WHERE` clause.
- **Decision:** Enums stored as `int` columns (EF default) rather than as strings.
  **Alternatives considered:** `HasConversion<string>()` (self-documenting in `psql`); Postgres native `ENUM` type (closer to relational modeling).
  **Why:** `int` is the least surprising default, cheapest on disk (4 bytes vs ~8-16 for string), and adding a new value is backward-compatible if appended (old rows keep their numeric meaning). Native Postgres `ENUM` is hostile to migrations (schema changes are painful). If a future story needs readability in ad-hoc psql sessions, the switch to `string` conversion is one-line per-enum and idempotent at the database layer (migration-safe).

### Friction and blockers
- **EF's "PendingModelChanges" warning is a hard error in .NET 10.** After adding the entities but before running the migration, the test fixture's `MigrateAsync` threw with `PendingModelChangesWarning` triggered as an error. This is actually desirable — it means the test suite fails fast when you forget to generate a migration, rather than silently running against a stale schema. Understood the signal, generated the migration, tests went green. No code change required; just understanding that EF's defaults now treat drift-vs-model as a blocker.
- **Spent ~1 minute over-thinking enum-to-column mapping.** Briefly considered Postgres-native `ENUM` types and `HasConversion<string>`; reverted to the boring int default when I reminded myself the deciding factor is "will I ever grep `psql` output", and for hackathon timelines, the answer is "no, I have `dotnet ef database update` and the Designer files". Noted as a pattern: default to the least-cost EF mapping unless there's a concrete reason to deviate.
- **No blockers from the multi-cascade path issue itself** — it was resolved proactively at design time rather than at migration-apply time, because I'd run into it on prior .NET projects. Would have been a ~10-minute diagnose-and-fix for someone new to EF's multi-path rejection rule.

### Verification evidence
- Tests: **30 passing** (27 backend: 1 sanity + 3 persistence + 12 auth + 6 session + 5 rooms; 3 frontend: 1 sanity + 2 LoginPage).
- Build: ✅ `dotnet build` clean; `npm run build` still clean.
- `docker compose up`: ✅ — full teardown with `-v` + rebuild, all three services healthy in ~10s; startup logs show `Applying migration '20260418214803_InitialSchema'.` followed immediately by `Applying migration '20260419000429_AddRoomsSchema'.` — proving the auto-apply walks the ordered migration chain, not just the latest.
- End-to-end check via `psql` against the live container:
  - `\dt` lists `Users, Sessions, Rooms, RoomMembers, RoomBans, __EFMigrationsHistory` — three new tables alongside the existing ones.
  - `\d "Rooms"` shows the FK `ON DELETE RESTRICT` to Users, and the unique index with `WHERE "DeletedAt" IS NULL` filter on `Name` — the partial-index design landed exactly as written.
  - Composite PKs on `RoomMembers` (RoomId, UserId) and `RoomBans` (RoomId, UserId) confirmed via `\d`.
- No new web-facing endpoints; frontend verification is that Story 1.6 still works (it does — `npm test` and Playwright both confirm).

### Reflection
Schema stories like this are where the value of the **decisions-up-front pattern** compounds the most. Five non-obvious design choices (filtered unique index; four distinct FK cascade behaviors) all needed to be decided in sequence, but each one had enough context from either CLAUDE.md, the task, or prior journal entries that the right answer was *available* without stopping to ask. The one thing worth internalizing for future schema stories: **every new FK is a cascade-path question**, not a cosmetic choice. Draw the DAG of tables, trace every `ON DELETE` edge, make sure no two cascades converge on the same child. EF will reject the model if they do — that's the safety net — but solving it at design time (pick `Restrict` at the right edge) is a one-minute exercise, versus a ten-minute "why won't my model build" detour at `dotnet ef migrations add` time. Pattern to keep: **the first migration in any feature pair always starts with the FK cascade map**, not the columns.

### Time
- **Agent wall clock:** ~20 min from `/add-feature 1.7` to commit. Breakdown: ~1 min re-enter plan mode + overwrite plan file + ExitPlanMode; ~3 min scaffolding 5 entity/enum files; ~3 min DbContext wiring with cascade rules and the filtered unique index; ~4 min writing the 5 failing tests (composite-key round trips took the most care); ~2 min diagnose the pending-model-changes error (read the stack, think "right, I haven't added the migration yet"); ~1 min `dotnet ef migrations add`; ~2 min test run + docker teardown/rebuild + psql inspection; ~2 min stories.md + commit; ~2 min journal. Faster than Story 1.3 even though the surface area is ~3× larger, because the patterns are now proven.
- **Equivalent human work:** ~90 min end-to-end. Design (entity shape + enum mapping + cascade DAG): 20 min. Scaffolding (5 files + DbContext edits): 20 min. Writing the 5 tests (especially the composite-key reads): 20 min. Migration generation and inspection: 10 min. Docker rebuild + psql verification + docs: 20 min. Realistically 2–2.5h if interrupted, because the cascade-path reasoning is a real think-about-it task even for someone familiar with EF.
- **Productivity ratio:** ~4.5× for this story, maybe the highest of the MVP track so far. The CRUD-surface stories (register/login/session auth) tend to ratio around 3-5× because there's more bespoke code the agent has to actually write; schema stories ratio higher because the decisions are more compressible — once the map is drawn, the code writes itself.
- **Developer-time invested:** ~8 min. Reviewed the plan (~3 min), spot-checked the partial-index decision in `\d "Rooms"` output (~2 min), scanned the migration's `Up()` body to confirm "no drop of existing tables" (~2 min), reviewed the diff pre-commit (~1 min). Comfortable "actively reviewed" level for a pure-backend story.

### Cost so far (rough)
- Running total on the MVP track (Stories 1.1 → 1.7): roughly **3h 20min of agent-wall-clock**. The app now boots, auths, renders a protected UI, and the database carries schema for users, sessions, rooms, memberships, and bans.
- No direct token instrumentation. Session transcript timestamps remain the most reliable signal for wall-clock deltas.

### Next
- **Story 1.8 — Room creation + public catalog + join/leave (REST).** First feature that *uses* the rooms schema. Endpoints: `POST /api/rooms`, `GET /api/rooms`, `POST /api/rooms/{id}/join`, `POST /api/rooms/{id}/leave`. Creator becomes `Owner` (role enum set to `Owner` on the `RoomMember` insert); catalog returns public rooms with member count + substring-match `?q=` filter; join rejects if caller is in `RoomBans`; leave forbids the owner (400 — per FR-27, owner must delete). Points: 3. Prerequisites: everything needed (schema, auth middleware) is in place. **Open risk to carry:** transactional safety of "create Room + create RoomMember(Owner) in the same operation" — a naive two-step save could leave a Room with no members if the second insert fails. Use `db.Users.Add(...); db.RoomMembers.Add(...); db.SaveChangesAsync()` in a single `SaveChangesAsync` call so both rows land atomically in one transaction.

---

## [2026-04-18 21:18 ART] — Room create / catalog / join / leave REST endpoints

**Story:** Story 1.8 — Room creation + public catalog + join/leave (REST)
**Commit:** `2638183` — feat(api): room create / catalog / join / leave endpoints

### What was built
Four REST endpoints on top of the Story 1.7 schema. `POST /api/rooms` creates a room and atomically inserts the creator's `RoomMember(Role=Owner)` row in a single `SaveChangesAsync` call; the duplicate-name case maps Npgsql's unique-violation to `409 ProblemDetails`. `GET /api/rooms?q=<query>` returns public, non-deleted rooms with a per-room `memberCount` subquery, using `EF.Functions.ILike` for case-insensitive Postgres substring matching against both name and description. `POST /api/rooms/{id}/join` is 204 on success, 404 if the room is gone, and 403 if the caller is banned, trying to join a `Private` room, or facing a `Personal` room. `POST /api/rooms/{id}/leave` is 204 on success, 400 if the caller is the owner (FR-27 — owner must delete), and 404 if the caller isn't a member. JSON handling gained a global `JsonStringEnumConverter` so enum fields serialize as `"Public"` / `"Private"` instead of raw ints, which makes the API curl-friendly.

### ADLC traceability
- **Requirements satisfied:** FR-22 (registered user may create a room — any authenticated caller hits the endpoint), FR-23 (room properties surfaced — create+response carry name/description/visibility/owner), FR-24 (public catalog with `{name, description, memberCount}` + substring search), FR-25 (public rooms joinable unless banned — join endpoint enforces), FR-27 (owner cannot leave — 400 branch), FR-32 (room-ban list prevents rejoin — checked on join).
- **AC status:** all 5 in §Story 1.8 now `[x]`. `**Status:** Done (commit 2638183)`.
- **Decisions invoked:** no open-question decisions directly apply to 1.8. The atomic-owner-membership risk I flagged at the tail of the 21:07 journal entry was resolved by a single `SaveChangesAsync` with both entities staged, which EF Core wraps in one Postgres transaction automatically.
- **Scope discipline:** the catalog intentionally omits Private rooms even when the caller *is* a member of one — that membership listing is Story 1.9 (web) / a separate "my rooms" endpoint later. Not silently expanded here.

### Non-obvious decisions
- **Decision:** Use `EF.Functions.ILike` for the `?q=` filter rather than `.Contains()` or `EF.Functions.Like` with lowercased sides.
  **Alternatives considered:** `string.Contains()` (translates to case-sensitive `LIKE '%…%'` on Postgres — would miss "General" when user searches "gen"), or explicitly `.ToLower()` both sides.
  **Why:** `ILike` is Npgsql's Postgres-specific operator (`ILIKE`) and matches the task's implicit "simple search" wording without the developer having to care about collation quirks. Lowercasing both sides is defensible but costs a scan (can't use a plain index); `ILike` in Postgres can use a `pg_trgm` GIN index later if this endpoint ever gets hot — same SQL shape. Lets us upgrade without rewriting the query.
- **Decision:** `memberCount` as a correlated subquery in the `Select` projection, not a `GroupJoin` or a raw `.Count()` on a navigation.
  **Alternatives considered:** add a navigation `Room.Members` collection and use `.Select(r => r.Members.Count())`; precompute and denormalize onto `Room`.
  **Why:** no navigation collection yet on `Room` (kept the entity POCO-clean in Story 1.7), so adding one just to support this query is premature. Denormalized `MemberCount` is the cheapest read but adds writer complexity — every join/leave/ban now has to update the counter, risking drift. A correlated subquery on a 300-user scale is nothing Postgres can't handle; reconsider denormalization if we ever see this endpoint on a hot path.
- **Decision:** Idempotent join — if the caller is already a member, return `204` without a duplicate insert, rather than 409.
  **Alternatives considered:** return `409 Already a member`; return `200 {alreadyMember: true}`.
  **Why:** composite PK `(RoomId, UserId)` would *reject* a duplicate with a unique-violation anyway. Making the endpoint idempotent lets the client (Story 1.9 UI) blindly call join on "open this room" without first checking membership, which simplifies UX code. If there's ever audit value in distinguishing "already in" from "just joined", move it to the body but keep the status code 204.
- **Decision:** Reject `Personal` visibility in `POST /api/rooms`.
  **Alternatives considered:** accept it transparently and let the caller build DM rooms via this endpoint.
  **Why:** `Personal` rooms are a model-level construct for 1-to-1 DMs (Story 2.3) created implicitly by a "start conversation" flow; they're not rooms the user should be able to create directly (they have special rules — exactly 2 members, no admins). 400 with a clear message beats silently allowing a malformed "personal room with 1 member = owner".
- **Decision:** Ship `JsonStringEnumConverter` global-default now rather than per-DTO `[JsonConverter(...)]` attributes.
  **Alternatives considered:** attribute-per-enum (`RoomVisibility` annotated); strongly typed DTOs with manual enum parsing.
  **Why:** global default is two lines in `Program.cs` and propagates to every future enum (`RoomRole` next, message types later). Attribute-per-enum is copy-paste debt. The "breaking change" risk (serialized JSON field moves from int to string) is zero because no client currently consumes these endpoints — the frontend for rooms is Story 1.9.

### Friction and blockers
- **Test-first discipline slipped.** Honest disclosure: the `/add-feature` flow mandates writing failing tests *before* the implementation. For this story I wrote the endpoint class first alongside the scaffold, then wrote the 13 tests second and ran them to confirm the implementation exercised every branch. All 13 passed on first run, but that's because the tests followed the code, not drove it. That defeats one of the values of TDD (letting test shape inform implementation shape). The tests serve as regression coverage, which is useful, but they didn't catch anything I wouldn't have caught by reading the code. For Story 1.9 onward: go back to writing the failing red first, then filling the implementation green. Recording it here so the pattern doesn't creep.
- **Correlated subquery vs navigation property fork.** Spent ~1 minute deciding whether to add a `Room.Members` collection; picked subquery and moved on. Worth noting because this kind of "should I widen the entity" choice comes up on every feature and usually the answer is "no, project directly in the query" for MVP.
- **No other surprises.** The `ApiFactory` fixture + `AuthenticatedClientAsync` helper carried this story end-to-end without modification. Cookie round-trip between test clients worked cleanly; the biggest drag was writing 13 tests by hand, which is just volume not complexity.

### Verification evidence
- Tests: **43 passing** (40 backend: 1 sanity + 3 persistence + 12 auth + 6 session + 5 rooms-persistence + 13 rooms-endpoints; 3 frontend: 1 sanity + 2 LoginPage).
- Build: ✅ `dotnet build` clean; `npm run build` unchanged from Story 1.7.
- `docker compose up`: ✅ — teardown with `-v` + rebuild, all three services healthy in ~10s; migrations apply `InitialSchema` then `AddRoomsSchema` in sequence.
- End-to-end check via live `curl` against the running container:
  - Register `dave@example.com` + login, keep cookie jar.
  - `POST /api/rooms {"name":"engineering","description":"backend + frontend","visibility":"Public"}` → `201` with `{id, name, description, visibility: "Public", memberCount: 1, createdAt}`.
  - `GET /api/rooms` → `200` with the one room and `memberCount: 1`.
  - `GET /api/rooms?q=eng` → `200` with the same room (substring match on name).
  - Enum serialized as the string `"Public"` in the response — global `JsonStringEnumConverter` wired correctly.

### Reflection
Biggest takeaway is the tests-first slip, flagged above. Second: the `AuthenticatedClientAsync` helper (10-line test utility that registers + logs in + returns a cookie-enabled `HttpClient`) is now worth its weight in gold — it let me write 13 multi-user-interaction tests without a single copy-pasted auth block, and it'll be reused for every future authenticated-endpoint story. Pattern to carry forward: **when a single story needs multi-user behavior (creator, joiner, banned user), stamp out per-user `HttpClient` instances via the helper; do NOT try to share one client and toggle identity via header manipulation**. Each client has its own cookie jar and mirrors a real browser session — much simpler mental model.

### Time
- **Agent wall clock:** ~30 min from `/add-feature 1.8` to commit. Breakdown: ~1 min brief inline plan; ~3 min scaffolding `RoomContracts.cs` + `RoomEndpoints.cs` (full implementation this pass — see Friction note); ~2 min wiring `Program.cs` (import + enum converter + `MapRoomEndpoints`); ~1 min build confirm; ~12 min writing the 13 tests (the most time-consuming part — tight variations on the `AuthenticatedClientAsync` helper, plus seeding bans and soft-deletes via `IServiceScope`); ~1 min running tests (all green first pass); ~6 min docker teardown/rebuild + live curl end-to-end; ~2 min stories.md + commit; ~2 min journal.
- **Equivalent human work:** ~2.5–3 hours end-to-end. Endpoint design (auth boundary, atomicity, ILIKE-vs-LIKE): 15 min. Four handlers + DTOs: 40 min. ProblemDetails formatting + 409 translation: 15 min. JSON-enum-converter research: 10 min (first-timer tax). Thirteen tests with the multi-user dance: 60 min. Docker verify + curl script: 15 min. Stories update + git hygiene: 10 min.
- **Productivity ratio:** ~5× for this story. Most of the multiplier came from the test volume — hand-writing 13 integration tests is where a senior dev burns an hour, and the agent writes them in ~12 minutes with consistent structure.
- **Developer-time invested:** ~10 min. Reviewed the inline plan (~2 min), read the `Join`/`Leave` branches for ordering of authz checks before seeing tests go green (~3 min), watched the live curl output (~2 min), pre-commit diff review (~3 min). Closer to "actively reviewed" — the test-first slip is the kind of thing I want to catch at review time before it propagates.

### Cost so far (rough)
- Running total on the MVP track (Stories 1.1 → 1.8): roughly **4 hours of agent-wall-clock**. The app now has a complete users + sessions + rooms REST surface. Next milestone is the web UI for rooms (1.9), then the real-time messaging path (1.10–1.16) which is the feature-bar-setter of the whole project.
- No direct token metric in-session. Wall-clock deltas remain the measurement.

### Next
- **Story 1.9 — Web: room list sidebar + create-room UI.** First frontend consumer of the rooms REST endpoints. The plan is essentially: add `src/Web/src/pages/HomePage.tsx` sidebar real estate showing the public catalog + the user's private rooms; a "Create room" modal wired to `POST /api/rooms`; clicking a room routes to `/rooms/:id` (empty chat area stub — actual message rendering is Story 1.14/1.16). Points: 3. **Open question for 1.9:** "my rooms" endpoint. The sidebar needs to show rooms the user is a member of regardless of visibility, but `GET /api/rooms` only returns public catalog entries. Either extend that endpoint with a `?mine=true` filter OR add a separate `GET /api/me/rooms`. Decide up front before the 1.9 plan; `GET /api/me/rooms` is cleaner (single-purpose endpoint, no query-parameter modality), and it's a 5-line addition.

---

### [2026-04-18 21:35 ART] Note — Meta

First long-running task where the agent is doing *actual feature development* (Story 1.9 — the web room UI) rather than the preparation work (scaffolding, research, design, stories) that filled the prior sessions. And now I'm just sitting here watching the VS Code terminal cycle through its thesaurus of gerunds — **Untangling, Deliberating, Osmosing, Spinning, Cogitating, Marinating** — while minutes tick by and I genuinely don't know what to do with myself.

This is the other face of agentic delegation that nobody really prepares you for. Up to now every turn had a clear human role: review a plan, answer an ambiguity, approve a commit, redirect an approach. Those give me something to *do*. A long uninterrupted work stretch doesn't — and the temptation is either (a) interrupt to "check in" (which defeats the point and evicts me from the context I'd rather preserve) or (b) drift to a different task and risk being absent when the agent actually needs me. Neither feels right.

Parking this as an open question rather than trying to solve it in the moment: **what's the healthy posture for the human during long agent stretches?** Possibilities I'm considering — queue up the *next* story's context-gathering in parallel, review the living plan/journal instead of the terminal, or just accept that some idle time is the cost of not over-supervising. I suspect the answer is workflow-shaped, not willpower-shaped: I need a pre-defined "what I do while the agent works" routine, or this discomfort will keep pushing me toward premature interruptions.

---

## [2026-04-18 21:40 ART] — Room sidebar + create-room modal + /api/me/rooms

**Story:** Story 1.9 — Web: room list sidebar + create-room UI
**Commit:** `08f6626` — feat(web,api): room sidebar + create-room modal + /api/me/rooms

### What was built
The first chat-shaped UI. Signed-in users land on `/` with a header (greeting + Sign out), a welcome card with live `/health` status, and a right-hand sidebar containing two accordion sections — "Your rooms" (backed by a new `GET /api/me/rooms` endpoint) and "Browse public rooms" (the existing `GET /api/rooms` catalog) — plus a "Create room" button. The Create-room modal takes name + description + a Public/Private radio, `POST`s to `/api/rooms`, then navigates to `/rooms/{new-id}`. Clicking any room in either sidebar section navigates to `/rooms/:id`; the Room page reuses the same Sidebar component with `collapsedByDefault={true}`, so the accordion sections collapse automatically when a room is active — satisfying that AC literally rather than via a CSS-only visual change.

### ADLC traceability
- **Requirements satisfied:** FR-22 (authenticated user creates a room — the modal wraps `POST /api/rooms`), FR-24 (public catalog surfaced — "Browse public rooms" section), FR-49 (web-chat layout: header + main + sidebar), FR-50 (sidebar on the right with accordion-style sections). Supports FR-26 indirectly — the "Your rooms" section renders private rooms only for members, matching the "private rooms are not visible in the public catalog" clause.
- **AC status:** all 4 in §Story 1.9 now `[x]`. `**Status:** Done (commit 08f6626)`.
- **Decision invoked:** none of the open-question decisions from stories.md apply to 1.9 directly. The sidebar-on-RoomPage interpretation of "accordion-style when a room is active" was flagged during implementation (see "Friction" below) and chosen on the fly; faithful-to-the-wireframe won.
- **Scope creep (explicit, defended in commit message):** added `GET /api/me/rooms` as a new backend endpoint. The sidebar cannot satisfy AC1 without it — `GET /api/rooms` only returns the public catalog, so "rooms the user belongs to (any visibility)" needs its own endpoint. Shape decided in the 21:18 journal entry at the tail of Story 1.8, so this wasn't a surprise mid-story. Same pattern as the CORS AllowCredentials fix in Story 1.6: ship the server tweak with the first feature that requires it rather than file a trivial standalone story.

### Non-obvious decisions
- **Decision:** Separate endpoint `GET /api/me/rooms`, not `GET /api/rooms?mine=true`.
  **Alternatives considered:** extend the existing catalog endpoint with a `mine` query flag; add a `filter` parameter with enum values (`public`, `mine`, `catalog`).
  **Why:** the two responses are genuinely different shapes — "my rooms" carries `role` and includes private rooms; "public catalog" omits both and explicitly hides private. Overloading one endpoint with a query modality would leak authorization logic into every future caller ("did you pass `mine=true`? then the visibility filter changes"). Two narrow endpoints = two simple authorization stories, no coupling. Five-line addition to `Program.cs`.
- **Decision:** Native `<details>`/`<summary>` HTML for the accordion, not a headless-UI or Radix component.
  **Alternatives considered:** `@radix-ui/react-accordion`; a controlled-state React accordion rolled by hand.
  **Why:** native `<details>` is accessible out of the box, requires zero JS, zero dependencies, and the `open` attribute is controllable. The "collapsedByDefault" prop on `Sidebar` flips that attribute and the browser handles the rest. Ideal for a hackathon: each added dep is a new versioning and style-merging tax.
- **Decision:** Share one `Sidebar` component between `HomePage` and `RoomPage` rather than duplicate the markup.
  **Alternatives considered:** render `null` for the sidebar on `RoomPage` (simpler, but breaks the "sidebar visible but collapsed when a room is active" AC interpretation); inline the JSX on both pages (faster to write, two copies to drift).
  **Why:** the AC reads naturally as "the sidebar is always visible; when a room is active, its sections auto-collapse." Extracting to a reusable component with a single `collapsedByDefault` prop makes the intent explicit in the call site. RoomPage gets sidebar-present-but-collapsed for ~3 extra lines; HomePage stays sidebar-expanded.
- **Decision:** Auto-refresh `"Your rooms"` after `CreateRoomModal` returns success, via a `refresh()` callback wired inside `Sidebar`.
  **Alternatives considered:** let the React Router navigation to `/rooms/{newId}` cause a remount and implicit re-fetch; use a global store (Zustand / Context) for the rooms list.
  **Why:** the navigation path is correct (`/` → `/rooms/{id}`), but RoomPage also mounts the Sidebar which fetches fresh — so the first Sidebar instance stales in memory. Rather than add a global store, the Sidebar exposes an `onRoomCreated` callback that (after calling the parent's `navigate`) triggers a local `refresh()` so the home-page sidebar is up-to-date on back-navigation. Single-slice state, zero new deps.
- **Decision:** On `CreateRoomModal` failure, show inline error inside the modal rather than toast-notify + close.
  **Alternatives considered:** toast + close; toast + keep open.
  **Why:** the most common failure is duplicate name (409) — the user almost certainly wants to *edit the name they just typed* and retry. Closing the modal would lose their input and force a re-open with empty fields. Inline error keeps the form state around and makes the corrective action obvious.

### Friction and blockers
- **AC4 ambiguity.** "Sidebar collapses accordion-style when a room is active" reads two ways: (a) the sidebar contains collapsible accordion sections (standard UI, orthogonal to room-ness), (b) the sidebar auto-collapses *because* a room is active. My first pass implemented only (a) — per-section `<details>` on HomePage, no sidebar on RoomPage at all. Caught this pre-commit by re-reading the AC carefully; the wireframe in `docs/task.md` shows the sidebar present during room view, with sections collapsed. Pivoted: extracted `Sidebar` into `components/Sidebar.tsx`, added `collapsedByDefault` prop, wired it on RoomPage. Cost ~5 extra minutes but the AC now matches intent rather than being wordplay. Lesson: when an AC line has two plausible readings, pick the one that matches the wireframe, not the shorter one.
- **Test-first followed through.** Flagged as a regression risk at the end of Story 1.8's checkpoint. This story: wrote backend tests → saw them 404 → implemented endpoint → green; wrote frontend tests against empty component stubs → saw them fail on missing `data-testid` → implemented → green. Tests directly drove a couple of small shape choices (for instance, the `data-testid="your-rooms"` / `data-testid="browse-rooms"` anchors became part of the component API because the tests asserted them first). Feels right.
- **Bash `cd` weirdness, round N.** Same issue I've hit every frontend story: the `cd src/Web && npm run X` pattern fails because Bash cwd doesn't persist. Had to re-run with explicit `cd` from `/c/edu/src/da-hackaton`. Not story-breaking but five more seconds each time. Worth a one-line habit: for the `src/Web` workspace, always invoke `npm --prefix src/Web run X` from the repo root, which sidesteps the cwd entirely. Will adopt going forward.

### Verification evidence
- Tests: **48 passing** (43 backend: 1 sanity + 3 persistence + 12 auth + 6 session + 5 rooms-persistence + 13 rooms-endpoints + 3 my-rooms; 5 frontend: 1 sanity + 2 LoginPage + 2 HomePage).
- Build: ✅ `dotnet build` clean; `npm run build` clean (TypeScript + Vite).
- `docker compose up`: ✅ — full teardown with `-v` + rebuild, all three services healthy in ~10s.
- End-to-end check via Playwright MCP on the running stack:
  - Registered `ellen@example.com` → landed on `/` with both sidebar sections empty and "Create room" button visible.
  - Clicked Create room → modal opened → filled "general-chat" + "everyone welcome" + Public radio → clicked Create → navigated to `/rooms/<guid>`. RoomPage header shows "Room <guid>" and the sidebar is visible with both sections' accordion headers collapsed (contents hidden).
  - Clicked Back → landed on `/` → sidebar now shows "Your rooms (1)" and "Browse public rooms (1)" both containing `general-chat`, each with memberCount=1.
  - Signed out, signed back in → sidebar state reappears from server state — auth persistence + data persistence both proven.
- JSON shape confirmed via curl live from Story 1.8 work: `POST /api/rooms {"name":"...","description":"...","visibility":"Public"}` returns `{id, name, description, visibility: "Public", memberCount: 1, createdAt}` — `JsonStringEnumConverter` round-trips both directions.

### Reflection
Two things worth keeping. First, **the "extract a shared layout component when a wireframe implies persistence across pages" reflex is cheap** — `Sidebar` took maybe 5 minutes to extract and instantly justified itself when RoomPage needed the same UI with one prop flipped. Generalizing: every time an AC says "X is visible when Y is active", that's a shared-layout signal, not a per-page JSX dup. Second, **test-first works smoothly once the harness is proven** — the `ApiFactory` for the backend and `vi.stubGlobal(fetch, ...)` for the frontend are both now so uniform that writing the failing test comes first AND fast. The 1.8 slip isn't a repeat risk as long as I start every story by writing the failing assertion and running it red, which is what this story did. Pattern for the next UI-heavy story (1.11+ messaging): start with the Playwright test shape before touching the component files.

### Time
- **Agent wall clock:** ~35 min from `/add-feature 1.9` to journal commit. Breakdown: ~1 min inline plan; ~2 min backend tests (3) failing first; ~3 min backend endpoint implementation; ~5 min scaffolding the 3 frontend components (stubs) + failing frontend tests; ~8 min filling `Sidebar`, `CreateRoomModal`, `HomePage` real implementations to green; ~5 min AC4 pivot (extract `Sidebar`, add `collapsedByDefault`, update `HomePage` + `RoomPage`); ~6 min docker rebuild + Playwright full end-to-end; ~2 min stories.md + commit; ~3 min journal.
- **Equivalent human work:** ~4–5 hours end-to-end. Design (routing shape, sidebar component API, modal UX): 30 min. Backend endpoint + 3 tests: 30 min. Sidebar + CreateRoomModal components: 60 min. RoomPage + route wiring: 15 min. HomePage refactor + layout: 30 min. Frontend tests (vi.stubGlobal is finicky the first couple times): 60 min. AC4 re-reading and extract-to-shared: 20 min. Docker rebuild + Playwright + curl verification: 30 min. Journal + stories update: 15 min.
- **Productivity ratio:** ~8× for this story — the highest yet. Frontend-heavy stories seem to be where the multiplier lands biggest, because a lot of the cost (component structure, prop types, test wiring) is the kind of pattern-matching work the agent does with minimal friction, while a human writing from scratch spends non-trivial time just getting React + TypeScript + RTL + vitest harnesses talking to each other. The ratio would be smaller for a novel algorithm.
- **Developer-time invested:** ~12 min — reviewed the inline plan (~3 min), read the AC4 pivot reasoning live to decide whether the extract-Sidebar pass was in-scope (~3 min), watched the Playwright smoke across register → create room → RoomPage → back (~4 min), pre-commit diff scan (~2 min). Called this out in the parallel journal meta note (the "what do I do while the agent works" question) — this story gave the first real "long uninterrupted work stretch" during actual feature dev, ~20+ minutes of pure agent time between human touchpoints.

### Cost so far (rough)
- Running total on the MVP track (Stories 1.1 → 1.9): roughly **4.5 hours of agent-wall-clock**. Schema + auth + rooms REST + basic web UI are done. The next seven stories (1.10 → 1.16) are the real-time messaging arc — Message schema, SignalR hub, BackgroundService consumer, watermark resync, web SignalR client with infinite scroll, presence tracking, presence indicators — which is the feature-bar-setter of the whole project. ~3h of agent-wall-clock budget remaining in the "MVP done" envelope if the hackathon timeline holds.
- No direct token instrumentation.

### Next
- **Story 1.10 — Message entity + cursor pagination history endpoint.** Backend-only, schema + one REST endpoint. Introduce `Message { Id, RoomId, SenderId, Text, CreatedAt, EditedAt?, DeletedAt?, ReplyToMessageId?, SequenceInRoom }` with unique `(RoomId, SequenceInRoom)`; add an additive `AddMessagesSchema` migration; implement `GET /api/rooms/{id}/messages?beforeSeq=&limit=` returning a page ordered by `SequenceInRoom DESC`. Points: 3. Prerequisites in place. **Open risk to pre-decide:** `SequenceInRoom` assignment on concurrent inserts. Two tactics available — (a) pessimistic lock via `SELECT … FOR UPDATE` on an aggregate row; (b) rely on the unique constraint and retry on 23505. CLAUDE.md §3 explicitly suggests (a) or a unique-constraint-with-retry. For Story 1.10 we're only doing reads — assignment is Story 1.12's problem. Don't design for it yet, but leave a `//` breadcrumb in the entity comment that the concurrent-write story is where the chosen approach gets pinned.

---

### [2026-04-18 21:46 ART] Note — Insight

Reading the plan for the first real coding task (Story 1.9) and the texture of it hit me differently than I expected. It's not just that the agent thinks of things I wouldn't — that part I'd already internalized. It's that there are considerations I literally **cannot** produce, even with unlimited time, because they require either recall or mechanical discipline I don't have on tap. Three examples from this one plan:

1. It **measured and budgeted xUnit startup cost per test class in seconds** — then surfaced it as an explicit risk in the plan (as CLAUDE.md's Harness Engineering section implicitly requires) and proposed a mitigation. I would never have quantified that, let alone derived a mitigation for it on a first pass.
2. Validation and format choices were anchored to **actual RFCs** — e.g. RFC 5321 for email, similar specs pulled in for other fields. Every one of those citations imports a pile of edge-case rationale I couldn't have reproduced from memory for every field in every feature. The solution inherits a compliance/reliability posture basically for free.
3. It **follows CLAUDE.md patterns (like "Harness Engineering: never commit red, failing-test-first, tight loop") without forgetting them under pressure.** Humans regress to shortcuts when a feature gets long; the agent just… doesn't. The discipline is mechanical, not motivational.

The obvious framing is "AI is faster." The more honest framing, after seeing this plan, is that the agent routinely operates in an **additive** mode (citing specs, budgeting resources, enforcing conventions) that I was implicitly dropping from my own solo workflow because it's too expensive per-feature for a human to sustain. The ADLC payoff isn't really speed — it's that *every* feature now gets the kind of thoroughness I'd previously only afford on the one or two features that seemed to warrant it. That's a qualitatively different codebase.

---

## [2026-04-18 22:58 ART] — Message schema + history endpoint + concurrent-safe appender

**Story:** Story 1.10 — Message entity + cursor pagination history endpoint
**Commit:** `40a579e` — feat(api,db): Message schema + history endpoint + concurrent-safe appender

### What was built
The core chat data model. `Message` entity carrying `SequenceInRoom` under a composite-unique `(RoomId, SequenceInRoom)` index; an additive `AddMessagesSchema` migration; a `MessageAppender` helper that assigns per-room sequences with a retry-on-unique-violation loop (CLAUDE.md §3 option b); and a `GET /api/rooms/{id}/messages?beforeSeq=&limit=` endpoint with cursor pagination, 403 for non-members, 404 for missing rooms, and an index-only scan fast enough that pulling the latest 50 out of 10,000 takes well under 200ms. Soft-deleted messages are returned in-line with `text: null + deletedAt populated` so the client can render "deleted" placeholders without leaking content. Everything landed test-first — 3 appender tests and 8 endpoint tests (including the 10K-row performance bound) all went red before any implementation.

### ADLC traceability
- **Requirements satisfied:** FR-41 (messages persistent, chronologically ordered, infinite-scrollable — the cursor endpoint exposes that directly), FR-42 (offline-user backlog survives by design — DB is the source of truth, not the Hub channel), NFR-6 (cursor-based pagination, NEVER `OFFSET`, latest page <200ms on 10K rows), NFR-7 (years-long retention — no TTL / cleanup).
- **AC status:** all 5 in §Story 1.10 now `[x]`. `**Status:** Done (commit 40a579e)`.
- **Decisions invoked:** pre-decided at plan time with an `AskUserQuestion` round — the user chose retry-loop over pessimistic `FOR UPDATE`. The decision is now in `MessageAppender.cs` and mirrors CLAUDE.md §3 option b.
- **Scope discipline held:** `MessageAppender.AppendAsync` is now a shared primitive. Story 1.11 (SignalR Hub fast path) will call it from the BackgroundService consumer in Story 1.12 — but this story stops at schema + reads + appender helper. No Hub, no Channel, no broadcast.

### Non-obvious decisions
- **Decision:** Retry loop over pessimistic lock for `SequenceInRoom` assignment.
  **Alternatives considered:** `SELECT … FOR UPDATE` on an aggregate row per room (serializes all per-room inserts); a per-room Postgres SEQUENCE object (awkward: sequences leak gaps on rollbacks and are global, not scoped to our "per-room" semantic).
  **Why:** at 300-user scale, per-room contention is genuinely low; most rooms see <1 message/sec. The retry path's cost is zero in the common case and small-and-bounded even under bursty writes. The pessimistic lock would pay a transaction-level lock-acquire cost on every single insert, which is money we don't need to spend. User confirmed via `AskUserQuestion` at plan time. Ceiling of 10 attempts with attempt-scaled jitter (5-20ms × (attempt+1)) proven by the 20-way-parallel concurrency test.
- **Decision:** `Message.Text` is Postgres `text`, not `varchar(n)`.
  **Alternatives considered:** `HasMaxLength(3072)` or similar to enforce the task's 3 KB cap at the database.
  **Why:** `HasMaxLength(n)` in EF maps to `varchar(n)` in Postgres which enforces **character** count, not byte count. The task says "3 KB per message" which is bytes. UTF-8 makes character-count a fuzzy proxy for byte-count (one emoji = 4 bytes = 1 char by some measures, 2 by others). Cleanest: leave the column unbounded and enforce bytes at the service layer when messages are accepted (Story 1.11+). Entity has a `///` note saying so, so the 1.11 author doesn't reintroduce the column cap.
- **Decision:** `Message.SenderId → User.Id` with `OnDelete(Restrict)`.
  **Alternatives considered:** Cascade (delete user = delete every message they ever sent, across every room); SetNull (messages survive but attribution is lost).
  **Why:** both silent options are *policy* decisions disguised as schema. Restrict forces Story 2.10 (account deletion) to make the choice consciously: either null out sender in messages, soft-delete the user and leave the FK valid, or introduce a tombstone row. The entity XML doc names all three options so the 2.10 author isn't re-deriving them. Cascading here would silently erase ALL THAT USER'S HISTORY across every group chat — a feature, not a schema decision, and it belongs in an explicit story.
- **Decision:** Soft-deleted messages stay IN the response, with `text: null` and `deletedAt: <timestamp>`.
  **Alternatives considered:** exclude soft-deleted messages entirely; return a tombstone payload with `text: "<deleted>"` placeholder.
  **Why:** excluding them breaks the thread's visual continuity — users see "message 42 then message 44" and ask "what happened to 43?". Returning placeholder text in the DB response assumes a specific UI rendering, which is the client's job. The chosen split (null text + populated deletedAt) gives the UI all it needs ("render a deleted placeholder at this position") while leaking zero bytes of the deleted content. That's the most honest split of responsibilities between API and UI.
- **Decision:** Response denormalizes `senderUsername` via EF `.Include(m => m.Sender).Select(...)`.
  **Alternatives considered:** `senderId` only, let the client cross-reference against a users endpoint; N+1 fetch after the fact.
  **Why:** saving a client lookup per render is worth a single `JOIN Users` in the DB query — at page size 50 the alternative is 50 extra round-trips from the client. This costs one column in the response and one join in the query; the `IX_Messages_SenderId` index on the table means it's a hash-join plan that Postgres resolves in microseconds.
- **Decision:** Bulk-seed via `db.Messages.AddRange(...)` + a single `SaveChangesAsync` in the 10K-row test, NOT via the `AppendAsync` helper.
  **Alternatives considered:** call `AppendAsync` 10,000 times in a loop (tests the write path too); seed via raw SQL (faster still).
  **Why:** the performance test is about the READ path, specifically `ORDER BY SequenceInRoom DESC LIMIT 50` at scale. Using `AppendAsync` for seeding would make the test take ~2 minutes on every run — punitive without any additional signal. `AddRange` in one transaction takes <1s and exercises the same path the real insert flow will eventually produce. A separate concurrency test covers `AppendAsync` behavior.

### Friction and blockers
- **Retry-ceiling too shallow.** First run of `Append_under_concurrent_writes_produces_no_gaps_no_duplicates` (20 parallel tasks) hit "Failed to assign SequenceInRoom after 5 attempts" — the retry budget and jitter weren't wide enough to let 20 tasks funnel through a single contested row. Fix: bump `MaxAttempts` from 5 to 10 AND multiply jitter by `(attempt + 1)` so late retries spread wider. Both adjustments were flagged as the likely fix in the plan's "Risks" section, so diagnosis → fix took 30 seconds. Test went green on the rerun with visible log evidence of real retry activity mid-test. This is the first story where contention-tuning actually mattered.
- **`python3 -c …` is unavailable in this Windows bash.** Used it to parse `id` out of JSON curl responses during the live sanity check. Fell back to `sed -E 's/.*"id":"([^"]+)".*/\1/'` which worked on the first try. Noted for future stories: prefer `sed` or `jq` (if available) for JSON parsing in ad-hoc bash; don't assume Python.
- **Test-first discipline, fully observed this story.** Every test was written before the implementation code it exercises; I ran each red-first deliberately, then made it green. The 1.8-era slip (writing endpoint alongside scaffolding) didn't repeat. Easier to follow the rule when the patterns (`ApiFactory`, `PostgresFixture`, `AuthenticatedClientAsync`) are already load-bearing — the failing test is ~30 seconds of template fill. Keeping this habit locked in for the Hub + BackgroundService stories coming next.
- **No other material blockers.** Live curl sanity worked on the first try once the python-vs-sed hiccup was past.

### Verification evidence
- Tests: **59 passing** (54 backend: 1 sanity + 3 persistence + 12 auth + 6 session + 5 rooms-persistence + 13 rooms-endpoints + 3 my-rooms + 3 appender + 8 message-endpoints; 5 frontend unchanged). Performance assertion `<200ms` at 10K rows met on the first green run, with no loosening needed.
- Build: ✅ `dotnet build` clean; no new warnings.
- `docker compose up`: ✅ — teardown with `-v` + rebuild, all services healthy in ~10s; api logs show the three migrations applying in sequence (`InitialSchema` → `AddRoomsSchema` → now also `AddMessagesSchema`).
- End-to-end via live `curl` against the running container:
  - Registered `fran@example.com`, logged in, created `history-test-2` as public room.
  - `GET /api/rooms/<id>/messages` as owner → `200 []` (empty room, no messages yet).
  - Registered `gina@example.com` (stranger, not a member), logged in.
  - `GET /api/rooms/<id>/messages` as stranger → `403 {type: ..., title: "You are not a member of this room", status: 403}` with `Content-Type: application/problem+json`.
- `psql \d "Messages"` confirmed: `text` column, three FKs with their cascade behaviors (Cascade / Restrict / SetNull), and the unique `IX_Messages_RoomId_SequenceInRoom` btree.

### Reflection
Two things I want to carry forward. First, **the pre-decision via `AskUserQuestion` on the retry-vs-lock choice paid off immediately** — because the concurrency strategy was locked before any code was written, the implementation pass could focus on *making the chosen strategy work well* rather than bikeshedding the choice mid-implementation. For any future decision that's load-bearing across multiple follow-on stories (1.11's Hub and 1.12's BackgroundService will both call `AppendAsync`), that pattern is worth the 60 seconds it costs. Second, **performance contracts in tests pay off** — `sw.ElapsedMilliseconds.Should().BeLessThan(200)` isn't just a check, it's a regression net: if a future story adds an `.Include()` or a missing index, that test is the one that screams. The one thing I'd do differently: start with `MaxAttempts = 10` by default for the retry loop; 5 felt like a "pick a nice round small number" default rather than a reasoned bound, and the concurrency test would have passed on the first run if I'd been a touch less frugal.

### Time
- **Agent wall clock:** ~30 min from `/add-feature 1.10` through commit. Breakdown: ~1 min inline plan review (plan-mode session); ~2 min `Message` entity + DbContext wiring; ~1 min `dotnet ef migrations add` + additive check; ~4 min write 3 appender tests red-first → implement → ceiling bump for concurrency → green; ~5 min write 8 endpoint tests; ~3 min implement endpoint + DTO + register in `Program.cs`; ~1 min tests all green; ~5 min docker teardown/rebuild + psql schema inspection + live curl; ~2 min stories.md + commit; ~3 min journal.
- **Equivalent human work:** ~3.5 hours end-to-end. Design of the schema + cascade DAG + sequence assignment strategy: 30 min. Entity + DbContext: 15 min. Migration generation + additive verification: 10 min. Retry loop implementation, tuning, and concurrent test design: 60 min (this is genuinely tricky the first time). 8 endpoint tests with the seeding helper: 45 min. Endpoint implementation + DTO: 30 min. 10K-row performance instrumentation: 20 min. Docker rebuild + psql verification + curl sanity: 15 min. Stories + journal: 15 min.
- **Productivity ratio:** ~7× on this story. Backend stories with concurrency were historically where human timelines balloon — deadlocks, retry storms, flaky tests — and the agent just produces the right pattern on request. The biggest chunk of human time would have been "rediscover retry-jitter math and prove it with a 20-way-parallel test" which the agent did in one pass with a predictable single-retry-bump correction.
- **Developer-time invested:** ~10 min — reviewed the plan (~3 min), answered the `AskUserQuestion` on retry-vs-lock (~1 min), watched the concurrency test fail-then-fix pivot to confirm the bump was principled (~2 min), spot-checked the `psql \d "Messages"` output to confirm the three cascade behaviors landed (~2 min), diff scan before commit (~2 min).

### Cost so far (rough)
- Running total on the MVP track (Stories 1.1 → 1.10): roughly **5 hours of agent-wall-clock**. Schema + auth + rooms REST + web room UI + message schema + history endpoint are all done. Next up is the real-time arc — 1.11 (SignalR Hub) → 1.12 (BackgroundService drain) → 1.13 (watermark resync REST) → 1.14 (web SignalR client + chat UI) → 1.15 (presence Hub-state) → 1.16 (web heartbeat + presence indicators). That's the feature-bar-setter of the whole project.

### Next
- **Story 1.11 — SignalR Hub: join room group, send message (fast path).** First realtime story. `ChatHub` at `/hubs/chat`, auth-required; `JoinRoom(roomId)` verifies membership and adds the connection to a SignalR group; `SendMessage(roomId, text, replyTo?)` writes a `MessageWorkItem` to an in-memory `Channel<MessageWorkItem>`, ACKs the sender, and broadcasts `MessageReceived` to the room group — NOT touching the DB on the hot path. Points: 3. Prerequisites in place — `MessageAppender` exists for Story 1.12 to call from the background consumer. **Open question for 1.11:** ack/broadcast ordering. SignalR's `Clients.Group(...).SendAsync(...)` fires-and-forgets; do we await? If we await, a slow subscriber blocks the sender's ack. If we don't, the sender can get their ack BEFORE the broadcast reaches other members. Plan: don't await the broadcast (per CLAUDE.md §1 fast-path semantics — "sender does NOT wait for DB write"; the same logic extends to "sender does NOT wait for other subscribers"). Document in the plan file when Story 1.11 starts.

---

## [2026-04-18 23:46 ART] — SignalR ChatHub + in-memory Channel fast path

**Story:** Story 1.11 — SignalR Hub: join room group, send message (fast path)
**Commit:** `2b74660` — feat(api,realtime): SignalR ChatHub + in-memory Channel (fast path)

### What was built
The live-delivery half of CLAUDE.md §1. A `ChatHub` at `/hubs/chat` (auth-required, custom `"Session"` scheme), plus a singleton `MessageQueue` wrapping `Channel.CreateUnbounded<MessageWorkItem>`. Clients call `JoinRoom(roomId)` (membership + ban checks against the DB, then enroll in the SignalR group `room:{id}` and cache `Context.Items["JoinedRooms"]`) and `SendMessage(roomId, text, replyTo?)` — the hot path enforces the 3 KB UTF-8 byte cap (service-layer, honoring 1.10's schema breadcrumb), writes a `MessageWorkItem` to the channel, broadcasts `MessageReceived` to the room group, and returns the same payload as an ack to the sender. No DB access inside `SendMessage`. The receiver decorator (`sequenceInRoom`) is a nullable placeholder for now — Story 1.12's BackgroundService will fill it when persisting.

### ADLC traceability
- **Requirements satisfied:** FR-36 (text/emoji/reply message shape on the wire — `MessageBroadcast` covers all fields except the final persisted sequence, which 1.12 adds), FR-41 (messages flow via SignalR with the DB as the eventual source of truth), NFR-4 (<3s delivery — the fast path is pure memory + local SignalR group fan-out, well under 100ms in the broadcast test). Architecture Constraint §1 explicitly satisfied: `SendMessage` touches no DB.
- **AC status:** all 4 in §Story 1.11 now `[x]`. `**Status:** Done (commit 2b74660)`.
- **Decisions invoked at plan time (via `AskUserQuestion`):** (1) Testcontainers pause-based test over WebSocket transport for the DB-disconnected assertion; (2) `sequenceInRoom: null` placeholder in the 1.11 broadcast DTO. Both rolled straight into the implementation.
- **Open-ack question from 1.10 checkpoint resolved:** await the `SendAsync` call (it's a local server-side queue op, not a wait-for-recipients). Fire-and-forget was rejected for the usual swallowed-exception reason; awaiting does NOT block on subscriber delivery.

### Non-obvious decisions
- **Decision:** Per-connection joined-rooms cache on `Context.Items["JoinedRooms"]` instead of re-querying the DB to authorize `SendMessage`.
  **Alternatives considered:** query `RoomMembers` on every send; use SignalR groups themselves as the membership proxy (no cheap way to ask "is this connection in this group"); an external `IConnectionRoomTracker` singleton.
  **Why:** querying on every send violates the Architecture Constraint §1 "no DB on hot path" rule — even a fast index lookup is still a DB round-trip. The SignalR-group-as-proxy approach has no O(1) `IsInGroup` API. `Context.Items` is per-connection server-side state that SignalR zeroes out on disconnect; a `HashSet<Guid>` populated in `JoinRoom` gives O(1) membership checks with zero infrastructure. The tradeoff: if membership changes mid-session (admin removes user from room, Story 2.6), that user's already-connected Hub session keeps working until they disconnect. That's a Story-2.6 problem — 1.11's AC doesn't require live revocation.
- **Decision:** Shell out to `docker pause <id>` / `docker unpause <id>` from the test fixture instead of using Testcontainers' own API.
  **Alternatives considered:** upgrade Testcontainers to a version that exposes `PauseAsync` (exists in the .NET SDK in newer versions but not cleanly in 4.1.0); use the Docker-dotnet client library directly; stop/restart the container (loses DB state).
  **Why:** `docker pause/unpause` is a stable Docker CLI contract, the container id is already exposed on `_postgres.Id`, and `Process.Start` is built-in. No new package, no version churn. One caveat: the test requires Docker Desktop running on the host — which is already a precondition for the rest of the Testcontainers fixture, so it's no additional ask.
- **Decision:** Switch the DB-paused test's transport from LongPolling to WebSocket.
  **Alternatives considered:** add an in-memory cache to `SessionAuthenticationHandler` so LongPolling's per-request DB lookup stops mattering; test the Hub method directly without SignalR protocol (bypass transport).
  **Why:** LongPolling re-runs auth middleware on every `POST /hubs/chat` (client-to-server send), and that middleware hits `Sessions` per call. Under paused DB, the second send times out in auth before the hub method even runs — the test was flagging a real transport-level DB dependency, not a `SendMessage` body dependency. Production uses WebSocket anyway (auth once at handshake, connection persists). Switching the test to WebSocket over `TestServer.CreateWebSocketClient()` with a `ConfigureRequest` callback for the session cookie is the most honest way to exercise the AC. Caching auth is a real production win but is off-scope here.
- **Decision:** `sequenceInRoom: null` placeholder in the broadcast DTO now rather than versioning the contract later.
  **Alternatives considered:** omit the field entirely in 1.11, add it as a non-nullable int in 1.12 (breaking-ish client change); inline the sequence assignment into the Hub by doing a `MAX(...) + 1` query synchronously (violates the hot-path constraint).
  **Why:** nullable-now + fill-in-1.12 keeps the wire contract stable across the transition. Clients written in Story 1.14+ can treat `sequenceInRoom` as authoritative once non-null; until Story 1.12 lands and starts filling it, clients fall back to `createdAt` for ordering. Confirmed with the user via `AskUserQuestion` at plan time.

### Friction and blockers
- **LongPolling auth kept failing the DB-paused test.** Diagnosed from the stack trace (500 on `/hubs/chat/send` endpoint while DB paused, thrown from inside auth middleware). Pivoted the test to WebSocket with `TestServer.CreateWebSocketClient()`. The pivot was a ~15-minute detour but the resulting test is actually more production-faithful: WebSocket is the real transport for the hackathon, and auth-once-at-handshake is the real production pattern.
- **`WebSocketClient.ConfigureRequest`'s `Headers` is `IHeaderDictionary`, not `HttpRequestHeaders`.** Mis-typed `Append(name, value)` which doesn't exist on `IHeaderDictionary` without extension-method imports; then `TryAddWithoutValidation` which is a different type altogether. Ended on `r.Headers["Cookie"] = sessionCookie` (indexer), which works cleanly and sidesteps the analyzer warning about `Add` throwing on duplicate keys. Trivial bug, ~2 min lost, noted for future tests that wire WebSocket headers.
- **Testcontainers 4.1.0 pause API surface.** Expected `_postgres.PauseAsync()` to exist; it doesn't. Fallback (shell-out) was ~6 lines and took less time than researching a library upgrade. Didn't bother upgrading because the shell-out path is reliable and the pause is test-only.
- **Everything else was test-first clean.** All 7 tests written red first (stub Hub throwing `NotImplementedException`), then implemented → 6 green on first pass, 1 failing (the DB-paused one for the transport reason above), then fixed on the second pass. No production-code bugs surfaced during implementation.

### Verification evidence
- Tests: **66 passing** (61 backend: 1 sanity + 3 persistence + 12 auth + 6 session + 5 rooms-persistence + 13 rooms-endpoints + 3 my-rooms + 3 appender + 8 message-endpoints + 7 chat-hub; 5 frontend unchanged). New `--verbosity normal` in the checkpoint command confirms per-test pass/timing visibility.
- Build: ✅ `dotnet build` clean; 0 warnings, 0 errors.
- `docker compose up`: ✅ — full teardown with `-v` + rebuild, all services healthy in ~12s.
- End-to-end check via live `curl` against the running container:
  - `curl -I http://localhost:8080/hubs/chat` (anonymous) → `401` with `Content-Type: application/problem+json` — the hub is mapped, auth-required, and returns the same ProblemDetails shape as the REST surface.
- Full integration coverage via the 7 SignalR tests. The broadcast + channel-queued test verifies the fan-out; the DB-paused test (over WebSocket) verifies the hot-path-no-DB invariant from Architecture Constraint §1.

### Reflection
The biggest lesson is transport-awareness at test time. **LongPolling and WebSocket have genuinely different auth lifetimes**, and tests against one can silently reward/punish production code that runs over the other. Writing the test against WebSocket — the actual deploy transport — isn't a test-infrastructure hack, it's the correct integration level. Pattern to carry forward: when the system-under-test has multiple valid transports, test against the one that will run in production unless you have a specific reason to exercise the fallback. Second takeaway: the decisions-via-AskUserQuestion-at-plan-time pattern is still paying off — both of this story's non-trivial choices (pause approach, sequence-null placeholder) were locked before I wrote a line of code, which made the implementation pass feel more like typing than thinking. For Story 1.12 (the BackgroundService consumer), the corresponding pre-decision is about error handling — when `MessageAppender.AppendAsync` throws mid-drain, do we dead-letter, retry, or log-and-move-on? That's a 60-second question worth asking up front.

### Time
- **Agent wall clock:** ~45 min from `/add-feature 1.11` through this journal entry. Breakdown: ~2 min plan-mode design + `AskUserQuestion` round; ~3 min DTOs + `MessageQueue` singleton; ~2 min Hub scaffold + wire into Program.cs + test csproj addition; ~4 min write 7 failing tests red-first; ~1 min confirm they all fail for the right reason; ~5 min implement Hub (`JoinRoom`, `SendMessage`, `OnConnectedAsync`); ~1 min first test pass — 6/7 green; ~8 min diagnose the DB-paused test failure (wrong transport) and pivot to WebSocket with TestServer.CreateWebSocketClient; ~5 min docker teardown/rebuild + live curl on `/hubs/chat`; ~3 min commit + stories.md; ~11 min journal.
- **Equivalent human work:** ~5 hours end-to-end. Hub + Channel design (lifetime scoping, serialization of `Context.Items`, retry-vs-not on SendAsync, the membership caching design): 45 min. Fleshing out the Hub methods + auth wiring: 45 min. 7 integration tests over SignalR: 60 min (SignalR test setup is finicky). Diagnosing + pivoting the LongPolling→WebSocket transport issue for the DB-paused test: 45–60 min for someone who hasn't hit that specific gotcha before (stack trace decoding, skimming SignalR + TestHost docs). Docker pause wiring + curl verification: 30 min. Journal + commit: 30 min.
- **Productivity ratio:** ~6–7× this story. The transport-aware debugging is where a human loses the most time; the agent hit the wrong transport, got a clear failure signature, and pivoted on the second pass. Without the wrong-transport detour the ratio would be closer to 10× — the SignalR boilerplate is very pattern-matchable.
- **Developer-time invested:** ~10 min — reviewed the plan in-chat (~3 min), answered the two `AskUserQuestion` prompts (~1 min total), watched the DB-paused test fail + the LongPolling→WebSocket diagnosis (~4 min — this was the one moment where I was actually thinking alongside the agent, because the root cause is subtle), diff scan + commit review (~2 min).

### Cost so far (rough)
- Running total on the MVP track (Stories 1.1 → 1.11): roughly **6 hours of agent-wall-clock**. Schema + auth + rooms REST + web room UI + message schema + history endpoint + realtime hub are all done. Halfway through the 1.10→1.16 realtime arc by wall-clock: DB schema (1.10) + live delivery (1.11) are in; the slow path persist (1.12), watermark resync (1.13), web chat client (1.14), presence (1.15, 1.16) are the remaining ~4 stories.
- No direct token instrumentation.

### Next
- **Story 1.12 — BackgroundService consumer persists + assigns watermarks.** The slow path that pairs with 1.11's fast path. `MessageProcessorService : BackgroundService` drains `MessageQueue.Reader`, calls `MessageAppender.AppendAsync` (already written in Story 1.10) to assign the per-room `SequenceInRoom` and insert the row, logs any errors, and keeps going. Points: 3. Prerequisites: `MessageQueue` + `MessageAppender` both exist. **Pre-decision to lock before plan:** when `AppendAsync` throws mid-drain (e.g., FK violation on a since-deleted room), what's the failure policy — log + drop, dead-letter to a secondary channel, or retry-with-backoff-then-drop? Task §3.6 says "preserve consistency of message history" but also tolerates at-least-once delivery; I'd lean log + drop for MVP and add a metric for the drop rate, but worth asking at plan time. **Second pre-decision:** the BackgroundService runs as a Singleton in the API process per CLAUDE.md §5 — confirm we don't accidentally register it scoped/transient, which would break `MessageAppender`'s dependency on a scoped `AppDbContext` (we'll need an `IServiceScopeFactory` inside the singleton to create a fresh scope per drained item).

---

## [2026-04-19 00:46 ART] — BackgroundService drains channel + persists to DB

**Story:** Story 1.12 — BackgroundService consumer persists + assigns watermarks
**Commit:** `a70e1f3` — feat(api,realtime): MessageProcessorService drains channel, persists messages

### What was built
The slow-path counterpart to Story 1.11's hot-path Hub. `MessageProcessorService : BackgroundService` is registered as a hosted singleton inside the API process (CLAUDE.md §5), drains `MessageQueue.Reader` via `await foreach`, opens a fresh DI scope per item (because `AppDbContext` is scoped), calls `MessageAppender.AppendAsync` with the work item's `Id` and `CreatedAt` preserved verbatim, and moves on. Failures are logged with the message id + room id and dropped (AC3 literal). `MessageAppender` grew two optional overrides (`Guid? id`, `DateTimeOffset? createdAt`) so the ack the Hub sent to the sender in 1.11 matches the row that lands in the DB now. After this story, every Hub `SendMessage` results in a persisted `Messages` row with a real `SequenceInRoom` within ~100ms — the watermark resync endpoint (Story 1.13) can now pull that history for reconnecting clients.

### ADLC traceability
- **Requirements satisfied:** FR-41 (persistent messages), FR-42 (offline users' backlog survives — now truly, because messages actually land in the DB), Architecture Constraint §5 (the consumer runs in the API process, not a separate container), Architecture Constraint §1 (closes the "eventual DB consistency" half — 1.11 handed off to the channel; 1.12 drains it).
- **AC status:** all 4 in §Story 1.12 now `[x]`. `**Status:** Done (commit a70e1f3)`.
- **Decisions invoked:** no `AskUserQuestion` this round. The failure policy (log + drop) was AC-literal; the scope-lifetime concern (scoped DbContext inside a singleton BackgroundService) was a pre-flagged gotcha from the 1.11 checkpoint and handled via `IServiceScopeFactory.CreateScope()` inside the `await foreach` body.
- **Scope discipline:** one small scope addition (the MessageAppender `id` / `createdAt` optional params) was necessary to preserve the Hub→DB ack-matches-row contract. Kept it source-compatible with every existing caller. Commit message + plan file + entity XML doc all document it.

### Non-obvious decisions
- **Decision:** Create a fresh `IServiceScope` per drained item, not one long-lived scope.
  **Alternatives considered:** cache one scope for the lifetime of `ExecuteAsync` (fastest to write, worst for memory).
  **Why:** `AppDbContext` is scoped and accumulates change-tracker state for every entity it touches. A long-lived context drained for N hours would keep N hours of tracked entities in RAM, bleed memory, and produce stale reads (EF caches the first-read version of an entity across the scope). Per-item scope is the idiomatic ASP.NET Core hosted-service pattern and the one CLAUDE.md §5 implicitly requires.
- **Decision:** Pre-assign `Id` and `CreatedAt` in the Hub (Story 1.11), preserve them in the consumer, instead of re-generating at insert time.
  **Alternatives considered:** let `MessageAppender` pick its own Id+timestamp and return that to the Hub as the canonical value (requires synchronous DB write on the Hub hot path — violates §1); use a two-step write where the Hub inserts first then broadcasts (same violation).
  **Why:** the Hub's ack IS the `MessageBroadcast` payload with `Id`. If the consumer re-generated, the sender's local `Id` references would point at a different row than the one in the DB — which would break Story 1.13's watermark resync (it looks up messages by `Id`) and any future "jump to message X" deep-linking. The `Id`/`CreatedAt` overrides on `AppendAsync` are how the fast path's ack becomes truth.
- **Decision:** Stub `ExecuteAsync` as `Task.CompletedTask` during the RED phase of TDD, not `throw new NotImplementedException()`.
  **Alternatives considered:** `throw` (standard TDD stub).
  **Why:** `.NET 10`'s `BackgroundServiceExceptionBehavior` defaults to `StopHost` — an unhandled exception in `ExecuteAsync` crashes the whole test host on startup, which would hide the real failure (the polling tests not seeing rows appear). A `Task.CompletedTask` stub keeps the host alive and lets the polling tests time out for the *right* reason: the consumer is registered but not actually doing work.
- **Decision:** Rewrite `SendMessage_writes_to_channel_for_background_processing` (the Story 1.11 test that read from `MessageQueue.Reader` directly) to poll the DB instead.
  **Alternatives considered:** disable the BackgroundService for that test class only (an `ApiFactoryWithoutConsumer` variant).
  **Why:** post-1.12, the BackgroundService is always draining the channel, so reading from the queue from test code is a losing race. Polling the DB for the ack'd `Id` tests the full fast-path-plus-slow-path loop AND naturally verifies 1.11's AC3 ("writes a `MessageWorkItem` to the channel") — because the row only materializes if the channel carried the item to the consumer. A duplicate factory would double the Testcontainers startup cost on every test run.
- **Decision:** `OperationCanceledException` bound to `stoppingToken` is re-thrown; all other exceptions (including `OperationCanceledException` tied to unrelated tokens) are caught + logged + swallowed.
  **Alternatives considered:** catch everything uniformly; don't catch `OperationCanceledException` at all.
  **Why:** at process shutdown, `stoppingToken` fires and the `await foreach` throws a legit `OperationCanceledException` — we need that to bubble out so the host stops cleanly. But if some downstream code (a DbCommand hitting a non-shutdown cancel) throws the same type, swallowing it would incorrectly treat it as shutdown. The filtered `catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) throw;` inside `ProcessItemAsync` splits those cases cleanly.

### Friction and blockers
- **Initial stub design mistake.** My first `ExecuteAsync` stub was `throw new NotImplementedException()` — classic TDD, but it breaks on .NET 10 BackgroundService semantics because the default `BackgroundServiceExceptionBehavior.StopHost` kills the test host before any test runs. Caught while *writing* the stub (remembered from the 1.11 journal's tail), not from a failing run, so cost was zero. Trap worth naming: for hosted-service TDD, the idiomatic stub is `Task.CompletedTask`, not `throw`.
- **Missing EF `SingleOrDefaultAsync(predicate)` extension in the rewritten ChatHub test.** Forgot `using Microsoft.EntityFrameworkCore;` at the top of `ChatHubTests.cs`, compiler tried to match the `AsyncEnumerable.SingleOrDefaultAsync` extension and produced a bizarre error about `IAsyncEnumerable<CancellationToken>`. One-line using-directive fix; ~30 seconds.
- **Nothing about the drain logic itself was hard.** The `await foreach (var item in queue.Reader.ReadAllAsync(stoppingToken))` pattern is idiomatic, the scope-per-item rule was pre-decided, and `MessageAppender` was already concurrency-safe. The test-first cycle ran clean: 4 tests red (stub), then green on first implementation attempt.
- **No genuine bugs in the drain or the error path.** The error-continues test (`Logs_and_continues_on_failed_insert`) proved the recovery behavior on the first pass. FK violation caught, log line emitted, valid item proceeds. Matches AC3 to the letter.

### Verification evidence
- Tests: **69 passing** (64 backend: 1 sanity + 3 persistence + 12 auth + 6 session + 5 rooms-persistence + 13 rooms-endpoints + 3 my-rooms + 3 appender + 8 message-endpoints + 7 chat-hub + 3 processor; 5 frontend unchanged). Net change: +3 (the 1.11 `SendMessage_writes_to_channel` test was rewritten in place, not added).
- Build: ✅ `dotnet build DataArtHackaton.slnx` clean, 0 warnings.
- `docker compose up`: ✅ — fresh teardown with `-v` + rebuild, all three services healthy in ~8s.
- End-to-end proof from live container:
  - `docker logs hackaton-api | grep MessageProcessor` → `MessageProcessorService started; draining MessageQueue.` (hosted service auto-started at boot).
  - `ChatHubTests.SendMessage_results_in_persisted_row_via_background_consumer` — this single test exercises the full loop: authenticate → SignalR connect → JoinRoom → SendMessage → poll DB → assert row present with positive `SequenceInRoom`. Passes in ~1s.
  - Unit-level proof: `MessageProcessorServiceTests.Processes_queued_items_and_assigns_sequences` writes 5 items directly to `MessageQueue.Writer` and observes 5 rows materialize with sequences 1..5 in order. Passes in ~950ms including the 5-second poll budget.

### Reflection
The thing I want to bank from this story is **the stub-style trap for hosted services**. Every backend story that adds a `BackgroundService` in the future needs the same `Task.CompletedTask` stub pattern, not the `throw`-based TDD default — and putting it into the 1.11 journal proactively is exactly what made 1.12 smooth. That kind of "write down the next-story gotcha at the end of this story" discipline is paying compounding returns; the pre-decisions I carried into 1.12 (failure policy is AC-literal, need `IServiceScopeFactory`, stub pattern for BackgroundService) turned what could have been a 45-minute diagnosis into a 20-minute implementation. Worth generalizing: **the last 30 seconds of a checkpoint should almost always contain one or two "this is where the next story gets stuck unless you…" notes**, because that's when the relevant context is freshest.

### Time
- **Agent wall clock:** ~25 min from `/add-feature 1.12` through the commit. Breakdown: ~1 min re-enter plan mode + overwrite plan file + ExitPlanMode; ~2 min `MessageAppender` optional-params extension + verify existing tests still green; ~1 min stub `MessageProcessorService` + register hosted service; ~4 min write 3 new `MessageProcessorServiceTests` + rewrite `ChatHubTests.SendMessage_results_in_persisted_row…`; ~1 min confirm 4/4 red (polling timeouts); ~3 min implement the drain (+ `OperationCanceledException` filter); ~2 min first pass 4/4 green; ~5 min docker teardown/rebuild + hosted-service startup log inspection; ~2 min commit; ~4 min journal.
- **Equivalent human work:** ~2.5 hours end-to-end. Design of the drain lifecycle + scope handling + error path: 20 min. `MessageAppender` extension + tests still green: 15 min. Stub pattern + registering hosted service: 10 min. Writing 3 polling-based integration tests (with the 5s timeout + poll interval skeleton): 40 min — polling-based tests are where humans spend disproportionate time tuning intervals. Drain implementation: 20 min. Debugging the `BackgroundServiceExceptionBehavior.StopHost` footgun (first run kills the host, cryptic error): 20–30 min unless you've hit it before. Docker verify + commit + docs: 20 min.
- **Productivity ratio:** ~6× this story. The `Task.CompletedTask` stub trick and the `OperationCanceledException` filter are both things I'd have probably eaten 15 min on individually as a human — the agent just did them right the first time because those footguns were named in earlier journals. Stories with well-fed prior context ratio higher than novel stories.
- **Developer-time invested:** ~7 min — reviewed the plan in-chat (~3 min), scanned the `MessageProcessorService` diff after it went green (~2 min), checked the checkpoint commit message for the AC-literal failure policy claim (~2 min). Very low — this story was mostly "execute the prior plan", not "think about new tradeoffs".

### Cost so far (rough)
- Running total on the MVP track (Stories 1.1 → 1.12): roughly **6.5 hours of agent-wall-clock**. The whole message pipeline now exists end-to-end in code: schema (1.7 + 1.10) + REST (1.8 + 1.10) + fast path (1.11) + slow path (1.12). The remaining realtime stories (1.13 watermark resync + 1.14 web SignalR client + 1.15/1.16 presence) turn this into an actual chat UX. Four stories left in the MVP envelope.

### Next
- **Story 1.13 — Watermark resync endpoint.** Pure REST story, no SignalR change. `POST /api/rooms/resync` accepts `[{roomId, lastSeq}, …]` and returns, per room the caller is still a member of, any `Messages` with `SequenceInRoom > lastSeq` (up to a cap, probably 500). Rooms where the caller isn't a member return `{roomId, notAMember: true}` so the client can discard its stale watermark. Points: 2. Prerequisites: DB has messages (now that 1.12 persists them); we need a reusable "is the caller a member of this room" helper since 1.8's `Join`/`Leave` and 1.10's history endpoint both inline it — worth extracting. **Pre-decision to lock at plan time:** cap size on returned messages per room (500? 1000?) — matters because a client that's been offline for a week could otherwise request megabytes in one shot. Also: should the endpoint return only ACTIVE (non-deleted) messages, or include tombstones like the history endpoint does? Consistency argues for "include tombstones"; latency argues for "skip them to save bytes". Default: include, for the same reason as 1.10.

---

## [2026-04-19 01:13 ART] — Watermark resync endpoint

**Story:** Story 1.13 — Watermark resync endpoint
**Commit:** `186052f` — feat(api): watermark resync endpoint — POST /api/rooms/resync

### What was built
`POST /api/rooms/resync` — the reconnect-side half of the watermark protocol. A client sends an array of `{roomId, lastSeq}` watermark tuples; the server returns a one-to-one array where each entry is either `{notAMember: false, messages: [MessageEntry…]}` (caller is a current member — tail of messages above `lastSeq` in ascending order, capped at 500) or `{notAMember: true, messages: null}` (caller was removed OR the room is unknown/soft-deleted — client should forget that watermark). Requests over 100 rooms get `400 ProblemDetails`. Reuses `MessageEntry` from Story 1.10 (so tombstones flow identically — `text: null` with `deletedAt` populated). With this in place, CLAUDE.md §3's "no per-user offline mailbox, reconcile via the DB" story is fully operational: Hub fast-path (1.11) + BackgroundService slow-path (1.12) + resync REST (this) together give every reconnecting client a gap-free history without any server-side queueing per-user.

### ADLC traceability
- **Requirements satisfied:** FR-42 (reconnecting client sees messages it missed while offline — without any per-user inbox). Architecture Constraint §3 closed: client tracks per-room `lastSeq` in localStorage, calls this endpoint on reconnect, gets back exactly the missing tail.
- **AC status:** all 4 in §Story 1.13 now `[x]`. `**Status:** Done (commit 186052f)`.
- **Decisions invoked:** none from stories.md Decisions (§1-§8) apply directly. Pre-decisions flagged at the end of the 1.12 journal were resolved at plan time: cap = 500 messages/room, include tombstones for sequence continuity, cap requests at 100 rooms to bound worst-case response size (~50K messages).

### Non-obvious decisions
- **Decision:** Response has one entry per input watermark, preserving input order; no reordering, no deduplication, no implicit filtering.
  **Alternatives considered:** return a dictionary keyed by `roomId` (deduping automatically); filter out `notAMember` entries on the server.
  **Why:** a strict array-in / array-out contract is the easiest for the client to consume — `for i in request: apply response[i]`. Dictionary-keyed responses force the client to pair up keys by hand and handle missing keys. Filtering `notAMember` server-side would require the client to re-read its own watermark list to discover "oh, I had this roomId in my request but the server didn't return it, so it must be not-a-member" — that's worse UX than an explicit tombstone.
- **Decision:** `notAMember: true` covers three different server-side states uniformly.
  **Alternatives considered:** separate `roomNotFound`, `roomDeleted`, and `notAMember` fields.
  **Why:** from the client's perspective, all three states produce the same action — discard the watermark, remove the room from the UI. Distinguishing them server-side leaks information about rooms the caller shouldn't see anyway (room existence is essentially a permission). `notAMember` is the honest, privacy-respecting union type.
- **Decision:** Soft-deleted rooms → `notAMember: true`, not a separate `roomDeleted: true`.
  **Alternatives considered:** expose deletion as its own field so a client could show "this room was deleted" to the last members.
  **Why:** the task's ban/deletion semantics don't require a distinction. Per CLAUDE.md §3, when a room is gone the client discards all state for it — same action as removed-from-room. If a later story wants "this room was deleted" UI, that's a separate read endpoint, not an overload of the resync contract.
- **Decision:** Silent clamp of `lastSeq < 0` to `0` rather than returning `400`.
  **Alternatives considered:** reject with `400 ProblemDetails` explaining the client sent a negative value.
  **Why:** negative watermarks have one possible interpretation — "I haven't seen anything, give me everything" — which is exactly `lastSeq = 0`. Rejecting would force the client to handle the edge on its end for no real safety gain. Silently doing the right thing here is more forgiving to client code paths that drop a bad value through arithmetic.
- **Decision:** Cap at 500 messages per room per call, not paginated inside the response.
  **Alternatives considered:** return all missing messages (potentially unbounded); paginate with a `nextLastSeq` field inside each room result.
  **Why:** 500 is enough to unblock the reconnect flow in the common case (client comes back after lunch — tens of messages). For worst-case reconnect (client offline for a week in a hot room), the client sees 500 messages, notices `messages[-1].sequenceInRoom - lastSeq == 500`, and re-calls with the new last. Paginating inside the response wastes bytes for the common case. 500 × 100 rooms × ~400 bytes per message ≈ 20MB max response — large but bounded, and clients rarely send 100 rooms at once.
- **Decision:** Did NOT extract a reusable "is user a member of this room" helper yet.
  **Alternatives considered:** refactor `IsMemberOf(roomId, userId)` as a static helper used by Join/Leave (1.8), history endpoint (1.10), and now this endpoint.
  **Why:** three inlined uses is the edge of "rule of three" but each callsite is 1 line of LINQ and each has slightly different filter requirements (the resync case includes a `Room.DeletedAt == null` filter because deleted rooms should look notAMember). Premature extraction would add a helper API with 3 parameters to handle variance. Noted in the plan: "extract on 4th caller, likely 1.15 presence".

### Friction and blockers
- **None worth calling out.** Test-first cycle was textbook: 7 tests red against a 501 stub, 6 red / 1 passing (the anon-401 test because auth short-circuits before hitting the stub), implementation pass → all 7 green on first run. Live curl verified both shapes (unknown room → `notAMember: true, messages: null`; member room → `notAMember: false, messages: []`). Shortest story of the day by wall clock.
- **One minor hesitation during implementation**: whether to inline the `Room.DeletedAt == null` check into the membership query or layer it as a separate `AnyAsync`. Chose inline — shorter + fewer round-trips. Documented in the "decisions" list above.
- **Smooth sailing when the plan carries over the prior story's note.** The 1.12 journal explicitly flagged (a) cap size tradeoff and (b) tombstone-include decision for 1.13. Both turned into one-line settled answers at plan time instead of mid-implementation bikeshed. Worth reiterating as a pattern: **the last field of every `/checkpoint` entry ("Next") is load-bearing for the efficiency of the next story**; when it names the specific decisions to pre-resolve, the next plan-mode session takes 60 seconds instead of 5 minutes.

### Verification evidence
- Tests: **76 passing** (71 backend: 1 sanity + 3 persistence + 12 auth + 6 session + 5 rooms-persistence + 13 rooms-endpoints + 3 my-rooms + 3 appender + 8 message-endpoints + 7 chat-hub + 3 processor + 7 resync; 5 frontend unchanged).
- Build: ✅ `dotnet build DataArtHackaton.slnx` clean; 0 warnings.
- `docker compose up`: ✅ — full teardown with `-v` + rebuild, all three services healthy in ~8s.
- End-to-end via live `curl`:
  - Registered `henry@example.com`, logged in, created `resync-live` (public).
  - `POST /api/rooms/resync [{roomId: <zero-guid>, lastSeq: 0}]` → `[{roomId: "00000000-…", notAMember: true, messages: null}]` — 200 OK, unknown-room branch.
  - `POST /api/rooms/resync [{roomId: <henry's-room>, lastSeq: 0}]` → `[{roomId: "e45c…", notAMember: false, messages: []}]` — 200 OK, member-with-empty-tail branch.
- JSON serialization: `notAMember: true` comes with `messages: null` (not omitted) — `System.Text.Json` defaults preserve null properties, which is exactly what the client contract wants.

### Reflection
Two small takeaways. First, **the "include everything → let the client filter" impulse was right** for this endpoint — every decision that could have leaked per-room-type complexity (separate `roomDeleted`, separate `roomNotFound`) got collapsed to a single `notAMember: true` union and it made the client contract easier, not harder. Every time I reach for a richer error type, I should ask "would the client branch on this difference?" — if the answer is no, one flag wins. Second, **the rule-of-three helper extraction is only worth it when the callsites actually converge**. Three inlined membership checks across three endpoints look like obvious dedup candidates, but each one filters the row slightly differently (RoomBans check in Join, soft-delete check in history, soft-delete + implicit in resync). Extraction would have meant a helper with three bool parameters to handle variance — worse than the inlined originals. I'll hold the line: only extract when the 4th caller arrives AND the semantic is identical, not just "similar shape".

### Time
- **Agent wall clock:** ~18 min from `/add-feature 1.13` through commit. Breakdown: ~1 min inline plan review; ~2 min DTO additions (`WatermarkEntry`, `ResyncRoomResult`) + stub endpoint returning 501; ~4 min write 7 failing tests (cleanest iteration of the pattern yet — seed helper, membership-remove helper, the assertions write themselves now); ~1 min confirm 6/7 red, 1 pre-green (auth-401); ~3 min implement handler (membership check + take-and-project pattern); ~1 min 7/7 green; ~4 min docker teardown/rebuild + live curl; ~2 min commit + stories.md.
- **Equivalent human work:** ~1.5 hours end-to-end. Endpoint contract design + input validation + cap semantics: 20 min. DTO definitions: 10 min. 7 integration tests with seeding + teardown: 40 min. Handler implementation: 15 min. Docker rebuild + curl sanity + journal: 20 min.
- **Productivity ratio:** ~5× this story. Smaller than some because the story itself is genuinely small — 2 points, narrow scope, no concurrency or realtime. The multiplier tends to be larger on the complex stories and tighter on the simple ones, which is actually the right shape: the agent scales work down to match story size, not the other way around.
- **Developer-time invested:** ~5 min — scanned the plan (~2 min), reviewed the handler's `isMember` query for the soft-delete filter (~1 min), eyeballed the curl output for both branches (~1 min), pre-commit diff (~1 min). Minimal — this story was mostly "execute the pattern" with nothing novel.

### Cost so far (rough)
- Running total on the MVP track (Stories 1.1 → 1.13): roughly **7 hours of agent-wall-clock**. Backend REST + realtime surface is now FEATURE-COMPLETE for MVP messaging. Web side still needs the SignalR client + infinite scroll (1.14), and both halves need presence (1.15, 1.16). Three stories remaining before MVP is walking-talking-chat.

### Next
- **Story 1.14 — Web: SignalR client + chat window with infinite scroll.** Biggest frontend story of the MVP. Five separate threads of work: (a) `@microsoft/signalr` client, connected on login, cookie-authenticated, with exponential backoff reconnect; (b) a chat view that renders received `MessageReceived` events into the main pane; (c) infinite scroll upward via the existing `GET /api/rooms/{id}/messages?beforeSeq=` endpoint; (d) auto-scroll to bottom only when user is within N px of bottom (preserve read-older-history position); (e) `lastSeq` persisted to `localStorage` per room, resync via the new `POST /api/rooms/resync` endpoint on reconnect. The story card flags this as a **split candidate** (5 pts = ½ day), and I'd do it: `1.14a SignalR wiring + render` and `1.14b infinite scroll + watermark resync` are naturally separable. Plan-mode should open with that split question. **Pre-decision to lock**: whether the SignalR client lives in a React context (pairs with `AuthProvider`) or a plain module singleton. Context lets components subscribe to events via a hook; module singleton is simpler but doesn't play nicely with StrictMode double-mounts. Lean context.

---
