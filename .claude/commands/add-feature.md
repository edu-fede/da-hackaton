---
description: Implement one story from docs/stories.md following plan → test → implement → verify → commit
---

# /add-feature

Implement a single story end-to-end, with test-first discipline and verification.

## Input

- If `$ARGUMENTS` is provided (e.g. `/add-feature 1.3`), it is the Story ID to implement.
- Otherwise, pick the first story in `docs/stories.md` whose acceptance criteria are not all checked (`- [ ]`).

## Flow

### 1. Understand and Plan (Plan Mode)
- Read the story from `docs/stories.md`: summary, description, acceptance criteria, labels, technical notes.
- If the story is ambiguous or acceptance criteria are unclear, STOP and ask the developer.
- Enter Plan Mode.
- Identify:
  - Files to create or modify
  - Tests to add (unit and/or integration)
  - Risks or unknowns
  - Whether the feature requires `docker-compose.yml` changes
- Present the plan. **Wait for developer approval before proceeding.**

### 2. Write failing tests first
- Write tests that cover the acceptance criteria.
- Run them: they must fail for the right reason (assertion failure, not compile error or missing dependency).
- If a test accidentally passes before implementation, it is not a real test — rewrite it.

### 3. Implement
- Write the minimum code needed to make the tests pass.
- Stay within the story's scope. If you discover work that belongs to a different story, note it and do not do it.
- Follow the conventions in `CLAUDE.md`.

### 4. Verify
Run, in order:
- **Unit tests (all, not just new ones):** must be green.
- **Build:** `dotnet build` / `npm run build` (whichever applies) must succeed with no errors.
- **Lint:** must be clean (no warnings treated as errors).
- **Deployable check:** `docker compose up -d --build` — all services healthy, API `/health` returns 200.
- **Functional check:** manually exercise the feature in the running containers (curl, browser, or Playwright MCP).

If any step fails: STOP, fix, and re-run all verification from the start. Do not commit red.

### 5. Update stories.md
- Mark each acceptance criterion as checked (`- [x]`) only if that specific criterion is actually met.
- If the story is fully complete, add a line at the end: `**Status:** Done (commit <hash>)`

### 6. Commit
- Message format: `<type>(<scope>): <description>`
- `type` ∈ {feat, fix, refactor, test, docs, chore}
- `scope` from story labels (`api`, `web`, `db`, `infra`, etc.)
- Do NOT push — leave that to the developer.

### 7. Checkpoint
After committing, invoke the `/checkpoint` workflow (or run it inline): run tests, ensure compose is up, append a journal entry.

### 8. Report
Print a concise summary:

> ✅ Story <ID> — <title> complete.
> Acceptance: <X>/<Y> criteria met.
> Commit: `<short hash>`.
> Next recommended: Story <next ID> — <next title>.

## Notes on scope discipline
- If during implementation you find a bug in existing code unrelated to this story, note it in `docs/journal.md` under "Blockers/Issues" of the current entry — do not fix it silently.
- If an acceptance criterion turns out to be impossible or requires a scope change, STOP and ask the developer. Do not silently redefine the criterion.
- Resist adding "just one more thing." The story is the contract.