import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { ToastService } from '../shared/toast.service';

@Component({
  selector: 'app-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ` <div class="min-h-screen bg-slate-50 text-slate-900">
    <header class="sticky top-0 z-40 border-b border-slate-200/80 bg-white/90 backdrop-blur">
      <div class="mx-auto flex h-16 max-w-[1500px] items-center gap-4 px-4 lg:px-8">
        <a routerLink="/" class="flex items-center gap-2.5 font-extrabold tracking-tight"
          ><span class="grid size-9 place-items-center rounded-xl bg-teal-600 text-white">TF</span
          ><span>TourFlow</span></a
        >
        <nav class="hidden items-center gap-1 md:flex">
          <a routerLink="/tours" routerLinkActive="nav-active" class="nav-link">Tours</a
          ><a routerLink="/destinations" routerLinkActive="nav-active" class="nav-link"
            >Destinations</a
          >
        </nav>
        <div class="ml-auto flex items-center gap-2">
          @if (!auth.authenticated()) {
            <a routerLink="/login" class="btn-ghost">Sign in</a
            ><a routerLink="/register" class="btn-primary">Create account</a>
          } @else {
            <button
              class="flex items-center gap-2 rounded-xl px-3 py-2 hover:bg-slate-100"
              (click)="menu.set(!menu())"
            >
              <span class="avatar">{{ initials() }}</span><span
                class="hidden text-left text-sm sm:block"
                ><b class="block">{{ auth.user()?.firstName || 'Account' }}</b
                ><small class="text-slate-500">{{ auth.roles()[0] }}</small></span
              >
            </button>
          }
        </div>
      </div>
    </header>
    @if (auth.authenticated()) {
      <div class="mx-auto flex max-w-[1500px]">
        <aside
          class="hidden min-h-[calc(100vh-4rem)] w-64 shrink-0 border-r border-slate-200 bg-white p-4 lg:block"
        >
          <p class="sidebar-title">Workspace</p>
          @if (auth.hasRole('CUSTOMER')) {
            <a routerLink="/portal/bookings" routerLinkActive="side-active" class="side-link"
              >My bookings</a
            ><a routerLink="/portal/profile" routerLinkActive="side-active" class="side-link"
              >My profile</a
            >
          }
          @if (auth.hasRole('DRIVER')) {
            <a routerLink="/driver" routerLinkActive="side-active" class="side-link">My schedule</a>
          }
          @if (auth.hasRole('ADMINISTRATOR', 'MANAGER', 'OPERATOR')) {
            <a routerLink="/operations/bookings" routerLinkActive="side-active" class="side-link"
              >Bookings</a
            ><a routerLink="/operations/customers" routerLinkActive="side-active" class="side-link"
              >Customers</a
            ><a routerLink="/operations/vehicles" routerLinkActive="side-active" class="side-link"
              >Vehicles</a
            ><a routerLink="/operations/drivers" routerLinkActive="side-active" class="side-link"
              >Drivers</a
            >
          }
          @if (auth.hasRole('ADMINISTRATOR', 'MANAGER')) {
            <p class="sidebar-title mt-6">Management</p>
            <a routerLink="/operations/dashboard" routerLinkActive="side-active" class="side-link"
              >Dashboard</a
            ><a routerLink="/operations/tours" routerLinkActive="side-active" class="side-link"
              >Tours</a
            ><a
              routerLink="/operations/destinations"
              routerLinkActive="side-active"
              class="side-link"
              >Destinations</a
            ><a routerLink="/operations/payments" routerLinkActive="side-active" class="side-link"
              >Payments</a
            ><a routerLink="/operations/reports" routerLinkActive="side-active" class="side-link"
              >Reports</a
            >
          }
          @if (auth.hasRole('ADMINISTRATOR')) {
            <a routerLink="/operations/users" routerLinkActive="side-active" class="side-link"
              >User access</a
            >
          }
          <button class="side-link mt-8 w-full text-left text-red-600" (click)="auth.logout()">
            Sign out
          </button>
        </aside>
        <main class="min-w-0 flex-1 p-4 sm:p-6 lg:p-8"><router-outlet /></main>
      </div>
    } @else {
      <main><router-outlet /></main>
    }
    @if (menu()) {
      <div class="fixed right-4 top-16 z-50 w-64 rounded-2xl border bg-white p-2 shadow-xl">
        @if (auth.hasRole('CUSTOMER')) {
          <a routerLink="/portal/bookings" class="side-link" (click)="menu.set(false)"
            >My bookings</a
          >
        }
        @if (auth.hasRole('DRIVER')) {
          <a routerLink="/driver" class="side-link" (click)="menu.set(false)">My schedule</a>
        }
        @if (auth.hasRole('ADMINISTRATOR', 'MANAGER')) {
          <a routerLink="/operations/dashboard" class="side-link" (click)="menu.set(false)"
            >Dashboard</a
          >
        }
        @if (auth.hasRole('ADMINISTRATOR', 'MANAGER', 'OPERATOR')) {
          <a routerLink="/operations/bookings" class="side-link" (click)="menu.set(false)"
            >Booking operations</a
          >
        }
        <a routerLink="/portal/profile" class="side-link" (click)="menu.set(false)">Profile</a
        ><button
          class="side-link w-full text-left text-red-600"
          (click)="auth.logout(); menu.set(false)"
        >
          Sign out
        </button>
      </div>
    }
    @if (toast.message(); as note) {
      <div
        class="fixed bottom-5 right-5 z-[60] max-w-sm rounded-2xl px-5 py-4 text-sm font-semibold text-white shadow-2xl"
        [class.bg-red-600]="note.error"
        [class.bg-slate-900]="!note.error"
      >
        {{ note.text }}
      </div>
    }
  </div>`,
})
export class Shell {
  readonly auth = inject(AuthService);
  readonly toast = inject(ToastService);
  readonly menu = signal(false);
  initials = () =>
    `${this.auth.user()?.firstName?.[0] ?? ''}${this.auth.user()?.lastName?.[0] ?? ''}` || 'TF';
}
