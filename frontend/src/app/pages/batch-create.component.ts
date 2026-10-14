import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Api, errorMessage } from '../core/api.service';
import { addDaysIso, todayIso } from '../core/dates';
import { Lookups } from '../core/models';
import { DmyPipe, LabelPipe } from '../shared/pipes';
import { DateInputComponent, Toasts } from '../shared/ui';

@Component({
  selector: 'brq-batch-create',
  imports: [FormsModule, RouterLink, DateInputComponent, LabelPipe, DmyPipe],
  template: `
    <div class="page">
      <div class="page-head">
        <div><h1>New batch</h1><p>The matching checklist template is suggested from product, site, markets, customer and batch type. You can pick another one.</p></div>
      </div>
      <div class="layout">
        <form class="card form-card" (ngSubmit)="create()" #form="ngForm">
          <div class="form-grid">
            <div class="form-section">Batch type</div>
            <label class="field"><span>Batch type<em class="req">*</em></span>
              <select name="batchType" [(ngModel)]="m.batchType" (change)="onTypeChange()">@for (t of lk()?.enums?.['batchTypes']; track t) {<option [value]="t">{{ t | label }}</option>}</select></label>
            @if (needsParent()) {
              <label class="field span-2"><span>Parent batch<em class="req">*</em></span>
                <div class="inline-pair">
                  <input name="parentSearch" [(ngModel)]="parentSearch" placeholder="Search parent batch no…" (keyup.enter)="$event.preventDefault(); findParents()" />
                  <button type="button" class="btn" (click)="findParents()">Find</button>
                </div>
                @if (parentOptions().length) {
                  <select name="parent" [(ngModel)]="m.parentBatchId" (change)="onParent()">
                    <option [ngValue]="null">Select…</option>
                    @for (p of parentOptions(); track p.id) {<option [ngValue]="p.id">{{ p.batchNo }} – {{ p.product }} ({{ p.disposition | label }}, exp {{ p.expiryDate | dmy }})</option>}
                  </select>}
              </label>
              <p class="small muted span-4">Repacked or relabelled batches inherit the parent's expiry and cannot be released before the parent is released (or released for further processing).</p>
            }

            <div class="form-section">Product information</div>
            <label class="field span-2"><span>Product<em class="req">*</em></span>
              <select name="productId" [(ngModel)]="m.productId" required (change)="onProduct()"><option [ngValue]="null">Select…</option>
                @for (p of activeProducts(); track p.id) {<option [ngValue]="p.id">{{ p.name }} {{ p.strength }} {{ p.strengthUnit }} ({{ p.code }})</option>}</select></label>
            <label class="field"><span>Dosage form</span><input [value]="product()?.dosageForm ?? ''" disabled /></label>
            <label class="field"><span>Pack size</span><input [value]="product()?.packSize ?? ''" disabled /></label>
            <label class="field span-2"><span>Manufacturing site<em class="req">*</em></span>
              <select name="siteId" [(ngModel)]="m.siteId" required (change)="match()"><option [ngValue]="null">Select…</option>
                @for (s of lk()?.sites; track s.id) {<option [ngValue]="s.id">{{ s.name }}@if (s.siteType === 'ContractManufacturer') { – contract manufacturer}</option>}</select></label>
            <label class="field span-2"><span>Customer</span>
              <select name="customerId" [(ngModel)]="m.customerId" (change)="match()"><option [ngValue]="null">(none / own brand)</option>
                @for (c of lk()?.customers; track c.id) {<option [ngValue]="c.id">{{ c.name }}</option>}</select></label>
            <fieldset class="field span-4 markets"><span>Target markets<em class="req">*</em> <small class="muted">– each market gets its own regulatory item and shelf-life rule</small></span>
              <div class="row">@for (mk of lk()?.markets; track mk.id) {
                <label class="checkbox"><input type="checkbox" [checked]="m.marketIds.includes(mk.id)" (change)="toggleMarket(mk.id)" />{{ mk.code }} – {{ mk.name }}</label>}</div>
            </fieldset>

            <div class="form-section">Batch information</div>
            <label class="field"><span>Batch no</span>
              <div class="inline-pair"><input name="batchNo" [(ngModel)]="m.batchNo" placeholder="Auto" /><button type="button" class="btn" (click)="suggest()" title="Suggest next number">↻</button></div></label>
            <label class="field"><span>LOT type<em class="req">*</em></span>
              <select name="lotType" [(ngModel)]="m.lotType">@for (l of lotTypes; track l) {<option>{{ l }}</option>}</select></label>
            <label class="field"><span>Batch size<em class="req">*</em></span>
              <div class="inline-pair"><input name="batchSize" type="number" min="1" [(ngModel)]="m.batchSize" required style="min-width:90px" />
                <select name="unit" [(ngModel)]="m.batchSizeUnit" style="max-width:110px">@for (u of units; track u) {<option>{{ u }}</option>}</select></div></label>
            <label class="field"><span>Manufacturing block</span><input name="block" [(ngModel)]="m.manufacturingBlock" /></label>
            <label class="field"><span>Process order date</span><brq-date name="pod" [(ngModel)]="m.processOrderDate" /></label>
            <label class="field"><span>Manufacturing date<em class="req">*</em></span><brq-date name="mfg" [(ngModel)]="m.manufacturingDate" (ngModelChange)="recalc()" /></label>
            <label class="field"><span>Batch completion date</span><brq-date name="bcd" [(ngModel)]="m.batchCompletionDate" /></label>
            <label class="field"><span>Target release date<em class="req">*</em></span><brq-date name="trd" [(ngModel)]="m.targetReleaseDate" /></label>
            <label class="field"><span>Shelf life (months)<em class="req">*</em></span><input name="shelf" type="number" min="1" [(ngModel)]="m.shelfLifeMonths" (change)="recalc()" /></label>
            <label class="field"><span>Expiry date<em class="req">*</em></span><brq-date name="exp" [(ngModel)]="m.expiryDate" /></label>
            <div class="field span-2"><span>Calculated</span>
              <div class="small secondary calc">Expiry {{ calcExpiry() | dmy }} · dispatch by {{ dispatchBy() | dmy }} to keep ≥ 75% shelf life</div></div>
            @if (expiryOverridden()) {
              <label class="field span-4"><span>Reason for expiry override (e.g. shelf-life extension)<em class="req">*</em></span>
                <input name="expReason" [(ngModel)]="m.expiryOverrideReason" required /></label>
            }
          </div>
          @if (error()) {<p class="callout danger">{{ error() }}</p>}
          <div class="actions">
            <a class="btn" routerLink="/batches">Cancel</a>
            <button class="btn primary" type="submit" [disabled]="busy() || !valid()">{{ busy() ? 'Creating…' : 'Create batch' }}</button>
          </div>
        </form>

        <aside class="card template-card">
          <div class="card-head"><div><h3>Checklist template</h3><p>Applied when the batch is created. Later changes need approval.</p></div></div>
          <div class="card-body">
            @if (!templates().length) {<p class="muted">Choose product, site and markets to see the matching template.</p>}
            @else {
              <label class="field"><span>Template</span>
                <select name="tpl" [(ngModel)]="templateId">
                  @for (t of templates(); track t.id; let first = $first) {<option [ngValue]="t.id">{{ t.name }} v{{ t.version }}{{ first ? ' (suggested)' : '' }}</option>}
                </select></label>
              @if (selected(); as t) {
                <p class="small muted">{{ t.description }}</p>
                <ol class="tpl-items">
                  @for (i of t.items; track i.code) {
                    <li [class.final]="i.isFinalRelease">
                      <span class="seq">{{ i.sequence }}</span>
                      <div><div>{{ i.name }}@if (!i.isMandatory) { <span class="tag">optional</span>}@if (i.isPerMarket) { <span class="tag">× {{ m.marketIds.length || 1 }} markets</span>}</div>
                        <div class="small muted">{{ i.stage }} · {{ i.department }} · {{ i.itemType | label }}</div></div>
                    </li>
                  }
                </ol>
              }
            }
          </div>
        </aside>
      </div>
    </div>`,
  styles: [`
    .layout { display: grid; grid-template-columns: minmax(0, 1fr) 380px; gap: 16px; align-items: start; }
    @media (max-width: 1100px) { .layout { grid-template-columns: minmax(0, 1fr); } }
    .form-card { padding: 16px; }
    .markets { border: 0; padding: 0; margin: 0; }
    .calc { padding-top: 6px; }
    .template-card { position: sticky; top: 64px; }
    .tpl-items { list-style: none; padding: 0; margin: 8px 0 0; display: flex; flex-direction: column; gap: 6px; }
    .tpl-items li { display: flex; gap: 10px; padding: 8px; border-radius: 6px; background: var(--surface-2); }
    .tpl-items li.final { border-left: 3px solid var(--status-good); }
    .seq { flex-shrink: 0; width: 22px; height: 22px; border-radius: 50%; background: var(--surface-sunk); display: grid; place-items: center; font-size: 11px; font-weight: 700; }`]
})
export class BatchCreateComponent implements OnInit {
  private api = inject(Api);
  private router = inject(Router);
  private toasts = inject(Toasts);
  lk = signal<Lookups | null>(null);
  templates = signal<any[]>([]);
  parentOptions = signal<any[]>([]);
  busy = signal(false);
  error = signal('');
  templateId: number | null = null;
  parentSearch = '';
  lotTypes = ['Commercial', 'Validation', 'Exhibit', 'Rework', 'Repack', 'Relabel'];
  units = ['Tablets', 'Kg', 'Packs', 'Bottles'];
  m: any = {
    batchType: 'Commercial', parentBatchId: null, productId: null, siteId: null, customerId: null, marketIds: [] as number[], batchNo: '',
    lotType: 'Commercial', batchSize: 600000, batchSizeUnit: 'Tablets', manufacturingBlock: '', processOrderDate: null,
    manufacturingDate: todayIso(), batchCompletionDate: null, targetReleaseDate: addDaysIso(todayIso(), 25), shelfLifeMonths: 24,
    expiryDate: null, expiryOverrideReason: ''
  };
  calcExpiry = signal<string | null>(null);
  dispatchBy = signal<string | null>(null);

  activeProducts = computed(() => (this.lk()?.products ?? []).filter((p: any) => p.isActive));
  product = () => this.lk()?.products.find((p: any) => p.id === this.m.productId);
  needsParent = () => ['Rework', 'Reprocess', 'Repack', 'Relabel'].includes(this.m.batchType);
  selected = () => this.templates().find(t => t.id === this.templateId);
  expiryOverridden = () => !!this.m.expiryDate && !!this.calcExpiry() && this.m.expiryDate !== this.calcExpiry() && !this.m.parentBatchId;
  valid = () => this.m.productId && this.m.siteId && this.m.marketIds.length && this.m.batchSize > 0 && this.m.manufacturingDate &&
    this.m.targetReleaseDate && this.m.expiryDate && (!this.needsParent() || this.m.parentBatchId) && (!this.expiryOverridden() || this.m.expiryOverrideReason?.trim());

  private route = inject(ActivatedRoute);

  async ngOnInit() {
    this.lk.set(await this.api.loadLookups());
    const q = this.route.snapshot.queryParamMap;
    if (q.get('type')) { this.m.batchType = q.get('type'); this.onTypeChange(); }
    const parentId = Number(q.get('parent'));
    if (parentId) {
      const p = await this.api.get<any>(`batches/${parentId}`);
      this.parentOptions.set([p.summary]);
      this.m.parentBatchId = parentId;
      this.m.siteId = p.details.site.id;
      this.m.marketIds = [];
      this.m.batchSize = p.details.batchSize;
      this.onParent();
      this.recalc();
    }
  }

  onTypeChange() {
    if (['Validation', 'Exhibit', 'Rework', 'Repack', 'Relabel'].includes(this.m.batchType)) this.m.lotType = this.m.batchType;
    else if (this.m.batchType === 'Commercial') this.m.lotType = 'Commercial';
    this.match();
  }
  onProduct() { const p = this.product(); if (p) this.m.shelfLifeMonths = p.shelfLifeMonths; this.recalc(); this.suggest(); this.match(); }
  toggleMarket(id: number) {
    this.m.marketIds = this.m.marketIds.includes(id) ? this.m.marketIds.filter((x: number) => x !== id) : [...this.m.marketIds, id];
    this.match();
  }

  async recalc() {
    if (!this.m.productId || !this.m.manufacturingDate) return;
    const r = await this.api.get<any>('batches/suggest', { productId: this.m.productId, manufacturingDate: this.m.manufacturingDate, shelfLifeMonths: this.m.shelfLifeMonths });
    const wasCalculated = !this.m.expiryDate || this.m.expiryDate === this.calcExpiry();
    this.calcExpiry.set(r.expiryDate); this.dispatchBy.set(r.dispatchBy);
    if (wasCalculated && !this.m.parentBatchId) this.m.expiryDate = r.expiryDate;
  }

  async suggest() {
    if (!this.m.productId || this.needsParent()) return;
    const r = await this.api.get<any>('batches/suggest', { productId: this.m.productId, manufacturingDate: this.m.manufacturingDate });
    this.m.batchNo = r.batchNo;
  }

  async findParents() {
    const r = await this.api.get<any>('batches', { search: this.parentSearch, pageSize: 30 });
    this.parentOptions.set(r.items);
  }

  onParent() {
    const p = this.parentOptions().find(x => x.id === this.m.parentBatchId);
    if (!p) return;
    const prod = this.lk()?.products.find((x: any) => x.code === p.productCode);
    if (prod) this.m.productId = prod.id;
    if (this.m.batchType !== 'Rework') this.m.expiryDate = p.expiryDate;
    this.m.batchNo = '';
    this.match();
  }

  async match() {
    if (!this.m.productId || !this.m.siteId) { this.templates.set([]); return; }
    try {
      const t = await this.api.get<any[]>('batches/template-match', { productId: this.m.productId, siteId: this.m.siteId, marketIds: this.m.marketIds,
        customerId: this.m.customerId, batchType: this.m.batchType });
      this.templates.set(t);
      this.templateId = t[0]?.id ?? null;
    } catch (e) { this.error.set(errorMessage(e)); }
  }

  async create() {
    this.busy.set(true); this.error.set('');
    try {
      const r = await this.api.post<{ id: number; batchNo: string }>('batches', { ...this.m, batchNo: this.m.batchNo || null, templateId: this.templateId,
        expiryOverrideReason: this.expiryOverridden() ? this.m.expiryOverrideReason : null });
      this.toasts.show(`Batch ${r.batchNo} created`);
      this.router.navigate(['/batches', r.id]);
    } catch (e) { this.error.set(errorMessage(e)); }
    finally { this.busy.set(false); }
  }
}
