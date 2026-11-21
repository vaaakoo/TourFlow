import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { Page, Payment, paymentMethods, paymentStatuses } from '../../core/models';
import { apiError } from '../../shared/errors';
import { ToastService } from '../../shared/toast.service';
@Component({
  imports: [FormsModule, ReactiveFormsModule, CurrencyPipe, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div>
    <div class="flex justify-between">
      <div>
        <h1 class="page-title">Payments</h1>
        <p class="page-subtitle">Internal payment records only—no card data is stored.</p>
      </div>
      <button class="btn-primary" (click)="showCreate.set(true)">Record payment</button>
    </div>
    <div class="card mt-6 flex gap-3">
      <input class="input" [(ngModel)]="bookingId" placeholder="Filter by booking ID" /><select
        class="input max-w-52"
        [(ngModel)]="status"
      >
        <option value="">All statuses</option>
        @for (s of statuses; track $index) {
          <option [value]="$index">{{ s }}</option>
        }</select
      ><button class="btn-secondary" (click)="load()">Filter</button>
    </div>
    <div class="table-wrap mt-6">
      <table class="data-table">
        <thead>
          <tr>
            <th>Created</th>
            <th>Booking ID</th>
            <th>Method</th>
            <th>Amount</th>
            <th>Status</th>
            <th>Reference</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (p of items(); track p.id) {
            <tr>
              <td>{{ p.createdAt | date: 'mediumDate' }}</td>
              <td class="font-mono text-xs">{{ p.bookingId }}</td>
              <td>{{ methods[p.paymentMethod] }}</td>
              <td class="font-bold">{{ p.amount | currency: p.currency }}</td>
              <td>
                <span
                  class="badge"
                  [class.badge-green]="p.status === 1"
                  [class.badge-amber]="p.status === 0"
                  [class.badge-red]="p.status === 2"
                  [class.badge-slate]="p.status === 3"
                  >{{ statuses[p.status] }}</span
                >
              </td>
              <td>{{ p.transactionReference || '—' }}</td>
              <td>
                @if (p.status === 0) {
                  <button class="font-bold text-teal-700" (click)="change(p, 1)">Mark paid</button
                  ><button class="ml-3 font-bold text-red-600" (click)="change(p, 2)">Fail</button>
                } @else if (p.status === 1) {
                  <button class="font-bold text-slate-600" (click)="change(p, 3)">Refund</button>
                }
              </td>
            </tr>
          } @empty {
            <tr>
              <td colspan="7"><div class="empty border-0">No payments found.</div></td>
            </tr>
          }
        </tbody>
      </table>
    </div>
    @if (showCreate()) {
      <div class="modal-backdrop" (click)="showCreate.set(false)">
        <form
          [formGroup]="form"
          (ngSubmit)="create()"
          class="modal"
          (click)="$event.stopPropagation()"
        >
          <div class="flex justify-between">
            <h2 class="text-2xl font-black">Record payment</h2>
            <button type="button" class="btn-ghost" (click)="showCreate.set(false)">✕</button>
          </div>
          <div class="mt-6 grid gap-4 sm:grid-cols-2">
            <div class="field sm:col-span-2">
              <label>Booking ID</label><input class="input" formControlName="bookingId" />
            </div>
            <div class="field">
              <label>Amount</label
              ><input class="input" type="number" step=".01" formControlName="amount" />
            </div>
            <div class="field">
              <label>Currency</label
              ><input class="input" maxlength="3" formControlName="currency" />
            </div>
            <div class="field sm:col-span-2">
              <label>Method</label
              ><select class="input" formControlName="paymentMethod">
                @for (m of methods; track $index) {
                  <option [ngValue]="$index">{{ m }}</option>
                }
              </select>
            </div>
          </div>
          @if (error()) {
            <p class="mt-4 text-sm text-red-600">{{ error() }}</p>
          }
          <div class="mt-6 flex justify-end gap-3">
            <button type="button" class="btn-secondary" (click)="showCreate.set(false)">
              Cancel</button
            ><button class="btn-primary" [disabled]="form.invalid || saving()">Save record</button>
          </div>
        </form>
      </div>
    }
  </div>`,
})
export class PaymentsPage {
  private api = inject(ApiService);
  private fb = inject(FormBuilder);
  private toast = inject(ToastService);
  items = signal<Payment[]>([]);
  showCreate = signal(false);
  saving = signal(false);
  error = signal('');
  bookingId = '';
  status = '';
  statuses = paymentStatuses;
  methods = paymentMethods;
  form = this.fb.nonNullable.group({
    bookingId: ['', Validators.required],
    amount: [0, [Validators.required, Validators.min(0.01)]],
    currency: ['USD', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]],
    paymentMethod: [0, Validators.required],
  });
  constructor() {
    this.load();
  }
  load() {
    this.api
      .get<Page<Payment>>('/api/payments', {
        pageSize: 100,
        bookingId: this.bookingId,
        status: this.status,
      })
      .subscribe((p) => this.items.set(p.items));
  }
  create() {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.api
      .post('/api/payments', this.form.getRawValue())
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.showCreate.set(false);
          this.toast.show('Payment recorded');
          this.load();
        },
        error: (e) => this.error.set(apiError(e)),
      });
  }
  change(p: Payment, status: number) {
    const reference = status === 1 ? prompt('Transaction reference') : null;
    if (status === 1 && !reference) return;
    this.api
      .put(`/api/payments/${p.id}/status`, { status, transactionReference: reference })
      .subscribe({
        next: () => {
          this.toast.show('Payment status updated');
          this.load();
        },
        error: (e) => this.toast.show(apiError(e), true),
      });
  }
}
