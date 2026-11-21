import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import {
  FormsModule,
  ReactiveFormsModule,
  UntypedFormBuilder,
  UntypedFormGroup,
  Validators,
} from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Destination, Page, tourTypes, vehicleTypes } from '../../core/models';
import { apiError } from '../../shared/errors';
import { ToastService } from '../../shared/toast.service';
type Kind = 'destinations' | 'tours' | 'customers' | 'vehicles' | 'drivers';
type Row = Record<string, unknown> & { id: string; isActive?: boolean };
const titles: Record<Kind, string> = {
  destinations: 'Destinations',
  tours: 'Tours',
  customers: 'Customers',
  vehicles: 'Vehicles',
  drivers: 'Drivers',
};
@Component({
  imports: [FormsModule, ReactiveFormsModule, CurrencyPipe, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div>
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="page-title">{{ title }}</h1>
        <p class="page-subtitle">Search, review, and maintain {{ title.toLowerCase() }}.</p>
      </div>
      @if (canWrite() && kind !== 'customers') {
        <button class="btn-primary" (click)="open()">Add {{ singular() }}</button>
      }
    </div>
    <div class="card mt-6 flex gap-3">
      <input
        class="input flex-1"
        [(ngModel)]="search"
        [placeholder]="'Search ' + title.toLowerCase()"
        (keyup.enter)="load()"
      /><button class="btn-secondary" (click)="load()">Search</button>
    </div>
    @if (loading()) {
      <div class="mt-6 skeleton h-80"></div>
    } @else {
      <div class="table-wrap mt-6">
        <table class="data-table">
          <thead>
            <tr>
              @for (c of columns(); track c.key) {
                <th>{{ c.label }}</th>
              }
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.id) {
              <tr>
                @for (c of columns(); track c.key) {
                  <td>
                    @if (c.key === 'price') {
                      <b>{{ asNumber(row[c.key]) | currency: 'USD' }}</b>
                    } @else if (c.key === 'createdAt') {
                      {{ asString(row[c.key]) | date: 'mediumDate' }}
                    } @else if (c.options) {
                      {{ c.options[asNumber(row[c.key])] }}
                    } @else {
                      {{ row[c.key] }}
                    }
                  </td>
                }
                <td>
                  @if (kind === 'customers') {
                    <span class="badge badge-slate">Profile</span>
                  } @else {
                    <span
                      class="badge"
                      [class.badge-green]="row.isActive"
                      [class.badge-slate]="!row.isActive"
                      >{{ row.isActive ? 'Active' : 'Inactive' }}</span
                    >
                  }
                </td>
                <td>
                  <div class="flex gap-2">
                    @if (canWrite() || kind === 'customers') {
                      <button class="font-bold text-teal-700" (click)="open(row)">Edit</button>
                    }
                    @if (canWrite() && kind !== 'customers' && row.isActive) {
                      <button class="font-bold text-red-600" (click)="deactivate(row)">
                        Deactivate
                      </button>
                    }
                  </div>
                </td>
              </tr>
            } @empty {
              <tr>
                <td [attr.colspan]="columns().length + 2">
                  <div class="empty border-0 shadow-none">No records found.</div>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
    @if (editing()) {
      <div class="modal-backdrop" (click)="close()">
        <div class="modal" (click)="$event.stopPropagation()">
          <div class="flex justify-between">
            <div>
              <h2 class="text-2xl font-black">
                {{ selected() ? 'Edit' : 'Add' }} {{ singular() }}
              </h2>
              <p class="mt-1 text-sm text-slate-500">Changes are validated by the TourFlow API.</p>
            </div>
            <button class="btn-ghost" (click)="close()">✕</button>
          </div>
          <form [formGroup]="form" (ngSubmit)="save()" class="mt-6 grid gap-4 sm:grid-cols-2">
            @switch (kind) {
              @case ('destinations') {
                <div class="field">
                  <label>Name</label><input class="input" formControlName="name" />
                </div>
                <div class="field">
                  <label>Country</label><input class="input" formControlName="country" />
                </div>
                <div class="field sm:col-span-2">
                  <label>Description</label
                  ><textarea class="input" formControlName="description"></textarea>
                </div>
                <div class="field sm:col-span-2">
                  <label>Image URL</label
                  ><input class="input" type="url" formControlName="imageUrl" />
                </div>
              }
              @case ('tours') {
                <div class="field">
                  <label>Name</label><input class="input" formControlName="name" />
                </div>
                <div class="field">
                  <label>Destination</label
                  ><select class="input" formControlName="destinationId">
                    @for (d of destinations(); track d.id) {
                      <option [value]="d.id">{{ d.name }}</option>
                    }
                  </select>
                </div>
                <div class="field">
                  <label>Tour type</label
                  ><select class="input" formControlName="tourType">
                    @for (x of tourTypes; track $index) {
                      <option [ngValue]="$index">{{ x }}</option>
                    }
                  </select>
                </div>
                <div class="field">
                  <label>Price per person</label
                  ><input
                    class="input"
                    type="number"
                    min="0.01"
                    step="0.01"
                    formControlName="price"
                  />
                </div>
                <div class="field">
                  <label>Duration (minutes)</label
                  ><input class="input" type="number" min="1" formControlName="durationMinutes" />
                </div>
                <div class="field">
                  <label>Max passengers</label
                  ><input class="input" type="number" min="1" formControlName="maxPassengers" />
                </div>
                <div class="field sm:col-span-2">
                  <label>Description</label
                  ><textarea class="input min-h-24" formControlName="description"></textarea>
                </div>
                <div class="field sm:col-span-2">
                  <label>Image URL</label><input class="input" formControlName="imageUrl" />
                </div>
              }
              @case ('customers') {
                <div class="field">
                  <label>First name</label><input class="input" formControlName="firstName" />
                </div>
                <div class="field">
                  <label>Last name</label><input class="input" formControlName="lastName" />
                </div>
                <div class="field sm:col-span-2">
                  <label>Phone</label><input class="input" formControlName="phoneNumber" />
                </div>
              }
              @case ('vehicles') {
                <div class="field">
                  <label>Name</label><input class="input" formControlName="name" />
                </div>
                <div class="field">
                  <label>Registration</label
                  ><input class="input" formControlName="registrationNumber" />
                </div>
                <div class="field">
                  <label>Vehicle type</label
                  ><select class="input" formControlName="vehicleType">
                    @for (x of vehicleTypes; track $index) {
                      <option [ngValue]="$index">{{ x }}</option>
                    }
                  </select>
                </div>
                <div class="field">
                  <label>Passenger capacity</label
                  ><input class="input" type="number" min="1" formControlName="passengerCapacity" />
                </div>
                <div class="field sm:col-span-2">
                  <label>Description</label
                  ><textarea class="input" formControlName="description"></textarea>
                </div>
              }
              @case ('drivers') {
                @if (!selected()) {
                  <div class="field sm:col-span-2">
                    <label>Identity user ID</label
                    ><input class="input" formControlName="userId" /><small class="text-slate-400"
                      >The account must already have the DRIVER role.</small
                    >
                  </div>
                }
                <div class="field">
                  <label>First name</label><input class="input" formControlName="firstName" />
                </div>
                <div class="field">
                  <label>Last name</label><input class="input" formControlName="lastName" />
                </div>
                <div class="field">
                  <label>Phone</label><input class="input" formControlName="phoneNumber" />
                </div>
                <div class="field">
                  <label>License number</label
                  ><input class="input" formControlName="licenseNumber" />
                </div>
              }
            }
            @if (error()) {
              <p class="text-sm font-semibold text-red-600 sm:col-span-2">{{ error() }}</p>
            }
            <div class="flex justify-end gap-3 sm:col-span-2">
              <button type="button" class="btn-secondary" (click)="close()">Cancel</button
              ><button class="btn-primary" [disabled]="form.invalid || saving()">
                {{ saving() ? 'Saving…' : 'Save changes' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    }
  </div>`,
})
export class ResourcesPage {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  private auth = inject(AuthService);
  private fb = inject(UntypedFormBuilder);
  private toast = inject(ToastService);
  kind = this.route.snapshot.data['kind'] as Kind;
  title = titles[this.kind];
  rows = signal<Row[]>([]);
  destinations = signal<Destination[]>([]);
  loading = signal(true);
  editing = signal(false);
  saving = signal(false);
  selected = signal<Row | null>(null);
  error = signal('');
  search = '';
  tourTypes = tourTypes;
  vehicleTypes = vehicleTypes;
  form: UntypedFormGroup = this.fb.group({});
  constructor() {
    if (this.kind === 'tours')
      this.api
        .get<Page<Destination>>('/api/destinations', { pageSize: 100, isActive: true })
        .subscribe((p) => this.destinations.set(p.items));
    this.load();
  }
  canWrite() {
    return this.auth.hasRole('ADMINISTRATOR', 'MANAGER');
  }
  singular() {
    return this.title.slice(0, -1);
  }
  columns() {
    switch (this.kind) {
      case 'destinations':
        return [
          { key: 'name', label: 'Name' },
          { key: 'country', label: 'Country' },
        ];
      case 'tours':
        return [
          { key: 'name', label: 'Name' },
          { key: 'tourType', label: 'Type', options: tourTypes },
          { key: 'price', label: 'Price' },
          { key: 'maxPassengers', label: 'Capacity' },
        ];
      case 'customers':
        return [
          { key: 'firstName', label: 'First name' },
          { key: 'lastName', label: 'Last name' },
          { key: 'email', label: 'Email' },
          { key: 'phoneNumber', label: 'Phone' },
          { key: 'createdAt', label: 'Joined' },
        ];
      case 'vehicles':
        return [
          { key: 'name', label: 'Vehicle' },
          { key: 'registrationNumber', label: 'Registration' },
          { key: 'vehicleType', label: 'Type', options: vehicleTypes },
          { key: 'passengerCapacity', label: 'Capacity' },
        ];
      default:
        return [
          { key: 'firstName', label: 'First name' },
          { key: 'lastName', label: 'Last name' },
          { key: 'phoneNumber', label: 'Phone' },
          { key: 'licenseNumber', label: 'License' },
        ];
    }
  }
  load() {
    this.loading.set(true);
    this.api
      .get<Page<Row>>(`/api/${this.kind}`, {
        pageSize: 100,
        search: this.search,
        sort: this.kind === 'customers' ? 'name' : 'name',
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe((r) => this.rows.set(r.items));
  }
  open(row?: Row) {
    this.selected.set(row ?? null);
    this.error.set('');
    this.buildForm(row);
    this.editing.set(true);
  }
  close() {
    this.editing.set(false);
  }
  buildForm(r?: Row) {
    const required = [Validators.required];
    switch (this.kind) {
      case 'destinations':
        this.form = this.fb.group({
          name: [r?.['name'] ?? '', required],
          country: [r?.['country'] ?? '', required],
          description: [r?.['description'] ?? ''],
          imageUrl: [r?.['imageUrl'] ?? ''],
          isActive: [r?.isActive ?? true],
        });
        break;
      case 'tours':
        this.form = this.fb.group({
          name: [r?.['name'] ?? '', required],
          description: [r?.['description'] ?? '', required],
          imageUrl: [r?.['imageUrl'] ?? ''],
          durationMinutes: [r?.['durationMinutes'] ?? 60, required],
          price: [r?.['price'] ?? 1, required],
          maxPassengers: [r?.['maxPassengers'] ?? 1, required],
          destinationId: [r?.['destinationId'] ?? this.destinations()[0]?.id, required],
          tourType: [r?.['tourType'] ?? 0, required],
          isActive: [r?.isActive ?? true],
        });
        break;
      case 'customers':
        this.form = this.fb.group({
          firstName: [r?.['firstName'] ?? '', required],
          lastName: [r?.['lastName'] ?? '', required],
          phoneNumber: [r?.['phoneNumber'] ?? '', required],
        });
        break;
      case 'vehicles':
        this.form = this.fb.group({
          name: [r?.['name'] ?? '', required],
          registrationNumber: [r?.['registrationNumber'] ?? '', required],
          vehicleType: [r?.['vehicleType'] ?? 0, required],
          passengerCapacity: [r?.['passengerCapacity'] ?? 1, required],
          description: [r?.['description'] ?? ''],
          isActive: [r?.isActive ?? true],
        });
        break;
      case 'drivers':
        this.form = this.fb.group({
          userId: [r?.['userId'] ?? '', required],
          firstName: [r?.['firstName'] ?? '', required],
          lastName: [r?.['lastName'] ?? '', required],
          phoneNumber: [r?.['phoneNumber'] ?? '', required],
          licenseNumber: [r?.['licenseNumber'] ?? '', required],
          isActive: [r?.isActive ?? true],
        });
    }
  }
  save() {
    if (this.form.invalid) return;
    this.saving.set(true);
    const row = this.selected();
    const body = this.form.getRawValue();
    const request = row
      ? this.api.put<Row>(`/api/${this.kind}/${row.id}`, body)
      : this.api.post<Row>(`/api/${this.kind}`, body);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.close();
        this.toast.show(`${this.singular()} saved`);
        this.load();
      },
      error: (e) => this.error.set(apiError(e)),
    });
  }
  deactivate(row: Row) {
    if (!confirm(`Deactivate this ${this.singular().toLowerCase()}?`)) return;
    this.api.delete(`/api/${this.kind}/${row.id}`).subscribe({
      next: () => {
        this.toast.show(`${this.singular()} deactivated`);
        this.load();
      },
      error: (e) => this.toast.show(apiError(e), true),
    });
  }
  asNumber(v: unknown) {
    return Number(v);
  }
  asString(v: unknown) {
    return String(v ?? '');
  }
}
