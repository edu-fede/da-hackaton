import { useEffect, useLayoutEffect, useRef } from 'react';
import type { ChatMessage } from '../hooks/useRoomMessages';

type MessageListProps = {
  messages: ChatMessage[];
  hasMoreHistory: boolean;
  loading: boolean;
  onReachTop: () => void;
};

const AUTO_SCROLL_THRESHOLD_PX = 120;

export function MessageList({ messages, hasMoreHistory, loading, onReachTop }: MessageListProps) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const previousCountRef = useRef(0);
  const stickyBottomRef = useRef(true);
  const previousTopMessageIdRef = useRef<string | null>(null);

  useLayoutEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    const beforeCount = previousCountRef.current;
    const afterCount = messages.length;
    previousCountRef.current = afterCount;

    if (afterCount === 0) return;

    const newTopId = messages[0]?.id ?? null;
    const topChanged = newTopId !== previousTopMessageIdRef.current;
    previousTopMessageIdRef.current = newTopId;

    if (beforeCount === 0) {
      el.scrollTop = el.scrollHeight;
      return;
    }

    if (topChanged && afterCount > beforeCount) {
      // Older messages were prepended. Preserve visual position.
      const addedCount = afterCount - beforeCount;
      const rows = el.querySelectorAll<HTMLElement>('[data-message-row]');
      let addedHeight = 0;
      for (let i = 0; i < addedCount && i < rows.length; i++) {
        addedHeight += rows[i].offsetHeight;
      }
      el.scrollTop = el.scrollTop + addedHeight;
      return;
    }

    if (stickyBottomRef.current) {
      el.scrollTop = el.scrollHeight;
    }
  }, [messages]);

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;

    const handleScroll = () => {
      const distanceFromBottom = el.scrollHeight - el.scrollTop - el.clientHeight;
      stickyBottomRef.current = distanceFromBottom <= AUTO_SCROLL_THRESHOLD_PX;

      if (el.scrollTop <= 0 && hasMoreHistory && !loading) {
        onReachTop();
      }
    };

    el.addEventListener('scroll', handleScroll);
    return () => el.removeEventListener('scroll', handleScroll);
  }, [hasMoreHistory, loading, onReachTop]);

  return (
    <div
      ref={scrollRef}
      data-testid="message-list"
      className="flex-1 overflow-y-auto px-6 py-4 space-y-3"
    >
      {hasMoreHistory && (
        <p className="text-center text-xs text-slate-500 py-2">
          {loading ? 'Loading older messages…' : 'Scroll up to load older messages'}
        </p>
      )}
      {messages.length === 0 && !loading && (
        <p className="text-center text-sm text-slate-500 py-8">No messages yet. Say hi.</p>
      )}
      {messages.map((m) => (
        <article
          key={m.id}
          data-message-row
          data-testid="message-row"
          className="rounded-md bg-slate-900/40 border border-slate-800 px-3 py-2"
        >
          <header className="flex items-baseline justify-between text-xs text-slate-400">
            <span className="text-slate-200 font-medium">{m.senderUsername}</span>
            <time className="text-slate-500">
              {new Date(m.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </time>
          </header>
          {m.deletedAt !== null ? (
            <p className="mt-1 text-sm text-slate-500 italic">(message deleted)</p>
          ) : (
            <p className="mt-1 text-sm text-slate-100 whitespace-pre-wrap break-words">
              {m.text}
            </p>
          )}
        </article>
      ))}
    </div>
  );
}
