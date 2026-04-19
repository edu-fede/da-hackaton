import { describe, test, expect, beforeEach, afterEach, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthProvider';
import { RoomPage } from './RoomPage';
import { SignalRProvider } from '../signalr/SignalRProvider';
import type {
  ChatHubClient,
  MessageBroadcast,
  MessageDeletedBroadcast,
  MessageEditedBroadcast,
  PresenceBroadcastPayload,
} from '../signalr/ChatHubClient';

type MessageReceivedHandler = (m: MessageBroadcast) => void;
type PresenceHandler = (p: PresenceBroadcastPayload) => void;
type MessageEditedHandler = (p: MessageEditedBroadcast) => void;
type MessageDeletedHandler = (p: MessageDeletedBroadcast) => void;

class FakeHub {
  private handlers: MessageReceivedHandler[] = [];
  private presenceHandlers: PresenceHandler[] = [];
  private editHandlers: MessageEditedHandler[] = [];
  private deleteHandlers: MessageDeletedHandler[] = [];
  private connectedResolvers: Array<() => void> = [];
  state: 'Connecting' | 'Connected' | 'Disconnected' = 'Connected';
  sendMessage = vi.fn(async (_roomId: string, _text: string, _replyToMessageId: string | null) => undefined);
  joinRoom = vi.fn(async (_roomId: string) => undefined);
  leaveRoom = vi.fn(async (_roomId: string) => undefined);
  start = vi.fn(async () => undefined);
  stop = vi.fn(async () => undefined);
  heartbeat = vi.fn(async () => undefined);
  whenConnected = vi.fn(async () => {
    if (this.state === 'Connected') return;
    return new Promise<void>((resolve) => {
      this.connectedResolvers.push(resolve);
    });
  });
  onMessageReceived = vi.fn((h: MessageReceivedHandler) => {
    this.handlers.push(h);
    return () => {
      this.handlers = this.handlers.filter((x) => x !== h);
    };
  });
  onPresenceChanged = vi.fn((h: PresenceHandler) => {
    this.presenceHandlers.push(h);
    return () => {
      this.presenceHandlers = this.presenceHandlers.filter((x) => x !== h);
    };
  });
  onMessageEdited = vi.fn((h: MessageEditedHandler) => {
    this.editHandlers.push(h);
    return () => {
      this.editHandlers = this.editHandlers.filter((x) => x !== h);
    };
  });
  onMessageDeleted = vi.fn((h: MessageDeletedHandler) => {
    this.deleteHandlers.push(h);
    return () => {
      this.deleteHandlers = this.deleteHandlers.filter((x) => x !== h);
    };
  });
  onReconnected = vi.fn(() => () => undefined);
  emit(message: MessageBroadcast) {
    for (const h of this.handlers) h(message);
  }
  emitPresence(payload: PresenceBroadcastPayload) {
    for (const h of this.presenceHandlers) h(payload);
  }
  emitEdit(payload: MessageEditedBroadcast) {
    for (const h of this.editHandlers) h(payload);
  }
  emitDelete(payload: MessageDeletedBroadcast) {
    for (const h of this.deleteHandlers) h(payload);
  }
  markConnected() {
    this.state = 'Connected';
    const toResolve = this.connectedResolvers;
    this.connectedResolvers = [];
    for (const r of toResolve) r();
  }
}

let fakeHub: FakeHub;
let callOrder: string[];

const ROOM_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

const currentUser = {
  id: '11111111-1111-1111-1111-111111111111',
  email: 'alice@example.com',
  username: 'alice',
};

type MessageEntry = {
  id: string;
  roomId: string;
  senderId: string;
  senderUsername: string;
  text: string | null;
  createdAt: string;
  editedAt: string | null;
  deletedAt: string | null;
  replyToMessageId: string | null;
  sequenceInRoom: number;
};

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function buildMessage(i: number): MessageEntry {
  return {
    id: `msg-${i}`,
    roomId: ROOM_ID,
    senderId: 'sender',
    senderUsername: `user-${i}`,
    text: `hello ${i}`,
    createdAt: `2026-04-19T00:0${i % 10}:00Z`,
    editedAt: null,
    deletedAt: null,
    replyToMessageId: null,
    sequenceInRoom: i,
  };
}

function renderRoom() {
  return render(
    <MemoryRouter initialEntries={[`/rooms/${ROOM_ID}`]}>
      <AuthProvider>
        <SignalRProvider hub={fakeHub as unknown as ChatHubClient}>
          <Routes>
            <Route path="/rooms/:id" element={<RoomPage />} />
            <Route path="/" element={<div data-testid="home-landing">home</div>} />
          </Routes>
        </SignalRProvider>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('RoomPage', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    localStorage.clear();
    callOrder = [];
    fakeHub = new FakeHub();
    const originalJoinRoom = fakeHub.joinRoom;
    fakeHub.joinRoom = vi.fn(async (roomId: string) => {
      callOrder.push('hub-join');
      return originalJoinRoom(roomId);
    });
    fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';
      if (url.endsWith('/api/me')) return jsonResponse(200, currentUser);
      if (url.endsWith('/api/me/rooms')) return jsonResponse(200, []);
      if (url.endsWith('/api/rooms') && method === 'GET') return jsonResponse(200, []);
      if (url.endsWith(`/api/rooms/${ROOM_ID}/join`) && method === 'POST') {
        callOrder.push('rest-join');
        return new Response(null, { status: 204 });
      }
      if (url.includes(`/api/rooms/${ROOM_ID}/messages`)) {
        const seed = [buildMessage(1), buildMessage(2), buildMessage(3)];
        return jsonResponse(200, seed);
      }
      if (url.match(new RegExp(`/api/rooms/${ROOM_ID}/messages/[0-9a-z-]+$`))) {
        if (method === 'PATCH') {
          return jsonResponse(200, { id: 'ignored', roomId: ROOM_ID, text: 'ignored', editedAt: '2026-04-19T00:05:00Z' });
        }
        if (method === 'DELETE') {
          return new Response(null, { status: 204 });
        }
      }
      if (url.endsWith(`/api/rooms/${ROOM_ID}/members`) && method === 'GET') {
        return jsonResponse(200, [
          { userId: 'carol-id', username: 'carol', role: 'Owner', status: 'Online' },
          { userId: 'alice-id', username: 'alice', role: 'Admin', status: 'Offline' },
          { userId: 'bob-id', username: 'bob', role: 'Member', status: 'Online' },
          { userId: 'dave-id', username: 'dave', role: 'Member', status: 'Offline' },
        ]);
      }
      if (url.endsWith('/api/rooms/resync')) return jsonResponse(200, []);
      if (url.endsWith('/health')) return jsonResponse(200, { status: 'healthy', database: 'up', timestamp: '2026-04-19T00:00:00Z' });
      throw new Error(`Unexpected fetch: ${method} ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.clearAllMocks();
  });

  test('renders seeded messages fetched from history endpoint', async () => {
    renderRoom();

    await waitFor(() => {
      const rows = screen.getAllByTestId('message-row');
      expect(rows).toHaveLength(3);
    });
    expect(screen.getByText('hello 1')).toBeInTheDocument();
    expect(screen.getByText('hello 3')).toBeInTheDocument();
  });

  test('composer submit invokes hub sendMessage with trimmed text', async () => {
    renderRoom();

    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(3));

    const textarea = screen.getByTestId('composer-input');
    fireEvent.change(textarea, { target: { value: '  live from vitest  ' } });
    fireEvent.click(screen.getByRole('button', { name: /send/i }));

    await waitFor(() => {
      expect(fakeHub.sendMessage).toHaveBeenCalledTimes(1);
    });
    expect(fakeHub.sendMessage).toHaveBeenCalledWith(ROOM_ID, 'live from vitest', null);
  });

  test('MessageReceived events append to the message list', async () => {
    renderRoom();

    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(3));

    act(() => {
      fakeHub.emit({
        id: 'live-1',
        roomId: ROOM_ID,
        senderId: 'carol',
        senderUsername: 'carol',
        text: 'live broadcast',
        createdAt: '2026-04-19T00:10:00Z',
        replyToMessageId: null,
        sequenceInRoom: 4,
      });
    });

    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(4));
    expect(screen.getByText('live broadcast')).toBeInTheDocument();
  });

  test('waits for SignalR Connected state before invoking hub.joinRoom on mount', async () => {
    fakeHub.state = 'Connecting';
    renderRoom();

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        expect.stringContaining(`/api/rooms/${ROOM_ID}/join`),
        expect.objectContaining({ method: 'POST' }),
      );
    });

    await act(async () => {
      await Promise.resolve();
    });
    expect(fakeHub.joinRoom).not.toHaveBeenCalled();

    await act(async () => {
      fakeHub.markConnected();
    });

    await waitFor(() => expect(fakeHub.joinRoom).toHaveBeenCalledWith(ROOM_ID));
    expect(fakeHub.joinRoom).toHaveBeenCalledTimes(1);
  });

  test('invokes REST /join before hub.joinRoom on mount', async () => {
    renderRoom();

    await waitFor(() => expect(fakeHub.joinRoom).toHaveBeenCalledWith(ROOM_ID));

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining(`/api/rooms/${ROOM_ID}/join`),
      expect.objectContaining({ method: 'POST' }),
    );
    const restIdx = callOrder.indexOf('rest-join');
    const hubIdx = callOrder.indexOf('hub-join');
    expect(restIdx).toBeGreaterThanOrEqual(0);
    expect(hubIdx).toBeGreaterThanOrEqual(0);
    expect(restIdx).toBeLessThan(hubIdx);
  });

  test('watermark is persisted to localStorage for the newest seen sequence', async () => {
    renderRoom();

    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(3));
    expect(localStorage.getItem(`hackaton.watermark.${ROOM_ID}`)).toBe('3');

    act(() => {
      fakeHub.emit({
        id: 'live-9',
        roomId: ROOM_ID,
        senderId: 'carol',
        senderUsername: 'carol',
        text: 'newer',
        createdAt: '2026-04-19T00:10:00Z',
        replyToMessageId: null,
        sequenceInRoom: 9,
      });
    });

    await waitFor(() => {
      expect(localStorage.getItem(`hackaton.watermark.${ROOM_ID}`)).toBe('9');
    });
  });

  test('MembersPanel renders fetched members sorted by role, then online-first, then alphabetical', async () => {
    renderRoom();

    await waitFor(() => {
      expect(screen.getAllByTestId('member-row')).toHaveLength(4);
    });

    // Seeded statuses (from the /members mock):
    //   carol: Owner, Online     alice: Admin, Offline
    //   bob:   Member, Online    dave:  Member, Offline
    // Sort = role, then online-first, then alphabetical. Expected: carol, alice, bob, dave.
    const rows = screen.getAllByTestId('member-row');
    expect(rows[0]).toHaveAttribute('data-user-id', 'carol-id');
    expect(rows[1]).toHaveAttribute('data-user-id', 'alice-id');
    expect(rows[2]).toHaveAttribute('data-user-id', 'bob-id');
    expect(rows[3]).toHaveAttribute('data-user-id', 'dave-id');

    // Flip dave to Online — within the Member group, Online must now come before Offline,
    // and alphabetical is the third-rank tiebreaker between bob (Online) and dave (Online).
    act(() => {
      fakeHub.emitPresence({ userId: 'dave-id', status: 'Online', at: '2026-04-19T00:00:00Z' });
    });

    await waitFor(() => {
      const afterRows = screen.getAllByTestId('member-row');
      expect(afterRows[2]).toHaveAttribute('data-user-id', 'bob-id');
      expect(afterRows[3]).toHaveAttribute('data-user-id', 'dave-id');
    });

    const daveRow = screen.getAllByTestId('member-row').find((r) => r.getAttribute('data-user-id') === 'dave-id')!;
    expect(within(daveRow).getByTestId('presence-badge')).toHaveAttribute('data-presence', 'Online');
  });

  test('MembersPanel seeds badges from API status before any PresenceChanged event', async () => {
    renderRoom();

    await waitFor(() => expect(screen.getAllByTestId('member-row')).toHaveLength(4));

    // No fakeHub.emitPresence has fired yet. Badges must reflect the /members snapshot.
    const bobRow = screen
      .getAllByTestId('member-row')
      .find((r) => r.getAttribute('data-user-id') === 'bob-id')!;
    expect(within(bobRow).getByTestId('presence-badge')).toHaveAttribute('data-presence', 'Online');

    const carolRow = screen
      .getAllByTestId('member-row')
      .find((r) => r.getAttribute('data-user-id') === 'carol-id')!;
    expect(within(carolRow).getByTestId('presence-badge')).toHaveAttribute('data-presence', 'Online');

    // Offline members aren't written into the shared map (invariant: map holds Online/AFK only),
    // so their badge falls through to the 'Unknown' branch, which renders the same ○ glyph.
    const aliceRow = screen
      .getAllByTestId('member-row')
      .find((r) => r.getAttribute('data-user-id') === 'alice-id')!;
    expect(within(aliceRow).getByTestId('presence-badge')).toHaveAttribute('data-presence', 'Unknown');
  });

  test('PresenceChanged updates the sender badge on rendered messages', async () => {
    renderRoom();

    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(3));

    // Seeded messages all have senderId = 'sender'; no presence known yet → Offline glyph.
    const firstRow = screen.getAllByTestId('message-row')[0];
    const initialBadge = within(firstRow).getByTestId('presence-badge');
    expect(initialBadge).toHaveAttribute('data-presence', 'Unknown');

    act(() => {
      fakeHub.emitPresence({ userId: 'sender', status: 'AFK', at: '2026-04-19T00:01:00Z' });
    });

    await waitFor(() => {
      const firstRowAfter = screen.getAllByTestId('message-row')[0];
      const badge = within(firstRowAfter).getByTestId('presence-badge');
      expect(badge).toHaveAttribute('data-presence', 'AFK');
    });
  });

  // ----- Story 2.4: edit, delete, reply -----

  function injectOwnMessage(overrides: Partial<MessageEntry> = {}) {
    act(() => {
      fakeHub.emit({
        id: overrides.id ?? 'own-msg',
        roomId: ROOM_ID,
        senderId: currentUser.id,
        senderUsername: currentUser.username,
        text: overrides.text ?? 'my own message',
        createdAt: '2026-04-19T00:10:00Z',
        replyToMessageId: overrides.replyToMessageId ?? null,
        sequenceInRoom: overrides.sequenceInRoom ?? 4,
      });
    });
  }

  function findRow(id: string): HTMLElement {
    return screen
      .getAllByTestId('message-row')
      .find((r) => r.getAttribute('data-message-id') === id)!;
  }

  test('clicking Reply opens the composer banner and subsequent send passes replyToMessageId', async () => {
    renderRoom();
    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(3));

    const target = findRow('msg-2');
    fireEvent.click(within(target).getByTestId('reply-button'));

    expect(screen.getByTestId('reply-banner')).toBeInTheDocument();

    const textarea = screen.getByTestId('composer-input');
    fireEvent.change(textarea, { target: { value: 'yes to msg-2' } });
    fireEvent.click(screen.getByRole('button', { name: /send/i }));

    await waitFor(() => expect(fakeHub.sendMessage).toHaveBeenCalledTimes(1));
    expect(fakeHub.sendMessage).toHaveBeenCalledWith(ROOM_ID, 'yes to msg-2', 'msg-2');

    // Banner clears after send.
    expect(screen.queryByTestId('reply-banner')).not.toBeInTheDocument();
  });

  test('MessageEdited event updates the message text and renders the (edited) indicator', async () => {
    renderRoom();
    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(3));

    act(() => {
      fakeHub.emitEdit({
        id: 'msg-1',
        roomId: ROOM_ID,
        text: 'hello 1 — updated',
        editedAt: '2026-04-19T00:06:00Z',
      });
    });

    await waitFor(() => expect(screen.getByText('hello 1 — updated')).toBeInTheDocument());
    expect(screen.queryByText('hello 1')).not.toBeInTheDocument();
    const row = findRow('msg-1');
    expect(within(row).getByTestId('edited-indicator')).toBeInTheDocument();
  });

  test('MessageDeleted event replaces the message with a tombstone placeholder', async () => {
    renderRoom();
    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(3));

    act(() => {
      fakeHub.emitDelete({
        id: 'msg-2',
        roomId: ROOM_ID,
        deletedAt: '2026-04-19T00:07:00Z',
      });
    });

    await waitFor(() => {
      const row = findRow('msg-2');
      expect(within(row).getByText('(message deleted)')).toBeInTheDocument();
    });
    expect(screen.queryByText('hello 2')).not.toBeInTheDocument();
  });

  test('reply preview renders the parent sender and text; clicking it invokes scrollIntoView on the parent', async () => {
    renderRoom();
    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(3));

    // Inject a live message that replies to msg-1 (seeded, parent loaded).
    injectOwnMessage({ id: 'reply-msg', text: 'responding', replyToMessageId: 'msg-1' });
    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(4));

    const replyRow = findRow('reply-msg');
    const preview = within(replyRow).getByTestId('reply-preview');
    expect(preview).toHaveTextContent('user-1');
    expect(preview).toHaveTextContent('hello 1');

    const parentRow = findRow('msg-1');
    const scrollSpy = vi.fn();
    parentRow.scrollIntoView = scrollSpy;

    fireEvent.click(preview);
    expect(scrollSpy).toHaveBeenCalledTimes(1);
  });

  test('Edit on own message opens an inline editor and Save sends PATCH to the API', async () => {
    renderRoom();
    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(3));
    injectOwnMessage();
    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(4));

    const ownRow = findRow('own-msg');
    fireEvent.click(within(ownRow).getByTestId('edit-button'));

    const textarea = screen.getByTestId('edit-textarea') as HTMLTextAreaElement;
    fireEvent.change(textarea, { target: { value: 'my own message — edited' } });
    fireEvent.click(screen.getByTestId('edit-save'));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        expect.stringContaining(`/api/rooms/${ROOM_ID}/messages/own-msg`),
        expect.objectContaining({ method: 'PATCH' }),
      );
    });
    const patchCall = fetchMock.mock.calls.find(
      ([url, init]) =>
        typeof url === 'string' &&
        url.includes(`/api/rooms/${ROOM_ID}/messages/own-msg`) &&
        (init as RequestInit | undefined)?.method === 'PATCH',
    );
    expect(patchCall).toBeDefined();
    const body = JSON.parse(((patchCall![1] as RequestInit).body as string) ?? '{}');
    expect(body).toEqual({ text: 'my own message — edited' });
  });

  test('Delete on own message sends DELETE to the API', async () => {
    renderRoom();
    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(3));
    injectOwnMessage();
    await waitFor(() => expect(screen.getAllByTestId('message-row')).toHaveLength(4));

    const ownRow = findRow('own-msg');
    fireEvent.click(within(ownRow).getByTestId('delete-button'));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        expect.stringContaining(`/api/rooms/${ROOM_ID}/messages/own-msg`),
        expect.objectContaining({ method: 'DELETE' }),
      );
    });
  });
});
