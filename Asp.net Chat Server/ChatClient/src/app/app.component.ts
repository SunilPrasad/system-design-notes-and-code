import { Component, inject, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common'; 
import { FormsModule } from '@angular/forms'; 
import { WebSocketService } from './websocket.service';

@Component({
  selector: 'app-root',
  standalone: true, // <--- Key for modern Angular
  imports: [CommonModule, FormsModule], // Import what we need directly
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'] // (CSS remains the same as before)
})
export class App implements OnDestroy {
  // 1. INJECT (The modern way, no constructor needed)
  public wsService = inject(WebSocketService);

  // 2. LOCAL SIGNALS for inputs
  // We can use standard vars for ngModel, or signals if binding manually. 
  // For simplicity with [(ngModel)], standard properties are still fine, 
  // but let's use signals to show the syntax.
  myUserId = signal('');
  targetUser = signal('');
  messageText = signal('');

  // 3. COMPUTED STATE
  // We can just read wsService.status() in the template directly!

  login() {
    if (this.myUserId()) {
      this.wsService.connect(this.myUserId());
    }
  }

  sendMessage() {
    const target = this.targetUser();
    const msg = this.messageText();

    if (target && msg) {
      this.wsService.sendMessage(target, msg);
      this.messageText.set(''); // Clear input
    }
  }

  ngOnDestroy() {
    this.wsService.disconnect();
  }
}