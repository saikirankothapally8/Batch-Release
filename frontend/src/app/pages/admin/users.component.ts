import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, errorMessage } from '../../core/api.service';
import { DmyTimePipe } from '../../shared/pipes';
import { ModalComponent, StatusComponent, Toasts } from '../../shared/ui';

@Component({
  selector: 'brq-users',
  imports: [FormsModule, DmyTimePipe, ModalComponent, StatusComponent],
  template: `
    <div class="page">
      <div class="page-head"><div><h1>Users</h1><p>User IDs are unique and never reused. Deactivate instead of deleting to keep history attributable.</p></div>
        <button class="btn primary" (click)="open()">+ New user</button></div>
      @if (error()) {<p class="callout danger">{{ error() }}</p>}
      <div class="card"><table class="data">
        <thead><tr><th>User ID</th><th>Name</th><th>Department</th><th>Roles</th><th>Status</th><th>Last login</th><th></th></tr></thead>
        <tbody>@for (u of users(); track u.id) {
          <tr><td><b>{{ u.username }}</b></td><td>{{ u.fullName }}<div class="small muted">{{ u.email }}</div></td><td>{{ u.department }}</td>
            <td>@for (r of u.roles; track r) {<span class="tag">{{ r }}</span>}</td>
            <td>@if (u.isLocked) {<brq-status value="Blocked" label="Locked" />} @else {<brq-status [value]="u.isActive ? 'Completed' : 'NotStarted'" [label]="u.isActive ? 'Active' : 'Inactive'" />}</td>
            <td>{{ u.lastLoginAt | dmyt }}</td><td><button class="btn sm" (click)="open(u)">Edit</button></td></tr>
        }</tbody></table></div>
      <brq-modal [open]="!!form()" [title]="form()?.id ? 'Edit user' : 'New user'" (close)="form.set(null)">
        @if (form(); as f) {
          <form class="form" (ngSubmit)="save()">
            <div class="form-grid">
              <label class="field span-2"><span>User ID<em class="req">*</em></span><input name="u" [(ngModel)]="f.username" [disabled]="!!f.id" required /></label>
              <label class="field span-2"><span>Full name<em class="req">*</em></span><input name="n" [(ngModel)]="f.fullName" required /></label>
              <label class="field span-2"><span>Email</span><input name="e" type="email" [(ngModel)]="f.email" /></label>
              <label class="field span-2"><span>Department</span><select name="d" [(ngModel)]="f.departmentId"><option [ngValue]="null">—</option>
                @for (d of depts(); track d.id) {<option [ngValue]="d.id">{{ d.name }}</option>}</select></label>
              <label class="field span-4"><span>{{ f.id ? 'Reset password (leave blank to keep)' : 'Initial password' }}@if (!f.id) {<em class="req">*</em>}</span>
                <input name="p" type="password" [(ngModel)]="f.password" autocomplete="new-password" /></label>
              <label class="checkbox span-4"><input type="checkbox" name="a" [(ngModel)]="f.isActive" /> Active</label>
            </div>
            <fieldset class="roles"><legend>Roles</legend>
              @for (r of roles(); track r.id) {<label class="checkbox"><input type="checkbox" [checked]="f.roleIds.includes(r.id)" (change)="toggle(r.id)" />{{ r.name }}</label>}
            </fieldset>
            <div class="actions"><button type="button" class="btn" (click)="form.set(null)">Cancel</button><button class="btn primary">Save</button></div>
          </form>
        }
      </brq-modal>
    </div>`,
  styles: [`.roles { border: 1px solid var(--border); border-radius: 6px; display: grid; grid-template-columns: repeat(2, 1fr); gap: 6px; padding: 8px 12px; } .roles legend { font-weight: 600; font-size: 13px; }`]
})
export class UsersComponent implements OnInit {
  private api = inject(Api);
  private toasts = inject(Toasts);
  users = signal<any[]>([]);
  roles = signal<any[]>([]);
  depts = signal<any[]>([]);
  form = signal<any>(null);
  error = signal('');

  async ngOnInit() {
    try {
      const [u, r, lk] = await Promise.all([this.api.get<any[]>('admin/users'), this.api.get<any[]>('admin/roles'), this.api.loadLookups()]);
      this.users.set(u); this.roles.set(r); this.depts.set(lk.departments);
    } catch (e) { this.error.set(errorMessage(e)); }
  }
  open(u?: any) { this.form.set(u ? { ...u, roleIds: [...u.roleIds], password: '' } : { username: '', fullName: '', email: '', departmentId: null, isActive: true, roleIds: [], password: '' }); }
  toggle(id: number) { const f = this.form(); f.roleIds = f.roleIds.includes(id) ? f.roleIds.filter((x: number) => x !== id) : [...f.roleIds, id]; this.form.set({ ...f }); }
  async save() {
    const f = this.form();
    try {
      if (f.id) await this.api.put(`admin/users/${f.id}`, f); else await this.api.post('admin/users', f);
      this.toasts.show('User saved'); this.form.set(null); this.users.set(await this.api.get<any[]>('admin/users'));
    } catch (e) { this.toasts.show(errorMessage(e), 'error'); }
  }
}
