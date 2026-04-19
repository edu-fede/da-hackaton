import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import type { ChatMessage } from '../hooks/useRoomMessages';
import { usePresenceMap } from '../signalr/SignalRProvider';
import { PresenceBadge } from './PresenceBadge';

type RoomRole = 'Owner' | 'Admin' | 'Member';

type MessageListProps = {
  messages: ChatMessage[];
  hasMoreHistory: boolean;
  loading: boolean;
  onReachTop: () => void;
  currentUserId?: string | null;
  viewerRole?: RoomRole;
  onReply?: (message: ChatMessage) => void;
  onEdit?: (messageId: string, newText: string) => Promise<void>;
  onDelete?: (messageId: string) => Promise<void>;
};

const AUTO_SCROLL_THRESHOLD_PX = 120;

export function MessageList({
  messages,
  hasMoreHistory,
  loading,
  onReachTop,
  currentUserId,
  viewerRole,
  onReply,
  onEdit,
  onDelete,
}: MessageListProps) {
  const presence = usePresenceMap();
  const scrollRef = useRef<HTMLDivElement>(null);
  const rowRefs = useRef<Map<string, HTMLElement>>(new Map());
  const previousCountRef = useRef(0);
  const stickyBottomRef = useRef(true);
  const previousTopMessageIdRef = useRef<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);

  const messagesById = useMemo(() => {
    const map = new Map<string, ChatMessage>();
    for (const m of messages) map.set(m.id, m);
    return map;
  }, [messages]);

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

  function scrollToMessage(id: string) {
    const el = rowRefs.current.get(id);
    if (!el) return;
    el.scrollIntoView({ block: 'center', behavior: 'smooth' });
  }

  function registerRow(id: string, el: HTMLElement | null) {
    if (el) rowRefs.current.set(id, el);
    else rowRefs.current.delete(id);
  }

  const isModerator = viewerRole === 'Admin' || viewerRole === 'Owner';

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
      {messages.map((m) => {
        const isOwn = currentUserId !== undefined && currentUserId !== null && m.senderId === currentUserId;
        const isDeleted = m.deletedAt !== null;
        const canEdit = isOwn && !isDeleted;
        const canDelete = (isOwn || isModerator) && !isDeleted;
        const canReply = !isDeleted;
        const parent = m.replyToMessageId ? messagesById.get(m.replyToMessageId) : null;

        return (
          <article
            key={m.id}
            ref={(el) => registerRow(m.id, el)}
            data-message-row
            data-testid="message-row"
            data-message-id={m.id}
            className="group rounded-md bg-slate-900/40 border border-slate-800 px-3 py-2"
          >
            {m.replyToMessageId && (
              <button
                type="button"
                data-testid="reply-preview"
                onClick={() => parent && scrollToMessage(parent.id)}
                disabled={!parent}
                className="mb-1 block w-full text-left border-l-2 border-emerald-700 pl-2 py-0.5 text-xs text-slate-400 hover:text-slate-200 disabled:cursor-default disabled:hover:text-slate-400"
              >
                {parent ? (
                  <>
                    <span className="text-slate-500">Replying to </span>
                    <span className="text-slate-300">{parent.senderUsername}</span>
                    <span className="text-slate-500">: </span>
                    <span className="italic truncate">
                      {parent.deletedAt !== null ? '(message deleted)' : parent.text}
                    </span>
                  </>
                ) : (
                  <span className="italic text-slate-500">(original message not loaded)</span>
                )}
              </button>
            )}

            <header className="flex items-baseline justify-between text-xs text-slate-400">
              <span className="flex items-center gap-1.5 text-slate-200 font-medium">
                <PresenceBadge status={presence.get(m.senderId)?.status} />
                {m.senderUsername}
              </span>
              <span className="flex items-center gap-2">
                {m.editedAt !== null && !isDeleted && (
                  <span data-testid="edited-indicator" className="text-slate-500 italic">(edited)</span>
                )}
                <time className="text-slate-500">
                  {new Date(m.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                </time>
              </span>
            </header>

            {isDeleted ? (
              <p className="mt-1 text-sm text-slate-500 italic">(message deleted)</p>
            ) : editingId === m.id ? (
              <InlineEditor
                initialText={m.text ?? ''}
                onCancel={() => setEditingId(null)}
                onSave={async (newText) => {
                  if (!onEdit) return;
                  await onEdit(m.id, newText);
                  setEditingId(null);
                }}
              />
            ) : (
              <p className="mt-1 text-sm text-slate-100 whitespace-pre-wrap break-words">
                {m.text}
              </p>
            )}

            {!isDeleted && editingId !== m.id && (
              <div className="mt-1 flex gap-2 text-xs opacity-0 group-hover:opacity-100 focus-within:opacity-100 transition-opacity">
                {canReply && onReply && (
                  <button
                    type="button"
                    data-testid="reply-button"
                    onClick={() => onReply(m)}
                    className="text-slate-400 hover:text-slate-200"
                  >
                    Reply
                  </button>
                )}
                {canEdit && onEdit && (
                  <button
                    type="button"
                    data-testid="edit-button"
                    onClick={() => setEditingId(m.id)}
                    className="text-slate-400 hover:text-slate-200"
                  >
                    Edit
                  </button>
                )}
                {canDelete && onDelete && (
                  <button
                    type="button"
                    data-testid="delete-button"
                    onClick={() => {
                      void onDelete(m.id);
                    }}
                    className="text-rose-400 hover:text-rose-300"
                  >
                    Delete
                  </button>
                )}
              </div>
            )}
          </article>
        );
      })}
    </div>
  );
}

function InlineEditor({
  initialText,
  onCancel,
  onSave,
}: {
  initialText: string;
  onCancel: () => void;
  onSave: (text: string) => Promise<void>;
}) {
  const [text, setText] = useState(initialText);
  const [saving, setSaving] = useState(false);

  async function handleSave() {
    const trimmed = text.trim();
    if (trimmed.length === 0 || trimmed === initialText) {
      onCancel();
      return;
    }
    setSaving(true);
    try {
      await onSave(trimmed);
    } catch {
      // error surfaced through the hook; stay in edit mode
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="mt-1">
      <textarea
        data-testid="edit-textarea"
        value={text}
        onChange={(e) => setText(e.target.value)}
        rows={2}
        className="w-full rounded-md border border-slate-700 bg-slate-900 px-2 py-1 text-sm text-slate-100 focus:outline-none focus:ring-1 focus:ring-emerald-500"
      />
      <div className="mt-1 flex gap-2 text-xs">
        <button
          type="button"
          data-testid="edit-save"
          onClick={handleSave}
          disabled={saving}
          className="rounded-md bg-emerald-700 hover:bg-emerald-600 px-2 py-0.5 text-white disabled:opacity-50"
        >
          {saving ? 'Saving…' : 'Save'}
        </button>
        <button
          type="button"
          data-testid="edit-cancel"
          onClick={onCancel}
          disabled={saving}
          className="rounded-md border border-slate-700 hover:bg-slate-800 px-2 py-0.5 text-slate-300"
        >
          Cancel
        </button>
      </div>
    </div>
  );
}
