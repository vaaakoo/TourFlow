import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards';
import { Shell } from './layout/shell';

export const routes: Routes = [
  {
    path: '',
    component: Shell,
    children: [
      {
        path: '',
        loadComponent: () => import('./features/public/landing').then((m) => m.LandingPage),
      },
      {
        path: 'tours',
        loadComponent: () => import('./features/public/catalog').then((m) => m.CatalogPage),
      },
      {
        path: 'tours/:id',
        loadComponent: () => import('./features/public/tour-detail').then((m) => m.TourDetailPage),
      },
      {
        path: 'destinations',
        loadComponent: () => import('./features/public/catalog').then((m) => m.DestinationsPage),
      },
      {
        path: 'login',
        loadComponent: () => import('./features/auth/login').then((m) => m.LoginPage),
      },
      {
        path: 'register',
        loadComponent: () => import('./features/auth/register').then((m) => m.RegisterPage),
      },
      {
        path: 'portal/profile',
        canActivate: [authGuard],
        loadComponent: () => import('./features/profile/profile').then((m) => m.ProfilePage),
      },
      {
        path: 'portal/bookings',
        canActivate: [authGuard, roleGuard(['CUSTOMER'])],
        loadComponent: () => import('./features/bookings/bookings').then((m) => m.BookingsPage),
      },
      {
        path: 'portal/bookings/:id',
        canActivate: [authGuard, roleGuard(['CUSTOMER'])],
        loadComponent: () =>
          import('./features/bookings/booking-detail').then((m) => m.BookingDetailPage),
      },
      {
        path: 'driver',
        canActivate: [authGuard, roleGuard(['DRIVER'])],
        loadComponent: () => import('./features/driver/driver').then((m) => m.DriverPage),
      },
      {
        path: 'operations/dashboard',
        canActivate: [authGuard, roleGuard(['ADMINISTRATOR', 'MANAGER'])],
        loadComponent: () => import('./features/operations/dashboard').then((m) => m.DashboardPage),
      },
      {
        path: 'operations/bookings',
        canActivate: [authGuard, roleGuard(['ADMINISTRATOR', 'MANAGER', 'OPERATOR'])],
        loadComponent: () => import('./features/bookings/bookings').then((m) => m.BookingsPage),
      },
      {
        path: 'operations/bookings/:id',
        canActivate: [authGuard, roleGuard(['ADMINISTRATOR', 'MANAGER', 'OPERATOR'])],
        loadComponent: () =>
          import('./features/bookings/booking-detail').then((m) => m.BookingDetailPage),
      },
      ...resourceRoutes(),
      {
        path: 'operations/payments',
        canActivate: [authGuard, roleGuard(['ADMINISTRATOR', 'MANAGER', 'OPERATOR'])],
        loadComponent: () => import('./features/operations/payments').then((m) => m.PaymentsPage),
      },
      {
        path: 'operations/reports',
        canActivate: [authGuard, roleGuard(['ADMINISTRATOR', 'MANAGER'])],
        loadComponent: () => import('./features/operations/reports').then((m) => m.ReportsPage),
      },
      {
        path: 'operations/users',
        canActivate: [authGuard, roleGuard(['ADMINISTRATOR'])],
        loadComponent: () => import('./features/operations/users').then((m) => m.UsersPage),
      },
      {
        path: 'forbidden',
        loadComponent: () => import('./shared/state-pages').then((m) => m.ForbiddenPage),
      },
      {
        path: 'not-found',
        loadComponent: () => import('./shared/state-pages').then((m) => m.NotFoundPage),
      },
      { path: '**', redirectTo: 'not-found' },
    ],
  },
];

function resourceRoutes(): Routes {
  const component = () => import('./features/operations/resources').then((m) => m.ResourcesPage);
  return [
    {
      path: 'operations/destinations',
      canActivate: [authGuard, roleGuard(['ADMINISTRATOR', 'MANAGER'])],
      loadComponent: component,
      data: { kind: 'destinations' },
    },
    {
      path: 'operations/tours',
      canActivate: [authGuard, roleGuard(['ADMINISTRATOR', 'MANAGER'])],
      loadComponent: component,
      data: { kind: 'tours' },
    },
    {
      path: 'operations/customers',
      canActivate: [authGuard, roleGuard(['ADMINISTRATOR', 'MANAGER', 'OPERATOR'])],
      loadComponent: component,
      data: { kind: 'customers' },
    },
    {
      path: 'operations/vehicles',
      canActivate: [authGuard, roleGuard(['ADMINISTRATOR', 'MANAGER', 'OPERATOR'])],
      loadComponent: component,
      data: { kind: 'vehicles' },
    },
    {
      path: 'operations/drivers',
      canActivate: [authGuard, roleGuard(['ADMINISTRATOR', 'MANAGER', 'OPERATOR'])],
      loadComponent: component,
      data: { kind: 'drivers' },
    },
  ];
}
