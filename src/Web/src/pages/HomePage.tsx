import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthProvider';
import { Sidebar } from '../components/Sidebar';

type HealthResponse = {
  status: string;
  database: string;
  timestamp: string;
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080';

export function HomePage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [health, setHealth] = useState<HealthResponse | null>(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await fetch(`${API_BASE_URL}/health`);
        if (!res.ok) return;
        const data = (await res.json()) as HealthResponse;
        if (!cancelled) setHealth(data);
      } catch {
        // non-fatal
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
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <header className="flex items-center justify-between border-b border-slate-800 px-6 py-4">
        <div>
          <h1 className="text-lg font-semibold tracking-tight">
            Hello, <span data-testid="home-username">{user?.username}</span>
          </h1>
          <p className="text-slate-400 text-xs">{user?.email}</p>
        </div>
        <button
          type="button"
          onClick={onLogout}
          className="rounded-md border border-slate-700 bg-slate-800 hover:bg-slate-700 px-3 py-1.5 text-sm"
        >
          Sign out
        </button>
      </header>

      <div className="grid grid-cols-1 lg:grid-cols-[1fr_320px] gap-0">
        <main className="p-6">
          <section className="rounded-2xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-sm font-medium text-slate-300 mb-3">Welcome</h2>
            <p className="text-slate-400 text-sm">
              Pick a room from the sidebar or create a new one to get started. Messaging will arrive in a later story.
            </p>
            {health && (
              <dl
                className="mt-6 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-xs text-slate-400"
                data-testid="health-card"
              >
                <dt>Status</dt>
                <dd
                  className={health.status === 'healthy' ? 'text-emerald-400' : 'text-amber-400'}
                >
                  {health.status}
                </dd>
                <dt>Database</dt>
                <dd className={health.database === 'up' ? 'text-emerald-400' : 'text-red-400'}>
                  {health.database}
                </dd>
              </dl>
            )}
          </section>
        </main>

        <Sidebar onRoomCreated={(id) => navigate(`/rooms/${id}`)} />
      </div>
    </div>
  );
}
