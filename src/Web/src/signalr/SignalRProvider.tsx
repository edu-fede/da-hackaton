import { createContext, useContext, useEffect, useMemo } from 'react';
import type { ReactNode } from 'react';
import { ChatHubClient } from './ChatHubClient';
import { api } from '../api/client';

type MyRoomForResync = { id: string };

type ResyncResult = {
  roomId: string;
  notAMember: boolean;
  messages: Array<{ id: string; sequenceInRoom: number }> | null;
};

type SignalRContextValue = {
  hub: ChatHubClient | null;
};

const SignalRContext = createContext<SignalRContextValue>({ hub: null });

const watermarkKeyPrefix = 'hackaton.watermark.';

export function watermarkKey(roomId: string): string {
  return `${watermarkKeyPrefix}${roomId}`;
}

export function readWatermark(roomId: string): number {
  const raw = localStorage.getItem(watermarkKey(roomId));
  if (raw === null) return 0;
  const parsed = Number.parseInt(raw, 10);
  return Number.isFinite(parsed) ? parsed : 0;
}

export function writeWatermark(roomId: string, sequence: number): void {
  if (!Number.isFinite(sequence) || sequence <= 0) return;
  const current = readWatermark(roomId);
  if (sequence > current) {
    localStorage.setItem(watermarkKey(roomId), String(sequence));
  }
}

export function useChatHub(): ChatHubClient | null {
  return useContext(SignalRContext).hub;
}

export function SignalRProvider({
  children,
  hub: providedHub,
}: {
  children: ReactNode;
  hub?: ChatHubClient;
}) {
  const hub = useMemo(() => providedHub ?? new ChatHubClient(), [providedHub]);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        await hub.start();
        if (cancelled) return;
        await resyncAllKnownRooms();
      } catch {
        // Connection failed (server down, auth expired). Automatic reconnect may recover.
      }
    })();

    const offReconnect = hub.onReconnected(() => {
      resyncAllKnownRooms().catch(() => undefined);
    });

    return () => {
      cancelled = true;
      offReconnect();
      hub.stop().catch(() => undefined);
    };
  }, [hub]);

  return <SignalRContext.Provider value={{ hub }}>{children}</SignalRContext.Provider>;
}

async function resyncAllKnownRooms(): Promise<void> {
  let rooms: MyRoomForResync[];
  try {
    rooms = await api.get<MyRoomForResync[]>('/api/me/rooms');
  } catch {
    return;
  }

  if (rooms.length === 0) return;

  const watermarks = rooms.map((r) => ({ roomId: r.id, lastSeq: readWatermark(r.id) }));
  let results: ResyncResult[];
  try {
    results = await api.post<ResyncResult[]>('/api/rooms/resync', watermarks);
  } catch {
    return;
  }

  for (const result of results) {
    if (result.notAMember) {
      localStorage.removeItem(watermarkKey(result.roomId));
      continue;
    }
    if (!result.messages || result.messages.length === 0) continue;
    const maxSeq = result.messages.reduce((m, x) => (x.sequenceInRoom > m ? x.sequenceInRoom : m), 0);
    writeWatermark(result.roomId, maxSeq);
    // The resync messages themselves are not piped to the UI in this pass — the RoomPage will
    // reload from history on mount, and watermarks keep the localStorage in sync.
  }
}
