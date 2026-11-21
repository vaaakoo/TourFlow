import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { Booking, Customer, Page, Tour, bookingStatuses, bookingTypes } from '../../core/models';
import { apiError } from '../../shared/errors';
import { StatusBadge } from '../../shared/status-badge';
import { ToastService } from '../../shared/toast.service';
@Component({
  imports: [RouterLink, FormsModule, ReactiveFormsModule, DatePipe, CurrencyPipe, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div>
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="page-title">{{ staff ? 'Booking operations' : 'My bookings' }}</h1>
        <p class="page-subtitle">
          {{
            staff
              ? 'Search and manage reservations across the business.'
              : 'Track every tour and transfer reservation.'
          }}
        </p>
      </div>
      <button class="btn-primary" (click)="showCreate.set(true)">
        {{ staff ? 'Create booking' : 'Book a transfer' }}
      </button>
    </div>
    <div class="card mt-6 flex flex-col gap-3 md:flex-row">
      <input
        class="input flex-1"
        [(ngModel)]="search"
        placeholder="Booking number or location"
        (keyup.enter)="load()"
      /><select class="input md:w-48" [(ngModel)]="status">
        <option value="">All statuses</option>
        @for (label of statuses; track $index) {
          <option [value]="$index">{{ label }}</option>
        }</select
      ><button class="btn-secondary" (click)="load()">Filter</button>
    </div>
    @if (loading()) {
      <div class="mt-6 space-y-3">
        @for (i of [1, 2, 3]; track i) {
          <div class="skeleton h-24"></div>
        }
      </div>
    } @else if (page()?.items?.length) {
      <div class="table-wrap mt-6">
        <table class="data-table">
          <thead>
            <tr>
              <th>Booking</th>
              <th>Date</th>
              <th>Type</th>
              <th>Passengers</th>
              <th>Total</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (item of page()!.items; track item.id) {
              <tr>
                <td>
                  <b>{{ item.bookingNumber }}</b><small class="block text-slate-400">{{
                    item.tourName || item.pickupLocation
                  }}</small>
                </td>
                <td>
                  {{ item.bookingDate | date: 'mediumDate'
                  }}<small class="block text-slate-400">{{ item.startTime }}</small>
                </td>
                <td>{{ types[item.bookingType] }}</td>
                <td>{{ item.passengerCount }}</td>
                <td>{{ item.totalPrice | currency: 'USD' }}</td>
                <td><app-status-badge [status]="item.status" /></td>
                <td>
                  <a
                    [routerLink]="
                      staff ? ['/operations/bookings', item.id] : ['/portal/bookings', item.id]
                    "
                    class="font-bold text-teal-700"
                    >View</a
                  >
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <div class="empty mt-6">
        <div>
          <b class="text-slate-800">No bookings found</b>
          <p class="mt-1 text-sm">Your reservations will appear here.</p>
        </div>
      </div>
    }
    @if (showCreate()) {
      <div class="modal-backdrop" (click)="showCreate.set(false)">
        <div class="modal" (click)="$event.stopPropagation()">
          <div class="flex justify-between">
            <div>
              <h2 class="text-2xl font-black">
                {{ staff ? 'Create customer booking' : 'Book an airport transfer' }}
              </h2>
              <p class="mt-1 text-sm text-slate-500">
                Pricing is calculated by the API after submission.
              </p>
            </div>
            <button class="btn-ghost" (click)="showCreate.set(false)">✕</button>
          </div>
          <form [formGroup]="form" (ngSubmit)="create()" class="mt-6 grid gap-4 sm:grid-cols-2">
            @if (staff) {
              <div class="field">
                <label>Customer</label
                ><select class="input" formControlName="customerId">
                  <option value="">Select customer</option>
                  @for (customer of customers(); track customer.id) {
                    <option [value]="customer.id">
                      {{ customer.firstName }} {{ customer.lastName }} · {{ customer.email }}
                    </option>
                  }
                </select>
              </div>
              <div class="field">
                <label>Booking type</label
                ><select class="input" formControlName="bookingType">
                  <option [ngValue]="1">Transfer</option>
                  <option [ngValue]="0">Tour</option>
                </select>
              </div>
              @if (form.value.bookingType === 0) {
                <div class="field sm:col-span-2">
                  <label>Tour</label
                  ><select class="input" formControlName="tourId">
                    <option value="">Select tour</option>
                    @for (tour of tours(); track tour.id) {
                      <option [value]="tour.id">
                        {{ tour.name }} · {{ tour.price | currency: 'USD' }}
                      </option>
                    }
                  </select>
                </div>
              }
            }
            <div class="field">
              <label>Date</label><input class="input" type="date" formControlName="bookingDate" />
            </div>
            <div class="field">
              <label>Pickup time</label
              ><input class="input" type="time" formControlName="startTime" />
            </div>
            <div class="field">
              <label>Passengers</label
              ><input class="input" type="number" min="1" formControlName="passengerCount" />
            </div>
            <div></div>
            <div class="field">
              <label>Pickup</label><input class="input" formControlName="pickupLocation" />
            </div>
            <div class="field">
              <label>Drop-off</label><input class="input" formControlName="dropoffLocation" />
            </div>
            <div class="field sm:col-span-2">
              <label>Special requests</label
              ><textarea class="input" formControlName="specialRequests"></textarea>
            </div>
            @if (error()) {
              <p class="text-sm text-red-600 sm:col-span-2">{{ error() }}</p>
            }
            <div class="flex justify-end gap-3 sm:col-span-2">
              <button type="button" class="btn-secondary" (click)="showCreate.set(false)">
                Cancel</button
              ><button class="btn-primary" [disabled]="form.invalid || saving()">
                Create booking
              </button>
            </div>
          </form>
        </div>
      </div>
    }
  </div>`,
})
export class BookingsPage {
  private api = inject(ApiService);
  private fb = inject(FormBuilder);
  private toast = inject(ToastService);
  staff = false;
  page = signal<Page<Booking> | null>(null);
  customers = signal<Customer[]>([]);
  tours = signal<Tour[]>([]);
  loading = signal(true);
  showCreate = signal(false);
  saving = signal(false);
  error = signal('');
  search = '';
  status = '';
  statuses = bookingStatuses;
  types = bookingTypes;
  form = this.fb.nonNullable.group({
    bookingDate: [this.tomorrow(), Validators.required],
    bookingType: [1, Validators.required],
    customerId: [''],
    tourId: [''],
    startTime: ['10:00', Validators.required],
    passengerCount: [1, [Validators.required, Validators.min(1)]],
    pickupLocation: ['', Validators.required],
    dropoffLocation: ['', Validators.required],
    specialRequests: [''],
  });
  constructor() {
    this.staff = location.pathname.startsWith('/operations');
    if (this.staff) {
      this.api
        .get<Page<Customer>>('/api/customers', { pageSize: 100, sort: 'name' })
        .subscribe((x) => this.customers.set(x.items));
      this.api
        .get<Page<Tour>>('/api/tours', { pageSize: 100, isActive: true, sort: 'name' })
        .subscribe((x) => this.tours.set(x.items));
    }
    this.load();
  }
  load() {
    this.loading.set(true);
    this.api
      .get<Page<Booking>>('/api/bookings', {
        pageSize: 50,
        search: this.search,
        status: this.status,
        sort: 'date',
        descending: true,
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe((r) => this.page.set(r));
  }
  create() {
    if (this.form.invalid) return;
    if (this.staff && !this.form.value.customerId) {
      this.error.set('Select a customer.');
      return;
    }
    if (this.form.value.bookingType === 0 && !this.form.value.tourId) {
      this.error.set('Select a tour.');
      return;
    }
    this.saving.set(true);
    this.api
      .post<Booking>('/api/bookings', {
        ...this.form.getRawValue(),
        tourId: this.form.value.bookingType === 0 ? this.form.value.tourId : null,
        customerId: this.staff ? this.form.value.customerId : null,
        specialRequests: this.form.value.specialRequests || null,
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.showCreate.set(false);
          this.toast.show('Transfer booking created');
          this.load();
        },
        error: (e) => this.error.set(apiError(e)),
      });
  }
  private tomorrow() {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    return d.toISOString().slice(0, 10);
  }
}
