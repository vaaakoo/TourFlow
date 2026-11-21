import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize, switchMap } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { apiError } from '../../shared/errors';
@Component({
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ` <section
    class="grid min-h-[calc(100vh-4rem)] place-items-center bg-[radial-gradient(circle_at_top_left,#ccfbf1,transparent_45%)] px-4 py-12"
  >
    <div class="w-full max-w-xl card p-7 sm:p-9">
      <p class="text-sm font-black uppercase tracking-[.2em] text-teal-600">Customer portal</p>
      <h1 class="mt-2 text-3xl font-black">Create your account</h1>
      <p class="mt-2 text-sm text-slate-500">Plan tours and transfers in one place.</p>
      @if (error()) {
        <div class="mt-5 rounded-xl bg-red-50 p-3 text-sm font-semibold text-red-700">
          {{ error() }}
        </div>
      }
      <form [formGroup]="form" (ngSubmit)="submit()" class="mt-6 grid gap-4 sm:grid-cols-2">
        <div class="field">
          <label>First name</label><input class="input" formControlName="firstName" />
        </div>
        <div class="field">
          <label>Last name</label><input class="input" formControlName="lastName" />
        </div>
        <div class="field sm:col-span-2">
          <label>Email</label
          ><input class="input" type="email" autocomplete="email" formControlName="email" />
        </div>
        <div class="field">
          <label>Phone number</label
          ><input class="input" autocomplete="tel" formControlName="phoneNumber" />
        </div>
        <div class="field">
          <label>Password</label
          ><input
            class="input"
            type="password"
            autocomplete="new-password"
            formControlName="password"
          />
          <p class="mt-1 text-xs text-slate-400">At least 12 characters.</p>
        </div>
        <button class="btn-primary sm:col-span-2" [disabled]="form.invalid || loading()">
          {{ loading() ? 'Creating account…' : 'Create account' }}
        </button>
      </form>
      <p class="mt-6 text-center text-sm text-slate-500">
        Already registered? <a routerLink="/login" class="font-bold text-teal-700">Sign in</a>
      </p>
    </div>
  </section>`,
})
export class RegisterPage {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  loading = signal(false);
  error = signal('');
  form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', Validators.required],
    password: ['', [Validators.required, Validators.minLength(12)]],
  });
  submit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set('');
    this.auth
      .register(this.form.getRawValue())
      .pipe(
        switchMap(() => this.auth.login(this.form.value.email!, this.form.value.password!)),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: () => this.router.navigateByUrl('/portal/bookings'),
        error: (e) => this.error.set(apiError(e)),
      });
  }
}
