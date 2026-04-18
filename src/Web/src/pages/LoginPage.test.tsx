import { describe, test, expect, beforeEach, afterEach, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AuthProvider } from '../auth/AuthProvider';
import { LoginPage } from './LoginPage';

type FetchMock = ReturnType<typeof vi.fn>;

function jsonResponse(status: number, body: unknown, init?: ResponseInit): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
    ...init,
  });
}

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/" element={<div data-testid="home-landing">home</div>} />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe('LoginPage', () => {
  let fetchMock: FetchMock;

  beforeEach(() => {
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  test('submits credentials and navigates to home on success', async () => {
    fetchMock.mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      if (url.endsWith('/api/me')) {
        return jsonResponse(401, { title: 'Unauthorized', status: 401 });
      }
      if (url.endsWith('/api/auth/login') && init?.method === 'POST') {
        const body = JSON.parse(String(init.body));
        expect(body).toEqual({ email: 'alice@example.com', password: 'Secret123' });
        return jsonResponse(200, {
          token: '11111111-2222-3333-4444-555555555555',
          user: {
            id: '11111111-1111-1111-1111-111111111111',
            email: 'alice@example.com',
            username: 'alice',
          },
        });
      }
      throw new Error(`Unexpected fetch: ${init?.method ?? 'GET'} ${url}`);
    });

    renderLogin();

    fireEvent.change(screen.getByLabelText(/email/i), {
      target: { value: 'alice@example.com' },
    });
    fireEvent.change(screen.getByLabelText(/password/i), {
      target: { value: 'Secret123' },
    });
    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => expect(screen.getByTestId('home-landing')).toBeInTheDocument());

    const loginCall = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/api/auth/login'));
    expect(loginCall?.[1]?.credentials).toBe('include');
  });

  test('shows inline error on invalid credentials (401)', async () => {
    fetchMock.mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      if (url.endsWith('/api/me')) {
        return jsonResponse(401, { title: 'Unauthorized', status: 401 });
      }
      if (url.endsWith('/api/auth/login') && init?.method === 'POST') {
        return jsonResponse(401, {
          type: 'https://tools.ietf.org/html/rfc9110#section-15.5.2',
          title: 'Invalid credentials',
          status: 401,
          detail: 'The email or password is incorrect.',
        });
      }
      throw new Error(`Unexpected fetch: ${init?.method ?? 'GET'} ${url}`);
    });

    renderLogin();

    fireEvent.change(screen.getByLabelText(/email/i), {
      target: { value: 'alice@example.com' },
    });
    fireEvent.change(screen.getByLabelText(/password/i), {
      target: { value: 'WrongPass1' },
    });
    fireEvent.click(screen.getByRole('button', { name: /sign in/i }));

    const alert = await screen.findByTestId('login-error');
    expect(alert).toHaveTextContent(/invalid credentials/i);
    expect(screen.queryByTestId('home-landing')).not.toBeInTheDocument();
  });
});
