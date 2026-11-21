import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import {
  Assignment,
  Booking,
  BookingHistory,
  Driver,
  Page,
  Vehicle,
  bookingStatuses,
  bookingTypes,
} from '../../core/models';
import { apiError } from '../../shared/errors';
import { StatusBadge } from '../../shared/status-badge';
import { ToastService } from '../../shared/toast.service';
@Component({
  imports: [RouterLink, DatePipe, CurrencyPipe, ReactiveFormsModule, StatusBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="mx-auto max-w-5xl">
    @if (item(); as b) {
      <a
        [routerLink]="staff ? '/operations/bookings' : '/portal/bookings'"
        class="text-sm font-bold text-teal-700"
        >← Back to bookings</a
      >
      <div class="mt-5 flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="page-title">{{ b.bookingNumber }}</h1>
            <app-status-badge [status]="b.status" />
          </div>
          <p class="page-subtitle">Created {{ b.createdAt | date: 'medium' }}</p>
        </div>
        <div class="flex flex-wrap gap-2">
          @if (b.status === 0 && staff) {
            <button class="btn-primary" (click)="transition(1)">Confirm</button
            ><button class="btn-secondary text-red-600" (click)="transition(6)">Reject</button>
          }
          @if (staff && auth.hasRole('ADMINISTRATOR', 'MANAGER') && b.status === 2) {
            <button class="btn-primary" (click)="transition(3)">Start trip</button>
          }
          @if (staff && auth.hasRole('ADMINISTRATOR', 'MANAGER') && b.status === 3) {
            <button class="btn-primary" (click)="transition(4)">Complete trip</button>
          }
          @if (
            (b.status === 0 || b.status === 1 || b.status === 2) &&
            (staff || auth.hasRole('CUSTOMER'))
          ) {
            <button class="btn-secondary text-red-600" (click)="transition(5)">
              Cancel booking
            </button>
          }
        </div>
      </div>
      <div class="mt-7 grid gap-6 lg:grid-cols-[1fr_340px]">
        <div class="space-y-6">
          <section class="card">
            <h2 class="text-lg font-black">Trip details</h2>
            <dl class="mt-5 grid gap-5 sm:grid-cols-2">
              <div>
                <dt class="text-xs font-bold uppercase text-slate-400">Type</dt>
                <dd class="mt-1 font-bold">
                  {{ types[b.bookingType] }}{{ b.tourName ? ' · ' + b.tourName : '' }}
                </dd>
              </div>
              <div>
                <dt class="text-xs font-bold uppercase text-slate-400">Schedule</dt>
                <dd class="mt-1 font-bold">
                  {{ b.bookingDate | date: 'fullDate' }} at {{ b.startTime }}
                </dd>
              </div>
              <div>
                <dt class="text-xs font-bold uppercase text-slate-400">Passengers</dt>
                <dd class="mt-1 font-bold">{{ b.passengerCount }}</dd>
              </div>
              <div>
                <dt class="text-xs font-bold uppercase text-slate-400">Total</dt>
                <dd class="mt-1 text-xl font-black text-teal-700">
                  {{ b.totalPrice | currency: 'USD' }}
                </dd>
              </div>
              <div>
                <dt class="text-xs font-bold uppercase text-slate-400">Pickup</dt>
                <dd class="mt-1">{{ b.pickupLocation }}</dd>
              </div>
              <div>
                <dt class="text-xs font-bold uppercase text-slate-400">Drop-off</dt>
                <dd class="mt-1">{{ b.dropoffLocation || '—' }}</dd>
              </div>
            </dl>
            @if (b.specialRequests) {
              <div class="mt-5 rounded-xl bg-slate-50 p-4 text-sm">
                <b>Special requests</b>
                <p class="mt-1 text-slate-600">{{ b.specialRequests }}</p>
              </div>
            }
          </section>
          @if (activeAssignment(); as assignment) {
            <section class="card">
              <div class="flex items-start justify-between gap-4">
                <div>
                  <h2 class="text-lg font-black">Active assignment</h2>
                  <p class="mt-1 text-sm text-slate-500">
                    {{ assignment.driverName }} · {{ assignment.vehicleName }} ·
                    {{ assignment.startTime }}–{{ assignment.endTime }}
                  </p>
                </div>
                @if (auth.hasRole('ADMINISTRATOR', 'MANAGER')) {
                  <button class="btn-secondary text-red-600" (click)="cancelAssignment(assignment)">
                    Cancel assignment
                  </button>
                }
              </div>
            </section>
          }
          @if (staff && b.status === 1 && auth.hasRole('ADMINISTRATOR', 'MANAGER')) {
            <section class="card">
              <h2 class="text-lg font-black">Assign resources</h2>
              <form
                [formGroup]="assignForm"
                (ngSubmit)="assign()"
                class="mt-4 grid gap-3 sm:grid-cols-2"
              >
                <div class="field">
                  <label>Driver</label
                  ><select class="input" formControlName="driverId">
                    <option value="">Select driver</option>
                    @for (driver of drivers(); track driver.id) {
                      <option [value]="driver.id">
                        {{ driver.firstName }} {{ driver.lastName }} · {{ driver.licenseNumber }}
                      </option>
                    }
                  </select>
                </div>
                <div class="field">
                  <label>Vehicle</label
                  ><select class="input" formControlName="vehicleId">
                    <option value="">Select vehicle</option>
                    @for (vehicle of vehicles(); track vehicle.id) {
                      <option [value]="vehicle.id">
                        {{ vehicle.name }} · {{ vehicle.registrationNumber }} ({{
                          vehicle.passengerCapacity
                        }})
                      </option>
                    }
                  </select>
                </div>
                <div class="field">
                  <label>End time</label
                  ><input type="time" class="input" formControlName="endTime" />
                </div>
                <button class="btn-primary self-end" [disabled]="assignForm.invalid">Assign</button>
              </form>
            </section>
          }
        </div>
        <aside class="card h-fit">
          <h2 class="text-lg font-black">Status history</h2>
          <ol class="mt-5 space-y-5">
            @for (h of history(); track h.id) {
              <li class="relative border-l-2 border-teal-200 pl-4">
                <span class="absolute -left-[7px] top-1 size-3 rounded-full bg-teal-500"></span
                ><b class="text-sm">{{ statuses[h.newStatus] }}</b>
                <p class="text-xs text-slate-400">{{ h.changedAt | date: 'medium' }}</p>
                @if (h.comment) {
                  <p class="mt-1 text-sm text-slate-600">{{ h.comment }}</p>
                }
              </li>
            } @empty {
              <li class="text-sm text-slate-500">No status changes yet.</li>
            }
          </ol>
        </aside>
      </div>
      @if (error()) {
        <div class="mt-5 rounded-xl bg-red-50 p-3 text-sm text-red-700">{{ error() }}</div>
      }
    } @else {
      <div class="skeleton h-96"></div>
    }
  </div>`,
})
export class BookingDetailPage {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private fb = inject(FormBuilder);
  private toast = inject(ToastService);
  auth = inject(AuthService);
  item = signal<Booking | null>(null);
  history = signal<BookingHistory[]>([]);
  drivers = signal<Driver[]>([]);
  vehicles = signal<Vehicle[]>([]);
  assignments = signal<Assignment[]>([]);
  error = signal('');
  staff = location.pathname.startsWith('/operations');
  statuses = bookingStatuses;
  types = bookingTypes;
  assignForm = this.fb.nonNullable.group({
    driverId: ['', Validators.required],
    vehicleId: ['', Validators.required],
    endTime: ['', Validators.required],
  });
  constructor() {
    this.load();
    if (this.staff && this.auth.hasRole('ADMINISTRATOR', 'MANAGER')) {
      forkJoin({
        drivers: this.api.get<Page<Driver>>('/api/drivers', { pageSize: 100, isActive: true }),
        vehicles: this.api.get<Page<Vehicle>>('/api/vehicles', { pageSize: 100, isActive: true }),
        assignments: this.api.get<Page<Assignment>>('/api/assignments', { pageSize: 100 }),
      }).subscribe((result) => {
        this.drivers.set(result.drivers.items);
        this.vehicles.set(result.vehicles.items);
        this.assignments.set(result.assignments.items);
      });
    }
  }
  activeAssignment() {
    return this.assignments().find((x) => x.bookingId === this.item()?.id && x.isActive);
  }
  cancelAssignment(assignment: Assignment) {
    const booking = this.item();
    if (!booking || !confirm('Cancel this assignment?')) return;
    this.api
      .delete(`/api/bookings/${booking.id}/assignments/${assignment.id}`, {
        version: booking.version,
      })
      .subscribe({
        next: () => {
          this.toast.show('Assignment cancelled');
          this.assignments.update((x) => x.filter((a) => a.id !== assignment.id));
          this.load();
        },
        error: (e) => this.error.set(apiError(e)),
      });
  }
  load() {
    const id = this.route.snapshot.paramMap.get('id')!;
    forkJoin({
      item: this.api.get<Booking>(`/api/bookings/${id}`),
      history: this.api.get<BookingHistory[]>(`/api/bookings/${id}/history`),
    }).subscribe({
      next: (r) => {
        this.item.set(r.item);
        this.history.set(r.history);
      },
      error: () => this.router.navigateByUrl('/not-found'),
    });
  }
  transition(status: number) {
    const b = this.item();
    if (!b) return;
    const path =
      status === 1
        ? `/api/bookings/${b.id}/confirm`
        : status === 5
          ? `/api/bookings/${b.id}/cancel`
          : `/api/bookings/${b.id}/status`;
    const body =
      status === 1 || status === 5
        ? { version: b.version, comment: null }
        : { status, version: b.version, comment: null };
    const request =
      status === 1 || status === 5
        ? this.api.post<Booking>(path, body)
        : this.api.put<Booking>(path, body);
    request.subscribe({
      next: () => {
        this.toast.show('Booking status updated');
        this.load();
      },
      error: (e) => this.error.set(apiError(e)),
    });
  }
  assign() {
    const b = this.item();
    if (!b || this.assignForm.invalid) return;
    this.api
      .post(`/api/bookings/${b.id}/assign`, {
        ...this.assignForm.getRawValue(),
        assignmentDate: b.bookingDate,
        startTime: b.startTime,
        version: b.version,
      })
      .subscribe({
        next: () => {
          this.toast.show('Resources assigned');
          this.load();
          this.api
            .get<Page<Assignment>>('/api/assignments', { pageSize: 100 })
            .subscribe((x) => this.assignments.set(x.items));
        },
        error: (e) => this.error.set(apiError(e)),
      });
  }
}
