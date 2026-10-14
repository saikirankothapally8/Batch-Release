import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Me } from './models';

const KEY = 'brq.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  readonly user = signal<Me | null>(null);
  readonly token = signal<string | null>(null);
  readonly isLoggedIn = computed(() => !!this.user());
  private expiresAt = 0;

  constructor() {
    // Session storage: the session ends with the tab, and the token expires on the server side regardless.
    try {
      const raw = sessionStorage.getItem(KEY);
      if (raw) {
        const s = JSON.parse(raw);
        if (s.expiresAt > Date.now()) { this.token.set(s.token); this.user.set(s.user); this.expiresAt = s.expiresAt; }
      }
    } catch { /* storage unavailable: stay logged out */ }
  }

  async login(username: string, password: string) {
    const r = await firstValueFrom(this.http.post<{ token: string; expiresAt: string; user: Me }>('api/auth/login', { username, password }));
    this.token.set(r.token);
    this.user.set(r.user);
    this.expiresAt = Date.parse(r.expiresAt);
    try { sessionStorage.setItem(KEY, JSON.stringify({ token: r.token, user: r.user, expiresAt: this.expiresAt })); } catch { }
  }

  async refreshMe() {
    const me = await firstValueFrom(this.http.get<Me>('api/auth/me'));
    this.user.set(me);
    try { sessionStorage.setItem(KEY, JSON.stringify({ token: this.token(), user: me, expiresAt: this.expiresAt })); } catch { }
  }

  logout(reason?: string) {
    this.token.set(null);
    this.user.set(null);
    try { sessionStorage.removeItem(KEY); } catch { }
    this.router.navigate(['/login'], { queryParams: reason ? { reason } : {} });
  }

  has(...permissions: string[]) {
    const p = this.user()?.permissions ?? [];
    return permissions.some(x => p.includes(x));
  }
}
