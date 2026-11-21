import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  readonly baseUrl = environment.apiUrl;
  get<T>(path: string, params?: Record<string, string | number | boolean | null | undefined>) {
    let query = new HttpParams();
    Object.entries(params ?? {}).forEach(([key, value]) => {
      if (value !== null && value !== undefined && value !== '')
        query = query.set(key, String(value));
    });
    return this.http.get<T>(`${this.baseUrl}${path}`, { params: query });
  }
  post<T>(path: string, body: unknown) {
    return this.http.post<T>(`${this.baseUrl}${path}`, body);
  }
  put<T>(path: string, body: unknown, params?: Record<string, string>) {
    return this.http.put<T>(`${this.baseUrl}${path}`, body, { params });
  }
  delete<T>(path: string, params?: Record<string, string>) {
    return this.http.delete<T>(`${this.baseUrl}${path}`, { params });
  }
}
