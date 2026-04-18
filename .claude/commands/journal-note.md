---
description: Add a mid-feature observation, reflection, or blocker note to docs/journal.md without running a full checkpoint
---

# /journal-note

Append a lightweight note to `docs/journal.md` to capture something worth recording outside of the normal checkpoint cycle — a friction point, an aha moment, a decision made while planning, a problem encountered, or a reflection on how the agent behaved.

The purpose: ensure the journal captures the **full texture** of the development experience, not just the clean post-feature summary. The task author explicitly asked for notes on "what was the most difficult part, what was the easiest, how you organized your workspace, your difficulties, any notes, nuances, your blueprints."

## Input

The developer's message after invoking the command is the note content. If no content was provided, ask the developer what to record.

## Steps

### 1. Format the entry

Append to `docs/journal.md` using this format:

```markdown
### [YYYY-MM-DD HH:MM ART] Note — <category>

<The observation, written as prose. Can be 1 sentence or 3 paragraphs, whatever fits the thought.>

---
```

Where `<category>` is one of:
- **Friction** — something took longer than expected, the agent got confused, a library didn't behave
- **Decision** — a design choice made outside the normal Plan Mode flow
- **Insight** — something learned about the tooling, the problem, or the agentic workflow
- **Blocker** — an unresolved issue parked for later
- **Meta** — an observation about the ADLC process itself (how the workflow is going)
- **Misc** — anything else worth capturing

### 2. Do NOT run tests or commit

This command is pure journaling. It does not touch code, tests, or git. The entry is added to `docs/journal.md`; that file will be committed with the next `/checkpoint` or manually.

### 3. Report back

> 📝 Journal note added to docs/journal.md (category: <category>).

## Guidance for good notes

Useful:
- "Claude proposed an ORM-heavy approach; I redirected to use Dapper for read-heavy queries. The first plan missed that message history pagination needs to be fast."
- "Getting SignalR to reconnect after a server restart required explicit reconnect config on the client — not obvious from the docs. Worth a lesson in CLAUDE.md."
- "Spent 40 minutes because I conflated 'room admin' with 'room owner' in the initial plan. The task distinguishes them sharply. Re-read section 2.4.7 carefully."

Less useful (avoid):
- "Starting work on story 2.1."
- "Everything is fine."
- "Will continue tomorrow."

These are status pings, not reflections. They do not help the evaluator understand the process.