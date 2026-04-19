import { useState } from 'react';
import type { FormEvent, KeyboardEvent } from 'react';

type MessageComposerProps = {
  onSubmit: (text: string) => Promise<void>;
  disabled?: boolean;
  sending?: boolean;
};

export function MessageComposer({ onSubmit, disabled, sending }: MessageComposerProps) {
  const [text, setText] = useState('');

  async function submit() {
    const trimmed = text.trim();
    if (trimmed.length === 0 || disabled || sending) return;
    setText('');
    try {
      await onSubmit(trimmed);
    } catch {
      // Error is surfaced by the hook's state; nothing to do here.
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    submit();
  }

  function handleKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      submit();
    }
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="border-t border-slate-800 bg-slate-950 px-6 py-3 flex items-end gap-3"
    >
      <textarea
        aria-label="Message"
        data-testid="composer-input"
        rows={2}
        value={text}
        onChange={(e) => setText(e.target.value)}
        onKeyDown={handleKeyDown}
        disabled={disabled || sending}
        placeholder="Type a message — Enter to send, Shift+Enter for newline"
        className="flex-1 resize-none rounded-md bg-slate-800 border border-slate-700 px-3 py-2 text-slate-100 focus:outline-none focus:ring-2 focus:ring-emerald-600 disabled:opacity-60"
      />
      <button
        type="submit"
        disabled={disabled || sending || text.trim().length === 0}
        className="rounded-md bg-emerald-600 hover:bg-emerald-500 disabled:bg-emerald-800 disabled:text-slate-400 px-4 py-2 text-sm font-medium text-white"
      >
        {sending ? 'Sending…' : 'Send'}
      </button>
    </form>
  );
}
