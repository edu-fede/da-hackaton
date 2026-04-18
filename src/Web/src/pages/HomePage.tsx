import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthProvider';

type HealthResponse = {
  status: string;
  database: string;
  timestamp: string;
  error?: string | null;
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080';

export function HomePage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [healthError, setHealthError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await fetch(`${API_BASE_URL}/health`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = (await res.json()) as HealthResponse;
        if (!cancelled) setHealth(data);
      } catch (e) {
        if (!cancelled) setHealthError(e instanceof Error ? e.message : String(e));
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  async function onLogout() {
    await logout();
    navigate('/login', { replace: true });
  }

  return (
    <main className="min-h-screen bg-slate-950 text-slate-100 p-6">
      <div className="mx-auto max-w-3xl space-y-6">
        <header className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">
              Hello, <span data-testid="home-username">{user?.username}</span>
            </h1>
            <p className="text-slate-400 text-sm mt-1">{user?.email}</p>
          </div>
          <button
            type="button"
            onClick={onLogout}
            className="rounded-md border border-slate-700 bg-slate-800 hover:bg-slate-700 px-3 py-1.5 text-sm"
          >
            Sign out
          </button>
        </header>

        <section className="rounded-2xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-sm font-medium text-slate-300 mb-3">Stack health</h2>
          {healthError && (
            <p className="text-red-300 text-sm font-mono break-all" data-testid="health-error">
              {healthError}
            </p>
          )}
          {health && (
            <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
              <dt className="text-slate-400">Status</dt>
              <dd
                className={
                  health.status === 'healthy'
                    ? 'text-emerald-400 font-medium'
                    : 'text-amber-400 font-medium'
                }
              >
                {health.status}
              </dd>
              <dt className="text-slate-400">Database</dt>
              <dd
                className={
                  health.database === 'up'
                    ? 'text-emerald-400 font-medium'
                    : 'text-red-400 font-medium'
                }
              >
                {health.database}
              </dd>
            </dl>
          )}
        </section>
      </div>
    </main>
  );
}
