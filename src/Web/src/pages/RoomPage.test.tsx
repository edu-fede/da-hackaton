import { describe, test, expect, beforeEach, afterEach, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthProvider';
import { RoomPage } from './RoomPage';
import { SignalRProvider } from '../signalr/SignalRProvider';
import type { ChatHubClient, MessageBroadcast, PresenceBroadcastPayload } from '../signalr/ChatHubClient';

type MessageReceivedHandler = (m: MessageBroadcast) => void;
type PresenceHandler = (p: PresenceBroadcastPayload) => void;

class FakeHub {
  private handlers: MessageReceivedHandler[] = [];
  private presenceHandlers: PresenceHandler[] = [];
  private connectedResolvers: Array<() => void> = [];
  state: 'Connecting' | 'Connected' | 'Disconnected' = 'Connected';
  sendMessage = vi.fn(async (_roomId: string, _text: string) => undefined);
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
  onReconnected = vi.fn(() => () => undefined);
  emit(message: MessageBroadcast) {
    for (const h of this.handlers) h(message);
  }
  emitPresence(payload: PresenceBroadcastPayload) {
    for (const h of this.presenceHandlers) h(payload);
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
      if (url.endsWith(`/api/rooms/${ROOM_ID}/members`) && method === 'GET') {
        return jsonResponse(200, [
          { userId: 'carol-id', username: 'carol', role: 'Owner' },
          { userId: 'alice-id', username: 'alice', role: 'Admin' },
          { userId: 'bob-id', username: 'bob', role: 'Member' },
          { userId: 'dave-id', username: 'dave', role: 'Member' },
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

    // Without any presence events, sort is: Owner first, Admin next, then Members alphabetically.
    const rows = screen.getAllByTestId('member-row');
    expect(rows[0]).toHaveAttribute('data-user-id', 'carol-id'); // Owner
    expect(rows[1]).toHaveAttribute('data-user-id', 'alice-id'); // Admin
    expect(rows[2]).toHaveAttribute('data-user-id', 'bob-id');   // Member, alphabetical
    expect(rows[3]).toHaveAttribute('data-user-id', 'dave-id');  // Member, alphabetical

    // Now make bob Online — within the Member group, online must come before offline.
    act(() => {
      fakeHub.emitPresence({ userId: 'bob-id', status: 'Online', at: '2026-04-19T00:00:00Z' });
    });

    await waitFor(() => {
      const afterRows = screen.getAllByTestId('member-row');
      expect(afterRows[2]).toHaveAttribute('data-user-id', 'bob-id');
      expect(afterRows[3]).toHaveAttribute('data-user-id', 'dave-id');
    });

    // bob's row carries the Online badge.
    const bobRow = screen.getAllByTestId('member-row').find((r) => r.getAttribute('data-user-id') === 'bob-id')!;
    const badge = within(bobRow).getByTestId('presence-badge');
    expect(badge).toHaveAttribute('data-presence', 'Online');
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
});
