import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, errorMessage } from '../../core/api.service';
import { Toasts } from '../../shared/ui';

interface Perm { code: string; group: string; description: string; }

@Component({
  selector: 'brq-roles',
  imports: [FormsModule],
  template: `
    <div class="page">
      <div class="page-head"><div><h1>Roles & activities</h1><p>Create roles and tick the activities each role may perform. Changes apply to users on their next action.</p></div>
        <button class="btn primary" (click)="newRole()">+ New role</button></div>
      @if (error()) {<p class="callout danger">{{ error() }}</p>}
      <div class="layout">
        <div class="card">
          <div class="card-head"><h3>Roles</h3></div>
          <ul class="role-list">
            @for (r of roles(); track r.id) {
              <li [class.active]="edit()?.id === r.id" (click)="select(r)"><b>{{ r.name }}</b>@if (r.isSystem) { <span class="tag">system</span>}
                <div class="small muted">{{ r.permissions.length }} activities · {{ r.userCount }} users</div></li>
            }
          </ul>
        </div>
        @if (edit(); as e) {
          <form class="card" (ngSubmit)="save()">
            <div class="card-head"><h3>{{ e.id ? 'Edit role' : 'New role' }}</h3>
              @if (e.id && !e.isSystem) {<button type="button" class="btn sm danger" (click)="remove()">Delete role</button>}</div>
            <div class="card-body form">
              <div class="form-grid">
                <label class="field span-2"><span>Role name<em class="req">*</em></span><input name="n" [(ngModel)]="e.name" required /></label>
                <label class="field span-2"><span>Description</span><input name="d" [(ngModel)]="e.description" /></label>
              </div>
              @for (g of groups(); track g) {
                <fieldset class="perm-group"><legend>{{ g }} <button type="button" class="link small" (click)="toggleGroup(g)">toggle all</button></legend>
                  @for (p of byGroup(g); track p.code) {
                    <label class="checkbox perm"><input type="checkbox" [checked]="e.permissions.includes(p.code)" (change)="toggle(p.code)" />
                      <span>{{ p.description }} <code class="small muted">{{ p.code }}</code></span></label>
                  }
                </fieldset>
              }
              <div class="actions"><button type="button" class="btn" (click)="edit.set(null)">Cancel</button><button class="btn primary" [disabled]="!e.name">Save role</button></div>
            </div>
          </form>
        } @else {<div class="card empty">Select a role to edit its activities.</div>}
      </div>
    </div>`,
  styles: [`
    .layout { display: grid; grid-template-columns: 300px minmax(0, 1fr); gap: 16px; align-items: start; }
    @media (max-width: 900px) { .layout { grid-template-columns: minmax(0, 1fr); } }
    .role-list { list-style: none; margin: 0; padding: 0 8px 8px; }
    .role-list li { padding: 8px 10px; border-radius: 6px; cursor: pointer; }
    .role-list li:hover { background: var(--surface-2); } .role-list li.active { background: var(--accent-soft); }
    .perm-group { border: 1px solid var(--border); border-radius: 6px; padding: 8px 12px 10px; display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 6px 16px; }
    .perm-group legend { font-weight: 600; font-size: 13px; padding: 0 4px; }
    .perm { align-items: flex-start; }`]
})
export class RolesComponent implements OnInit {
  private api = inject(Api);
  private toasts = inject(Toasts);
  roles = signal<any[]>([]);
  perms = signal<Perm[]>([]);
  edit = signal<any>(null);
  error = signal('');
  groups = computed(() => [...new Set(this.perms().map(p => p.group))]);

  async ngOnInit() {
    try { this.perms.set(await this.api.get<Perm[]>('admin/permissions')); await this.load(); }
    catch (e) { this.error.set(errorMessage(e)); }
  }
  async load() { this.roles.set(await this.api.get<any[]>('admin/roles')); }
  byGroup(g: string) { return this.perms().filter(p => p.group === g); }
  select(r: any) { this.edit.set({ ...r, permissions: [...r.permissions] }); }
  newRole() { this.edit.set({ id: null, name: '', description: '', permissions: ['batch.view'], isSystem: false }); }
  toggle(code: string) {
    const e = this.edit();
    e.permissions = e.permissions.includes(code) ? e.permissions.filter((x: string) => x !== code) : [...e.permissions, code];
    this.edit.set({ ...e });
  }
  toggleGroup(g: string) {
    const e = this.edit(); const codes = this.byGroup(g).map(p => p.code);
    const all = codes.every(c => e.permissions.includes(c));
    e.permissions = all ? e.permissions.filter((x: string) => !codes.includes(x)) : [...new Set([...e.permissions, ...codes])];
    this.edit.set({ ...e });
  }
  async save() {
    const e = this.edit();
    try {
      const body = { name: e.name, description: e.description, permissions: e.permissions };
      if (e.id) await this.api.put(`admin/roles/${e.id}`, body); else await this.api.post('admin/roles', body);
      this.toasts.show('Role saved'); this.edit.set(null); await this.load();
    } catch (err) { this.toasts.show(errorMessage(err), 'error'); }
  }
  async remove() {
    if (!confirm('Delete this role?')) return;
    try { await this.api.delete(`admin/roles/${this.edit().id}`); this.toasts.show('Role deleted'); this.edit.set(null); await this.load(); }
    catch (err) { this.toasts.show(errorMessage(err), 'error'); }
  }
}
