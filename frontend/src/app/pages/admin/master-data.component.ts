import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, errorMessage } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { DmyPipe, LabelPipe } from '../../shared/pipes';
import { DateInputComponent, ModalComponent, Toasts } from '../../shared/ui';

interface Field { key: string; label: string; type?: 'text' | 'number' | 'bool' | 'date' | 'enum'; options?: string[]; }
interface Entity { key: string; label: string; singular: string; url: string; fields: Field[]; blank: any; }

const ENTITIES: Entity[] = [
  { key: 'products', label: 'Products', singular: 'product', url: 'master/products', blank: { code: '', name: '', strength: '', strengthUnit: 'mg', dosageForm: 'Tablet', productType: 'Generic Rx', packSize: '', shelfLifeMonths: 24, batchPrefix: '', isActive: true },
    fields: [{ key: 'code', label: 'Code' }, { key: 'name', label: 'Name' }, { key: 'strength', label: 'Strength' }, { key: 'strengthUnit', label: 'Unit' }, { key: 'dosageForm', label: 'Dosage form' },
      { key: 'productType', label: 'Product type' }, { key: 'packSize', label: 'Pack size' }, { key: 'shelfLifeMonths', label: 'Shelf life (months)', type: 'number' },
      { key: 'batchPrefix', label: 'Batch prefix' }, { key: 'isActive', label: 'Active', type: 'bool' }] },
  { key: 'markets', label: 'Markets', singular: 'market', url: 'master/markets', blank: { code: '', name: '', regulatoryAuthority: '', minShelfLifePctAtDispatch: 75, isActive: true },
    fields: [{ key: 'code', label: 'Code' }, { key: 'name', label: 'Name' }, { key: 'regulatoryAuthority', label: 'Authority' },
      { key: 'minShelfLifePctAtDispatch', label: 'Min shelf life at dispatch (%)', type: 'number' }, { key: 'isActive', label: 'Active', type: 'bool' }] },
  { key: 'sites', label: 'Sites', singular: 'site', url: 'master/sites', blank: { code: '', name: '', city: '', country: '', siteType: 'Own', isActive: true },
    fields: [{ key: 'code', label: 'Code' }, { key: 'name', label: 'Name' }, { key: 'city', label: 'City' }, { key: 'country', label: 'Country' },
      { key: 'siteType', label: 'Site type', type: 'enum', options: ['Own', 'ContractManufacturer'] }, { key: 'isActive', label: 'Active', type: 'bool' }] },
  { key: 'parties', label: 'External parties', singular: 'external party', url: 'master/external-parties', blank: { code: '', name: '', partyType: 'ContractLab', city: '', country: '', isApproved: true, qualityAgreementValidUntil: null, isActive: true },
    fields: [{ key: 'code', label: 'Code' }, { key: 'name', label: 'Name' }, { key: 'partyType', label: 'Type', type: 'enum', options: ['ContractManufacturer', 'ContractLab', 'PackagingVendor', 'LabellingVendor', 'Distributor'] },
      { key: 'city', label: 'City' }, { key: 'country', label: 'Country' }, { key: 'isApproved', label: 'Approved', type: 'bool' },
      { key: 'qualityAgreementValidUntil', label: 'Quality agreement valid until', type: 'date' }, { key: 'isActive', label: 'Active', type: 'bool' }] },
  { key: 'customers', label: 'Customers', singular: 'customer', url: 'master/customers', blank: { code: '', name: '', country: '', isActive: true },
    fields: [{ key: 'code', label: 'Code' }, { key: 'name', label: 'Name' }, { key: 'country', label: 'Country' }, { key: 'isActive', label: 'Active', type: 'bool' }] },
  { key: 'departments', label: 'Departments', singular: 'department', url: 'master/departments', blank: { code: '', name: '' },
    fields: [{ key: 'code', label: 'Code' }, { key: 'name', label: 'Name' }] },
];

@Component({
  selector: 'brq-master-data',
  imports: [FormsModule, DmyPipe, LabelPipe, ModalComponent, DateInputComponent],
  template: `
    <div class="page">
      <div class="page-head"><div><h1>Master data</h1><p>Records are deactivated rather than deleted so that history stays intact. Every change is audited.</p></div>
        @if (canEdit) {<button class="btn primary" (click)="open()">+ Add {{ entity().singular }}</button>}</div>
      <div class="tabs">@for (e of entities; track e.key) {<button [class.active]="entity().key === e.key" (click)="select(e)">{{ e.label }}</button>}</div>
      @if (error()) {<p class="callout danger">{{ error() }}</p>}
      <div class="card"><div class="table-scroll"><table class="data">
        <thead><tr>@for (f of entity().fields; track f.key) {<th>{{ f.label }}</th>}<th></th></tr></thead>
        <tbody>@for (r of rows(); track r.id) {
          <tr>@for (f of entity().fields; track f.key) {
            <td>@switch (f.type) { @case ('bool') { {{ r[f.key] ? 'Yes' : 'No' }} } @case ('date') { {{ r[f.key] | dmy }} } @case ('enum') { {{ r[f.key] | label }} } @default { {{ r[f.key] }} } }</td>}
            <td>@if (canEdit) {<button class="btn sm" (click)="open(r)">Edit</button>}</td></tr>
        }</tbody></table></div></div>
      <brq-modal [open]="!!form()" [title]="(form()?.id ? 'Edit ' : 'Add ') + entity().singular" (close)="form.set(null)">
        @if (form(); as m) {
          <form class="form" (ngSubmit)="save()">
            <div class="form-grid">
              @for (f of entity().fields; track f.key) {
                @switch (f.type) {
                  @case ('bool') {<label class="checkbox span-2"><input type="checkbox" [name]="f.key" [(ngModel)]="m[f.key]" /> {{ f.label }}</label>}
                  @case ('number') {<label class="field span-2"><span>{{ f.label }}</span><input type="number" [name]="f.key" [(ngModel)]="m[f.key]" /></label>}
                  @case ('date') {<label class="field span-2"><span>{{ f.label }}</span><brq-date [name]="f.key" [(ngModel)]="m[f.key]" /></label>}
                  @case ('enum') {<label class="field span-2"><span>{{ f.label }}</span><select [name]="f.key" [(ngModel)]="m[f.key]">@for (o of f.options; track o) {<option [value]="o">{{ o | label }}</option>}</select></label>}
                  @default {<label class="field span-2"><span>{{ f.label }}</span><input [name]="f.key" [(ngModel)]="m[f.key]" /></label>}
                }
              }
            </div>
            <div class="actions"><button type="button" class="btn" (click)="form.set(null)">Cancel</button><button class="btn primary">Save</button></div>
          </form>
        }
      </brq-modal>
    </div>`,
})
export class MasterDataComponent implements OnInit {
  private api = inject(Api);
  private toasts = inject(Toasts);
  canEdit = inject(AuthService).has(P.masterData);
  entities = ENTITIES;
  entity = signal(ENTITIES[0]);
  rows = signal<any[]>([]);
  form = signal<any>(null);
  error = signal('');
  ngOnInit() { this.load(); }
  select(e: Entity) { this.entity.set(e); this.load(); }
  async load() { try { this.rows.set(await this.api.get<any[]>(this.entity().url)); this.error.set(''); } catch (e) { this.error.set(errorMessage(e)); } }
  open(r?: any) { this.form.set(r ? { ...r } : { ...this.entity().blank }); }
  async save() {
    const m = this.form();
    try {
      if (m.id) await this.api.put(`${this.entity().url}/${m.id}`, m); else await this.api.post(this.entity().url, { ...m, id: 0 });
      this.toasts.show('Saved'); this.form.set(null); await this.load(); await this.api.loadLookups(true);
    } catch (e) { this.toasts.show(errorMessage(e), 'error'); }
  }
}
