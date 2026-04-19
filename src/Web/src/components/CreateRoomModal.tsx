import { useState } from 'react';
import type { FormEvent } from 'react';
import { ApiError, api } from '../api/client';

export type CreateRoomModalProps = {
  onCancel: () => void;
  onCreated: (roomId: string) => void;
};

type CreateRoomResponse = {
  id: string;
  name: string;
  description: string;
  visibility: 'Public' | 'Private';
  memberCount: number;
  createdAt: string;
};

export function CreateRoomModal({ onCancel, onCreated }: CreateRoomModalProps) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [visibility, setVisibility] = useState<'Public' | 'Private'>('Public');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const response = await api.post<CreateRoomResponse>('/api/rooms', {
        name: name.trim(),
        description: description.trim(),
        visibility,
      });
      onCreated(response.id);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Something went wrong.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="create-room-title"
      className="fixed inset-0 z-10 flex items-center justify-center bg-slate-950/70 p-6"
    >
      <div className="w-full max-w-md rounded-2xl border border-slate-800 bg-slate-900 shadow-xl p-6">
        <h2 id="create-room-title" className="text-lg font-semibold tracking-tight mb-4">
          Create room
        </h2>

        <form onSubmit={onSubmit} className="space-y-4" noValidate>
          <div>
            <label htmlFor="room-name" className="block text-sm text-slate-300 mb-1">
              Name
            </label>
            <input
              id="room-name"
              type="text"
              required
              maxLength={64}
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full rounded-md bg-slate-800 border border-slate-700 px-3 py-2 text-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600"
            />
          </div>

          <div>
            <label htmlFor="room-description" className="block text-sm text-slate-300 mb-1">
              Description
            </label>
            <textarea
              id="room-description"
              rows={3}
              maxLength={1024}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              className="w-full rounded-md bg-slate-800 border border-slate-700 px-3 py-2 text-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600"
            />
          </div>

          <fieldset className="space-y-1">
            <legend className="text-sm text-slate-300 mb-1">Visibility</legend>
            <label className="flex items-center gap-2 text-sm text-slate-200">
              <input
                type="radio"
                name="visibility"
                value="Public"
                checked={visibility === 'Public'}
                onChange={() => setVisibility('Public')}
              />
              Public — visible in the catalog
            </label>
            <label className="flex items-center gap-2 text-sm text-slate-200">
              <input
                type="radio"
                name="visibility"
                value="Private"
                checked={visibility === 'Private'}
                onChange={() => setVisibility('Private')}
              />
              Private — invite only
            </label>
          </fieldset>

          {error && (
            <div
              role="alert"
              className="rounded-md bg-red-950/60 border border-red-800 px-3 py-2 text-red-200 text-sm"
              data-testid="create-room-error"
            >
              {error}
            </div>
          )}

          <div className="flex items-center justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onCancel}
              className="rounded-md border border-slate-700 bg-slate-800 hover:bg-slate-700 px-3 py-1.5 text-sm"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="rounded-md bg-emerald-600 hover:bg-emerald-500 disabled:bg-emerald-800 disabled:text-slate-400 px-3 py-1.5 text-sm font-medium text-white"
            >
              {submitting ? 'Creating…' : 'Create'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
