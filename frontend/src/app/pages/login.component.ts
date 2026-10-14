import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { errorMessage } from '../core/api.service';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'brq-login',
  imports: [FormsModule],
  template: `
    <div class="login">
      <form class="card login-card" (ngSubmit)="submit()">
        <div class="brand-row">
          <svg viewBox="0 0 32 32" width="34" height="34" aria-hidden="true"><rect x="2" y="2" width="28" height="28" rx="7" fill="#2a78d6"/>
            <path d="M9 17.5l4.5 4.5L23 11" stroke="#fff" stroke-width="3" fill="none" stroke-linecap="round" stroke-linejoin="round"/></svg>
          <div><h1>Batch Release IQ</h1><p class="muted">Which batch can I release next?</p></div>
        </div>
        @if (reason === 'idle') {<p class="callout warn">You were signed out after 30 minutes of inactivity.</p>}
        @if (reason === 'expired') {<p class="callout warn">Your session expired. Please sign in again.</p>}
        <label class="field"><span>User ID</span><input name="u" [(ngModel)]="username" autocomplete="username" required autofocus /></label>
        <label class="field"><span>Password</span><input name="p" type="password" [(ngModel)]="password" autocomplete="current-password" required /></label>
        @if (error()) {<p class="error" role="alert">{{ error() }}</p>}
        <button class="btn primary" type="submit" [disabled]="busy()">{{ busy() ? 'Signing in…' : 'Sign in' }}</button>
        <details class="demo">
          <summary>Demo accounts</summary>
          <p class="small muted">Password for all: <code>Passw0rd!</code></p>
          <div class="demo-grid">
            @for (d of demo; track d.u) {<button type="button" class="link small" (click)="username = d.u; password = 'Passw0rd!'">{{ d.u }}</button><span class="small muted">{{ d.r }}</span>}
          </div>
        </details>
      </form>
    </div>`,
  styles: [`
    .login { min-height: 100vh; display: grid; place-items: center; padding: 16px; background: radial-gradient(circle at 30% 20%, var(--accent-soft), var(--page) 60%); }
    .login-card { width: min(400px, 100%); padding: 28px; display: flex; flex-direction: column; gap: 14px; }
    .brand-row { display: flex; gap: 12px; align-items: center; margin-bottom: 6px; }
    .brand-row p { margin: 2px 0 0; }
    .demo summary { cursor: pointer; color: var(--text-secondary); font-size: 13px; }
    .demo-grid { display: grid; grid-template-columns: auto 1fr; gap: 4px 12px; justify-items: start; }`]
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  reason = inject(ActivatedRoute).snapshot.queryParamMap.get('reason');
  username = '';
  password = '';
  busy = signal(false);
  error = signal('');
  demo = [
    { u: 'qa.head', r: 'QA Release Authority' }, { u: 'qa.reviewer', r: 'QA Reviewer' }, { u: 'mfg.user', r: 'Manufacturing' },
    { u: 'qc.user', r: 'Quality Control' }, { u: 'qms.user', r: 'QMS coordinator' }, { u: 'ra.user', r: 'Regulatory Affairs' },
    { u: 'wh.user', r: 'Warehouse' }, { u: 'mgmt.user', r: 'Management' }, { u: 'admin', r: 'Administrator' },
  ];

  async submit() {
    this.busy.set(true); this.error.set('');
    try { await this.auth.login(this.username, this.password); this.router.navigate(['/']); }
    catch (e) { this.error.set(errorMessage(e)); }
    finally { this.busy.set(false); this.password = ''; }
  }
}
