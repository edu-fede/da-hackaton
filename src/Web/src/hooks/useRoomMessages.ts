import { useCallback, useEffect, useRef, useState } from 'react';
import { api, ApiError } from '../api/client';
import { useChatHub, writeWatermark } from '../signalr/SignalRProvider';
import type { MessageBroadcast } from '../signalr/ChatHubClient';

export type ChatMessage = {
  id: string;
  roomId: string;
  senderId: string;
  senderUsername: string;
  text: string | null;
  createdAt: string;
  editedAt: string | null;
  deletedAt: string | null;
  replyToMessageId: string | null;
  sequenceInRoom: number | null;
};

type HistoryEntry = {
  id: string;
  roomId: string;
  senderId: string;
  senderUsername: string;
  text: string | null;
  createdAt: string;
  editedAt: string | null;
  deletedAt: string | null;
  replyToMessageId: string | null;
  sequenceInRoom: number;
};

const PAGE_SIZE = 50;

export type UseRoomMessagesResult = {
  messages: ChatMessage[];
  hasMoreHistory: boolean;
  loading: boolean;
  sending: boolean;
  error: string | null;
  send: (text: string, replyToMessageId?: string | null) => Promise<void>;
  editMessage: (messageId: string, newText: string) => Promise<void>;
  deleteMessage: (messageId: string) => Promise<void>;
  loadOlder: () => Promise<void>;
};

export function useRoomMessages(roomId: string | undefined): UseRoomMessagesResult {
  const hub = useChatHub();
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [loading, setLoading] = useState(false);
  const [sending, setSending] = useState(false);
  const [hasMoreHistory, setHasMoreHistory] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const joinedRef = useRef<string | null>(null);

  useEffect(() => {
    if (!roomId) return;
    let cancelled = false;

    setLoading(true);
    setError(null);

    (async () => {
      try {
        const page = await api.get<HistoryEntry[]>(
          `/api/rooms/${roomId}/messages?limit=${PAGE_SIZE}`,
        );
        if (cancelled) return;
        const asc = [...page].sort((a, b) => a.sequenceInRoom - b.sequenceInRoom);
        setMessages(asc);
        setHasMoreHistory(page.length >= PAGE_SIZE);
        if (asc.length > 0) {
          writeWatermark(roomId, asc[asc.length - 1].sequenceInRoom);
        }
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Failed to load messages.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [roomId]);

  useEffect(() => {
    if (!roomId || !hub) return;
    let cancelled = false;

    (async () => {
      try {
        if (joinedRef.current && joinedRef.current !== roomId) {
          await hub.leaveRoom(joinedRef.current).catch(() => undefined);
        }
        // Establish DB membership first — idempotent on the backend. The Hub's JoinRoom
        // only adds the connection to the SignalR group and requires an existing RoomMember row.
        await api.post(`/api/rooms/${roomId}/join`);
        if (cancelled) return;
        // On F5 the SignalRProvider is still handshaking when this effect fires; invoking
        // on a non-Connected HubConnection throws. Wait for the shared start promise.
        await hub.whenConnected();
        if (cancelled) return;
        await hub.joinRoom(roomId);
        if (!cancelled) joinedRef.current = roomId;
      } catch (err) {
        if (cancelled) return;
        if (err instanceof ApiError) {
          if (err.status === 403) {
            setError('You cannot join this room.');
          } else if (err.status === 404) {
            setError('This room no longer exists.');
          } else {
            setError(err.message || 'Failed to join room.');
          }
        } else {
          setError(err instanceof Error ? err.message : 'Failed to join room.');
        }
      }
    })();

    const off = hub.onMessageReceived((message: MessageBroadcast) => {
      if (message.roomId !== roomId) return;
      setMessages((prev) => {
        if (prev.some((m) => m.id === message.id)) return prev;
        if (message.sequenceInRoom !== null) {
          writeWatermark(roomId, message.sequenceInRoom);
        }
        return [
          ...prev,
          {
            id: message.id,
            roomId: message.roomId,
            senderId: message.senderId,
            senderUsername: message.senderUsername,
            text: message.text,
            createdAt: message.createdAt,
            editedAt: null,
            deletedAt: null,
            replyToMessageId: message.replyToMessageId,
            sequenceInRoom: message.sequenceInRoom,
          },
        ];
      });
    });

    const offEdit = hub.onMessageEdited((edited) => {
      if (edited.roomId !== roomId) return;
      setMessages((prev) =>
        prev.map((m) =>
          m.id === edited.id ? { ...m, text: edited.text, editedAt: edited.editedAt } : m,
        ),
      );
    });

    const offDelete = hub.onMessageDeleted((deleted) => {
      if (deleted.roomId !== roomId) return;
      setMessages((prev) =>
        prev.map((m) =>
          m.id === deleted.id ? { ...m, text: null, deletedAt: deleted.deletedAt } : m,
        ),
      );
    });

    return () => {
      cancelled = true;
      off();
      offEdit();
      offDelete();
      if (joinedRef.current === roomId) {
        hub.leaveRoom(roomId).catch(() => undefined);
        joinedRef.current = null;
      }
    };
  }, [roomId, hub]);

  const send = useCallback(
    async (text: string, replyToMessageId: string | null = null) => {
      if (!hub || !roomId) return;
      const trimmed = text.trim();
      if (trimmed.length === 0) return;
      setSending(true);
      setError(null);
      try {
        await hub.sendMessage(roomId, trimmed, replyToMessageId);
        // MessageReceived callback appends the message (including to the sender's own UI).
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Send failed.');
      } finally {
        setSending(false);
      }
    },
    [hub, roomId],
  );

  const editMessage = useCallback(
    async (messageId: string, newText: string) => {
      if (!roomId) return;
      const trimmed = newText.trim();
      if (trimmed.length === 0) return;
      setError(null);
      try {
        await api.patch(`/api/rooms/${roomId}/messages/${messageId}`, { text: trimmed });
        // MessageEdited broadcast applies the new text. We don't mutate locally here.
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Edit failed.');
        throw err;
      }
    },
    [roomId],
  );

  const deleteMessage = useCallback(
    async (messageId: string) => {
      if (!roomId) return;
      setError(null);
      try {
        await api.delete(`/api/rooms/${roomId}/messages/${messageId}`);
        // MessageDeleted broadcast flips the row to the tombstone placeholder.
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Delete failed.');
        throw err;
      }
    },
    [roomId],
  );

  const loadOlder = useCallback(async () => {
    if (!roomId || loading || messages.length === 0) return;
    const oldest = messages[0];
    if (oldest.sequenceInRoom === null) return;
    setLoading(true);
    try {
      const page = await api.get<HistoryEntry[]>(
        `/api/rooms/${roomId}/messages?beforeSeq=${oldest.sequenceInRoom}&limit=${PAGE_SIZE}`,
      );
      const asc = [...page].sort((a, b) => a.sequenceInRoom - b.sequenceInRoom);
      setMessages((prev) => [...asc, ...prev]);
      setHasMoreHistory(page.length >= PAGE_SIZE);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load older messages.');
    } finally {
      setLoading(false);
    }
  }, [roomId, loading, messages]);

  return { messages, hasMoreHistory, loading, sending, error, send, editMessage, deleteMessage, loadOlder };
}
