import { useEffect, useMemo, useState } from 'react';
import { api } from '../api/client';
import { usePresenceMap, useSeedPresence } from '../signalr/SignalRProvider';
import type { PresenceStatus } from '../signalr/ChatHubClient';
import { PresenceBadge } from './PresenceBadge';

type RoomRole = 'Owner' | 'Admin' | 'Member';

type RoomMemberEntry = {
  userId: string;
  username: string;
  role: RoomRole;
  status: PresenceStatus;
};

const ROLE_ORDER: Record<RoomRole, number> = {
  Owner: 0,
  Admin: 1,
  Member: 2,
};

function presenceRank(status: PresenceStatus | undefined): number {
  if (status === 'Online') return 0;
  if (status === 'AFK') return 1;
  return 2;
}

export function MembersPanel({ roomId }: { roomId: string }) {
  const [members, setMembers] = useState<RoomMemberEntry[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const presence = usePresenceMap();
  const seedPresence = useSeedPresence();

  useEffect(() => {
    let cancelled = false;
    setMembers(null);
    setError(null);
    (async () => {
      try {
        const list = await api.get<RoomMemberEntry[]>(`/api/rooms/${roomId}/members`);
        if (cancelled) return;
        setMembers(list);
        // Seed the shared presence map so badges render the server-side snapshot
        // immediately — without this we only saw users who transitioned AFTER we connected.
        const nowIso = new Date().toISOString();
        seedPresence(list.map((m) => ({ userId: m.userId, status: m.status, at: nowIso })));
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Failed to load members.');
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [roomId, seedPresence]);

  const sorted = useMemo(() => {
    if (!members) return null;
    const copy = [...members];
    copy.sort((a, b) => {
      const roleDelta = ROLE_ORDER[a.role] - ROLE_ORDER[b.role];
      if (roleDelta !== 0) return roleDelta;
      const presenceDelta =
        presenceRank(presence.get(a.userId)?.status) - presenceRank(presence.get(b.userId)?.status);
      if (presenceDelta !== 0) return presenceDelta;
      return a.username.localeCompare(b.username);
    });
    return copy;
  }, [members, presence]);

  return (
    <aside
      data-testid="members-panel"
      className="hidden w-56 shrink-0 border-l border-slate-800 bg-slate-950/40 px-3 py-4 md:block"
    >
      <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">
        Members {sorted ? `(${sorted.length})` : ''}
      </h3>
      {error && <p className="text-xs text-rose-400">{error}</p>}
      {sorted && (
        <ul className="space-y-1">
          {sorted.map((m) => (
            <li
              key={m.userId}
              data-testid="member-row"
              data-user-id={m.userId}
              data-role={m.role}
              className="flex items-center justify-between gap-2 rounded-md px-2 py-1 text-sm text-slate-200"
            >
              <span className="flex items-center gap-2 truncate">
                <PresenceBadge status={presence.get(m.userId)?.status} />
                <span className="truncate">{m.username}</span>
              </span>
              {m.role !== 'Member' && (
                <span className="text-[0.65rem] uppercase text-slate-500">{m.role}</span>
              )}
            </li>
          ))}
        </ul>
      )}
    </aside>
  );
}
