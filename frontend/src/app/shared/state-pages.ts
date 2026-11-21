import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="grid min-h-[65vh] place-items-center px-6 text-center">
    <div>
      <p class="text-7xl font-black text-teal-600">403</p>
      <h1 class="mt-4 text-3xl font-bold">Access denied</h1>
      <p class="mt-2 text-slate-500">
        Your account does not have permission to view this workspace.
      </p>
      <a routerLink="/" class="btn-primary mt-6 inline-flex">Return home</a>
    </div>
  </section>`,
})
export class ForbiddenPage {}
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="grid min-h-[65vh] place-items-center px-6 text-center">
    <div>
      <p class="text-7xl font-black text-teal-600">404</p>
      <h1 class="mt-4 text-3xl font-bold">Page not found</h1>
      <a routerLink="/" class="btn-primary mt-6 inline-flex">Return home</a>
    </div>
  </section>`,
})
export class NotFoundPage {}
