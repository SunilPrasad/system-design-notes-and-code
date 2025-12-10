import { Injectable, signal, computed } from '@angular/core';

export interface MessagePacket {
  TargetId: string;
  Content: string;
}

@Injectable({
  providedIn: 'root' // Standalone service
})
export class WebSocketService {
  private socket: WebSocket | null = null;

  // 1. STATE SIGNALS
  // We use signals to hold state. Components will read these directly.
  // The 'public' exposed signals are ReadOnly to prevent components from messing them up.
  private _messages = signal<string[]>([]);
  public messages = this._messages.asReadonly();

  private _status = signal<string>('Disconnected');
  public status = this._status.asReadonly();

  // 2. COMPUTED SIGNAL (Example)
  // Automatically updates whenever _messages changes.
  public messageCount = computed(() => this._messages().length);

  connect(userId: string): void {
    this.socket = new WebSocket(`ws://localhost:5265/chat?id=${userId}`);

    this.socket.onopen = () => {
      this._status.set(`Connected as ${userId}`);
      this.addMessage("System: Connected to server!");
    };

    this.socket.onmessage = (event) => {
      this.addMessage(event.data);
    };

    this.socket.onclose = () => {
      this._status.set('Disconnected');
      this.addMessage("System: Disconnected.");
    };
  }

  sendMessage(targetId: string, content: string): void {
    if (this.socket && this.socket.readyState === WebSocket.OPEN) {
      const packet: MessagePacket = { TargetId: targetId, Content: content };
      this.socket.send(JSON.stringify(packet));
      
      // Optimistic update: Show my message immediately
      this.addMessage(`You -> ${targetId}: ${content}`);
    }
  }

  disconnect() {
    this.socket?.close();
  }

  private addMessage(msg: string) {
    // Modern way to update arrays in signals
    this._messages.update(current => [...current, msg]);
  }
}