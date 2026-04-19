import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { api } from '../api/client';
import { MessageComposer } from '../components/MessageComposer';
import { MessageList } from '../components/MessageList';
import { Sidebar } from '../components/Sidebar';
import { useRoomMessages } from '../hooks/useRoomMessages';

type RoomSummary = {
  id: string;
  name: string;
  description: string;
  visibility: 'Public' | 'Private' | 'Personal';
};

export function RoomPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { messages, hasMoreHistory, loading, sending, error, send, loadOlder } = useRoomMessages(id);
  const [room, setRoom] = useState<RoomSummary | null>(null);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    (async () => {
      try {
        const mine = await api.get<RoomSummary[]>('/api/me/rooms');
        if (cancelled) return;
        const match = mine.find((r) => r.id === id);
        if (match) {
          setRoom(match);
          return;
        }
        const publicRooms = await api.get<RoomSummary[]>('/api/rooms');
        if (cancelled) return;
        setRoom(publicRooms.find((r) => r.id === id) ?? null);
      } catch {
        // Best-effort; room header can stay blank.
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [id]);

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col">
      <header className="flex items-center justify-between border-b border-slate-800 px-6 py-4">
        <div>
          <h1 className="text-lg font-semibold tracking-tight">
            {room?.name ?? 'Room'}
            <span data-testid="room-id" className="ml-3 text-slate-500 font-mono text-xs">{id}</span>
          </h1>
          {room?.description && (
            <p className="text-slate-400 text-xs mt-0.5">{room.description}</p>
          )}
        </div>
        <button
          type="button"
          onClick={() => navigate('/')}
          className="rounded-md border border-slate-700 bg-slate-800 hover:bg-slate-700 px-3 py-1.5 text-sm"
        >
          Back
        </button>
      </header>

      <div className="flex-1 grid grid-cols-1 lg:grid-cols-[1fr_320px] min-h-0">
        <section className="flex flex-col min-h-0">
          {error && (
            <div
              role="alert"
              data-testid="room-error"
              className="border-b border-red-900 bg-red-950/40 text-red-200 text-sm px-6 py-2"
            >
              {error}
            </div>
          )}
          <MessageList
            messages={messages}
            hasMoreHistory={hasMoreHistory}
            loading={loading}
            onReachTop={loadOlder}
          />
          <MessageComposer onSubmit={send} sending={sending} />
        </section>

        <Sidebar
          collapsedByDefault
          onRoomCreated={(newId) => navigate(`/rooms/${newId}`)}
        />
      </div>
    </div>
  );
}
