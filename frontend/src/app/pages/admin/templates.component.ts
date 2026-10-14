import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, errorMessage } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Lookups, P } from '../../core/models';
import { DmyTimePipe, LabelPipe } from '../../shared/pipes';
import { Toasts } from '../../shared/ui';

@Component({
  selector: 'brq-templates',
  imports: [FormsModule, LabelPipe, DmyTimePipe],
  template: `
    <div class="page">
      <div class="page-head"><div><h1>Checklist templates</h1>
        <p>A batch gets the most specific active template whose criteria all match (empty = any). Ties go to the higher priority. Editing creates a new version; existing batches keep their own checklist.</p></div>
        @if (canEdit) {<button class="btn primary" (click)="newTemplate()">+ New template</button>}</div>
      @if (error()) {<p class="callout danger">{{ error() }}</p>}
      @if (!edit()) {
        <div class="card"><table class="data">
          <thead><tr><th>Template</th><th>Applies to</th><th class="num">Items</th><th class="num">Priority</th><th class="num">Version</th><th class="num">Batches</th><th>Status</th><th>Updated</th><th></th></tr></thead>
          <tbody>@for (t of list(); track t.id) {
            <tr><td><b>{{ t.name }}</b><div class="small muted">{{ t.description }}</div></td>
              <td class="small">@for (c of criteria(t); track c) {<span class="tag">{{ c }}</span>} @empty {<span class="muted">Any batch</span>}</td>
              <td class="num">{{ t.items.length }}</td><td class="num">{{ t.priority }}</td><td class="num">v{{ t.version }}</td><td class="num">{{ t.batchCount }}</td>
              <td>{{ t.isActive ? 'Active' : 'Inactive' }}</td><td class="nowrap">{{ t.updatedAt | dmyt }}</td>
              <td><button class="btn sm" (click)="open(t)">{{ canEdit ? 'Edit' : 'View' }}</button></td></tr>
          }</tbody></table></div>
      } @else {
        @if (edit(); as e) {
          <form class="card" (ngSubmit)="save()">
            <div class="card-head"><h3>{{ e.id ? e.name + ' (v' + e.version + ')' : 'New template' }}</h3></div>
            <div class="card-body form">
              <div class="form-grid">
                <label class="field span-2"><span>Name<em class="req">*</em></span><input name="n" [(ngModel)]="e.name" [disabled]="!canEdit" /></label>
                <label class="field"><span>Priority (tie-break)</span><input type="number" name="pr" [(ngModel)]="e.priority" [disabled]="!canEdit" /></label>
                <label class="checkbox"><input type="checkbox" name="a" [(ngModel)]="e.isActive" [disabled]="!canEdit" /> Active</label>
                <label class="field span-4"><span>Description</span><input name="d" [(ngModel)]="e.description" [disabled]="!canEdit" /></label>
                <div class="form-section">Applies to (leave empty for "any")</div>
                <label class="field"><span>Product</span><select name="p" [(ngModel)]="e.productId" [disabled]="!canEdit"><option [ngValue]="null">Any</option>@for (x of lk()?.products; track x.id) {<option [ngValue]="x.id">{{ x.name }}</option>}</select></label>
                <label class="field"><span>Dosage form</span><input name="df" [(ngModel)]="e.dosageForm" placeholder="Any" [disabled]="!canEdit" /></label>
                <label class="field"><span>Market</span><select name="m" [(ngModel)]="e.marketId" [disabled]="!canEdit"><option [ngValue]="null">Any</option>@for (x of lk()?.markets; track x.id) {<option [ngValue]="x.id">{{ x.code }}</option>}</select></label>
                <label class="field"><span>Customer</span><select name="c" [(ngModel)]="e.customerId" [disabled]="!canEdit"><option [ngValue]="null">Any</option>@for (x of lk()?.customers; track x.id) {<option [ngValue]="x.id">{{ x.name }}</option>}</select></label>
                <label class="field"><span>Batch type</span><select name="bt" [(ngModel)]="e.batchType" [disabled]="!canEdit"><option [ngValue]="null">Any</option>@for (x of lk()?.enums?.['batchTypes']; track x) {<option [ngValue]="x">{{ x }}</option>}</select></label>
                <label class="field"><span>Site type</span><select name="st" [(ngModel)]="e.siteType" [disabled]="!canEdit"><option [ngValue]="null">Any</option>@for (x of lk()?.enums?.['siteTypes']; track x) {<option [ngValue]="x">{{ x | label }}</option>}</select></label>
              </div>
              <div class="form-section">Items</div>
              <p class="small muted">Step: items with the same step can run in parallel. Weight: leave empty to derive from the step (later steps weigh more). Depends on: comma-separated item codes. Due: days relative to target release date.</p>
              <div class="table-scroll">
                <table class="data compact items">
                  <thead><tr><th>Step</th><th>Code</th><th>Name</th><th>Stage</th><th>Department</th><th>Type</th><th>Mand.</th><th>Weight</th><th>Due ±d</th><th>Per market</th><th>Final</th><th>Source</th><th>Depends on</th><th></th></tr></thead>
                  <tbody>@for (i of e.items; track $index; let idx = $index) {
                    <tr>
                      <td><input type="number" [(ngModel)]="i.sequence" [name]="'s' + idx" class="w-xs" [disabled]="!canEdit" /></td>
                      <td><input [(ngModel)]="i.code" [name]="'c' + idx" class="w-sm" [disabled]="!canEdit" /></td>
                      <td><input [(ngModel)]="i.name" [name]="'n' + idx" class="w-lg" [disabled]="!canEdit" /></td>
                      <td><input [(ngModel)]="i.stage" [name]="'st' + idx" class="w-sm" [disabled]="!canEdit" /></td>
                      <td><select [(ngModel)]="i.departmentId" [name]="'d' + idx" [disabled]="!canEdit">@for (x of lk()?.departments; track x.id) {<option [ngValue]="x.id">{{ x.code }}</option>}</select></td>
                      <td><select [(ngModel)]="i.itemType" [name]="'t' + idx" [disabled]="!canEdit">@for (x of lk()?.enums?.['itemTypes']; track x) {<option [value]="x">{{ x | label }}</option>}</select></td>
                      <td><input type="checkbox" [(ngModel)]="i.isMandatory" [name]="'m' + idx" [disabled]="!canEdit" /></td>
                      <td><input type="number" step="0.1" [(ngModel)]="i.weight" [name]="'w' + idx" class="w-xs" placeholder="auto" [disabled]="!canEdit" /></td>
                      <td><input type="number" [(ngModel)]="i.dueOffsetDays" [name]="'o' + idx" class="w-xs" [disabled]="!canEdit" /></td>
                      <td><input type="checkbox" [(ngModel)]="i.isPerMarket" [name]="'pm' + idx" [disabled]="!canEdit" /></td>
                      <td><input type="checkbox" [(ngModel)]="i.isFinalRelease" [name]="'f' + idx" [disabled]="!canEdit" /></td>
                      <td><select [(ngModel)]="i.defaultSource" [name]="'src' + idx" [disabled]="!canEdit">@for (x of lk()?.enums?.['dataSources']; track x) {<option [value]="x">{{ x }}</option>}</select></td>
                      <td><input [(ngModel)]="i.dependsOn" [name]="'dep' + idx" class="w-sm" [disabled]="!canEdit" /></td>
                      <td>@if (canEdit) {<button type="button" class="icon-btn" (click)="e.items.splice(idx, 1)" aria-label="Remove item">✕</button>}</td>
                    </tr>
                  }</tbody>
                </table>
              </div>
              @if (canEdit) {<div><button type="button" class="btn sm" (click)="addItem()">+ Add item</button></div>
                <label class="field"><span>Reason for change<em class="req">*</em></span><input name="reason" [(ngModel)]="reason" /></label>}
              <div class="actions"><button type="button" class="btn" (click)="edit.set(null)">Back</button>
                @if (canEdit) {<button class="btn primary" [disabled]="!reason.trim() || !e.name">Save {{ e.id ? 'as v' + (e.version + 1) : 'template' }}</button>}</div>
            </div>
          </form>
        }
      }
    </div>`,
  styles: [`.items input, .items select { height: 28px; padding: 2px 6px; } .w-xs { width: 60px; } .w-sm { width: 110px; } .w-lg { width: 260px; } .items input[type=checkbox] { height: auto; }`]
})
export class TemplatesComponent implements OnInit {
  private api = inject(Api);
  private toasts = inject(Toasts);
  canEdit = inject(AuthService).has(P.template);
  list = signal<any[]>([]);
  lk = signal<Lookups | null>(null);
  edit = signal<any>(null);
  error = signal('');
  reason = '';

  async ngOnInit() {
    try { this.lk.set(await this.api.loadLookups()); await this.load(); } catch (e) { this.error.set(errorMessage(e)); }
  }
  async load() { this.list.set(await this.api.get<any[]>('templates')); }
  criteria(t: any) {
    return [t.product && `Product: ${t.product}`, t.dosageForm && `Form: ${t.dosageForm}`, t.market && `Market: ${t.market}`, t.customer && `Customer: ${t.customer}`,
      t.batchType && `Type: ${t.batchType}`, t.siteType && `Site: ${t.siteType === 'ContractManufacturer' ? 'CMO' : 'own'}`].filter(Boolean);
  }
  open(t: any) { this.reason = ''; this.edit.set(JSON.parse(JSON.stringify(t))); }
  newTemplate() {
    this.reason = 'New template';
    this.edit.set({ id: null, name: '', description: '', isActive: true, priority: 0, productId: null, dosageForm: 'Tablet', marketId: null, customerId: null,
      batchType: null, siteType: null, version: 0, items: [
        { code: 'QA_RELEASE', name: 'Final QA release', stage: 'QA Release', departmentId: this.lk()?.departments.find((d: any) => d.code === 'QA')?.id, itemType: 'Approval',
          isMandatory: true, weight: null, sequence: 5, dueOffsetDays: 0, isFinalRelease: true, isPerMarket: false, defaultSource: 'Manual', dependsOn: '' }] });
  }
  addItem() {
    const e = this.edit();
    e.items.splice(e.items.length - 1, 0, { code: '', name: '', stage: '', departmentId: this.lk()?.departments[0]?.id, itemType: 'DataCheck', isMandatory: true,
      weight: null, sequence: 1, dueOffsetDays: -5, isFinalRelease: false, isPerMarket: false, defaultSource: 'Manual', dependsOn: '' });
  }
  async save() {
    const e = this.edit();
    const body = { ...e, reason: this.reason, items: e.items.map((i: any) => ({ ...i, weight: i.weight === '' ? null : i.weight })) };
    try {
      if (e.id) await this.api.put(`templates/${e.id}`, body); else await this.api.post('templates', body);
      this.toasts.show('Template saved'); this.edit.set(null); await this.load();
    } catch (err) { this.toasts.show(errorMessage(err), 'error'); }
  }
}
