import { describe, test, expect, beforeEach, afterEach, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthProvider';
import { HomePage } from './HomePage';

type FetchMock = ReturnType<typeof vi.fn>;

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

const currentUser = {
  id: '11111111-1111-1111-1111-111111111111',
  email: 'alice@example.com',
  username: 'alice',
};

const myRooms = [
  { id: 'aaaa0001-0000-0000-0000-000000000000', name: 'core-team', description: 'private ops', visibility: 'Private', role: 'Owner', memberCount: 2 },
  { id: 'aaaa0002-0000-0000-0000-000000000000', name: 'engineering', description: 'backend + frontend', visibility: 'Public', role: 'Member', memberCount: 5 },
];

const publicCatalog = [
  { id: 'bbbb0001-0000-0000-0000-000000000000', name: 'general', description: 'everyone welcome', memberCount: 42 },
  { id: 'aaaa0002-0000-0000-0000-000000000000', name: 'engineering', description: 'backend + frontend', memberCount: 5 },
];

function renderHome() {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <AuthProvider>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/rooms/:id" element={<div data-testid="room-landing">room</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('HomePage', () => {
  let fetchMock: FetchMock;

  beforeEach(() => {
    fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      if (url.endsWith('/api/me')) return jsonResponse(200, currentUser);
      if (url.endsWith('/api/me/rooms')) return jsonResponse(200, myRooms);
      if (url.match(/\/api\/rooms$/) && (!init || init.method === undefined || init.method === 'GET')) {
        return jsonResponse(200, publicCatalog);
      }
      if (url.endsWith('/health')) return jsonResponse(200, { status: 'healthy', database: 'up', timestamp: '2026-04-19T00:00:00Z' });
      throw new Error(`Unexpected fetch: ${init?.method ?? 'GET'} ${url}`);
    });
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  test('sidebar renders your-rooms and public-catalog sections', async () => {
    renderHome();

    const yourRooms = await screen.findByTestId('your-rooms');
    expect(within(yourRooms).getByText('core-team')).toBeInTheDocument();
    expect(within(yourRooms).getByText('engineering')).toBeInTheDocument();

    const browse = screen.getByTestId('browse-rooms');
    expect(within(browse).getByText('general')).toBeInTheDocument();
  });

  test('create-room modal submits POST /api/rooms and navigates to /rooms/:id', async () => {
    const createdRoomId = 'cccc0001-0000-0000-0000-000000000000';
    fetchMock.mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      if (url.endsWith('/api/me')) return jsonResponse(200, currentUser);
      if (url.endsWith('/api/me/rooms')) return jsonResponse(200, myRooms);
      if (url.match(/\/api\/rooms$/) && init?.method === 'POST') {
        const body = JSON.parse(String(init.body));
        expect(body.name).toBe('new-room');
        expect(body.visibility).toBe('Public');
        return jsonResponse(201, {
          id: createdRoomId,
          name: body.name,
          description: body.description ?? '',
          visibility: body.visibility,
          memberCount: 1,
          createdAt: '2026-04-19T00:00:00Z',
        });
      }
      if (url.match(/\/api\/rooms$/)) return jsonResponse(200, publicCatalog);
      if (url.endsWith('/health')) return jsonResponse(200, { status: 'healthy', database: 'up', timestamp: '2026-04-19T00:00:00Z' });
      throw new Error(`Unexpected fetch: ${init?.method ?? 'GET'} ${url}`);
    });

    renderHome();

    fireEvent.click(await screen.findByRole('button', { name: /create room/i }));
    fireEvent.change(screen.getByLabelText(/name/i), { target: { value: 'new-room' } });
    fireEvent.change(screen.getByLabelText(/description/i), { target: { value: 'a new room' } });
    fireEvent.click(screen.getByRole('button', { name: /^create$/i }));

    await waitFor(() => expect(screen.getByTestId('room-landing')).toBeInTheDocument());
    const postCalls = fetchMock.mock.calls.filter(([, init]) => init?.method === 'POST');
    expect(postCalls.length).toBeGreaterThanOrEqual(1);
  });
});
