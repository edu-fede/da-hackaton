---
description: Transform the hackathon task description into structured requirements and Jira-compatible stories
---

# /process-task

Read the task description and generate two structured artifacts:
1. `docs/requirements.md` — cleaned, classified requirements with traceability IDs
2. `docs/stories.md` — implementable stories in Jira-compatible markdown format

## Input resolution

Look for the task file in this order:
1. `docs/task.md` — use directly
2. `docs/task.docx` — convert first:
   - Try: `pandoc docs/task.docx -o docs/task.md`
   - Fallback: `python3 -c "from docx import Document; d=Document('docs/task.docx'); print('\n'.join(p.text for p in d.paragraphs))" > docs/task.md`
   - If both fail, ask the developer to paste the task content into `docs/task.md` manually.

If neither file exists, stop and ask the developer where the task is.

## Output 1: `docs/requirements.md`

Produce a structured requirements document with these sections (omit sections that have no content):

```markdown
# Requirements

_Source: `docs/task.md`_

## Functional Requirements

- **FR-1:** <requirement>
- **FR-2:** <requirement>
- ...

## Non-Functional Requirements

- **NFR-1:** <requirement — performance, security, UX, etc.>
- ...

## Extensions (bonus / asterisk features)

- **EXT-1:** <bonus feature>
- ...

## Out of Scope

- <things the task explicitly excludes>

## Open Questions

- <ambiguities to resolve with the developer or decide on explicitly>

## Technical Constraints

- <any constraints called out in the task: language, framework, deployment, etc.>
```

**Rules for extraction:**
- Every FR/NFR/EXT must be atomic (one verifiable thing).
- Use traceable IDs (FR-1, FR-2, NFR-1, EXT-1) — these will be referenced from stories.
- If the task is ambiguous on a point, put it in Open Questions rather than guessing.
- Do not invent requirements. If it is not in the task, it does not belong here.

## Output 2: `docs/stories.md`

Break requirements into ordered implementable stories. Use this format (each story is a top-level `##` section — parseable by Jira MCP tools):

```markdown
# Stories

_Ordered by implementation dependency. Status tracked via acceptance criteria checkboxes._

---

## Story 1.1 — <concise title>

**Summary:** <one-line summary; this becomes the Jira ticket title>

**Description:**
As a <user type>, I want <goal> so that <benefit>.

<Optional: additional technical context>

**Acceptance Criteria:**
- [ ] <testable criterion 1>
- [ ] <testable criterion 2>
- [ ] <testable criterion 3>

**Priority:** High
**Labels:** infra
**Story Points:** 1
**Traces to:** FR-1, NFR-2

**Technical Notes:**
- <optional implementation hints or constraints>

---

## Story 1.2 — ...
```

**Ordering rules:**
1. **Foundations first.** The first 1-2 stories should establish the "golden rule skeleton" — `docker compose up` produces a running (if empty) app. Priority: High.
2. **Core features next.** Stories that cover the minimum functional requirements. Priority: High.
3. **Full feature set.** Remaining functional requirements. Priority: Medium.
4. **Extensions last.** Any EXT-* items. Priority: Low.

**Labels vocabulary:** `infra`, `api`, `web`, `db`, `realtime`, `testing`, `docs`.

**Story points scale (shorthand):**
- 1 = ~30 min of agent work
- 2 = ~1 hour
- 3 = ~2 hours
- 5 = ~half day

Keep stories small. If something is 5+, split it.

## After generating

1. Print a summary:
   > Generated `docs/requirements.md` (N functional, M non-functional, K extensions) and `docs/stories.md` (S stories).
   > **Recommended first story:** Story 1.1 — <title>.
   > **Open questions requiring developer input:** <count, if any>.

2. If there are Open Questions, list them explicitly and ask the developer to resolve before implementation begins.

3. **Do NOT start implementing.** Wait for developer approval of the requirement/story breakdown.

## ADLC note
This command is the first stage of the Agentic Development Life Cycle: task → requirements → stories. The stories format is intentionally Jira-compatible (Summary / Description / Acceptance Criteria / Priority / Labels / Story Points) so that a downstream Jira MCP integration could create real tickets from this file without reformatting.