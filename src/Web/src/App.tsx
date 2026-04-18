import { useEffect, useState } from 'react';

type HealthResponse = {
  status: string;
  database: string;
  timestamp: string;
  error?: string | null;
};

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080';

export default function App() {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await fetch(`${API_BASE_URL}/health`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const data = (await res.json()) as HealthResponse;
        if (!cancelled) setHealth(data);
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : String(e));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <main className="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center p-6">
      <section className="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900 shadow-xl p-8">
        <header className="mb-6">
          <h1 className="text-2xl font-semibold tracking-tight">DataArt Hackaton 2026</h1>
          <p className="text-slate-400 text-sm mt-1">Stack health check</p>
        </header>

        {loading && (
          <p className="text-slate-300" data-testid="health-loading">
            Calling <code className="text-emerald-400">/health</code>…
          </p>
        )}

        {error && (
          <div
            role="alert"
            className="rounded-lg bg-red-950/60 border border-red-800 px-4 py-3 text-red-200"
            data-testid="health-error"
          >
            <p className="font-medium">Failed to reach API</p>
            <p className="text-sm mt-1 font-mono break-all">{error}</p>
            <p className="text-xs mt-2 text-red-300/80">API base: {API_BASE_URL}</p>
          </div>
        )}

        {health && (
          <dl
            className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm"
            data-testid="health-result"
          >
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
            <dt className="text-slate-400">Timestamp</dt>
            <dd className="text-slate-200 font-mono text-xs break-all">{health.timestamp}</dd>
          </dl>
        )}
      </section>
    </main>
  );
}
