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

type MessageHandler = (message: MessageBroadcast) => void;

/**
 * Typed wrapper around the SignalR HubConnection. Cookie-authenticated via `withCredentials`,
 * exponential-backoff auto-reconnect built-in. One instance per authenticated session.
 */
export class ChatHubClient {
  private readonly connection: HubConnection;
  private readonly handlers = new Set<MessageHandler>();
  private readonly reconnectHandlers = new Set<() => void>();
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

  onMessageReceived(handler: MessageHandler): () => void {
    this.handlers.add(handler);
    return () => this.handlers.delete(handler);
  }

  onReconnected(handler: () => void): () => void {
    this.reconnectHandlers.add(handler);
    return () => this.reconnectHandlers.delete(handler);
  }
}
