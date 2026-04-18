import { createContext, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';
import { ApiError, api } from '../api/client';

export type UserSummary = {
  id: string;
  email: string;
  username: string;
};

type LoginResponse = {
  token: string;
  user: UserSummary;
};

type AuthContextValue = {
  user: UserSummary | null;
  loading: boolean;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (ctx === null) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return ctx;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserSummary | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const me = await api.get<UserSummary>('/api/me');
        if (!cancelled) setUser(me);
      } catch (err) {
        if (!cancelled) {
          if (err instanceof ApiError && err.status === 401) {
            setUser(null);
          } else {
            setUser(null);
          }
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  async function login(email: string, password: string): Promise<void> {
    const response = await api.post<LoginResponse>('/api/auth/login', { email, password });
    setUser(response.user);
  }

  async function register(email: string, username: string, password: string): Promise<void> {
    await api.post<UserSummary>('/api/auth/register', { email, username, password });
    await login(email, password);
  }

  async function logout(): Promise<void> {
    try {
      await api.post<void>('/api/auth/logout');
    } finally {
      setUser(null);
    }
  }

  return (
    <AuthContext.Provider value={{ user, loading, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  );
}
