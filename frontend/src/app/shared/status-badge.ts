import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { bookingStatuses } from '../core/models';
@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [class]="tone()">{{ label() }}</span>`,
})
export class StatusBadge {
  status = input.required<number>();
  label = () => bookingStatuses[this.status()] ?? 'Unknown';
  tone = () =>
    `badge ${['badge-amber', 'badge-blue', 'badge-violet', 'badge-cyan', 'badge-green', 'badge-red', 'badge-slate'][this.status()] ?? 'badge-slate'}`;
}
