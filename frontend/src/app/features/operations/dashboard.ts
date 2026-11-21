import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ApiService } from '../../core/api.service';
import { Dashboard, bookingStatuses } from '../../core/models';
import { StatusBadge } from '../../shared/status-badge';
@Component({
  imports: [CurrencyPipe, DatePipe, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div>
    <div>
      <p class="font-black uppercase tracking-widest text-teal-600">Operations overview</p>
      <h1 class="page-title mt-1">Dashboard</h1>
      <p class="page-subtitle">Live business metrics from the TourFlow API.</p>
    </div>
    @if (data(); as d) {
      <div class="mt-7 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <div class="card">
          <small class="font-bold text-slate-400">TOTAL BOOKINGS</small>
          <p class="mt-2 text-3xl font-black">{{ d.totalBookings }}</p>
        </div>
        <div class="card">
          <small class="font-bold text-slate-400">PAID REVENUE</small>
          <p class="mt-2 text-3xl font-black text-teal-700">{{ d.revenue | currency: 'USD' }}</p>
        </div>
        <div class="card">
          <small class="font-bold text-slate-400">ACTIVE TOURS</small>
          <p class="mt-2 text-3xl font-black">{{ d.activeTours }}</p>
        </div>
        <div class="card">
          <small class="font-bold text-slate-400">CUSTOMERS</small>
          <p class="mt-2 text-3xl font-black">{{ d.totalCustomers }}</p>
        </div>
      </div>
      <div class="mt-6 grid gap-6 xl:grid-cols-[1.25fr_.75fr]">
        <section class="card">
          <h2 class="text-lg font-black">Recent bookings</h2>
          <div class="mt-4 overflow-x-auto">
            <table class="data-table min-w-[600px]">
              <thead>
                <tr>
                  <th>Booking</th>
                  <th>Date</th>
                  <th>Total</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                @for (b of d.recentBookings; track b.id) {
                  <tr>
                    <td>
                      <b>{{ b.bookingNumber }}</b>
                    </td>
                    <td>{{ b.bookingDate | date: 'mediumDate' }}</td>
                    <td>{{ b.totalPrice | currency: 'USD' }}</td>
                    <td><app-status-badge [status]="b.status" /></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>
        <section class="card">
          <h2 class="text-lg font-black">Booking mix</h2>
          <div class="mt-5 space-y-4">
            @for (s of d.byStatus; track s.status) {
              <div>
                <div class="mb-1 flex justify-between text-sm">
                  <span>{{ statuses[s.status] }}</span
                  ><b>{{ s.count }}</b>
                </div>
                <div class="h-2 rounded-full bg-slate-100">
                  <div
                    class="h-2 rounded-full bg-teal-500"
                    [style.width.%]="d.totalBookings ? (s.count / d.totalBookings) * 100 : 0"
                  ></div>
                </div>
              </div>
            }
          </div>
        </section>
      </div>
      <div class="mt-6 grid gap-6 lg:grid-cols-2">
        <section class="card">
          <h2 class="text-lg font-black">Popular tours</h2>
          <div class="mt-4 space-y-3">
            @for (t of d.popularTours; track t.tourId) {
              <div class="flex items-center justify-between rounded-xl bg-slate-50 p-3">
                <div>
                  <b>{{ t.name }}</b
                  ><small class="block text-slate-400">{{ t.bookings }} bookings</small>
                </div>
                <b class="text-teal-700">{{ t.revenue | currency: 'USD' }}</b>
              </div>
            } @empty {
              <p class="text-sm text-slate-500">No tour bookings in this period.</p>
            }
          </div>
        </section>
        <section class="card">
          <h2 class="text-lg font-black">Resource health</h2>
          <div class="mt-5 grid grid-cols-2 gap-4">
            <div class="rounded-2xl bg-teal-50 p-5">
              <b class="text-3xl text-teal-800">{{ d.activeDrivers }}</b
              ><small class="block text-teal-700">Active drivers</small>
            </div>
            <div class="rounded-2xl bg-blue-50 p-5">
              <b class="text-3xl text-blue-800">{{ d.activeVehicles }}</b
              ><small class="block text-blue-700">Active vehicles</small>
            </div>
          </div>
        </section>
      </div>
    } @else {
      <div class="mt-7 grid gap-4 sm:grid-cols-4">
        @for (i of [1, 2, 3, 4]; track i) {
          <div class="skeleton h-28"></div>
        }
      </div>
    }
  </div>`,
})
export class DashboardPage {
  private api = inject(ApiService);
  data = signal<Dashboard | null>(null);
  statuses = bookingStatuses;
  constructor() {
    this.api.get<Dashboard>('/api/dashboard').subscribe((d) => this.data.set(d));
  }
}
