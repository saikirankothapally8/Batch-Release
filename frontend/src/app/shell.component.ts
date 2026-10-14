import { Component, HostListener, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { P } from './core/models';
import { Toasts } from './shared/ui';

interface NavItem { path: string; label: string; perms: string[]; icon: string; }

const IDLE_LIMIT_MS = 30 * 60 * 1000; // Annex 11 §12.1: automatic log-off after inactivity

@Component({
  selector: 'brq-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="shell">
      <nav class="shell-nav" [class.open]="menuOpen()" aria-label="Main">
        <a class="brand" routerLink="/">
          <svg viewBox="0 0 32 32" width="26" height="26" aria-hidden="true"><rect x="2" y="2" width="28" height="28" rx="7" fill="#2a78d6"/>
            <path d="M9 17.5l4.5 4.5L23 11" stroke="#fff" stroke-width="3" fill="none" stroke-linecap="round" stroke-linejoin="round"/></svg>
          <span>Batch Release <b>IQ</b></span>
        </a>
        @for (group of groups(); track group.title) {
          <div class="nav-group">
            <div class="nav-title">{{ group.title }}</div>
            @for (n of group.items; track n.path) {
              <a [routerLink]="n.path" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: n.path === '/batches' }" (click)="menuOpen.set(false)">
                <span class="nav-icon" aria-hidden="true">{{ n.icon }}</span>{{ n.label }}</a>
            }
          </div>
        }
      </nav>
      <div class="shell-main">
        <header class="topbar">
          <button class="icon-btn menu-btn" (click)="menuOpen.set(!menuOpen())" aria-label="Menu">☰</button>
          <div class="spacer"></div>
          <button class="icon-btn" (click)="toggleTheme()" [attr.aria-label]="'Switch theme'" title="Switch light/dark">{{ theme() === 'dark' ? '☀' : '☾' }}</button>
          <div class="user">
            <div class="user-name">{{ auth.user()?.fullName }}</div>
            <div class="user-role">{{ auth.user()?.roles?.join(', ') }} · {{ auth.user()?.department }}</div>
          </div>
          <button class="btn sm" (click)="auth.logout()">Sign out</button>
        </header>
        <main><router-outlet /></main>
      </div>
    </div>
    <div class="toasts" aria-live="polite">@for (t of toasts.items(); track t.id) {<div class="toast" [class.error]="t.kind === 'error'">{{ t.text }}</div>}</div>`,
  styles: [`
    .shell { display: flex; min-height: 100vh; }
    .shell-nav { width: 232px; flex-shrink: 0; background: var(--nav-bg); color: var(--nav-fg); padding: 14px 10px; position: sticky; top: 0; height: 100vh; overflow-y: auto; }
    .brand { display: flex; align-items: center; gap: 10px; color: #fff; font-size: 16px; padding: 4px 8px 16px; text-decoration: none !important; }
    .brand b { color: #86b6ef; }
    .nav-group { margin-bottom: 14px; }
    .nav-title { font-size: 10px; text-transform: uppercase; letter-spacing: 0.08em; opacity: 0.6; padding: 4px 10px; }
    .shell-nav a:not(.brand) { display: flex; align-items: center; gap: 10px; padding: 7px 10px; border-radius: 6px; color: var(--nav-fg); text-decoration: none; font-weight: 500; }
    .shell-nav a:not(.brand):hover { background: var(--nav-active-bg); color: #fff; }
    .shell-nav a.active { background: var(--nav-active-bg); color: #fff; box-shadow: inset 3px 0 0 #3987e5; }
    .nav-icon { width: 18px; text-align: center; opacity: 0.85; }
    .shell-main { flex: 1; min-width: 0; display: flex; flex-direction: column; }
    .topbar { display: flex; align-items: center; gap: 14px; padding: 8px 24px; border-bottom: 1px solid var(--border); background: var(--surface-1); position: sticky; top: 0; z-index: 20; }
    .user { text-align: right; line-height: 1.2; }
    .user-name { font-weight: 600; }
    .user-role { font-size: 11px; color: var(--text-muted); }
    .menu-btn { display: none; font-size: 18px; }
    @media (max-width: 900px) {
      .shell-nav { position: fixed; z-index: 40; transform: translateX(-100%); transition: transform .2s; }
      .shell-nav.open { transform: none; }
      .menu-btn { display: inline-block; }
      .user-role { display: none; }
    }`]
})
export class ShellComponent implements OnInit, OnDestroy {
  auth = inject(AuthService);
  toasts = inject(Toasts);
  menuOpen = signal(false);
  theme = signal<'light' | 'dark'>(this.initialTheme());
  private lastActivity = Date.now();
  private timer?: ReturnType<typeof setInterval>;

  private nav: { title: string; items: NavItem[] }[] = [
    { title: 'Release', items: [
      { path: '/workbench', label: 'QA workbench', perms: [P.queue], icon: '▤' },
      { path: '/batches', label: 'Batches', perms: [P.batchView], icon: '▦' },
      { path: '/batches/new', label: 'New batch', perms: [P.batchCreate], icon: '+' },
      { path: '/approvals', label: 'Approvals', perms: [P.batchView], icon: '✓' },
    ] },
    { title: 'Insight', items: [
      { path: '/dashboard', label: 'Management dashboard', perms: [P.dashboard], icon: '◔' },
      { path: '/admin/audit', label: 'Audit trail', perms: [P.audit], icon: '⎙' },
    ] },
    { title: 'Administration', items: [
      { path: '/admin/templates', label: 'Checklist templates', perms: [P.batchView], icon: '☰' },
      { path: '/admin/master-data', label: 'Master data', perms: [P.batchView], icon: '⚙' },
      { path: '/admin/roles', label: 'Roles & activities', perms: [P.roles], icon: '⛨' },
      { path: '/admin/users', label: 'Users', perms: [P.users], icon: '☺' },
      { path: '/admin/settings', label: 'Prioritisation', perms: [P.settings, P.queue], icon: '⚖' },
    ] },
  ];
  groups = computed(() => {
    this.auth.user();
    return this.nav.map(g => ({ ...g, items: g.items.filter(i => this.auth.has(...i.perms)) })).filter(g => g.items.length);
  });

  ngOnInit() {
    this.applyTheme();
    this.auth.refreshMe().catch(() => { });
    this.timer = setInterval(() => { if (Date.now() - this.lastActivity > IDLE_LIMIT_MS) this.auth.logout('idle'); }, 30_000);
  }
  ngOnDestroy() { clearInterval(this.timer); }

  @HostListener('document:pointerdown') @HostListener('document:keydown') activity() { this.lastActivity = Date.now(); }

  private initialTheme(): 'light' | 'dark' {
    try { const t = localStorage.getItem('brq.theme'); if (t === 'light' || t === 'dark') return t; } catch { }
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
  toggleTheme() {
    this.theme.set(this.theme() === 'dark' ? 'light' : 'dark');
    try { localStorage.setItem('brq.theme', this.theme()); } catch { }
    this.applyTheme();
  }
  private applyTheme() { document.documentElement.setAttribute('data-theme', this.theme()); }
}
