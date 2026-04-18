import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './AuthProvider';

export function ProtectedRoute() {
  const { user, loading } = useAuth();

  if (loading) {
    return (
      <main
        className="min-h-screen bg-slate-950 text-slate-400 flex items-center justify-center"
        data-testid="auth-loading"
      >
        Loading…
      </main>
    );
  }

  if (user === null) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}
