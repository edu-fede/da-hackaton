# Stories

_Ordered by implementation dependency. Status tracked via acceptance criteria checkboxes._

Legend:
- **Priority:** `High` (MVP / golden rule), `Medium` (full feature set), `Low` (extensions).
- **Story Points:** 1 ≈ 30 min, 2 ≈ 1 h, 3 ≈ 2 h, 5 ≈ half day. Split anything larger.
- **Labels:** `infra`, `api`, `web`, `db`, `realtime`, `testing`, `docs`, `security`.
- **Traces to:** requirement IDs from `docs/requirements.md`.

## Decisions on open questions

These resolve the 8 open questions in `docs/requirements.md` so MVP work can proceed without re-deciding. Recorded verbatim as provided by the developer for traceability.

1. **Password reset delivery channel (→ affects Story 2.10).** Stub only. Implement the UI form (enter email, submit) and a generic response like _"If an account exists for this email, password reset instructions will be sent."_ Do NOT send any email. Do NOT implement token generation or reset link validation. Document in the journal as a conscious scope concession: email infrastructure is out of scope for the hackathon; the stub satisfies the UI presence of the flow.

2. **Ban visibility (→ affects Story 2.6).** Silent disappearance. When a user is banned from a room, the room simply vanishes from their room list on next refresh/reconnect. No toast, no banner, no UI notification. This matches Slack/Discord conventions and reduces UI surface.

3. **Frozen personal history after user-to-user ban (→ affects Story 2.2).** Visible to both parties, read-only, frozen. The task statement already specifies this explicitly in §2.3.5: _"existing personal message history remains visible but becomes read-only/frozen"_. No new messages possible either direction; no edits; no deletes beyond what was already allowed. Both users see the same frozen thread.

4. **Session metadata (→ affects Story 2.9).** Server-side derivation. Parse `User-Agent` from request headers and extract remote IP from `HttpContext.Connection.RemoteIpAddress`. The client sends nothing additional. Simpler, more trustworthy, and avoids a client-side detection library.

5. **Password strength policy (→ affects Story 1.4, Story 2.10).** Minimum 8 characters, at least one letter and one digit. No zxcvbn dependency (avoids adding a library). Enforce on the server side in the registration and password-change endpoints. Document the choice in journal: prioritized time-to-working over sophistication; production would use zxcvbn ≥ 3.

6. **Rate limiting on register/login (→ affects Story 1.4, backlog).** Out of scope. Do not implement. Add a single-line note in the README under a "Known limitations" or "Production gaps" section stating that production deployment would add per-IP rate limiting using ASP.NET Core's built-in `AddRateLimiter`.

7. **Jabber protocol depth (→ affects Stories 3.1, 3.2).** XMPP Core only, basic 1-to-1 federation between two server instances. Do NOT include MUC (XEP-0045) or HTTP upload (XEP-0363). This whole EXT track is Low priority and will only be attempted if the entire core MVP is green and verified by Sunday evening.

8. **Presence broadcast scoping (→ affects Story 2.11, Story 1.15).** Lazy push to relevant peers only. Broadcast presence changes only to: (a) users who share at least one room with the changed user AND who are currently connected, and (b) the user's friends who are currently connected. Do NOT fan out to all 300 users. This keeps presence traffic O(rooms × members) per change rather than O(N²).

---

## Story 1.1 — Docker Compose skeleton boots cleanly

**Summary:** `docker compose up` starts db + api + web with a green `/health` endpoint.

**Description:**
As a grader, I want `git clone && docker compose up` to bring the app to a working state so that the binary evaluation gate passes before any feature work begins.

**Acceptance Criteria:**
- [x] `docker compose up -d --build` returns all three services running/healthy (db, api, web).
- [x] `GET http://localhost:8080/health` returns 200 with `{status, database, timestamp}` and reflects DB reachability.
- [x] `GET http://localhost:3000/` returns the React shell HTML; the page calls `/health` and renders the status.
- [x] Fresh machine path verified via `/verify-deployable` after `down -v`.

**Priority:** High
**Labels:** infra, api, web, db
**Story Points:** 3
**Traces to:** Technical Constraints (golden rule)

**Technical Notes:**
- Already delivered in commit `764313f` (feat: scaffold minimum working stack).

---

## Story 1.2 — Structured logging to console and file

**Summary:** Serilog emits to console AND to a bind-mounted `./logs/` directory.

**Description:**
As the developer, I want all API logs both in `docker compose logs` and in host-accessible files so that incident analysis works both during and after container lifetime.

**Acceptance Criteria:**
- [x] `Serilog.Sinks.File` referenced in `Api.csproj`.
- [x] `Log.Logger` configured with `Console()` + `File(path: "/app/logs/api-.log", rollingInterval: Day, retainedFileCountLimit: 7)`.
- [x] `docker-compose.yml` mounts `./logs:/app/logs` into the `api` service.
- [x] `./logs/` is git-ignored.
- [x] After boot, `./logs/api-YYYYMMDD.log` exists on the host and contains request-level entries.

**Priority:** High
**Labels:** infra, api, docs
**Story Points:** 1
**Traces to:** NFR-16 (structured logging), Technical Constraints

**Technical Notes:**
- Already delivered (this session).

---

## Story 1.3 — User & Session data model + initial EF migration

**Summary:** Introduce `User` and `Session` entities, wire `AppDbContext`, generate and apply the initial migration.

**Description:**
As the backend, I need a persistent user and session model so that auth endpoints can register, authenticate, and list/revoke sessions per FR-1..FR-6 and FR-15.

**Acceptance Criteria:**
- [x] `User { Id, Email, Username, PasswordHash, CreatedAt, DeletedAt? }` with unique indexes on `Email` and `Username`.
- [x] `Session { Id, UserId, CreatedAt, LastSeenAt, UserAgent, RemoteIp, RevokedAt? }`.
- [x] `AppDbContext` registers both; `dotnet ef migrations add InitialSchema` succeeds.
- [x] Migration auto-applies on API startup in Development.
- [x] `dotnet test` has a round-trip test inserting and reading a `User`.

**Priority:** High
**Labels:** api, db
**Story Points:** 2
**Traces to:** FR-1, FR-2, FR-3, FR-9, FR-15

**Status:** Done (commit `59248cb`)

---

## Story 1.4 — Register & login endpoints (REST)

**Summary:** `POST /api/auth/register` and `POST /api/auth/login` returning a session token.

**Description:**
As a new user, I want to create an account and sign in so I can access the app (FR-1, FR-4).

**Acceptance Criteria:**
- [x] `POST /api/auth/register` validates unique email + username, hashes password (Argon2id or ASP.NET Identity hasher), returns `201` with user summary.
- [x] `POST /api/auth/login` verifies password, creates a `Session` row, returns an opaque session token (cookie + body).
- [x] Duplicate email or username returns `409` `ProblemDetails`.
- [x] Password stored as hash only — plain text never written to DB or logs.
- [x] xUnit integration tests cover happy path, duplicate email, duplicate username, wrong password.

**Priority:** High
**Labels:** api, security, testing
**Story Points:** 3
**Traces to:** FR-1, FR-2, FR-3, FR-4, FR-9

**Status:** Done (commit `4210a02`)

---

## Story 1.5 — Session-based auth middleware, logout, and current-user endpoint

**Summary:** Authenticated middleware validates session tokens; `POST /api/auth/logout` revokes the current session; `GET /api/me` returns the user.

**Description:**
As an authenticated caller, I want the API to enforce session validity and to log me out without touching other sessions (FR-5).

**Acceptance Criteria:**
- [x] Requests with a valid session cookie populate `HttpContext.User`; expired/revoked tokens return `401` `ProblemDetails`.
- [x] `POST /api/auth/logout` sets `Session.RevokedAt` for the current session only; other sessions remain valid.
- [x] `GET /api/me` returns `{ id, email, username }`.
- [x] Persistent cookie lifetime allows return visits after browser close (FR-6).

**Priority:** High
**Labels:** api, security, testing
**Story Points:** 2
**Traces to:** FR-5, FR-6, NFR-12

**Status:** Done (commit `ae27847`)

---

## Story 1.6 — Web: login, register, and auth context

**Summary:** Login and Register pages, an `AuthProvider` context, and a session-aware Axios/fetch wrapper.

**Description:**
As a user, I want web pages to register, sign in, and stay signed in across reloads so I can use the chat (FR-1, FR-4, FR-6).

**Acceptance Criteria:**
- [x] `/login` and `/register` routes render per Appendix A wireframes.
- [x] Successful login stores the session cookie (HTTP-only set by server) and redirects to `/`.
- [x] Route guard redirects unauthenticated users to `/login` for protected routes.
- [x] Auth state persists across full-page reload.
- [x] Vitest tests cover the login form happy path and an invalid-credentials branch.

**Priority:** High
**Labels:** web, testing
**Story Points:** 3
**Traces to:** FR-1, FR-4, FR-6, NFR-12

**Status:** Done (commit `d25d88a`)

---

## Story 1.7 — Room & RoomMember data model + migration

**Summary:** `Room`, `RoomMember` (+ role enum), and `RoomBan` entities; migration applied.

**Description:**
As the backend, I need the room model so room features (creation, listing, membership, admins) can be built on top (FR-22, FR-23, FR-29..FR-32).

**Acceptance Criteria:**
- [x] `Room { Id, Name (unique), Description, Visibility, OwnerId, CreatedAt, DeletedAt? }`.
- [x] `RoomMember { RoomId, UserId, Role (Member|Admin|Owner), JoinedAt }`; composite PK `(RoomId, UserId)`.
- [x] `RoomBan { RoomId, UserId, BannedByUserId, BannedAt, Reason? }`; composite PK `(RoomId, UserId)`.
- [x] Migration applied; round-trip insert/query test green.

**Priority:** High
**Labels:** api, db
**Story Points:** 2
**Traces to:** FR-22, FR-23, FR-29, FR-30, FR-31, FR-32

**Status:** Done (commit `f2c9b9d`)

---

## Story 1.8 — Room creation + public catalog + join/leave (REST)

**Summary:** `POST /api/rooms`, `GET /api/rooms` (public catalog w/ search), `POST /api/rooms/{id}/join`, `POST /api/rooms/{id}/leave`.

**Description:**
As an authenticated user, I want to create rooms, discover public ones, and join/leave them so I can participate in conversations (FR-22, FR-24, FR-25, FR-27).

**Acceptance Criteria:**
- [x] Create room: creator becomes `Owner`; room name uniqueness enforced (409 on conflict).
- [x] List catalog: returns public rooms with `{name, description, memberCount}`; supports `?q=` substring search.
- [x] Join: inserts `RoomMember(Role=Member)`; rejects if user is in the room's ban list (`403`).
- [x] Leave: removes membership; owner leave request returns `400` (owner must delete).
- [x] Integration tests for each endpoint.

**Priority:** High
**Labels:** api, testing
**Story Points:** 3
**Traces to:** FR-22, FR-23, FR-24, FR-25, FR-27, FR-32

**Status:** Done (commit `2638183`)

---

## Story 1.9 — Web: room list sidebar + create-room UI

**Summary:** Right-sidebar public/private room accordion; create-room modal; join/leave buttons.

**Description:**
As a user, I want to see and interact with rooms in the sidebar per Appendix A layout (FR-49, FR-50).

**Acceptance Criteria:**
- [x] Sidebar lists public + private rooms the user belongs to, plus a public catalog view.
- [x] "Create room" modal with name, description, visibility toggle.
- [x] Clicking a room opens it in the main chat area (empty at this stage is acceptable).
- [x] Sidebar collapses accordion-style when a room is active.

**Priority:** High
**Labels:** web
**Story Points:** 3
**Traces to:** FR-22, FR-24, FR-49, FR-50

**Status:** Done (commit `08f6626`)

---

## Story 1.10 — Message entity + cursor pagination history endpoint

**Summary:** `Message` entity with `SequenceInRoom`; `GET /api/rooms/{id}/messages?beforeSeq=&limit=` for history.

**Description:**
As the chat, I need persistent, ordered, efficiently paginated messages so infinite scroll works at 10K+ messages (FR-41, FR-42, NFR-6, NFR-7, NFR-8).

**Acceptance Criteria:**
- [x] `Message { Id, RoomId, SenderId, Text, CreatedAt, EditedAt?, DeletedAt?, ReplyToMessageId?, SequenceInRoom }`.
- [x] Unique constraint `(RoomId, SequenceInRoom)`.
- [x] Insert computes `SequenceInRoom = COALESCE(MAX, 0) + 1` scoped to the room, safe under concurrency (rely on unique constraint + retry).
- [x] History endpoint returns page ordered by `SequenceInRoom DESC`, size capped (e.g., 50), cursor-based via `beforeSeq`.
- [x] Load test-ish sanity: 10,000 messages in a room, history endpoint returns the latest page in <200ms.

**Priority:** High
**Labels:** api, db, testing
**Story Points:** 3
**Traces to:** FR-41, FR-42, NFR-6, NFR-7

**Status:** Done (commit `40a579e`)

---

## Story 1.11 — SignalR Hub: join room group, send message (fast path)

**Summary:** `ChatHub` with `JoinRoom(roomId)`, `SendMessage(roomId, text, replyTo?)`; pushes to in-memory `Channel<MessageWorkItem>`; broadcasts to room group.

**Description:**
As a user, I want my message to appear for recipients in <3s without waiting on DB writes (FR-36, NFR-4, architecture §1).

**Acceptance Criteria:**
- [x] `ChatHub : Hub` registered at `/hubs/chat`; auth required.
- [x] `JoinRoom` verifies membership (not banned) and adds connection to SignalR group `room:{id}`.
- [x] `SendMessage` writes a `MessageWorkItem` to the channel, acks the sender, broadcasts `MessageReceived` to `room:{id}`.
- [x] No DB call on the hot path of `SendMessage` (verified by test with DB disconnected — send still acks).

**Priority:** High
**Labels:** api, realtime
**Story Points:** 3
**Traces to:** FR-36, FR-41, NFR-4 (Architecture Constraint §1)

**Status:** Done (commit `2b74660`)

---

## Story 1.12 — BackgroundService consumer persists + assigns watermarks

**Summary:** `MessageProcessorService : BackgroundService` drains the channel, persists to Postgres, assigns `SequenceInRoom`.

**Description:**
As the system, I need the slow path to durably record every message with a monotonic per-room sequence so watermark resync and history work (FR-41, FR-42, Architecture Constraint §3).

**Acceptance Criteria:**
- [x] Consumer is a singleton `BackgroundService` hosted inside the API process.
- [x] Each `MessageWorkItem` results in exactly one `Message` row with a valid `SequenceInRoom`.
- [x] Under concurrency (parallel sends to the same room), no duplicate `SequenceInRoom` (unique-constraint safety).
- [x] On DB failure the item is logged as error and the consumer continues (no crash loop).

**Priority:** High
**Labels:** api, db, realtime, testing
**Story Points:** 3
**Traces to:** FR-41, FR-42, NFR-4 (Architecture Constraint §3)

**Status:** Done (commit `a70e1f3`)

---

## Story 1.13 — Watermark resync endpoint

**Summary:** `POST /api/rooms/resync` — client sends `{roomId, lastSeq}[]`; server returns missed messages per room.

**Description:**
As a reconnecting client, I want to fetch only the messages I missed per room, avoiding an offline per-user mailbox (Architecture Constraint §3).

**Acceptance Criteria:**
- [x] Endpoint accepts a batch of `{roomId, lastSeq}` tuples.
- [x] For each room the caller is still a member of, returns messages with `SequenceInRoom > lastSeq`, ordered ascending, capped (e.g., 500).
- [x] Rooms where the caller is no longer a member return `{roomId, notAMember: true}` so the client discards that entry.
- [x] Test: client with stale watermark receives exactly the missing tail.

**Priority:** High
**Labels:** api, testing
**Story Points:** 2
**Traces to:** FR-42 (Architecture Constraint §3)

**Status:** Done (commit `186052f`)

---

## Story 1.14 — Web: SignalR client + chat window with infinite scroll

**Summary:** Connect to `/hubs/chat` with the session cookie; render messages; infinite scroll upward; auto-scroll only when at bottom.

**Description:**
As a user, I want to see messages in real time and scroll back through history smoothly (FR-41, FR-51, NFR-6).

**Acceptance Criteria:**
- [x] `@microsoft/signalr` client connects on login, reconnects with exponential backoff.
- [x] Receives `MessageReceived`, prepends/appends to the current room's list.
- [x] Infinite scroll upward: when scroll reaches top, fetches next page via REST history endpoint.
- [x] Auto-scroll to bottom only when the user is within N px of bottom; respects read-older-history state.
- [x] Watermark persisted to `localStorage`; on reconnect, calls resync endpoint.

**Priority:** High
**Labels:** web, realtime, testing
**Story Points:** 5
**Traces to:** FR-41, FR-42, FR-51, NFR-6 — **⚠ split candidate:** `a) SignalR wiring + render`, `b) infinite scroll + watermark resync`.

**Status:** Done (commit `5d3c056`)

---

## Story 1.15 — Presence tracking (Hub state + heartbeats + AFK timer)

**Summary:** `ConcurrentDictionary<userId, PresenceInfo>` in `ChatHub`; `Heartbeat` hub method; `PeriodicTimer` demotes to AFK after 60s.

**Description:**
As a contact, I want to see online/AFK/offline indicators reflecting real activity (FR-12, FR-13, FR-14, NFR-5, Architecture Constraint §2).

**Acceptance Criteria:**
- [ ] `OnConnectedAsync` adds the connection to a per-user entry keyed by `userId`, stamps `lastHeartbeat = now`.
- [ ] `Heartbeat()` hub method refreshes `lastHeartbeat` for that connection.
- [ ] `OnDisconnectedAsync` removes the connection; the user is `offline` only when all connections are gone.
- [ ] A `PeriodicTimer` (e.g., every 10s) marks users `AFK` whose ALL connections have `now - lastHeartbeat > 60s`.
- [ ] State transitions broadcast `PresenceChanged` to affected contacts/rooms; no DB writes.

**Priority:** High
**Labels:** api, realtime, testing
**Story Points:** 5
**Traces to:** FR-12, FR-13, FR-14, NFR-5 — **⚠ split candidate:** `a) hub + heartbeat + per-connection tracking`, `b) PeriodicTimer + AFK rule + broadcast fan-out`.

---

## Story 1.16 — Web: heartbeat emitter + presence indicators

**Summary:** Throttled activity listener emits `Heartbeat` every 10–15s while tab is foregrounded; presence dots rendered in sidebar.

**Description:**
As a user, I want my presence to reflect actual activity and to see others' presence updating live (FR-12..FR-14, FR-50).

**Acceptance Criteria:**
- [ ] Web attaches listeners for `mousemove`, `keydown`, `touchstart`, `scroll`; collapses into at most one `Heartbeat` call per 10–15s window.
- [ ] When tab loses focus OR is hidden (Page Visibility API), stop emitting.
- [ ] Sidebar shows ● (online) / ◐ (AFK) / ○ (offline) glyphs per contact and per room member.
- [ ] Updates render within 2s of the server-side `PresenceChanged` event.

**Priority:** High
**Labels:** web, realtime
**Story Points:** 2
**Traces to:** FR-12, FR-13, FR-14, FR-50, NFR-5

---

## Story 2.1 — Friend requests + accept/remove

**Summary:** REST endpoints to send, accept, decline, and remove friendships; notification over SignalR.

**Description:**
As a user, I want to manage my friend list so I can start personal chats (FR-16, FR-17, FR-18, FR-19).

**Acceptance Criteria:**
- [ ] `POST /api/friends/requests { toUsername, message? }` creates a pending request.
- [ ] `POST /api/friends/requests/{id}/accept|decline`.
- [ ] `DELETE /api/friends/{userId}` removes friendship.
- [ ] Hub event `FriendRequestReceived` pushed to the target if connected.
- [ ] UI page/panel for pending requests + current friends.

**Priority:** Medium
**Labels:** api, web, realtime
**Story Points:** 3
**Traces to:** FR-16, FR-17, FR-18, FR-19

---

## Story 2.2 — User-to-user ban + personal messaging gate

**Summary:** `POST /api/users/{id}/ban` and `DELETE /api/users/{id}/ban`; messaging gate enforced in ChatHub.

**Description:**
As a user, I want to cut off unwanted contact; personal messaging must be gated to friends who haven't banned each other (FR-20, FR-21).

**Acceptance Criteria:**
- [ ] Ban creates a `UserBlock(FromUserId, ToUserId)` row; terminates friendship; freezes personal thread (read-only in UI).
- [ ] Attempting to send to a user you banned or who banned you returns `403` from the hub.
- [ ] Unban restores the ability to *re-send* friend requests, not automatic friendship.
- [ ] Existing history remains visible but flagged `frozen: true`.

**Priority:** Medium
**Labels:** api, realtime, web
**Story Points:** 3
**Traces to:** FR-20, FR-21

---

## Story 2.3 — Personal (1-to-1) rooms

**Summary:** Auto-create a 2-member private room for any friend pair when the first personal message is sent.

**Description:**
As a user, I want personal DMs to behave exactly like rooms (FR-35), reusing the hub + history + watermark stack.

**Acceptance Criteria:**
- [ ] Helper `GetOrCreatePersonalRoom(userA, userB)` returns the deterministic room; no admins, `Visibility = Personal`.
- [ ] Messaging, edit, delete, attachments, watermarks all work identically to room chats.
- [ ] UI surfaces personal rooms in the "Contacts" section rather than "Rooms".

**Priority:** Medium
**Labels:** api, web, realtime
**Story Points:** 2
**Traces to:** FR-35

---

## Story 2.4 — Message edit, delete, and reply

**Summary:** Edit own message, delete own or by admin, reply-to quoting.

**Description:**
As a user, I want standard chat message operations (FR-38, FR-39, FR-40).

**Acceptance Criteria:**
- [ ] `PATCH /api/messages/{id}` by author sets `EditedAt`; hub broadcasts `MessageEdited`.
- [ ] `DELETE /api/messages/{id}` by author OR room admin sets `DeletedAt`; hub broadcasts `MessageDeleted`; body replaced with "deleted" placeholder client-side.
- [ ] Reply-to renders a quoted preview of the parent; clicking scrolls to the parent if loaded.
- [ ] Edited messages show a gray "edited" indicator.

**Priority:** Medium
**Labels:** api, web, realtime, testing
**Story Points:** 3
**Traces to:** FR-38, FR-39, FR-40

---

## Story 2.5 — Attachment upload + download with ACL

**Summary:** `POST /api/rooms/{id}/attachments` (multipart, size-capped); attachments stored on volume; download endpoint enforces room membership.

**Description:**
As a user, I want to share images and files within room/personal ACL boundaries (FR-43..FR-47, NFR-9, NFR-10).

**Acceptance Criteria:**
- [ ] Upload max 20 MB (images max 3 MB); rejects oversize with `413 ProblemDetails`.
- [ ] Stored at `/app/uploads/{roomId}/{uuid}-{originalName}`; bind-mounted host volume.
- [ ] Download endpoint returns `403` if the caller is no longer a room member, even if they were the uploader (FR-47).
- [ ] Attachment metadata row carries `OriginalFileName`, optional `Comment`, `Size`, `MimeType`, `RoomId`, `MessageId`, `UploadedByUserId`.
- [ ] Copy-paste upload in the composer works (FR-44).

**Priority:** Medium
**Labels:** api, web, security
**Story Points:** 5
**Traces to:** FR-43..FR-47, NFR-9, NFR-10 — **⚠ split candidate:** `a) backend upload + storage + ACL`, `b) web composer paste/button + download link`.

---

## Story 2.6 — Room admin moderation UI + API

**Summary:** Ban, unban, remove member, delete message, promote/demote admin, manage-room modal.

**Description:**
As an admin/owner, I want full moderation per FR-29..FR-34 and FR-53.

**Acceptance Criteria:**
- [ ] `POST /api/rooms/{id}/ban/{userId}`, `DELETE /api/rooms/{id}/ban/{userId}`, `POST /api/rooms/{id}/admins/{userId}`, `DELETE /api/rooms/{id}/admins/{userId}`, `DELETE /api/rooms/{id}/members/{userId}`.
- [ ] Owner cannot be demoted (server-enforced); admins cannot demote the owner.
- [ ] Banned user sees the room disappear from their list on next hub event.
- [ ] Manage-room modal with tabs: Members / Admins / Banned / Invitations / Settings (per Appendix A).

**Priority:** Medium
**Labels:** api, web, security
**Story Points:** 5
**Traces to:** FR-29..FR-34, FR-53 — **⚠ split candidate:** `a) API endpoints + authz`, `b) Manage-room modal UI`.

---

## Story 2.7 — Private rooms + invitations

**Summary:** Room visibility honored in catalog; invite-by-username flow for private rooms.

**Description:**
As an owner, I want private rooms hidden from the public catalog and joinable only by invite (FR-26, FR-34).

**Acceptance Criteria:**
- [ ] Catalog endpoint excludes private rooms.
- [ ] `POST /api/rooms/{id}/invitations { username }` creates an invite; `POST /api/invitations/{id}/accept` joins the room.
- [ ] Private rooms appear in "Private Rooms" sidebar section only for members.

**Priority:** Medium
**Labels:** api, web
**Story Points:** 2
**Traces to:** FR-26, FR-34

---

## Story 2.8 — Unread indicators

**Summary:** Per-room/per-contact unread counters; cleared when the chat is opened.

**Description:**
As a user, I want to see which rooms/contacts have new activity (FR-48).

**Acceptance Criteria:**
- [ ] Web tracks `lastReadSeq` per room in localStorage; unread count = `server latestSeq - lastReadSeq`.
- [ ] Sidebar badge shows count; clears when room is opened and scrolled to bottom.
- [ ] Hub event `MessageReceived` increments unread on not-currently-viewing rooms.

**Priority:** Medium
**Labels:** web, realtime
**Story Points:** 2
**Traces to:** FR-48

---

## Story 2.9 — Active sessions screen + per-session logout

**Summary:** `GET /api/sessions`, `DELETE /api/sessions/{id}`; UI listing all sessions with revoke buttons.

**Description:**
As a security-conscious user, I want to see where I'm logged in and sign out specific sessions (FR-15).

**Acceptance Criteria:**
- [ ] List returns `{id, userAgent, remoteIp, lastSeenAt, current?}`.
- [ ] Revoking a session sets `RevokedAt`; affected sockets disconnected within 30s.
- [ ] Revoking the current session logs the user out in that browser.

**Priority:** Medium
**Labels:** api, web, security
**Story Points:** 2
**Traces to:** FR-15

---

## Story 2.10 — Password change, password reset, account deletion

**Summary:** `PATCH /api/me/password`, `POST /api/auth/reset/request` + `POST /api/auth/reset/confirm`, `DELETE /api/me`.

**Description:**
As a user, I want full account lifecycle management (FR-7, FR-8, FR-10, FR-11).

**Acceptance Criteria:**
- [ ] Password change requires current password.
- [ ] Reset flow issues a short-lived one-time token (delivery channel TBD — see Open Question #1).
- [ ] Account deletion cascades: delete owned rooms (and their messages + files), null-ize sender on messages in other rooms OR soft-delete user, remove memberships, revoke sessions.
- [ ] Tests cover cascades and ensure other users' history stays intact.

**Priority:** Medium
**Labels:** api, security, db, testing
**Story Points:** 3
**Traces to:** FR-7, FR-8, FR-10, FR-11

---

## Story 2.11 — Presence broadcast scoping + multi-tab smoke test

**Summary:** Presence changes only fan out to affected rooms/friends; exercise 2-tab scenario end-to-end.

**Description:**
As a system, presence broadcasts must not be O(N²); and the multi-tab aggregation rule must hold under test (FR-14, NFR-5, Open Question #8).

**Acceptance Criteria:**
- [ ] `PresenceChanged` is sent only to users who share at least one room or friendship with the changed user.
- [ ] Manual Playwright test: open app in 2 tabs as same user, idle one, verify user stays `online` due to the other tab; idle both >1 min, verify `AFK`.
- [ ] Close both tabs, verify `offline` after `OnDisconnectedAsync`.

**Priority:** Medium
**Labels:** realtime, testing
**Story Points:** 2
**Traces to:** FR-14, NFR-5

---

## Story 3.1 — Jabber (XMPP) server integration (EXT-1)

**Summary:** Embed or sidecar an XMPP server; users authenticate with same credentials; basic 1-to-1 messaging works.

**Priority:** Low
**Labels:** api, realtime, infra
**Story Points:** 5
**Traces to:** EXT-1 — **⚠ split candidate:** `a) server embed + auth bridge`, `b) 1-to-1 XMPP messaging wired to existing Message store`.

**Acceptance Criteria:**
- [ ] XMPP endpoint reachable from a standard Jabber client (Pidgin, Gajim).
- [ ] Credentials map to `User` table (same email/username + password hash).
- [ ] Messages exchanged via XMPP land in the same `Message` table and are visible in the web UI.

---

## Story 3.2 — Server federation between two instances (EXT-2)

**Summary:** Two docker-compose-wired servers federate XMPP; message from A's user to B's user is delivered cross-server.

**Priority:** Low
**Labels:** realtime, infra
**Story Points:** 5
**Traces to:** EXT-2

**Acceptance Criteria:**
- [ ] `docker-compose.federation.yml` spins up servers A and B.
- [ ] User on A sends to `user@B`; message arrives on B within <3s.
- [ ] Both servers log the federation event.

---

## Story 3.3 — Federation load test (EXT-3)

**Summary:** Scripted 50+ clients per side, cross-server messaging harness, report pass/fail.

**Priority:** Low
**Labels:** testing, realtime
**Story Points:** 3
**Traces to:** EXT-3

**Acceptance Criteria:**
- [ ] Test runner (e.g., a script using `Slixmpp` or a .NET XMPP client) fans out 50+ connections per server.
- [ ] Sends N messages A→B and B→A; measures delivery latency distribution.
- [ ] Passes if p95 <3s and zero message loss.

---

## Story 3.4 — Admin UI: Jabber connection dashboard + federation stats (EXT-4)

**Summary:** Admin-only pages showing active XMPP connections and federation traffic counters.

**Priority:** Low
**Labels:** web, api
**Story Points:** 2
**Traces to:** EXT-4

**Acceptance Criteria:**
- [ ] `/admin/jabber` gated behind an "admin" flag on `User`.
- [ ] Dashboard shows per-user XMPP connection count.
- [ ] Federation stats: messages sent per peer server, messages received, errors, last-contacted timestamp.

---

## Backlog / deferred

These are tracked but not scheduled; lift into a story if evaluation time allows.

- **NFR-1 / NFR-2 load test** at 300 concurrent users and a 1000-member room. Scripted with a headless SignalR client.
- **Rate limiting** on register/login (Open Question #6).
- **Room settings tab** UI polish (room name/description/visibility edit).
- **Admin UI on main app** (separate from EXT-4 Jabber dashboard).
- **Observability**: per-request correlation IDs surfaced in client-visible error responses.
