import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { Page, Tour } from '../../core/models';
@Component({
  imports: [RouterLink, CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: ` <section class="relative overflow-hidden bg-slate-950 text-white">
      <div
        class="absolute inset-0 bg-[radial-gradient(circle_at_75%_20%,#0d9488_0,transparent_38%)] opacity-80"
      ></div>
      <div
        class="relative mx-auto grid min-h-[620px] max-w-7xl items-center gap-12 px-6 py-24 lg:grid-cols-[1.1fr_.9fr] lg:px-8"
      >
        <div>
          <span
            class="rounded-full border border-teal-300/30 bg-teal-300/10 px-4 py-2 text-sm font-bold text-teal-200"
            >Tours and transfers, beautifully coordinated</span
          >
          <h1 class="mt-7 max-w-3xl text-5xl font-black leading-[1.05] tracking-tight sm:text-7xl">
            The journey starts before you arrive.
          </h1>
          <p class="mt-6 max-w-2xl text-lg leading-8 text-slate-300">
            Discover local experiences and book reliable transfers through one seamless travel
            platform.
          </p>
          <div class="mt-9 flex flex-wrap gap-3">
            <a routerLink="/tours" class="btn-primary px-6 py-3">Explore tours</a
            ><a
              routerLink="/register"
              class="btn-secondary border-white/20 bg-white/10 px-6 py-3 text-white hover:bg-white/20"
              >Plan a transfer</a
            >
          </div>
        </div>
        <div class="hidden lg:grid">
          <div
            class="rotate-2 rounded-[2rem] border border-white/15 bg-white/10 p-5 shadow-2xl backdrop-blur"
          >
            <div class="h-72 rounded-3xl bg-[linear-gradient(135deg,#14b8a6,#0f172a)] p-8">
              <p class="text-sm font-bold text-teal-100">Next adventure</p>
              <p class="mt-28 text-3xl font-black">Explore Georgia<br />with local experts</p>
            </div>
            <div class="grid grid-cols-3 gap-3 pt-5 text-center">
              <div>
                <b class="text-xl">24/7</b><small class="block text-slate-400">Support</small>
              </div>
              <div>
                <b class="text-xl">Secure</b><small class="block text-slate-400">Booking</small>
              </div>
              <div>
                <b class="text-xl">Local</b><small class="block text-slate-400">Guides</small>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
    <section class="mx-auto max-w-7xl px-6 py-20 lg:px-8">
      <div class="flex items-end justify-between">
        <div>
          <p class="font-black uppercase tracking-widest text-teal-600">Popular experiences</p>
          <h2 class="mt-2 text-3xl font-black">Tours worth taking</h2>
        </div>
        <a routerLink="/tours" class="font-bold text-teal-700">View all →</a>
      </div>
      <div class="mt-8 grid gap-6 md:grid-cols-3">
        @for (tour of tours(); track tour.id) {
          <a
            [routerLink]="['/tours', tour.id]"
            class="group overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-sm transition hover:-translate-y-1 hover:shadow-xl"
            ><div class="h-44 bg-gradient-to-br from-teal-500 to-slate-900">
              @if (tour.imageUrl) {
                <img [src]="tour.imageUrl" [alt]="tour.name" class="h-full w-full object-cover" />
              }
            </div>
            <div class="p-5">
              <h3 class="text-lg font-black group-hover:text-teal-700">{{ tour.name }}</h3>
              <p class="mt-2 line-clamp-2 text-sm leading-6 text-slate-500">
                {{ tour.description }}
              </p>
              <div class="mt-4 flex items-center justify-between">
                <span class="text-sm text-slate-500">{{ tour.durationMinutes / 60 }} hours</span
                ><b class="text-teal-700">{{ tour.price | currency: 'USD' }}</b>
              </div>
            </div></a
          >
        } @empty {
          <div class="empty md:col-span-3">
            Tours will appear here when the API has active inventory.
          </div>
        }
      </div>
    </section>`,
})
export class LandingPage {
  private api = inject(ApiService);
  tours = signal<Tour[]>([]);
  constructor() {
    this.api
      .get<Page<Tour>>('/api/tours', { pageSize: 3, sort: 'createdAt', descending: true })
      .subscribe((r) => this.tours.set(r.items));
  }
}
