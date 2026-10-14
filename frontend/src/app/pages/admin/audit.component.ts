import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Api, errorMessage } from '../../core/api.service';
import { DmyTimePipe } from '../../shared/pipes';
import { DateInputComponent } from '../../shared/ui';

@Component({
  selector: 'brq-audit',
  imports: [FormsModule, RouterLink, DmyTimePipe, DateInputComponent],
  template: `
    <div class="page">
      <div class="page-head"><div><h1>Audit trail</h1><p>System-wide, append-only record of every change: who, what, when, why, old and new value. Protected against edits at database level.</p></div></div>
      <form class="filter-bar" (ngSubmit)="load()">
        <label class="field compact"><span>Record type</span><select name="t" [(ngModel)]="f.entityType"><option value="">All</option>
          @for (t of types; track t) {<option>{{ t }}</option>}</select></label>
        <label class="field compact"><span>User</span><input name="u" [(ngModel)]="f.username" placeholder="User ID" /></label>
        <label class="field compact"><span>From</span><brq-date name="from" [(ngModel)]="f.from" /></label>
        <label class="field compact"><span>To</span><brq-date name="to" [(ngModel)]="f.to" /></label>
        <div class="filter-actions"><button class="btn">Apply</button></div>
      </form>
      @if (error()) {<p class="callout danger">{{ error() }}</p>}
      <div class="card"><div class="table-scroll" style="max-height: 72vh">
        <table class="data compact">
          <thead><tr><th>#</th><th>When</th><th>User</th><th>Action</th><th>Record</th><th>Batch</th><th>Field</th><th>Old value</th><th>New value</th><th>Reason</th></tr></thead>
          <tbody>@for (a of rows(); track a.id) {
            <tr><td class="muted">{{ a.id }}</td><td class="nowrap">{{ a.timestamp | dmyt }}</td><td>{{ a.username }}</td><td>{{ a.action }}</td>
              <td class="nowrap">{{ a.entityType }} #{{ a.entityId }}</td><td>@if (a.batchId) {<a [routerLink]="['/batches', a.batchId]" [queryParams]="{ tab: 'audit' }">{{ a.batchId }}</a>}</td>
              <td>{{ a.fieldName }}</td><td class="small val">{{ a.oldValue }}</td><td class="small val">{{ a.newValue }}</td><td class="small">{{ a.reason }}</td></tr>
          } @empty {<tr><td colspan="10" class="empty">No entries.</td></tr>}</tbody>
        </table></div></div>
    </div>`,
  styles: [`.val { max-width: 300px; overflow-wrap: anywhere; color: var(--text-secondary); }`]
})
export class AuditComponent implements OnInit {
  private api = inject(Api);
  rows = signal<any[]>([]);
  error = signal('');
  f: any = { entityType: '', username: '', from: null, to: null };
  types = ['Batch', 'ChecklistItem', 'ChecklistChangeRequest', 'ElectronicSignature', 'BatchRelease', 'Dispatch', 'QualityEvent', 'ExternalMovement',
    'BatchMarket', 'ChecklistTemplate', 'TemplateItem', 'Role', 'RolePermission', 'User', 'UserRole', 'PrioritySettings', 'Product', 'Market'];
  ngOnInit() { this.load(); }
  async load() {
    try { this.rows.set(await this.api.get<any[]>('admin/audit', { ...this.f, take: 500 })); this.error.set(''); }
    catch (e) { this.error.set(errorMessage(e)); }
  }
}
