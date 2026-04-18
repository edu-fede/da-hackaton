# Requirements

_Source: `docs/task.md`_

Traceability IDs: **FR-\*** functional, **NFR-\*** non-functional, **EXT-\*** extensions. Each item is atomic and verifiable. Stories in `docs/stories.md` reference these IDs.

## Functional Requirements

### Accounts & authentication (§2.1)
- **FR-1:** Self-registration with email, password, and unique username. _(2.1.1)_
- **FR-2:** Email must be unique across users. _(2.1.2)_
- **FR-3:** Username must be unique and immutable after registration. _(2.1.2)_
- **FR-4:** Sign-in with email + password. _(2.1.3)_
- **FR-5:** Sign-out invalidates the current browser session only; other sessions remain valid. _(2.1.3)_
- **FR-6:** Login persists across browser close/reopen. _(2.1.3 / 3.5)_
- **FR-7:** Password reset flow for forgotten passwords. _(2.1.4)_
- **FR-8:** Password change for logged-in users. _(2.1.4)_
- **FR-9:** Passwords stored only in hashed form (strong KDF). _(2.1.4)_
- **FR-10:** "Delete account" action. _(2.1.5)_
- **FR-11:** On account deletion: user account removed; chat rooms owned by user deleted (with their messages and files); user's membership in other rooms removed. _(2.1.5)_

### Presence & sessions (§2.2)
- **FR-12:** Contact presence exposed in three states: `online`, `AFK`, `offline`. _(2.2.1)_
- **FR-13:** AFK transition after 1 minute with no interaction across ALL the user's open tabs. _(2.2.2)_
- **FR-14:** Multi-tab presence aggregation: `online` if any tab active, `AFK` only if all tabs idle >1 min, `offline` only when all tabs closed. _(2.2.3)_
- **FR-15:** Active sessions screen listing per-session browser/IP metadata, with per-session logout. _(2.2.4)_

### Contacts / friends (§2.3)
- **FR-16:** Each user has a personal friend list. _(2.3.1)_
- **FR-17:** Send friend request by username or from a room's member list, with optional message text. _(2.3.2)_
- **FR-18:** Friendship activates only after the recipient confirms. _(2.3.3)_
- **FR-19:** Remove a user from own friend list. _(2.3.4)_
- **FR-20:** User-to-user ban: blocks new personal messages both ways; freezes existing personal history as read-only; terminates the friendship. _(2.3.5)_
- **FR-21:** Personal messages allowed only between friends, and only if neither side has banned the other. _(2.3.6)_

### Chat rooms (§2.4)
- **FR-22:** Any authenticated user may create a chat room. _(2.4.1)_
- **FR-23:** Room entity has: unique name, description, visibility (public/private), owner, admins, members, banned-users list. _(2.4.2)_
- **FR-24:** Public room catalog showing name, description, member count, with simple search. _(2.4.3)_
- **FR-25:** Public rooms joinable freely by any authenticated user unless banned from that room. _(2.4.3 / 2.4.5)_
- **FR-26:** Private rooms are not visible in the catalog; joinable only by invitation. _(2.4.4 / 2.4.9)_
- **FR-27:** Users may leave rooms freely; owner cannot leave, only delete. _(2.4.5)_
- **FR-28:** Room deletion permanently removes all its messages, files, and images. _(2.4.6)_
- **FR-29:** Room has exactly one owner, always an admin, cannot be demoted. _(2.4.7)_
- **FR-30:** Admins may: delete messages in the room, remove members, ban members, view the banned list (including who banned each), unban, demote other admins (but not the owner). _(2.4.7)_
- **FR-31:** Owner may do all admin actions plus: demote any admin, remove any member, delete the room. _(2.4.7)_
- **FR-32:** Removing a member from a room is treated as a room-ban; banned users cannot rejoin until unbanned. _(2.4.8)_
- **FR-33:** Loss of room access immediately revokes UI access to the room's messages, files, and images. Files remain on disk unless the room itself is deleted. _(2.4.8 / 2.6.5)_
- **FR-34:** Invite a user to a private room by username. _(2.4.9)_

### Messaging (§2.5)
- **FR-35:** Personal 1-to-1 dialogs behave identically to rooms from the user's perspective; modeled as a 2-participant room; no admin moderation in personal chats. _(2.5.1)_
- **FR-36:** Message content supports plain text, multiline text, emoji, attachments, and reply-to reference. _(2.5.2)_
- **FR-37:** Text payload is UTF-8; max 3 KB per message. _(2.5.2)_
- **FR-38:** Reply-to messages visually quote/outline the parent message in the UI. _(2.5.3)_
- **FR-39:** Authors may edit their own messages; edited messages show a gray "edited" indicator. _(2.5.4)_
- **FR-40:** Messages deletable by the author, or by an admin in a room chat; deletion is not recoverable. _(2.5.5)_
- **FR-41:** Messages persisted and returned in chronological order; history browsable via infinite scroll. _(2.5.6)_
- **FR-42:** Messages sent while recipient is offline are persisted and delivered when they next connect. _(2.5.6)_

### Attachments (§2.6)
- **FR-43:** Users may send images and arbitrary file types. _(2.6.1)_
- **FR-44:** Attachments addable via explicit upload button and via copy-paste. _(2.6.2)_
- **FR-45:** Attachments preserve the original file name; user may add an optional comment. _(2.6.3)_
- **FR-46:** Files/images are downloadable only by current room members or authorized personal-chat participants. _(2.6.4)_
- **FR-47:** Files remain stored after an uploader loses room access, but the uploader can no longer see, download, or manage them. _(2.6.5)_

### Notifications (§2.7)
- **FR-48:** Unread indicators shown near room names and contact names; cleared when the user opens the chat. _(2.7.1 / 4.4)_

### UI (§4)
- **FR-49:** Standard web-chat layout: top menu, center message area, bottom input, side rooms/contacts list. _(4.1)_
- **FR-50:** Side panel: rooms/contacts on the right; room list collapses accordion-style after entering a room; room member list with presence statuses on the right. _(4.1.1)_
- **FR-51:** Chat window: auto-scroll to new messages ONLY when user is already at the bottom; no forced autoscroll while reading older messages; infinite scroll upward for history. _(4.2)_
- **FR-52:** Message composer supports multiline text, emoji, attachments, and reply. _(4.3)_
- **FR-53:** Admin actions (ban/unban, remove member, manage admins, view banned users, delete messages, delete room) available through menu entries and modal dialogs. _(4.5)_

## Non-Functional Requirements

### Capacity (§3.1)
- **NFR-1:** Support up to 300 simultaneously connected users.
- **NFR-2:** A single room may hold up to 1000 participants.
- **NFR-3:** Unlimited rooms per user (sizing assumption: ~20 rooms and ~50 contacts per typical user).

### Performance (§3.2)
- **NFR-4:** After sending, a message reaches recipients within 3 seconds end-to-end.
- **NFR-5:** Presence updates propagate with latency <2 seconds.
- **NFR-6:** UX remains smooth in rooms with ≥10,000 messages — cursor-based pagination, never OFFSET.

### Persistence (§3.3)
- **NFR-7:** Messages retained for years (no automatic purge).
- **NFR-8:** History browsable via infinite scroll.

### File storage (§3.4)
- **NFR-9:** Files stored on the local filesystem (mounted as a Docker volume).
- **NFR-10:** Max file size 20 MB; max image size 3 MB.

### Session behavior (§3.5)
- **NFR-11:** No forced logout due to inactivity.
- **NFR-12:** Login state persists across browser close/open.
- **NFR-13:** Multi-tab correctness for the same user.

### Reliability (§3.6)
- **NFR-14:** The system must preserve consistency of: room membership, room bans, file access rights, message history, admin/owner permissions.

### Security (implicit)
- **NFR-15:** No hardcoded credentials/URLs; configuration via env vars and `appsettings.json`.
- **NFR-16:** Errors returned as RFC 9457 `ProblemDetails`; structured logging with correlation IDs (from CLAUDE.md).

## Extensions (bonus / asterisk features)

_From §6 "Advanced requirements". Implemented only after the core is green._

- **EXT-1:** Jabber (XMPP) protocol support — users can connect with a Jabber client against the server.
- **EXT-2:** Server federation — messages travel between two servers via XMPP federation.
- **EXT-3:** Federation load test — 50+ clients on server A, 50+ on server B, cross-server messaging.
- **EXT-4:** Admin UI for Jabber: connection dashboard and federation traffic stats.

## Out of Scope

- Email verification on registration (explicitly waived, §2.1.2).
- Forced periodic password change (explicitly waived, §2.1.4).
- Recovery of deleted messages (explicitly waived, §2.5.5).
- Automatic logout due to inactivity (explicitly waived, §3.5).
- Native mobile or desktop clients — the task is "classic web chat".

## Open Questions

1. **Password reset delivery channel.** §2.1.4 mandates a password-reset flow, but §2.1.2 waives email verification. Should reset links go to the registered email (requiring trust-on-first-use), or is a security-question / recovery-code flow acceptable?
2. **Ban visibility.** Does a user who was banned from a room get an explicit UI notification, or do they just find the room missing from their list on next load?
3. **Frozen personal history after user-to-user ban** (§2.3.5) — can both parties still *see* the frozen thread, or only the one who issued the ban?
4. **Per-session metadata for the sessions screen** (§2.2.4) — how much do we derive from the request (User-Agent, remote IP) vs. ask the client to self-describe?
5. **Password strength rules.** Task does not specify min length, character classes, or complexity. Pick a sensible default (e.g., ≥10 chars, zxcvbn score ≥ 3)?
6. **Rate limiting.** Not called out, but a register/login endpoint without throttling invites abuse. Apply IP-based limits by default?
7. **Jabber protocol depth** (EXT-1/2). XMPP Core only, or include MUC (XEP-0045) for multi-user chat and XEP-0363 for HTTP upload? Scope to "basic 1-to-1 federation" unless directed otherwise.
8. **Real-time presence broadcast scope.** Broadcast every presence change to every contact and every co-member of every room, or lazy-push only to currently-visible peers? The latter is cheaper at 300 users × 20 rooms.

## Technical Constraints

- **Golden rule:** `git clone && cd && docker compose up` must bring the app to a working state with no extra setup (§7 + CLAUDE.md).
- **Stack (from CLAUDE.md — NON-NEGOTIABLE):** .NET 10 Minimal APIs + EF Core + Npgsql + Serilog; xUnit v3 + FluentAssertions; React + Vite + TypeScript + Tailwind; PostgreSQL 16; SignalR for real-time; `System.Threading.Channels` for in-process message buffering; `BackgroundService` for async persistence.
- **Architecture invariants (from CLAUDE.md §1–§6 of Architecture Constraints):**
  - Message path: `SignalR → Channel → ack/broadcast` fast path, `BackgroundService → DB` slow path. Never synchronous receive→INSERT→SELECT→broadcast.
  - Presence: in-memory `ConcurrentDictionary<userId, PresenceInfo>` in the Hub. Server-side AFK inference via `PeriodicTimer` on absence of heartbeat. Never a DB column.
  - Watermarks: per-room `SequenceInRoom` int; client holds `Map<RoomId, LastSeenSequence>` in localStorage; resync on reconnect via REST history fetch.
  - Deployment: single `api` container runs Kestrel + SignalR Hub + Channel + BackgroundService. No separate worker container.
- **Repo:** public GitHub (§7).
