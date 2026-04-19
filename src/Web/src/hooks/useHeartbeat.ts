import { useEffect, useRef } from 'react';
import type { ChatHubClient } from '../signalr/ChatHubClient';

const HEARTBEAT_INTERVAL_MS = 12_000;
const ACTIVITY_EVENTS = ['mousemove', 'keydown', 'touchstart', 'scroll'] as const;

/**
 * Emits `hub.heartbeat()` at most once per 12s while the tab is foregrounded AND the
 * window has focus. Driven by real user activity — mouse/key/touch/scroll — so an idle
 * foregrounded tab stops emitting and the server's AFK timer takes over (CLAUDE.md §2:
 * inactivity inference is server-side because browsers hibernate hidden tabs).
 */
export function useHeartbeat(hub: ChatHubClient | null): void {
  const hubRef = useRef<ChatHubClient | null>(hub);
  hubRef.current = hub;

  useEffect(() => {
    if (!hub) return;

    let disposed = false;
    let lastEmit = 0;

    const isForegrounded = () =>
      document.visibilityState === 'visible' && document.hasFocus();

    const emit = async () => {
      if (disposed) return;
      if (!isForegrounded()) return;
      const now = Date.now();
      if (now - lastEmit < HEARTBEAT_INTERVAL_MS) return;
      lastEmit = now;
      try {
        await hub.whenConnected();
        if (disposed) return;
        await hub.heartbeat();
      } catch {
        // Transient: connection dropped mid-call, server rejected, etc. The next activity
        // (or the next re-foreground) will retry — we never retry on this path ourselves.
        lastEmit = 0;
      }
    };

    const onActivity = () => {
      void emit();
    };

    const onVisibility = () => {
      if (isForegrounded()) {
        // Re-foregrounding counts as activity — emit immediately without waiting.
        lastEmit = 0;
        void emit();
      }
    };

    for (const ev of ACTIVITY_EVENTS) {
      window.addEventListener(ev, onActivity, { passive: true });
    }
    document.addEventListener('visibilitychange', onVisibility);
    window.addEventListener('focus', onVisibility);
    window.addEventListener('blur', () => {
      // No-op: emission already gated on hasFocus() + visibilityState. Kept as an
      // explicit listener so future extensions (telemetry, pause indicators) have a hook.
    });

    return () => {
      disposed = true;
      for (const ev of ACTIVITY_EVENTS) {
        window.removeEventListener(ev, onActivity);
      }
      document.removeEventListener('visibilitychange', onVisibility);
      window.removeEventListener('focus', onVisibility);
    };
  }, [hub]);
}
