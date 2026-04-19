import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import { CreateRoomModal } from './CreateRoomModal';

type MyRoom = {
  id: string;
  name: string;
  description: string;
  visibility: 'Public' | 'Private' | 'Personal';
  role: 'Member' | 'Admin' | 'Owner';
  memberCount: number;
};

type CatalogEntry = {
  id: string;
  name: string;
  description: string;
  memberCount: number;
};

export type SidebarProps = {
  onRoomCreated: (roomId: string) => void;
  collapsedByDefault?: boolean;
};

export function Sidebar({ onRoomCreated, collapsedByDefault = false }: SidebarProps) {
  const [myRooms, setMyRooms] = useState<MyRoom[]>([]);
  const [catalog, setCatalog] = useState<CatalogEntry[]>([]);
  const [modalOpen, setModalOpen] = useState(false);

  const refresh = useCallback(async () => {
    const [mine, publicRooms] = await Promise.all([
      api.get<MyRoom[]>('/api/me/rooms'),
      api.get<CatalogEntry[]>('/api/rooms'),
    ]);
    setMyRooms(mine);
    setCatalog(publicRooms);
  }, []);

  useEffect(() => {
    refresh().catch(() => undefined);
  }, [refresh]);

  function handleCreated(roomId: string) {
    setModalOpen(false);
    refresh().catch(() => undefined);
    onRoomCreated(roomId);
  }

  const open = !collapsedByDefault;

  return (
    <aside className="border-l border-slate-800 bg-slate-950 p-4 space-y-4">
      <button
        type="button"
        onClick={() => setModalOpen(true)}
        className="w-full rounded-md bg-emerald-600 hover:bg-emerald-500 px-3 py-1.5 text-sm font-medium text-white"
      >
        Create room
      </button>

      <details open={open} data-testid="your-rooms" className="group">
        <summary className="cursor-pointer text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
          Your rooms ({myRooms.length})
        </summary>
        <ul className="mt-2 space-y-1">
          {myRooms.map((room) => (
            <li key={room.id}>
              <Link
                to={`/rooms/${room.id}`}
                className="flex items-center justify-between rounded-md px-2 py-1.5 text-sm hover:bg-slate-800"
              >
                <span className="flex items-center gap-2">
                  <span
                    className={room.visibility === 'Private' ? 'text-slate-500' : 'text-slate-300'}
                    aria-label={`${room.visibility} room`}
                  >
                    {room.visibility === 'Private' ? '🔒' : '#'}
                  </span>
                  <span className="text-slate-200">{room.name}</span>
                </span>
                <span className="text-xs text-slate-500">{room.memberCount}</span>
              </Link>
            </li>
          ))}
          {myRooms.length === 0 && (
            <li className="px-2 py-1.5 text-xs text-slate-500">You're not in any rooms yet.</li>
          )}
        </ul>
      </details>

      <details open={open} data-testid="browse-rooms" className="group">
        <summary className="cursor-pointer text-xs font-semibold uppercase tracking-wider text-slate-400 mb-2">
          Browse public rooms ({catalog.length})
        </summary>
        <ul className="mt-2 space-y-1">
          {catalog.map((room) => (
            <li key={room.id}>
              <Link
                to={`/rooms/${room.id}`}
                className="flex items-center justify-between rounded-md px-2 py-1.5 text-sm hover:bg-slate-800"
              >
                <span className="flex items-center gap-2">
                  <span className="text-slate-300">#</span>
                  <span className="text-slate-200">{room.name}</span>
                </span>
                <span className="text-xs text-slate-500">{room.memberCount}</span>
              </Link>
            </li>
          ))}
          {catalog.length === 0 && (
            <li className="px-2 py-1.5 text-xs text-slate-500">No public rooms yet.</li>
          )}
        </ul>
      </details>

      {modalOpen && (
        <CreateRoomModal
          onCancel={() => setModalOpen(false)}
          onCreated={handleCreated}
        />
      )}
    </aside>
  );
}
