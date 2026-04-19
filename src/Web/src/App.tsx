import { Outlet, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './auth/AuthProvider';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { HomePage } from './pages/HomePage';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { RoomPage } from './pages/RoomPage';
import { SignalRProvider } from './signalr/SignalRProvider';

function AuthenticatedShell() {
  return (
    <SignalRProvider>
      <Outlet />
    </SignalRProvider>
  );
}

export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route element={<ProtectedRoute />}>
          <Route element={<AuthenticatedShell />}>
            <Route path="/" element={<HomePage />} />
            <Route path="/rooms/:id" element={<RoomPage />} />
          </Route>
        </Route>
      </Routes>
    </AuthProvider>
  );
}
