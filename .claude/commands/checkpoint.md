---
description: Run tests, commit if green, log rich progress entry to docs/journal.md (journal is HALF the evaluation — make entries substantive)
---

# /checkpoint

Verify current work and record it in the development journal. Invoke after every completed feature.

**Important context:** the task author stated explicitly and repeatedly that the development journal is the **most valuable output** of this exercise. It is used to evaluate the developer's process, not just the code. Entries must be substantive, honest, and reflective — not just a list of commits.

## Steps

### 1. Run all tests
- **Backend:** `dotnet test` from repo root
- **Frontend:** `cd src/Web && npm test -- --run && cd ../..`

If **any** test fails: STOP. Report the failing test(s) to the developer. Do not commit. Do not log. Wait for direction.

### 2. Verify deployable state (lightweight)
- Run `docker compose up -d --build`
- Wait up to 60 seconds for services to report healthy
- Hit the API health endpoint (typically `GET /health`) — expect 200

If anything fails to start or respond: STOP. Report the failure. Do not commit.

### 3. Commit
If tests pass AND compose is up:
- `git add -A`
- Write a commit message in the form `type(scope): short description` where:
  - `type` ∈ {feat, fix, refactor, test, docs, chore}
  - `scope` is the affected area (e.g., `api`, `web`, `db`, `infra`, `signalr`)
- `git commit -m "<message>"`

Do NOT push automatically — developer decides when to push.

### 4. Append a substantive entry to `docs/journal.md`

Append using this exact format (fill every field honestly; if a section does not apply, write "N/A" — don't omit):

```markdown
## [YYYY-MM-DD HH:MM ART] — <short title of what was done>

**Story:** <Story ID from stories.md> — <story title>
**Commit:** `<short hash>` — <commit message>

### What was built
<2-4 sentences describing the feature in user-facing or system-facing terms — what the user can now do, or what the system now supports internally. Keep this scannable; the deep technical detail belongs in "Non-obvious decisions" below.>

### ADLC traceability
<Which requirement(s) from requirements.md this satisfies (FR-X, NFR-Y, etc). Which acceptance criteria from the story were checked off. If any criterion was deferred or reinterpreted, note it. Mention which open-question decisions (Decisions §N in stories.md) shaped the work.>

### Non-obvious decisions
<Technical or design decisions that required judgement — not boilerplate. Examples: chosen trade-offs, rejected alternatives, patterns imported from elsewhere, things the developer corrected during planning.>
<Explicit format:
- **Decision:** <what was decided>
  **Alternatives considered:** <what else was on the table>
  **Why:** <the reasoning>>

### Friction and blockers
<What slowed things down? Where did the agent go off-track and need correction? Did tests reveal anything surprising? Did a requirement turn out to be ambiguous?>
<Be honest. "None" is acceptable IF it's really true, but for most features there is something worth noting. This is the content the task author wants most.>

### Verification evidence
- Tests: <N> passing (<N_backend> backend, <N_frontend> frontend)
- Build: ✅
- `docker compose up`: ✅ (healthy in <N>s)
- End-to-end check: <how was the feature exercised — curl, browser, Playwright MCP? What was the outcome?>

### Reflection
<1-3 sentences. What did the developer learn? What would be done differently next time? Is there a pattern worth reusing for later stories? This is the field the task author cares about most — do not skip it.>

### Time

Report TWO distinct metrics:

- **Agent wall clock:** <X> min — actual elapsed time from when the developer invoked `/add-feature` (or equivalent prompt) to when control returned to the developer. Take this from Claude Code's session indicator (`Crunched for ...`) or measure it as real-world elapsed time. This is the literal time the agent worked.
- **Equivalent human work:** ~<Y> min — your honest estimate of how long this same work would take a senior developer doing it manually (no AI assistance), end-to-end. Break it down by phase if useful (design, scaffolding, implementation, debugging, verification, docs). This is a productivity-multiplier metric, not a real-time metric.

Note any meaningful gap. The ratio human/agent over the project is itself a finding worth surfacing in the final wrap-up.

If the developer was actively involved during the story (review loop, decisions, manual checks), also note **(c) developer-time invested** as a third line — to distinguish "watched the agent work" from "actively reviewed and directed".

### Cost so far (rough)
<Approximate tokens or running totals if the info is available. Keep this brief — the time block above is more useful.>

### Next
<What's the logical next story to tackle? Any prerequisites or open questions before starting it?>

---
```

Use the current local timestamp in ART (UTC-3). Separate entries with `---`.

### 5. Report back
Print a concise confirmation to the developer:

> ✅ Checkpoint complete.
> Tests: <N> passing. Commit: `<short hash>`. Journal entry added for Story <ID>.
> Next recommended: Story <next ID> — <next title>.

Do not elaborate further unless asked.

## Notes
- If `docs/journal.md` does not exist yet, create it with a brief header:
  ```markdown
  # Development Journal

  Narrative log of the Hackathon 2026 development process — what was built, why, what went wrong, and what was learned.
  Each entry is produced by the `/checkpoint` command after a completed feature, or by `/journal-note` for mid-feature observations.
  ```
- **Quality over cadence.** One honest, reflective entry is worth ten "✅ added feature X" bullets. Write as if explaining to a thoughtful colleague what the session was actually like.
- If there is nothing substantive to say in Non-obvious decisions / Friction / Reflection sections, the feature was probably too small or the agent was on autopilot — briefly note that, do not pad.
- **About the Time block:** the "equivalent human work" estimate is intentionally subjective. Don't over-think it — a thoughtful guess is more useful than refusing to estimate. The point is to surface the productivity delta of agentic development for the post-hackathon analysis.
