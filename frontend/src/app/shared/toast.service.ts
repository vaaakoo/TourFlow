import { Injectable, signal } from '@angular/core';
@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly message = signal<{ text: string; error: boolean } | null>(null);
  private timer?: ReturnType<typeof setTimeout>;
  show(text: string, error = false) {
    this.message.set({ text, error });
    clearTimeout(this.timer);
    this.timer = setTimeout(() => this.message.set(null), 4200);
  }
}
