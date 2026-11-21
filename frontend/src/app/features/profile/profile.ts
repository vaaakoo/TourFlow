import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { apiError } from '../../shared/errors';
import { ToastService } from '../../shared/toast.service';
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="mx-auto max-w-2xl">
    <h1 class="page-title">My profile</h1>
    <p class="page-subtitle">Your email and access role are managed securely by TourFlow.</p>
    <div class="card mt-6">
      <div class="mb-6 flex items-center gap-4">
        <span class="avatar size-14 text-base"
          >{{ auth.user()?.firstName?.[0] }}{{ auth.user()?.lastName?.[0] }}</span
        >
        <div>
          <b>{{ auth.user()?.email }}</b>
          <p class="text-sm text-slate-500">{{ auth.roles().join(', ') }}</p>
        </div>
      </div>
      <form [formGroup]="form" (ngSubmit)="save()" class="space-y-4">
        <div class="grid gap-4 sm:grid-cols-2">
          <div class="field">
            <label>First name</label><input class="input" formControlName="firstName" />
          </div>
          <div class="field">
            <label>Last name</label><input class="input" formControlName="lastName" />
          </div>
        </div>
        <div class="field">
          <label>Phone number</label><input class="input" formControlName="phoneNumber" />
        </div>
        @if (error()) {
          <p class="text-sm font-semibold text-red-600">{{ error() }}</p>
        }
        <button class="btn-primary" [disabled]="form.invalid || saving()">
          {{ saving() ? 'Saving…' : 'Save profile' }}
        </button>
      </form>
    </div>
  </div>`,
})
export class ProfilePage {
  auth = inject(AuthService);
  private fb = inject(FormBuilder);
  private toast = inject(ToastService);
  saving = signal(false);
  error = signal('');
  form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    phoneNumber: ['', Validators.required],
  });
  constructor() {
    effect(() => {
      const u = this.auth.user();
      if (u)
        this.form.patchValue(
          {
            firstName: u.firstName ?? '',
            lastName: u.lastName ?? '',
            phoneNumber: u.phoneNumber ?? '',
          },
          { emitEvent: false },
        );
    });
  }
  save() {
    if (this.form.invalid) return;
    this.saving.set(true);
    this.auth
      .updateProfile(this.form.getRawValue())
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => this.toast.show('Profile updated'),
        error: (e) => this.error.set(apiError(e)),
      });
  }
}
