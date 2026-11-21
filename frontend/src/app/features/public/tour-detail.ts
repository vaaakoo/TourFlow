import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Booking, Tour, tourTypes } from '../../core/models';
import { apiError } from '../../shared/errors';
import { ToastService } from '../../shared/toast.service';
@Component({
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="mx-auto max-w-6xl px-4 py-10 sm:px-6 lg:px-8">
    @if (tour(); as item) {
      <a routerLink="/tours" class="text-sm font-bold text-teal-700">← Back to tours</a>
      <div class="mt-6 overflow-hidden rounded-[2rem] bg-slate-900">
        <div
          class="relative grid min-h-80 items-end bg-gradient-to-br from-teal-500 to-slate-950 p-8 text-white sm:p-12"
        >
          @if (item.imageUrl) {
            <img
              [src]="item.imageUrl"
              [alt]="item.name"
              class="absolute inset-0 h-full w-full object-cover opacity-60"
            />
            <div class="absolute inset-0 bg-gradient-to-t from-slate-950 via-slate-950/20"></div>
          }
          <div class="relative">
            <span class="badge bg-white/15 text-white">{{ types[item.tourType] }}</span>
            <h1 class="mt-4 text-4xl font-black sm:text-5xl">{{ item.name }}</h1>
          </div>
        </div>
      </div>
      <div class="mt-8 grid gap-8 lg:grid-cols-[1fr_380px]">
        <article class="card">
          <h2 class="text-xl font-black">About this experience</h2>
          <p class="mt-4 whitespace-pre-line leading-7 text-slate-600">{{ item.description }}</p>
          <div class="mt-8 grid grid-cols-3 gap-4 border-t pt-6 text-center">
            <div>
              <b class="block text-xl">{{ item.durationMinutes }}</b
              ><small class="text-slate-500">minutes</small>
            </div>
            <div>
              <b class="block text-xl">{{ item.maxPassengers }}</b
              ><small class="text-slate-500">max guests</small>
            </div>
            <div>
              <b class="block text-xl text-teal-700">{{ item.price | currency: 'USD' }}</b
              ><small class="text-slate-500">per person</small>
            </div>
          </div>
        </article>
        <aside class="card h-fit lg:sticky lg:top-24">
          <h2 class="text-xl font-black">Reserve your tour</h2>
          @if (!auth.authenticated()) {
            <p class="mt-3 text-sm text-slate-500">
              Sign in as a customer to book this experience.
            </p>
            <a
              [routerLink]="['/login']"
              [queryParams]="{ returnUrl: '/tours/' + item.id }"
              class="btn-primary mt-5 w-full"
              >Sign in to book</a
            >
          } @else if (!auth.hasRole('CUSTOMER')) {
            <p class="mt-3 rounded-xl bg-amber-50 p-3 text-sm text-amber-800">
              Customer accounts can create public reservations.
            </p>
          } @else {
            <form [formGroup]="form" (ngSubmit)="book(item)" class="mt-5 space-y-4">
              <div class="field">
                <label>Date</label><input class="input" type="date" formControlName="bookingDate" />
              </div>
              <div class="field">
                <label>Start time</label
                ><input class="input" type="time" formControlName="startTime" />
              </div>
              <div class="field">
                <label>Passengers</label
                ><input
                  class="input"
                  type="number"
                  min="1"
                  [max]="item.maxPassengers"
                  formControlName="passengerCount"
                />
              </div>
              <div class="field">
                <label>Pickup location</label
                ><input class="input" formControlName="pickupLocation" />
              </div>
              <div class="field">
                <label>Special requests</label
                ><textarea class="input min-h-24" formControlName="specialRequests"></textarea>
              </div>
              @if (error()) {
                <p class="text-sm font-semibold text-red-600">{{ error() }}</p>
              }
              <div class="rounded-xl bg-teal-50 p-4">
                <div class="flex justify-between text-sm">
                  <span>Estimated total</span
                  ><b class="text-teal-800">{{
                    item.price * (form.value.passengerCount || 0) | currency: 'USD'
                  }}</b>
                </div>
                <small class="mt-1 block text-slate-500"
                  >Final price is always calculated by the server.</small
                >
              </div>
              <button class="btn-primary w-full" [disabled]="form.invalid || saving()">
                {{ saving() ? 'Creating booking…' : 'Reserve now' }}
              </button>
            </form>
          }
        </aside>
      </div>
    } @else {
      <div class="skeleton h-96"></div>
    }
  </div>`,
})
export class TourDetailPage {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private fb = inject(FormBuilder);
  private toast = inject(ToastService);
  auth = inject(AuthService);
  types = tourTypes;
  tour = signal<Tour | null>(null);
  saving = signal(false);
  error = signal('');
  form = this.fb.nonNullable.group({
    bookingDate: [this.tomorrow(), Validators.required],
    startTime: ['10:00', Validators.required],
    passengerCount: [1, [Validators.required, Validators.min(1)]],
    pickupLocation: ['', Validators.required],
    specialRequests: [''],
  });
  constructor() {
    this.api
      .get<Tour>(`/api/tours/${this.route.snapshot.paramMap.get('id')}`)
      .subscribe({
        next: (t) => this.tour.set(t),
        error: () => this.router.navigateByUrl('/not-found'),
      });
  }
  book(tour: Tour) {
    if (this.form.invalid) return;
    this.saving.set(true);
    const v = this.form.getRawValue();
    this.api
      .post<Booking>('/api/bookings', {
        bookingType: 0,
        tourId: tour.id,
        customerId: null,
        bookingDate: v.bookingDate,
        startTime: v.startTime,
        passengerCount: v.passengerCount,
        pickupLocation: v.pickupLocation,
        dropoffLocation: null,
        specialRequests: v.specialRequests || null,
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (b) => {
          this.toast.show('Booking created');
          this.router.navigate(['/portal/bookings', b.id]);
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
