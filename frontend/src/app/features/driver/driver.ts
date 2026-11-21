import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { Assignment, Booking, Page, bookingStatuses } from '../../core/models';
import { apiError } from '../../shared/errors';
import { StatusBadge } from '../../shared/status-badge';
import { ToastService } from '../../shared/toast.service';
@Component({
  imports: [DatePipe, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div>
    <div>
      <p class="font-black uppercase tracking-widest text-teal-600">Driver workspace</p>
      <h1 class="page-title mt-1">My schedule</h1>
      <p class="page-subtitle">Mobile-ready assignment details and trip actions.</p>
    </div>
    <div class="mt-6 grid gap-4 xl:grid-cols-2">
      @for (a of assignments(); track a.id) {
        <article class="card border-l-4 border-l-teal-500">
          <div class="flex items-start justify-between gap-4">
            <div>
              <span class="text-xs font-black uppercase tracking-wider text-teal-700">{{
                a.assignmentDate | date: 'fullDate'
              }}</span>
              <h2 class="mt-1 text-xl font-black">{{ a.bookingNumber }}</h2>
              <p class="mt-1 text-sm text-slate-500">{{ a.startTime }}–{{ a.endTime }}</p>
            </div>
            <span
              class="badge"
              [class.badge-green]="a.isActive"
              [class.badge-slate]="!a.isActive"
              >{{ a.isActive ? 'Active' : 'Inactive' }}</span
            >
          </div>
          <div class="mt-5 grid grid-cols-2 gap-3 rounded-2xl bg-slate-50 p-4 text-sm">
            <div>
              <small class="text-slate-400">Vehicle</small><b class="block">{{ a.vehicleName }}</b>
            </div>
            <div>
              <small class="text-slate-400">Driver</small><b class="block">{{ a.driverName }}</b>
            </div>
          </div>
          @if (details()[a.bookingId]; as b) {
            <div class="mt-4">
              <div class="flex justify-between">
                <span class="text-sm text-slate-500"
                  >{{ b.pickupLocation }} → {{ b.dropoffLocation || 'Tour return' }}</span
                ><app-status-badge [status]="b.status" />
              </div>
              <div class="mt-4 flex gap-3">
                @if (b.status === 2) {
                  <button class="btn-primary flex-1" (click)="status(b, 3)">Start trip</button>
                }
                @if (b.status === 3) {
                  <button class="btn-primary flex-1" (click)="status(b, 4)">Complete trip</button>
                }
              </div>
            </div>
          } @else {
            <button class="btn-secondary mt-4 w-full" (click)="detail(a.bookingId)">
              Load booking details
            </button>
          }
        </article>
      } @empty {
        <div class="empty xl:col-span-2">You have no assignments in the current schedule.</div>
      }
    </div>
  </div>`,
})
export class DriverPage {
  private api = inject(ApiService);
  private toast = inject(ToastService);
  assignments = signal<Assignment[]>([]);
  details = signal<Record<string, Booking>>({});
  statuses = bookingStatuses;
  constructor() {
    this.load();
  }
  load() {
    this.api
      .get<Page<Assignment>>('/api/assignments', {
        pageSize: 100,
        from: new Date().toISOString().slice(0, 10),
      })
      .subscribe((x) => {
        this.assignments.set(x.items);
        x.items.slice(0, 10).forEach((a) => this.detail(a.bookingId));
      });
  }
  detail(id: string) {
    this.api
      .get<Booking>(`/api/bookings/${id}`)
      .subscribe((b) => this.details.update((x) => ({ ...x, [id]: b })));
  }
  status(b: Booking, status: number) {
    this.api
      .put<Booking>(`/api/bookings/${b.id}/status`, {
        status,
        version: b.version,
        comment: status === 3 ? 'Trip started by driver' : 'Trip completed by driver',
      })
      .subscribe({
        next: (x) => {
          this.details.update((d) => ({ ...d, [b.id]: x }));
          this.toast.show(`Booking ${bookingStatuses[status].toLowerCase()}`);
        },
        error: (e) => this.toast.show(apiError(e), true),
      });
  }
}
