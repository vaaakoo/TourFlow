import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  catchError,
  finalize,
  firstValueFrom,
  map,
  Observable,
  of,
  switchMap,
  tap,
  throwError,
} from 'rxjs';
import { ApiService } from './api.service';
import { Role, TokenPair, UserProfile } from './models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly tokenKey = 'tourflow.session';
  readonly user = signal<UserProfile | null>(null);
  readonly restoring = signal(true);
  readonly authenticated = computed(() => !!this.user());
  readonly roles = computed(() => this.user()?.roles ?? []);
  get accessToken() {
    return this.read()?.accessToken ?? null;
  }
  hasRole(...roles: Role[]) {
    return roles.some((role) => this.roles().includes(role));
  }
  login(email: string, password: string) {
    return this.api.post<TokenPair>('/api/auth/login', { email, password }).pipe(
      tap((tokens) => this.store(tokens)),
      switchMap(() => this.loadProfile()),
    );
  }
  register(body: object) {
    return this.api.post<UserProfile>('/api/auth/register', body);
  }
  loadProfile() {
    return this.api.get<UserProfile>('/api/auth/me').pipe(tap((profile) => this.user.set(profile)));
  }
  async restore() {
    if (!this.read()) {
      this.restoring.set(false);
      return;
    }
    try {
      await firstValueFrom(this.loadProfile().pipe(catchError(() => this.refresh())));
    } catch {
      this.clear();
    } finally {
      this.restoring.set(false);
    }
  }
  refresh(): Observable<UserProfile> {
    const refreshToken = this.read()?.refreshToken;
    if (!refreshToken) return throwError(() => new Error('No refresh token'));
    return this.api.post<TokenPair>('/api/auth/refresh', { refreshToken }).pipe(
      tap((tokens) => this.store(tokens)),
      switchMap(() => this.loadProfile()),
    );
  }
  updateProfile(body: object) {
    return this.api.put<UserProfile>('/api/auth/me', body).pipe(tap((user) => this.user.set(user)));
  }
  logout() {
    const refreshToken = this.read()?.refreshToken;
    const done = () => {
      this.clear();
      void this.router.navigate(['/']);
    };
    if (!refreshToken) {
      done();
      return;
    }
    this.api
      .post('/api/auth/logout', { refreshToken })
      .pipe(
        finalize(done),
        catchError(() => of(null)),
      )
      .subscribe();
  }
  clear() {
    sessionStorage.removeItem(this.tokenKey);
    this.user.set(null);
  }
  private store(tokens: TokenPair) {
    sessionStorage.setItem(this.tokenKey, JSON.stringify(tokens));
  }
  private read(): TokenPair | null {
    try {
      return JSON.parse(sessionStorage.getItem(this.tokenKey) ?? 'null') as TokenPair | null;
    } catch {
      return null;
    }
  }
}
