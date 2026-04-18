import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthProvider';
import { ApiError } from '../api/client';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login(email, password);
      navigate('/', { replace: true });
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError('Something went wrong. Please try again.');
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center p-6">
      <section className="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900 shadow-xl p-8">
        <header className="mb-6">
          <h1 className="text-2xl font-semibold tracking-tight">Sign in</h1>
          <p className="text-slate-400 text-sm mt-1">DataArt Hackaton 2026</p>
        </header>

        <form onSubmit={onSubmit} className="space-y-4" noValidate>
          <div>
            <label htmlFor="email" className="block text-sm text-slate-300 mb-1">
              Email
            </label>
            <input
              id="email"
              type="email"
              autoComplete="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full rounded-md bg-slate-800 border border-slate-700 px-3 py-2 text-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600"
            />
          </div>

          <div>
            <label htmlFor="password" className="block text-sm text-slate-300 mb-1">
              Password
            </label>
            <input
              id="password"
              type="password"
              autoComplete="current-password"
              required
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full rounded-md bg-slate-800 border border-slate-700 px-3 py-2 text-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600"
            />
          </div>

          {error && (
            <div
              role="alert"
              data-testid="login-error"
              className="rounded-md bg-red-950/60 border border-red-800 px-3 py-2 text-red-200 text-sm"
            >
              {error}
            </div>
          )}

          <button
            type="submit"
            disabled={submitting}
            className="w-full rounded-md bg-emerald-600 hover:bg-emerald-500 disabled:bg-emerald-800 disabled:text-slate-400 transition text-white font-medium py-2"
          >
            {submitting ? 'Signing in…' : 'Sign in'}
          </button>
        </form>

        <p className="mt-6 text-sm text-slate-400">
          No account?{' '}
          <Link to="/register" className="text-emerald-400 hover:underline">
            Create one
          </Link>
        </p>
      </section>
    </main>
  );
}
