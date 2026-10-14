import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../core/api.service';
import { BatchFilter, Lookups } from '../core/models';
import { LabelPipe } from './pipes';
import { DateInputComponent } from './ui';

/** The one filter row that scopes everything below it (queue, list, dashboard). */
@Component({
  selector: 'brq-filter-bar',
  imports: [FormsModule, DateInputComponent, LabelPipe],
  template: `
    <form class="filter-bar" (ngSubmit)="apply()">
      <label class="field compact"><span>Date</span>
        <select name="dateField" [(ngModel)]="f.dateField" (change)="apply()">
          <option value="target">Target release</option><option value="manufacturing">Manufacturing</option>
          <option value="released">Released</option><option value="expiry">Expiry</option>
        </select></label>
      <label class="field compact"><span>From</span><brq-date name="from" [(ngModel)]="f.from" (ngModelChange)="apply()" /></label>
      <label class="field compact"><span>To</span><brq-date name="to" [(ngModel)]="f.to" (ngModelChange)="apply()" /></label>
      <label class="field compact"><span>Product</span>
        <select name="productId" [(ngModel)]="f.productId" (change)="apply()"><option [ngValue]="null">All products</option>
          @for (p of lk()?.products; track p.id) {<option [ngValue]="p.id">{{ p.name }} {{ p.strength }}{{ p.strengthUnit }}</option>}</select></label>
      <label class="field compact"><span>Product code</span>
        <input name="productCode" [(ngModel)]="f.productCode" list="brq-product-codes" placeholder="Any" (change)="apply()" size="8" />
        <datalist id="brq-product-codes">@for (p of lk()?.products; track p.id) {<option [value]="p.code"></option>}</datalist></label>
      <label class="field compact"><span>Site</span>
        <select name="siteId" [(ngModel)]="f.siteId" (change)="apply()"><option [ngValue]="null">All sites</option>
          @for (s of lk()?.sites; track s.id) {<option [ngValue]="s.id">{{ s.name }}</option>}</select></label>
      <label class="field compact"><span>Market</span>
        <select name="marketId" [(ngModel)]="f.marketId" (change)="apply()"><option [ngValue]="null">All markets</option>
          @for (m of lk()?.markets; track m.id) {<option [ngValue]="m.id">{{ m.code }} – {{ m.name }}</option>}</select></label>
      <label class="field compact"><span>Customer</span>
        <select name="customerId" [(ngModel)]="f.customerId" (change)="apply()"><option [ngValue]="null">All customers</option>
          @for (c of lk()?.customers; track c.id) {<option [ngValue]="c.id">{{ c.name }}</option>}</select></label>
      @if (showStatus) {
        <label class="field compact"><span>Status</span>
          <select name="disposition" [(ngModel)]="f.disposition" (change)="apply()"><option [ngValue]="null">All statuses</option>
            @for (d of lk()?.enums?.['dispositions']; track d) {<option [ngValue]="d">{{ d | label }}</option>}</select></label>
      }
      @if (showSearch) {
        <label class="field compact grow"><span>Search</span><input name="search" [(ngModel)]="f.search" placeholder="Batch no, product…" /></label>
      }
      <div class="filter-actions">
        @if (showSearch) {<button type="submit" class="btn">Apply</button>}
        <button type="button" class="link" (click)="clear()">Clear</button>
      </div>
    </form>`,
})
export class FilterBarComponent implements OnInit {
  private api = inject(Api);
  @Input() value: BatchFilter = {};
  @Input() showStatus = true;
  @Input() showSearch = false;
  @Output() changed = new EventEmitter<BatchFilter>();
  lk = signal<Lookups | null>(null);
  f: BatchFilter = {};

  async ngOnInit() {
    this.f = { ...this.value, dateField: this.value.dateField ?? 'target' };
    this.lk.set(await this.api.loadLookups());
  }
  apply() { this.changed.emit(clean(this.f)); }
  clear() { this.f = { dateField: 'target' }; this.apply(); }
}

export function clean(f: BatchFilter): BatchFilter {
  const out: any = {};
  for (const [k, v] of Object.entries(f)) if (v !== null && v !== undefined && v !== '') out[k] = v;
  if (!out.from && !out.to) delete out.dateField;
  return out;
}

/** Reads a BatchFilter from route query params (numbers parsed). */
export function filterFromParams(p: Record<string, string>): BatchFilter {
  const num = (v?: string) => (v ? Number(v) : null);
  return {
    productId: num(p['productId']), productCode: p['productCode'] ?? null, siteId: num(p['siteId']), marketId: num(p['marketId']),
    customerId: num(p['customerId']), disposition: p['disposition'] ?? null, stage: p['stage'] ?? null, dateField: p['dateField'] ?? null,
    from: p['from'] ?? null, to: p['to'] ?? null, search: p['search'] ?? null, metric: p['metric'] ?? null, batchType: p['batchType'] ?? null,
  };
}
