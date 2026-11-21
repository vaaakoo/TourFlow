import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { Destination, Page, Tour, tourTypes } from '../../core/models';
@Component({
  imports: [FormsModule, RouterLink, CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="mx-auto max-w-7xl px-4 py-12 sm:px-6 lg:px-8">
    <div class="max-w-2xl">
      <p class="font-black uppercase tracking-widest text-teal-600">Curated experiences</p>
      <h1 class="mt-2 text-4xl font-black">Find your next tour</h1>
      <p class="mt-3 text-slate-500">
        Browse active tours with transparent server-calculated pricing.
      </p>
    </div>
    <div class="card mt-8 flex flex-col gap-3 md:flex-row">
      <input
        class="input flex-1"
        [(ngModel)]="search"
        placeholder="Search tours…"
        (keyup.enter)="load()"
      /><select class="input md:w-56" [(ngModel)]="type">
        <option value="">All tour types</option>
        @for (label of types; track $index) {
          <option [value]="$index">{{ label }}</option>
        }</select
      ><button class="btn-primary" (click)="load()">Search</button>
    </div>
    @if (loading()) {
      <div class="mt-8 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
        @for (i of [1, 2, 3, 4, 5, 6]; track i) {
          <div class="card">
            <div class="skeleton h-44"></div>
            <div class="skeleton mt-5 h-5 w-3/4"></div>
            <div class="skeleton mt-3 h-12"></div>
          </div>
        }
      </div>
    } @else {
      <div class="mt-8 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
        @for (tour of page()?.items; track tour.id) {
          <a
            [routerLink]="['/tours', tour.id]"
            class="group overflow-hidden rounded-3xl border border-slate-200 bg-white shadow-sm transition hover:-translate-y-1 hover:shadow-xl"
            ><div class="relative h-48 bg-gradient-to-br from-teal-400 to-slate-900">
              @if (tour.imageUrl) {
                <img [src]="tour.imageUrl" [alt]="tour.name" class="h-full w-full object-cover" />
              }
              <span class="absolute left-4 top-4 badge bg-white/90 text-slate-800">{{
                types[tour.tourType]
              }}</span>
            </div>
            <div class="p-5">
              <h2 class="text-xl font-black">{{ tour.name }}</h2>
              <p class="mt-2 line-clamp-2 text-sm leading-6 text-slate-500">
                {{ tour.description }}
              </p>
              <div class="mt-5 flex items-end justify-between">
                <span class="text-sm text-slate-500"
                  >{{ tour.durationMinutes }} min · up to {{ tour.maxPassengers }}</span
                ><span
                  ><small class="block text-slate-400">per person</small
                  ><b class="text-xl text-teal-700">{{ tour.price | currency: 'USD' }}</b></span
                >
              </div>
            </div></a
          >
        } @empty {
          <div class="empty md:col-span-2 lg:col-span-3">No active tours match these filters.</div>
        }
      </div>
    }
    @if (page() && page()!.totalCount > page()!.pageSize) {
      <div class="mt-8 flex justify-center gap-3">
        <button class="btn-secondary" [disabled]="currentPage === 1" (click)="go(-1)">
          Previous</button
        ><span class="px-4 py-2 text-sm font-bold">Page {{ currentPage }}</span
        ><button
          class="btn-secondary"
          [disabled]="currentPage * page()!.pageSize >= page()!.totalCount"
          (click)="go(1)"
        >
          Next
        </button>
      </div>
    }
  </div>`,
})
export class CatalogPage {
  private api = inject(ApiService);
  types = tourTypes;
  page = signal<Page<Tour> | null>(null);
  loading = signal(true);
  search = '';
  type = '';
  currentPage = 1;
  constructor() {
    this.load();
  }
  load() {
    this.loading.set(true);
    this.api
      .get<Page<Tour>>('/api/tours', {
        page: this.currentPage,
        pageSize: 9,
        search: this.search,
        tourType: this.type,
        sort: 'name',
      })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe((r) => this.page.set(r));
  }
  go(delta: number) {
    this.currentPage += delta;
    this.load();
    scrollTo({ top: 0, behavior: 'smooth' });
  }
}
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="mx-auto max-w-7xl px-4 py-12 sm:px-6 lg:px-8">
    <p class="font-black uppercase tracking-widest text-teal-600">Places to explore</p>
    <h1 class="mt-2 text-4xl font-black">Destinations</h1>
    <div class="mt-8 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
      @for (item of items(); track item.id) {
        <article class="overflow-hidden rounded-3xl border bg-white shadow-sm">
          <div class="h-44 bg-gradient-to-br from-cyan-400 to-teal-900">
            @if (item.imageUrl) {
              <img [src]="item.imageUrl" [alt]="item.name" class="h-full w-full object-cover" />
            }
          </div>
          <div class="p-5">
            <p class="text-xs font-black uppercase tracking-wider text-teal-600">
              {{ item.country }}
            </p>
            <h2 class="mt-1 text-xl font-black">{{ item.name }}</h2>
            <p class="mt-2 text-sm leading-6 text-slate-500">
              {{ item.description || 'Discover tours and transfers in this destination.' }}
            </p>
          </div>
        </article>
      } @empty {
        <div class="empty sm:col-span-2 lg:col-span-3">
          No destinations are currently available.
        </div>
      }
    </div>
  </div>`,
})
export class DestinationsPage {
  private api = inject(ApiService);
  items = signal<Destination[]>([]);
  constructor() {
    this.api
      .get<Page<Destination>>('/api/destinations', { pageSize: 100, sort: 'name' })
      .subscribe((r) => this.items.set(r.items));
  }
}
