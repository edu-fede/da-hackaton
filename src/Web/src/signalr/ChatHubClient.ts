import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080';

export type MessageBroadcast = {
  id: string;
  roomId: string;
  senderId: string;
  senderUsername: string;
  text: string;
  createdAt: string;
  replyToMessageId: string | null;
  sequenceInRoom: number | null;
};

export type PresenceStatus = 'Online' | 'AFK' | 'Offline';

export type PresenceBroadcastPayload = {
  userId: string;
  status: PresenceStatus;
  at: string;
};

export type MessageEditedBroadcast = {
  id: string;
  roomId: string;
  text: string;
  editedAt: string;
};

export type MessageDeletedBroadcast = {
  id: string;
  roomId: string;
  deletedAt: string;
};

type MessageHandler = (message: MessageBroadcast) => void;
type PresenceHandler = (payload: PresenceBroadcastPayload) => void;
type MessageEditedHandler = (payload: MessageEditedBroadcast) => void;
type MessageDeletedHandler = (payload: MessageDeletedBroadcast) => void;

/**
 * Typed wrapper around the SignalR HubConnection. Cookie-authenticated via `withCredentials`,
 * exponential-backoff auto-reconnect built-in. One instance per authenticated session.
 */
export class ChatHubClient {
  private readonly connection: HubConnection;
  private readonly handlers = new Set<MessageHandler>();
  private readonly reconnectHandlers = new Set<() => void>();
  private readonly presenceHandlers = new Set<PresenceHandler>();
  private readonly editHandlers = new Set<MessageEditedHandler>();
  private readonly deleteHandlers = new Set<MessageDeletedHandler>();
  private startPromise: Promise<void> | null = null;

  constructor() {
    this.connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/chat`, {
        withCredentials: true,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('MessageReceived', (message: MessageBroadcast) => {
      for (const h of this.handlers) h(message);
    });

    this.connection.on('PresenceChanged', (payload: PresenceBroadcastPayload) => {
      for (const h of this.presenceHandlers) h(payload);
    });

    this.connection.on('MessageEdited', (payload: MessageEditedBroadcast) => {
      for (const h of this.editHandlers) h(payload);
    });

    this.connection.on('MessageDeleted', (payload: MessageDeletedBroadcast) => {
      for (const h of this.deleteHandlers) h(payload);
    });

    this.connection.onreconnected(() => {
      for (const h of this.reconnectHandlers) h();
    });
  }

  async start(): Promise<void> {
    if (this.connection.state === 'Connected') return;
    if (this.startPromise) return this.startPromise;
    const p = this.connection.start();
    p.catch(() => {
      this.startPromise = null;
    });
    this.startPromise = p;
    return p;
  }

  async stop(): Promise<void> {
    this.startPromise = null;
    await this.connection.stop();
  }

  async whenConnected(): Promise<void> {
    if (this.connection.state === 'Connected') return;
    if (this.startPromise) {
      await this.startPromise;
      return;
    }
    await this.start();
  }

  async joinRoom(roomId: string): Promise<void> {
    await this.connection.invoke('JoinRoom', roomId);
  }

  async leaveRoom(roomId: string): Promise<void> {
    await this.connection.invoke('LeaveRoom', roomId);
  }

  async sendMessage(
    roomId: string,
    text: string,
    replyToMessageId: string | null = null,
  ): Promise<MessageBroadcast> {
    return await this.connection.invoke<MessageBroadcast>(
      'SendMessage',
      roomId,
      text,
      replyToMessageId,
    );
  }

  async heartbeat(): Promise<void> {
    await this.connection.invoke('Heartbeat');
  }

  onMessageReceived(handler: MessageHandler): () => void {
    this.handlers.add(handler);
    return () => this.handlers.delete(handler);
  }

  onPresenceChanged(handler: PresenceHandler): () => void {
    this.presenceHandlers.add(handler);
    return () => this.presenceHandlers.delete(handler);
  }

  onMessageEdited(handler: MessageEditedHandler): () => void {
    this.editHandlers.add(handler);
    return () => this.editHandlers.delete(handler);
  }

  onMessageDeleted(handler: MessageDeletedHandler): () => void {
    this.deleteHandlers.add(handler);
    return () => this.deleteHandlers.delete(handler);
  }

  onReconnected(handler: () => void): () => void {
    this.reconnectHandlers.add(handler);
    return () => this.reconnectHandlers.delete(handler);
  }
}
