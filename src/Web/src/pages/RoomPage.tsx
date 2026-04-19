import { useNavigate, useParams } from 'react-router-dom';
import { Sidebar } from '../components/Sidebar';

export function RoomPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <header className="flex items-center justify-between border-b border-slate-800 px-6 py-4">
        <h1 className="text-lg font-semibold tracking-tight">
          Room <span data-testid="room-id" className="text-slate-400 font-mono text-xs">{id}</span>
        </h1>
        <button
          type="button"
          onClick={() => navigate('/')}
          className="rounded-md border border-slate-700 bg-slate-800 hover:bg-slate-700 px-3 py-1.5 text-sm"
        >
          Back
        </button>
      </header>

      <div className="grid grid-cols-1 lg:grid-cols-[1fr_320px] gap-0">
        <main className="p-6">
          <section className="rounded-2xl border border-slate-800 bg-slate-900 p-6 min-h-[60vh]">
            <p className="text-slate-400 text-sm">
              Messages will appear here once messaging lands in a later story.
            </p>
          </section>
        </main>

        <Sidebar
          collapsedByDefault
          onRoomCreated={(newId) => navigate(`/rooms/${newId}`)}
        />
      </div>
    </div>
  );
}
