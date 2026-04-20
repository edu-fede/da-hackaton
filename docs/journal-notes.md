# Journal Notes

Methodological observations captured during the hackathon — workflow patterns, tool usage findings, friction points, self-observed habits, and small insights that didn't fit inside a per-story checkpoint. 

Captured in real time via the `/journal-note` slash command; the command categorizes each entry as Meta / Insight / Friction / Decision / Blocker.

> **Three records of this weekend.** This file captures methodological notes from the work. For the per-feature technical log see [`journal.md`](./journal.md). For personal, discursive reflections on the experience see [`reflections.md`](./reflections.md). Together the three capture the full ADLC cycle — from spec authoring through implementation to methodological reflection.

Each note carries a _Context_ line indicating which story was in progress or which transition the note occurred at — useful for situating the observation inside the weekend's development flow.

---

### [2026-04-18 13:58 ART] Note — Meta

_Context: between Scaffold minimum working stack and User/Session schema + initial EF migration._

Auto mode is trickier than I expected. The instinct is to stay out of the loop so I don't become the bottleneck, but I'm finding the human checkpoint is needed more often than I'd hoped. Concrete example: automatic updates to `CLAUDE.md`. Not only do those deserve my review on their own merits — they also quietly **evict me from the shared context** if I skim the generated plan instead of reading it carefully. Plan mode (my default for every task) helps, but only if I actually read what's been done, not just what's proposed next.

The deeper question I'm parking for later: do I even need to stay abreast of the full context, or is that a losing battle? For `CLAUDE.md` specifically the answer is clearly yes — it's the contract that shapes every future turn. For other artifacts it's less obvious, and "how much context the human keeps" might be the real ADLC design knob.

---

### [2026-04-18 14:12 ART] Note — Insight

_Context: between Scaffold minimum working stack and User/Session schema + initial EF migration._

Mind note: **importance of Plan Mode — verified, several times over.** Building on the earlier Meta note: the cost-benefit math is lopsided in plan mode's favor. A few seconds spent reading a proposed plan routinely saves me from unwanted actions (wrong file touched, over-eager refactor, a command I'd have vetoed if asked). Even on trivial tasks, where the overhead feels unnecessary, the downside of *not* planning dominates the tiny overhead of reading.

Conclusion for this project: **plan mode stays always-on.** I'd rather pay the overhead tax on trivial tasks than re-learn the "I should have checked" lesson on a non-trivial one. The asymmetry is the point.

---

### [2026-04-18 14:18 ART] Note — Insight

_Context: between Scaffold minimum working stack and User/Session schema + initial EF migration._

Companion to the previous note, on the *other* dial: **auto-accept.** It's a velocity multiplier for repetitive mechanical work (scaffolding, edits across many files, test boilerplate) and a liability for architecture work, where I actually want to see the plan and the concrete file changes before anything lands.

Working rule I'm settling on:
- **Default: auto-accept ON + plan mode ON.** Plan mode is the contingency net — I review intent before execution, then let the mechanical work fly.
- **Auto-accept OFF for high-blast-radius changes:** `CLAUDE.md`, `docker-compose.yml`, DB migrations, structural refactors, anything that silently rewires how future turns behave. For these, I want a per-tool confirmation, not just a plan review.

The two dials aren't redundant: plan mode gates the *strategy*, auto-accept gates the *execution*. Turning the right one off at the right moment is the actual skill.

---

### [2026-04-18 14:26 ART] Note — Friction

_Context: between Scaffold minimum working stack and User/Session schema + initial EF migration._

Workspace organization note: I'm running Claude Code in **two surfaces simultaneously** inside VS Code — the integrated terminal CLI *and* the Claude Code extension panel. Two reasons:

1. **Parallelism.** I can have the CLI chewing on a feature implementation while I use the extension for side work like these journal notes, without stepping on the same session.
2. **Input ergonomics.** The CLI's text input is genuinely painful for anything more than a short prompt — no real multi-line editing, no easy paste-and-edit, awkward cursor behavior. For composing longer messages (journal notes, detailed directions, pasted specs) the extension's editor-backed input is dramatically better.

Worth flagging as a process observation: the "one session, one terminal" mental model doesn't match how I actually work. Running two surfaces against the same repo has been the pragmatic answer, with the extension effectively acting as the "writing desk" and the CLI as the "workbench."

---

### [2026-04-18 14:34 ART] Note — Insight

_Context: between Scaffold minimum working stack and User/Session schema + initial EF migration._

`/process-task` output was, frankly, **incredible** — the single biggest "this is why ADLC works" moment so far.

- **~5 minutes wall clock** from invocation to a structured `docs/requirements.md` + `docs/stories.md` pair. No human on Earth produces that volume at that fidelity in that window.
- **Coverage:** 53 FRs + 16 NFRs + 4 EXT items, all with traceable IDs cross-referencing the task sections. Organized by domain, not by order-of-appearance in the source.
- **Discipline I didn't prompt for explicitly:** an explicit **Out of Scope** section (including the task's own waivers like XMPP federation being optional), and a **Open Questions** section flagging genuine ambiguities that deserved human judgement rather than being silently decided.
- **Review cost:** I read the full output and could not find anything material to change. That's the part that surprised me — usually AI output is a *starting point* you edit. Here it was closer to a *finished artifact* you sign off on.

The leverage ratio (input prompt → structured spec) on this command is the strongest argument I have for investing in good slash commands up front. The command file is a couple hundred lines of guidance; it produced a spec I'd normally spend half a day on.

---

### [2026-04-18 14:48 ART] Note — Meta

_Context: between Scaffold minimum working stack and User/Session schema + initial EF migration._

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

_Context: between Scaffold minimum working stack and User/Session schema + initial EF migration._

Concrete evidence for the previous note: in reviewing a Claude Code plan built from a spec that claude.ai had largely drafted, **Claude Code flagged important missing details in the spec itself.** I caught it while reading the plan (plan mode earning its keep, again). When I took that finding back to claude.ai, claude.ai acknowledged the omission as its own error.

Two takeaways worth logging:

1. **Careful human review is still necessary — even when both ends of the loop are the same underlying model family.** "Same LLM on both sides" does not mean the inputs are self-consistent. Different sessions, different context windows, different framings produce different blind spots. Treating claude.ai output as pre-vetted just because "it's Claude" is wrong.
2. **A strange but productive iteration emerges: claude.ai ↔ Claude Code as mutual critics.** Claude Code caught what claude.ai missed; claude.ai, shown the finding, corrected itself cleanly. I'm effectively using the two surfaces to cross-check each other, with me as the courier. Not a workflow I'd have predicted, but it's been useful.

Caveat for future-me: this probably reflects my current tool-usage skill level. A more experienced operator might structure prompts well enough that these gaps don't appear in the first place. For now, the cross-check is load-bearing.

---

### [2026-04-18 15:04 ART] Note — Meta

_Context: between Scaffold minimum working stack and User/Session schema + initial EF migration._

Amusing recursion: even `/journal-note` — the command whose entire purpose is capturing the process — had to be reviewed, revised, and iterated on before I trusted it. The tool built to document the work was itself work.

Not a complaint, just worth naming:

- **Authoring slash commands is slow, and that's okay.** They're reusable, and once they're right the payback compounds across every future invocation. The front-loaded cost is the investment; the per-use cost is near zero.
- **"Right" is slippery for soft/process commands.** `/journal-note` has no tests, no deterministic "does it compile" gate. What makes it *good* is whether the entries it produces are useful to a reader months from now — that's a judgement call, not a pass/fail. So iteration is more about calibration ("does this category scheme catch the things I actually want to record?") than about correctness.
- **Contrast with technical commands:** `/process-task` or `/add-feature` have clearer success signals (spec produced, tests green, feature works). `/journal-note`, `/checkpoint`'s narrative portion, and CLAUDE.md live in the fuzzier category where "good" is an editorial judgement.

Lesson for the rest of the hackathon and beyond: budget real time for the fuzzy commands up front, don't expect them to feel "done" as quickly as the technical ones, and accept that their value compounds silently over many future uses.

---

### [2026-04-18 18:19 ART] Note — Meta

_Context: between Scaffold minimum working stack and User/Session schema + initial EF migration._

A lot of my wall clock is going into *reading* the agent's output — both the streaming Claude Code narration and the final diffs — rather than steering or correcting it. Most of that reading is technically skippable if I were willing to trade oversight for speed, and in some cases that trade is probably fine. Right now I'm deliberately keeping it because the reading is doing double duty: it's how I learn what the agent is actually doing under the hood, and occasionally it catches something that needs a redirect before it compounds.

Worth noting, though: the time I spend *correcting* input/output is minimal so far. That's the interesting signal. If reading is mostly for comprehension and rarely for intervention, then at some point the honest move is to stop reading and trust more — same tension I flagged earlier today on the autonomy dial (see `reflections.md` for the companion note). Parking it for now; I'll keep tracking the ratio of (read-to-understand) vs (read-to-correct) and revisit once I have enough data to know whether the oversight is earning its cost.

---

### [2026-04-18 21:48 ART] Note — Decision

_Context: between Scaffold minimum working stack and User/Session schema + initial EF migration._

Consolidated the initial migration rather than shipping an additive `AddUniqueIndexes` migration on top of it. Acceptable here because no production environment has the first migration applied yet — the schema has never been deployed anywhere persistent, so there is no deployed history to preserve.

This pattern (remove a migration and re-add a modified version) **must not** be used once the schema has been deployed anywhere persistent. After that point, the only safe path is an additive forward migration; rewriting prior migrations retroactively breaks environments that have already recorded them in `__EFMigrationsHistory`.

Noted for future reference — if we hit a second migration, promote this rule into `CLAUDE.md` so the agent stops reaching for the "just collapse them" shortcut once the schema goes live.

---

### [2026-04-18 21:49 ART] Note — Decision

_Context: between Scaffold minimum working stack and User/Session schema + initial EF migration._

Story 1.3 enforces user uniqueness **case-sensitively at the DB level by design**. Case-insensitive semantics (so that `Juan@x.com` and `juan@x.com` are treated as the same identity) are deferred to the Story 1.4 service layer, where registration/login normalize inputs before hitting the DB.

**Risk:** if normalization is inconsistent across endpoints — e.g., registration lowercases but another write path doesn't — two rows differing only in case could coexist, since the DB constraint won't catch them. This is a latent data-integrity bug that stays invisible until someone tries to log in with the "other" casing.

**Mitigation plan:** Story 1.4 tests must include an explicit case-conflict assertion: register `Juan@x.com`, then attempt to register `juan@x.com` — expect a conflict response. Same shape of test for username. This pins the normalization contract at the service boundary and will fail loudly if any future endpoint skips the normalization step.

---

### [2026-04-18 22:23 ART] Note — Insight

_Context: between User/Session schema + initial EF migration and Register + login endpoints with session cookies._

Companion observation to the 14:34 note on `/process-task` — the **user-story generation half** of that run deserves its own entry, because the quality is what actually determines whether the spec is usable downstream.

What landed in `docs/stories.md` in a single pass:

- **Jira-native format.** Each story is already shaped for create/update via the Atlassian Rovo MCP — summary, description, type, priority, story-point estimate, acceptance criteria as a checklist, and parent/epic linkage. No reshaping needed before pushing tickets; the MCP call is basically a for-loop over the file.
- **Estimates that survived first contact.** The point estimates felt reasonable on read and have held up against actuals so far — Story 1.3 was estimated 2pts / ~1h, came in at ~25min wall clock (inside the envelope, not wildly off). A few stories have already had their estimates stress-tested in earlier journal entries, and the reasoning behind them was sound.
- **Priority genuinely reflects the MVP framing from the task.** The first ~16 stories are marked High and, taken together, cover exactly the MVP acceptance criteria the task author called out as the minimum bar. The rest ladder down to Medium/Low by feature weight, not alphabetically or by section order. That's the kind of judgement call I'd expect to argue about in a grooming session, and the agent just… got it right.
- **Descriptions, traceability, and ACs are the real win.** Every story back-links to the FR/NFR IDs from `requirements.md` (which themselves back-link to task sections), so from any story I can walk the chain: story → requirement → original task clause. ACs are concrete and testable, not hand-wavy ("user sees message" vs "POST returns 201 with `{id, createdAt}` and the message appears in the room within 3s").

This is where the ADLC leverage compounds: `/process-task` didn't just produce a spec, it produced a spec that's **directly executable** by the rest of the pipeline (`/add-feature` reads it, the Jira MCP can ingest it, the journal cross-references it). The investment in the command's prompt paid for itself on turn one.

---

### [2026-04-18 21:46 ART] Note — Insight

_Context: between Room sidebar + create-room modal + /api/me/rooms and Message schema + history endpoint + concurrent-safe appender._

Reading the plan for the first real coding task (Story 1.3) and the texture of it hit me differently than I expected. It's not just that the agent thinks of things I wouldn't — that part I'd already internalized. It's that there are considerations I literally **cannot** produce, even with unlimited time, because they require either recall or mechanical discipline I don't have on tap. Three examples from this one plan:

1. It **measured and budgeted xUnit startup cost per test class in seconds** — then surfaced it as an explicit risk in the plan (as CLAUDE.md's Harness Engineering section implicitly requires) and proposed a mitigation. I would never have quantified that, let alone derived a mitigation for it on a first pass.
2. Validation and format choices were anchored to **actual RFCs** — e.g. RFC 5321 for email, similar specs pulled in for other fields. Every one of those citations imports a pile of edge-case rationale I couldn't have reproduced from memory for every field in every feature. The solution inherits a compliance/reliability posture basically for free.
3. It **follows CLAUDE.md patterns (like "Harness Engineering: never commit red, failing-test-first, tight loop") without forgetting them under pressure.** Humans regress to shortcuts when a feature gets long; the agent just… doesn't. The discipline is mechanical, not motivational.

The obvious framing is "AI is faster." The more honest framing, after seeing this plan, is that the agent routinely operates in an **additive** mode (citing specs, budgeting resources, enforcing conventions) that I was implicitly dropping from my own solo workflow because it's too expensive per-feature for a human to sustain. The ADLC payoff isn't really speed — it's that *every* feature now gets the kind of thoroughness I'd previously only afford on the one or two features that seemed to warrant it. That's a qualitatively different codebase.

---

### [2026-04-19 15:54 ART] Note — Insight

_Context: between Story 1.15: Presence tracking (Hub state + heartbeats + AFK timer) and Fix: wait for SignalR Connected before hub.joinRoom (F5 race)._

**Resolution of the bug.** A couple of seconds after I pasted the logs into claude.ai, it had the diagnosis: the REST `POST /api/rooms` path never inserted the owner as a member. The Hub — correctly, per its design — only attaches a caller to SignalR groups they are already a DB member of. So the owner creating a room would connect to the Hub, the Hub would iterate their memberships, find zero, and add them to zero groups. Their own messages broadcast to an empty group, no self-receive, and peers who *were* joined didn't see anything from the owner either (same root). Two visible symptoms, one conceptual gap: the frontend was implicitly assuming the Hub would *join* users, but the Hub's contract is narrower — it only *syncs groups for existing memberships*. Joining a room is a REST responsibility, and on the create-room path it was missing. Same pattern as something I'd already seen earlier in the project: the tests verified exactly what the spec said, and the cross-flow gap (create-room REST handler vs Hub join semantics) only surfaced in manual smoke. Unit tests per-component were all green; nobody was asking the meta-question "does the sequence of REST + WebSocket actions a real user takes actually wire up correctly end-to-end."

**Where the human-in-the-loop actually earns its keep.** Stepping back from this one incident, I think I can now name the pattern I've been sensing for a couple of days: 
- **design, spec, and QA are where my intervention has it's leverage; implementation and in-the-moment micro-decisions are where it mostly isn't**. 

The agent is very good at the latter and genuinely can surprise me on the former, but "can" isn't "will" — the design envelope is set by whoever frames the problem, and if that's me I get a better system; if I hand that off, I get a defensible but often suboptimal one. The fix for this specific bug is itself a design decision (should the Hub auto-join based on DB state? should REST be the sole source of truth for membership? should the server reconcile on connect?), and even though I was perfectly capable of delegating it, I chose to push it through claude.ai first to get the *reasoning* behind the options laid out. 

That turned out to be the higher-value move: I didn't have to spend the time enumerating alternatives myself, but I still got to own the decision — I read through the tradeoffs, mostly agreed with the recommended one, and accepted it *knowing why* rather than accepting it as a black box. That feels like the correct use of the tooling: **not skipping the thinking, but shortening the research phase so I can spend my budget on the validation phase.**

**Keeping the loop.** Related observation that's now happened twice: claude-code's in-situ fix for this was defensible (patch the REST create-room to add the owner membership — minimal, local, self-consistent) but arguably suboptimal vs the broader design question of where membership authority lives and whether the Hub should reconcile defensively. **Claude.ai, with no IDE context and therefore forced to look at it through a design lens, surfaced the broader question and picked the cleaner option.** *That asymmetry matters.* If I'd left claude-code on "auto mode" and let it ship its local fix without a design check, **I'd have a working system *and* a small pool of technical debt** that would only get more expensive to untangle once more code leaned on the current shape. The meta-lesson: 
- "auto mode" is fine for mechanical work, actively dangerous for anything that touches a contract boundary between subsystems. I need to be the one who flags when a fix is at a boundary and pulls it out of auto mode into a design conversation — because claude-code, by design, is not going to pull the emergency brake on itself.

---

### [2026-04-19 15:57 ART] Note — Insight

_Context: between Story 1.15: Presence tracking (Hub state + heartbeats + AFK timer) and Fix: wait for SignalR Connected before hub.joinRoom (F5 race)._

**Residual bug from the owner fix.** The earlier membership-authority fix (Hub reconciles DB memberships to SignalR groups on connect) resolved the *owner* case but left the *joiner* case uncovered. Symptom: a user browsing the public-rooms catalog and clicking a room would land on `RoomPage`, the Hub would connect, iterate their memberships, find none for this room, and leave them out of the group. Root cause: the frontend was entering the room by calling the Hub's `JoinRoom` directly and skipping the REST `POST /api/rooms/{id}/join` endpoint entirely — so the DB never learned the user was a member, and the Hub (correctly) refused to group-attach a non-member. The fix is tiny: always call `POST /join` on `RoomPage` mount *before* opening the hub connection. The backend endpoint was already idempotent, so repeated calls for existing members are harmless.

**Pattern.** That's now twice in a weekend that the same shape of bug has surfaced: a role's path through the system (owner creating, joiner entering, sender sending, receiver reading) is individually well-tested, but the *interaction* between two roles or two subsystems only breaks in multi-actor manual smoke. Unit and integration tests per role cover each role in isolation. The Hub tests mock a member; the REST tests assert the endpoint shape; the frontend tests inject a fake hub client. Every layer is green. But nothing in the suite plays "user A creates a room while user B, in a different browser, joins it from the catalog" — because that requires two stateful clients acting concurrently. I don't think unit tests are the right place to catch this; the cost of simulating concurrent clients in a unit test is higher than the cost of the bug. The right tool is a small Playwright multi-context smoke that scripts the two-role interaction and runs on CI, or at minimum a named manual smoke script run before each checkpoint. Something to factor in when I plan the "E2E hardening" pass after the MVP is green.

---

### [2026-04-19 16:55 ART] Note — Meta

_Context: between Story 1.15: Presence tracking (Hub state + heartbeats + AFK timer) and Fix: wait for SignalR Connected before hub.joinRoom (F5 race)._

**Milestone achieved!** Fixed, at last. Two browsers, two different users, two messages sent and rendered on both sides in real time. The first end-to-end, honest-to-god, look-ma-it's-a-chat moment of the project. All of the architecture the task demanded is actually doing its job under the hood: SignalR on the socket, the in-memory `Channel<T>` buffering messages between Hub and consumer, `BackgroundService` draining to Postgres on the slow path, per-room watermarks, idempotent REST for room/membership concerns, Hub reconciling DB state into SignalR groups on connect. Everything that was just diagrams on Saturday afternoon is a live system now.

**Rough time accounting so far.** Worth banking before the numbers blur further:

- **~4–6 hours: setup and shaping.** CLAUDE.md, the `/process-task` → `/add-feature` → `/checkpoint` → `/verify-deployable` → `/journal-note` command chain, the requirements/stories pass, initial Plan-Mode work. Front-loaded investment in the agent's context and workflow. No production code written in this window.
- **~12 hours: implementation wall-clock.** Stories 1.1 through 1.14 + 1.15, plus the two bug-fix cycles. Numbers are per-story in the checkpoints — this is a rough sum; I'll reconcile precisely when I write the final postmortem.
- **Substantial time "chatting" with claude.ai.** Mostly plan review, some design-decision pressure-testing, a lot of me watching plans go by and accepting them, a lot of idle time interleaved with journaling, thinking, and reading what the agent produced. The implementation phase is, for me, surprisingly low-cognitive-load — the machine does the typing and a lot of the micro-decisions; I'm mostly watching for boundary/design concerns.
- **Where my input was actually worth something.** Design-envelope decisions (the architecture constraints in CLAUDE.md §1–§6, the watermark keying by ID vs name, the presence-broadcast scope). Catching the two cross-flow bugs in manual smoke. Redirecting fixes when the agent's proposed patch was locally correct but globally suboptimal. Less valuable: most of the per-story implementation choices, which the agent handled fine.
- **~1–2 hours on the membership bug.** Started the investigation Saturday around 1 a.m., which was a mistake I should have seen coming — my brain was cooked and I spent most of that first session copy-pasting log-gathering commands without reading them. Picked it back up this morning with rested eyes and the claude.ai root-cause dropped in seconds from the same logs. Second half of the fix (the joiner case from the previous note) took another short cycle. The wall-clock cost was probably 2× what it would have been with a fresh head. **Banking this explicitly**: when blocked on a non-trivial bug, stop — sleep, eat, walk — before throwing more agent cycles at it. The AI doesn't get tired; the director does, and a tired director accepts plausible-looking patches without scrutiny.

**Where this leaves me.** Core realtime chat end-to-end is live: auth, rooms (public and private), membership, messaging, history, watermarks, presence (server side done in 1.15; client half is next). That is the functional backbone of the task. Not yet MVP — the MVP bar also includes friends/contacts, 1-to-1 dialogs (which are "rooms of 2" and will mostly reuse what's already here), attachments, moderation, and the admin surface. Stories remaining: ~20 give or take, though several of them should be much cheaper now that the hard architectural pieces (queue, hub, watermarks, reconciliation) are all in place and validated. Plan is to push on 1.16 (heartbeat emitter + presence indicators), then do a verify-deployable pass to re-confirm the golden rule still holds, then attack friends/DMs next since they light up a big chunk of the UI.

Lots of findings along the way, and the journal is going to be the interesting artifact at the end — possibly more interesting than the code.

---

### [2026-04-19 17:12 ART] Note — Insight

_Context: between Story 1.15: Presence tracking (Hub state + heartbeats + AFK timer) and Fix: wait for SignalR Connected before hub.joinRoom (F5 race)._

Small but sharp one, and the source is worth noting: it came from claude.ai itself while we were triaging what to pick up next. 

Roughly (translating from Spanish): **"Fresh head is a scarce resource. Four hours today are worth more than two hours on Monday morning. Take the hard story now, while you can."**

Agreed, and took the advice. The interesting part isn't the observation — any experienced engineer would say the same — but that **the agent volunteered it as a scheduling input**. 

It was reading the situation (remaining stories, difficulty, time-of-day, the fact that I'd just bounced off a rested-brain win) and **optimizing for *my* capacity, not just its own backlog**. Important reminder that the model is willing/able to reason about the human side of the loop if (it verified it's somehow what it suites me), and that **"what should I work on next?" is a question where the constraint is rarely technical — it's energy and focus**. 

Banking the heuristic: hard stories go to the hours with the best brain; easy mechanical stories to the tired hours.

---

### [2026-04-19 17:16 ART] Note — Insight

_Context: between Fix: wait for SignalR Connected before hub.joinRoom (F5 race) and Story 1.16: heartbeat emitter + presence indicators._

Trying to distil what I've actually learned about **where the human matters in this workflow**. 

Once the initial design/setup pass is done (CLAUDE.md, commands, architecture constraints, stories), and assuming *velocity is the operative constraint* — which it is in a hackathon, and arguably in a lot of product work — the high-leverage human-in-the-loop interventions collapse to four:

1. **Classify stories by plan-review depth needed.** Not every story deserves the same scrutiny. Some are mechanical (CRUD endpoint, wire up a prop, add a migration) and the agent's first plan is almost certainly fine; skimming it is enough. Others touch a contract boundary between subsystems, cross a role boundary (owner vs joiner, sender vs receiver), or introduce a new shared primitive — those need real review. Spending equal attention on both wastes the budget I have for the risky ones.

2. **Pick the milestones that matter.** Not every checkpoint is a milestone. A milestone is a point where enough pieces are composed that a new *user-visible* behavior becomes possible — first two-user chat, first presence dot, first DM round-trip. Those are the moments where the cross-flow bugs surface and where "feels done" and "is done" can diverge.

3. **Peer-review plans with AI assistance on the risky stories.** Specifically for the category-1 stories above, spend time with a second agent (claude.ai for me this weekend) on the plan **before** implementation. Both of my cross-flow bugs this weekend would have been caught here if I'd done it: 
"what happens when user B enters the room from the catalog, not as the owner?" is a question claude.ai asks readily when you hand it a plan; claude-code, implementing against a story, tends not to.

4. **Smoke E2E after every milestone, with real multi-actor setups.** Not "do the unit tests pass" — I know they pass — but two browsers, two users, actually exercising the flow. Both bugs this weekend were green in the suite and red in two browsers. The cost of a 5-minute multi-browser smoke is trivial; the cost of missing one is the Saturday-1 a.m. debug session I do not want to repeat. This is one of the fine lines found where human intervention is more needed or an AI alternative maybe suitable, but with my knolowdge the manual smoke was required and imporant for this cases.

Worth noting what's *not* on this list: reviewing every implementation diff line-by-line, hand-holding the agent through micro-decisions, writing code directly. Those were instincts from my non-agentic muscle memory. Most of that attention was wasted on this project — the implementation phase is where the agent is strongest and my marginal contribution is lowest. The four items above are where I actually can move the needle more.

---

### [2026-04-19 17:24 ART] Note — Meta

_Context: between Fix: wait for SignalR Connected before hub.joinRoom (F5 race) and Story 1.16: heartbeat emitter + presence indicators._

Flagging Story 1.15 (Presence) as a category-1 story per the framework from the note above — heavy plan, pre-approval review required. Four connected moving pieces:

- Hub with `ConcurrentDictionary<userId, PresenceInfo>`, lifecycle driven by `OnConnectedAsync` / `OnDisconnectedAsync`.
- `Heartbeat` hub method with server-side throttle.
- `PeriodicTimer` background sweep that demotes stale connections to AFK.
- Status-change broadcast to the relevant peers only — lazy scope, no fan-out.

These pieces compose: the dictionary feeds the timer, the timer produces transitions, transitions feed the broadcast, and the heartbeat mutates the dictionary from the hot path. Any one of them trivial in isolation; the interactions are where the bugs will live (same lesson as this weekend's cross-flow bugs). Deliberately pausing before I click "approve" on the generated plan — want to walk through it with claude.ai first, specifically to pressure-test the broadcast-scope query (how do we compute "relevant peers" without a DB call on every transition?) and the heartbeat-throttle placement (client, server, or both — and what happens when the same user has multiple tabs). Let's see.

---

### [2026-04-19 17:41 ART] Note — Friction

_Context: between Fix: wait for SignalR Connected before hub.joinRoom (F5 race) and Story 1.16: heartbeat emitter + presence indicators._

**Multi-browser smoke on 1.15 surfaced two bugs.** Framework from the 17:16 note validated again: unit tests green, E2E in two browsers red.

- **Presence not reliably detected.** Transitions weren't landing on the peer client under realistic conditions — still diagnosing whether it's heartbeat emission, server-side timer, or broadcast scope.
- **Small race on page refresh (F5).** `SignalRProvider` is re-opening the connection while `RoomPage` mounts and fires `POST /api/rooms/{id}/join` immediately followed by `hub.joinRoom`, and the provider's state mutation collides with the mount chain. This is adjacent to the same class as the morning's "wait for Connected before joinRoom" fix, but on a different seam — the provider lifecycle vs the page mount effect.

**Fix-proposal patterns keep reinforcing.** For both issues, claude.ai handed me a proposal and I went with it. And I notice I still haven't hit a case where I disagreed with a fix proposal *and* genuinely understood the trade-off well enough to defend an alternative. The honest read: my ability to spot which class of problem is brewing ("this is a lifecycle race", "this is a cross-flow gap") is holding up, but my independent judgment on *how to fix it* is either agreeing with the first plausible proposal or deferring because the trade-off space is larger than I can evaluate in the moment. Not a problem yet — the proposals have been solid — but worth flagging so I can notice if and when it breaks. If I *always* agree, one of two things is true: claude.ai is genuinely picking the best option each time (possible), or I've stopped searching for alternatives (also possible). Probably healthier to force myself to articulate the rejected alternatives even when I agree, just to keep the muscle alive.

---

### [2026-04-19 17:41 ART] Note — Insight

_Context: between Fix: wait for SignalR Connected before hub.joinRoom (F5 race) and Story 1.16: heartbeat emitter + presence indicators._

Maybe obvious, maybe repeating myself, but worth stating plainly since it keeps proving out: **plan-mode is the single highest-leverage guardrail in this workflow.** Not equally for every story — mechanical stories get almost no lift from it — but for anything that touches architecture, shared structure, or a design boundary, and *especially* for bug fixes, it is the difference between "agent ships the first plausible patch" and "agent ships the right patch". Bug fixes in particular have a shape claude-code is locally correct about and globally suboptimal about more often than implementation work does, because a bug fix is almost by definition sitting at a seam the original plan didn't anticipate. Forcing the agent to surface its plan *before* editing — and running that plan through a second opinion when the story or fix is non-trivial — has caught every cross-flow concern this weekend that would otherwise have gone to smoke to be discovered.

---

### [2026-04-19 18:40 ART] Note — Insight

_Context: between Story 1.16: heartbeat emitter + presence indicators and Fix: include current presence status in /members for initial snapshot._

**Within-session learning, caught in the act.** While reviewing the fix plan for Story 1.15's residual issues, I noticed claude-code had added `npm run build` as an explicit verification step — and then *justified it in writing* with something close to "the tsc regression this morning taught me to run this explicitly". It wasn't responding to a prompt, wasn't following a checklist I'd handed it, wasn't in CLAUDE.md. It had observed its own failure earlier in the day (a tsc regression that slipped through `dotnet test` + `vitest` because `tsc --noEmit` wasn't in the loop), incorporated the lesson, and modified its own verification protocol to prevent recurrence. And it *cited the incident by reference* when explaining the change.

This is the behavior I've been hoping to see, and seeing it is qualitatively different from reading about it. Autocomplete doesn't do this. An IDE snippet engine doesn't do this. The agent is carrying forward lessons across tasks within a session, adjusting its methodology, and narrating the why. That's a meaningful line — the line between "faster typing" and "development collaborator that learns on the job". It's also the exact behavior CLAUDE.md's "Self-Improvement Loop" section asks for, but asking for it in a rules file is one thing; watching it happen autonomously on a concrete recurrence risk is another.

Worth banking as a data point for the final writeup: **methodological self-learning within a session is observable, verifiable, and (more surprisingly) honest about its source** — claude-code didn't dress the change up as "best practice"; it named the specific earlier incident that motivated it. That transparency matters for trust. If the agent is changing its behavior, I want to know *why*, and today it told me unprompted.

---

### [2026-04-19 18:48 ART] Note — Insight

_Context: between Story 1.16: heartbeat emitter + presence indicators and Fix: include current presence status in /members for initial snapshot._

**Yet another presence bug, yet again caught only in manual multi-browser smoke.** I am going to stop being surprised by this. The unit suite stays green, a single browser feels fine, and two browsers with two users immediately surface something. The case for a scripted multi-context Playwright smoke gets stronger each time I repeat this experience — *noting again* as a followup-candidate after MVP. For this specific bug the fix came together quickly with claude.ai's help.

**What's actually interesting is what claude.ai volunteered during the fix review.** It flagged that the fix I was about to take mixes two responsibilities into a single endpoint (the `/members` endpoint that now also carries presence-adjacent data), and explicitly characterized the tradeoff: acceptable at this scale — MVP, 300 users, small rooms — and a reasonable candidate for separation later if rooms grow or if we'd want to cache the members roster aggressively without invalidating it on every presence transition. It didn't refuse the fix, didn't moralize about "proper" design; it named the tradeoff, named the conditions under which the tradeoff would flip, and let me take the fit-for-MVP path with eyes open. That is the shape of architectural feedback I'd want from a senior reviewer.

**And the honest reflection:** would I have noticed that tradeoff on my own, at this pace, at this hour? I don't think I would have. I'd have seen the fix, confirmed it made the bug go away, and moved on. The design-shaped cost — "this endpoint now has two reasons to change" — is exactly the kind of thing that compounds into technical debt months later and feels, in the moment, like premature worry. Having a second set of eyes that defaults to surfacing those observations *without making them binding* is genuinely additive. It's also a specific counter-point to the earlier note about me "always agreeing with claude.ai's fixes" — the agreement is more defensible when the tradeoff has been named and accepted, versus silently adopted. This session I accepted the tradeoff; next time the tradeoff might flip. The important thing is I'll have been told about it.

---

### [2026-04-19 20:03 ART] Note — Blocker

_Context: between Fix: include current presence status in /members for initial snapshot and Story 2.4: Message edit, delete, and reply._

**TBR — extended-hibernation edge case in presence.** If a user's tab is backgrounded for hours and the browser fully hibernates the JS runtime (not uncommon on Chromium under memory pressure, standard behavior on mobile), the heartbeat loop stops emitting. Server-side the `PeriodicTimer` will correctly transition the user to AFK after 60s, which is fine. The edge case is on *wake*: when the tab is foregrounded again, the user is still shown as AFK until the next activity event (mouse/key/scroll) nudges the heartbeat emitter. In practice that's usually sub-second once the user touches anything, but there's a perceptible window where the peer view and the user's actual "I'm here" state disagree.

Root cause, per CLAUDE.md §2, is structural: browsers hibernate inactive tabs, JS pauses inside them, and the client cannot be relied on to emit "I'm back" without user input. Partial mitigation — emit a heartbeat synchronously on `visibilitychange` when the tab becomes visible — would shrink the window but not eliminate it, because there's no guarantee JS is scheduled in time before the user interacts. Full mitigation would require server-side guesswork (e.g., a grace period on reconnect, or treating a fresh SignalR `Reconnected` event as implicit activity), which is its own can of worms and not worth it at MVP scope.

Tradeoff accepted for MVP: live with the small "stuck AFK until next activity" window. If presence correctness becomes a sharper requirement post-MVP, the cleanest implementable improvement is a `visibilitychange` → `hub.heartbeat()` on `visible`. Banking this so future me doesn't rediscover the same edge from scratch.