import { HttpClient, HttpErrorResponse, HttpInterceptorFn, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { catchError, firstValueFrom, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { BatchFilter, Lookups } from './models';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token();
  const r = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  return next(r).pipe(catchError((e: HttpErrorResponse) => {
    if (e.status === 401 && !req.url.endsWith('/auth/login') && token) auth.logout('expired');
    return throwError(() => e);
  }));
};

export function errorMessage(e: unknown): string {
  if (e instanceof HttpErrorResponse) {
    if (e.error?.message) return e.error.message;
    if (e.error?.errors) return Object.values(e.error.errors).flat().join(' ');
    if (e.status === 0) return 'The server could not be reached.';
    return `${e.status} ${e.statusText}`;
  }
  return String(e);
}

export function toParams(f: object): HttpParams {
  let p = new HttpParams();
  for (const [k, v] of Object.entries(f)) {
    if (v === null || v === undefined || v === '') continue;
    if (Array.isArray(v)) v.forEach(x => p = p.append(k, String(x)));
    else p = p.set(k, String(v));
  }
  return p;
}

@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);
  readonly lookups = signal<Lookups | null>(null);

  get<T>(url: string, params: object = {}) { return firstValueFrom(this.http.get<T>('api/' + url, { params: toParams(params) })); }
  post<T>(url: string, body: unknown) { return firstValueFrom(this.http.post<T>('api/' + url, body)); }
  put<T>(url: string, body: unknown) { return firstValueFrom(this.http.put<T>('api/' + url, body)); }
  delete<T>(url: string) { return firstValueFrom(this.http.delete<T>('api/' + url)); }
  upload<T>(url: string, file: File) {
    const fd = new FormData();
    fd.append('file', file, file.name);
    return firstValueFrom(this.http.post<T>('api/' + url, fd));
  }
  async download(url: string, fileName: string) {
    const blob = await firstValueFrom(this.http.get('api/' + url, { responseType: 'blob' }));
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = fileName;
    a.click();
    URL.revokeObjectURL(a.href);
  }

  async loadLookups(force = false) {
    if (!this.lookups() || force) this.lookups.set(await this.get<Lookups>('lookups'));
    return this.lookups()!;
  }

  filterParams(f: BatchFilter) { return f; }
}
