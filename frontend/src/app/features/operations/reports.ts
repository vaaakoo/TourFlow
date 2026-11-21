import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
interface BookingRow {
  date: string;
  total: number;
  completed: number;
  cancelled: number;
  bookedValue: number;
}
interface RevenueRow {
  date: string;
  revenue: number;
  payments: number;
}
interface ResourceRow {
  id: string;
  name: string;
  assignments: number;
  scheduledHours: number;
}
@Component({
  imports: [FormsModule, CurrencyPipe, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div>
    <h1 class="page-title">Reports</h1>
    <p class="page-subtitle">Database-backed operational and revenue reporting.</p>
    <div class="card mt-6 flex flex-wrap items-end gap-3">
      <div class="field">
        <label>From</label><input type="date" class="input" [(ngModel)]="from" />
      </div>
      <div class="field"><label>To</label><input type="date" class="input" [(ngModel)]="to" /></div>
      <button class="btn-primary" (click)="load()">Apply range</button>
    </div>
    <div class="mt-6 grid gap-6 xl:grid-cols-2">
      <section class="card">
        <h2 class="text-lg font-black">Booking report</h2>
        <div class="mt-4 overflow-x-auto">
          <table class="data-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Total</th>
                <th>Completed</th>
                <th>Cancelled</th>
                <th>Value</th>
              </tr>
            </thead>
            <tbody>
              @for (r of bookings(); track r.date) {
                <tr>
                  <td>{{ r.date | date }}</td>
                  <td>{{ r.total }}</td>
                  <td>{{ r.completed }}</td>
                  <td>{{ r.cancelled }}</td>
                  <td>{{ r.bookedValue | currency: 'USD' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
      <section class="card">
        <h2 class="text-lg font-black">Paid revenue</h2>
        <div class="mt-4 overflow-x-auto">
          <table class="data-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Payments</th>
                <th>Revenue</th>
              </tr>
            </thead>
            <tbody>
              @for (r of revenue(); track r.date) {
                <tr>
                  <td>{{ r.date | date }}</td>
                  <td>{{ r.payments }}</td>
                  <td class="font-bold text-teal-700">{{ r.revenue | currency: 'USD' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
      <section class="card">
        <h2 class="text-lg font-black">Driver assignments</h2>
        <div class="mt-4 space-y-3">
          @for (r of drivers(); track r.id) {
            <div class="flex justify-between rounded-xl bg-slate-50 p-3">
              <span
                ><b>{{ r.name }}</b
                ><small class="block text-slate-400">{{ r.assignments }} assignments</small></span
              ><b>{{ r.scheduledHours | number: '1.1-1' }} h</b>
            </div>
          } @empty {
            <p class="text-sm text-slate-500">No assignment activity.</p>
          }
        </div>
      </section>
      <section class="card">
        <h2 class="text-lg font-black">Vehicle utilization</h2>
        <div class="mt-4 space-y-3">
          @for (r of vehicles(); track r.id) {
            <div class="flex justify-between rounded-xl bg-slate-50 p-3">
              <span
                ><b>{{ r.name }}</b
                ><small class="block text-slate-400">{{ r.assignments }} assignments</small></span
              ><b>{{ r.scheduledHours | number: '1.1-1' }} h</b>
            </div>
          } @empty {
            <p class="text-sm text-slate-500">No utilization activity.</p>
          }
        </div>
      </section>
    </div>
  </div>`,
})
export class ReportsPage {
  private api = inject(ApiService);
  from = '';
  to = '';
  bookings = signal<BookingRow[]>([]);
  revenue = signal<RevenueRow[]>([]);
  drivers = signal<ResourceRow[]>([]);
  vehicles = signal<ResourceRow[]>([]);
  constructor() {
    this.load();
  }
  load() {
    const p = { from: this.from, to: this.to };
    forkJoin({
      b: this.api.get<BookingRow[]>('/api/reports/bookings', p),
      r: this.api.get<RevenueRow[]>('/api/reports/revenue', p),
      d: this.api.get<ResourceRow[]>('/api/reports/drivers', p),
      v: this.api.get<ResourceRow[]>('/api/reports/vehicles', p),
    }).subscribe((x) => {
      this.bookings.set(x.b);
      this.revenue.set(x.r);
      this.drivers.set(x.d);
      this.vehicles.set(x.v);
    });
  }
}
