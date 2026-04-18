---
description: Run tests, commit if green, log progress to docs/journal.md
---

# /checkpoint

Verify current work and record it in the development journal. Invoke after every completed feature.

## Steps

### 1. Run all tests
- **Backend:** `cd src/Api.Tests && dotnet test` (or from root: `dotnet test`)
- **Frontend:** `cd src/Web && npm test -- --run`

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
  - `scope` is the affected area (e.g., `api`, `web`, `db`, `infra`)
- `git commit -m "<message>"`

Do NOT push automatically — developer decides when to push.

### 4. Append to journal
Append a new entry to `docs/journal.md` using this exact format:

```markdown
## [YYYY-MM-DD HH:MM] — <short title of what was done>

**What:** <1-2 sentence description of the feature or change>

**Story:** <Story ID from stories.md, if applicable, e.g. "1.3">

**Decisions:** <any non-obvious technical decisions made and why>

**Blockers/Issues:** <problems encountered and how they were resolved, or "none">

**Tokens/Time (rough):** <approx tokens consumed or wall-clock time for this feature>

**Next:** <what the logical next step is>

---
```

Use the current local timestamp. Separate entries with `---`.

### 5. Report back
Print a concise confirmation to the developer:

> ✅ Checkpoint complete.
> Tests: <N> passing. Commit: `<short hash>`. Journal updated.
> Next: <next story or action>

Do not elaborate further unless asked.

## Notes
- If `docs/journal.md` does not exist yet, create it with a brief header:
  ```markdown
  # Development Journal

  Log of features completed, decisions made, and issues encountered during the hackathon.
  ```
- Keep journal entries terse but substantive. Denis will read these; make them useful.