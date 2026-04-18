---
description: Verify the golden rule (git clone → docker compose up) works cleanly, as a grader would experience it
---

# /verify-deployable

Validate that the project satisfies the hackathon golden rule. This command **verifies**; it does NOT fix. If it finds problems, it reports them and stops — the developer decides what to do.

Run this periodically (every 2-3 completed features) and always immediately before submission.

## Steps

### 1. Clean teardown
Simulate a fresh environment:
```bash
docker compose down -v --remove-orphans
```
The `-v` removes volumes — this is important. Graders do not have your local DB state.

### 2. Fresh build and up
```bash
docker compose up -d --build
```
Wait up to 90 seconds for all services to become healthy. Poll `docker compose ps` if needed.

### 3. Smoke tests

Perform these checks and record each as ✅ or ❌:

- **All services running:** `docker compose ps` — every service shows "running" or "healthy". No "exited" or "restarting".
- **API reachable:** `curl -f http://localhost:<api-port>/health` — returns 200 with expected body.
- **Web reachable:** `curl -f http://localhost:<web-port>/` — returns 200 with HTML.
- **API ↔ DB connectivity:** hit an endpoint that reads from the database (or check `/health` if it validates DB). Expect 200 with real data.
- **Web ↔ API connectivity:** verify the frontend can actually call the API — either via Playwright MCP (load the page, check a fetch happens and succeeds) or by inspecting a page that renders data from the API.

### 4. One-feature end-to-end check

Pick the most recently completed story from `docs/stories.md` and exercise its main happy path end-to-end through the running containers (not via `dotnet run` or `npm run dev` locally). This catches "works in dev mode but not in container" drift.

### 5. Report

Print a status block to the developer:

> **Deployable verification — <timestamp>**
> - Clean teardown: ✅
> - Build and up: ✅ (took <N>s)
> - Services running: ✅ (api, web, db)
> - API /health: ✅ 200
> - Web /: ✅ 200
> - API ↔ DB: ✅
> - Web ↔ API: ✅
> - End-to-end check (Story <ID>): ✅
>
> **Result:** The golden rule holds. Safe to continue / submit.

If any check is ❌:

> **Deployable verification — FAILED**
> - <list passing checks>
> - ❌ <which check failed, with the exact error message or log excerpt>
>
> **Logs (relevant portion):**
> ```
> <paste the relevant logs>
> ```
>
> **Result:** Golden rule is broken. Do NOT submit in this state.
>
> This command does not auto-fix. Developer decision required.

### 6. Cleanup
Leave the containers running. The developer will decide whether to tear down or continue working.

## Why this command exists

Every hackathon participant builds their app, and many ship code that "works on their machine" — their dev server, their locally installed dependencies, their specific database state. The grader does none of that. The grader runs `git clone && docker compose up` and expects it to work. This command simulates that exact experience, so any drift is caught early — not on the submission deadline.