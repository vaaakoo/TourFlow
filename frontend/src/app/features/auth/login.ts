import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth.service';
import { apiError } from '../../shared/errors';

@Component({
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ` <section
    class="grid min-h-[calc(100vh-4rem)] place-items-center bg-[radial-gradient(circle_at_top_right,#ccfbf1,transparent_45%)] px-4 py-12"
  >
    <div class="w-full max-w-md">
      <div class="card p-7 sm:p-9">
        <p class="text-sm font-black uppercase tracking-[.2em] text-teal-600">Welcome back</p>
        <h1 class="mt-2 text-3xl font-black">Sign in to TourFlow</h1>
        <p class="mt-2 text-sm text-slate-500">Continue to your bookings and workspace.</p>
        @if (error()) {
          <div class="mt-5 rounded-xl bg-red-50 p-3 text-sm font-semibold text-red-700">
            {{ error() }}
          </div>
        }
        <form [formGroup]="form" (ngSubmit)="submit()" class="mt-6 space-y-4">
          <div class="field">
            <label for="email">Email</label
            ><input
              id="email"
              class="input"
              type="email"
              autocomplete="email"
              formControlName="email"
            />
          </div>
          <div class="field">
            <label for="password">Password</label
            ><input
              id="password"
              class="input"
              type="password"
              autocomplete="current-password"
              formControlName="password"
            />
          </div>
          <button class="btn-primary w-full" [disabled]="form.invalid || loading()">
            {{ loading() ? 'Signing in…' : 'Sign in' }}
          </button>
        </form>
        <p class="mt-6 text-center text-sm text-slate-500">
          New to TourFlow?
          <a routerLink="/register" class="font-bold text-teal-700">Create an account</a>
        </p>
      </div>
    </div>
  </section>`,
})
export class LoginPage {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  loading = signal(false);
  error = signal('');
  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(12)]],
  });
  submit() {
    if (this.form.invalid) return;
    this.loading.set(true);
    this.error.set('');
    this.auth
      .login(this.form.value.email!, this.form.value.password!)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () =>
          this.router.navigateByUrl(
            this.route.snapshot.queryParamMap.get('returnUrl') || this.home(),
          ),
        error: (e) => this.error.set(apiError(e)),
      });
  }
  private home() {
    if (this.auth.hasRole('ADMINISTRATOR', 'MANAGER')) return '/operations/dashboard';
    if (this.auth.hasRole('OPERATOR')) return '/operations/bookings';
    if (this.auth.hasRole('DRIVER')) return '/driver';
    return '/portal/bookings';
  }
}
