import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Api, errorMessage } from '../core/api.service';
import { Batch360, BatchFilter, Dashboard } from '../core/models';
import { BatchMapComponent, MapTarget } from '../shared/batch-map.component';
import { ChartComponent, ChartPick, Series } from '../shared/chart.component';
import { FilterBarComponent, clean, filterFromParams } from '../shared/filter-bar.component';
import { DmyPipe } from '../shared/pipes';
import { ModalComponent, ReadinessComponent, StatusComponent } from '../shared/ui';

const EXPIRY_TONE = ['--status-critical', '--status-critical', '--status-serious', '--status-warning', '--status-neutral', '--status-good'];
const ORDINAL = ['--ordinal-1', '--ordinal-2', '--ordinal-3', '--ordinal-4', '--ordinal-5'];

@Component({
  selector: 'brq-dashboard',
  imports: [FormsModule, RouterLink, DecimalPipe, DmyPipe, FilterBarComponent, ChartComponent, ModalComponent, BatchMapComponent, ReadinessComponent, StatusComponent],
  template: `
    <div class="page">
      <div class="page-head">
        <div><h1>Management dashboard</h1><p>Throughput, bottlenecks, on-time performance and risk. Click any number, bar or batch to see what is behind it.</p></div>
        @if (d(); as d) {<span class="small muted">As of {{ d.today | dmy }}</span>}
      </div>
      <brq-filter-bar [value]="filter" (changed)="onFilter($event)" />
      @if (error()) {<p class="callout danger">{{ error() }}</p>}

      @if (d(); as d) {
        <div [style.opacity]="loading() ? 0.6 : 1">
          <div class="kpis">
            @for (k of d.kpis; track k.key) {
              <button class="kpi" (click)="drill(k.drill)" [title]="k.definition">
                <div class="kpi-label">{{ k.label }}</div>
                <div class="kpi-value" [class.bad]="(k.key === 'overdue' || k.key === 'stuck') && k.value > 0">{{ k.value | number: '1.0-1' }}{{ k.unit }}</div>
                <div class="kpi-def">{{ k.definition }}</div>
              </button>
            }
          </div>

          <div class="grid cols-3 top-row">
            <div class="card explorer">
              <div class="card-head"><div><h3>Batch explorer</h3><p>Open any batch as a map of its stages, tests, QMS events, approvals and movements.</p></div></div>
              <div class="card-body">
                <form class="inline-pair" (ngSubmit)="searchBatch()">
                  <input name="q" [(ngModel)]="query" placeholder="Batch no or product…" list="brq-batch-hits" (input)="suggest()" />
                  <datalist id="brq-batch-hits">@for (h of hits(); track h.id) {<option [value]="h.batchNo">{{ h.product }}</option>}</datalist>
                  <button class="btn primary">Open map</button>
                </form>
                <h4 class="sub">Most open quality events</h4>
                <table class="data compact">
                  <tbody>
                    @for (b of d.qualityEventsByBatch; track b.drill) {
                      <tr class="clickable" (click)="openMap(+b.drill.split(':')[1])"><td><button class="link" type="button">{{ b.label }}</button></td>
                        <td class="small muted">{{ b.group }}</td><td class="num"><b>{{ b.value }}</b> open</td></tr>
                    } @empty {<tr><td class="empty">No open quality events.</td></tr>}
                  </tbody>
                </table>
              </div>
            </div>
            <div class="card span-2">
              <div class="card-head"><div><h3>Planned vs released</h3><p>Planned = target release date in the period · Released = final QA release in the period</p></div>
                <div class="seg"><button [class.on]="period() === 'week'" (click)="period.set('week')">Weekly</button><button [class.on]="period() === 'month'" (click)="period.set('month')">Monthly</button></div></div>
              <div class="card-body chart-pad">
                <brq-chart [labels]="periodLabels()" [series]="periodSeries()" [height]="330" categoryLabel="Period" ariaLabel="Planned versus released batches" (pick)="pickPeriod($event)" />
              </div>
            </div>
          </div>

          <div class="grid cols-3">
            <div class="card">
              <div class="card-head"><div><h3>Where batches are now</h3><p>Active batches by current stage</p></div></div>
              <div class="card-body chart-pad"><brq-chart [horizontal]="true" [labels]="stageLabels()" [series]="[{ label: 'Batches waiting', data: stageWaiting(), color: '--series-1' }]"
                [height]="chartHeight(stageLabels().length)" categoryLabel="Stage" ariaLabel="Active batches by stage" (pick)="drill(d.stages[$event.index].drill)" /></div>
            </div>
            <div class="card">
              <div class="card-head"><div><h3>Average days per stage</h3><p>Bottleneck view: start to completion of completed items</p></div></div>
              <div class="card-body chart-pad"><brq-chart [horizontal]="true" [labels]="stageLabels()" [series]="[{ label: 'Average days', data: stageDays(), color: '--series-1' }]" unit=" d"
                [height]="chartHeight(stageLabels().length)" categoryLabel="Stage" ariaLabel="Average days per stage" (pick)="drill('cleared:' + d.stages[$event.index].stage)" /></div>
            </div>
            <div class="card">
              <div class="card-head"><div><h3>Stage funnel</h3><p>Batches that have cleared each stage</p></div></div>
              <div class="card-body chart-pad"><brq-chart [horizontal]="true" [labels]="stageLabels()" [series]="[{ label: 'Cleared', data: stageCleared(), color: '--ordinal-3' }]"
                [height]="chartHeight(stageLabels().length)" categoryLabel="Stage" ariaLabel="Stage funnel" (pick)="drill('cleared:' + d.stages[$event.index].stage)" /></div>
            </div>
          </div>

          <div class="grid cols-3">
            <div class="card">
              <div class="card-head"><div><h3>Open quality events by type</h3><p>Events with status Open on batches in scope</p></div></div>
              <div class="card-body chart-pad"><brq-chart [labels]="labels(d.qualityEventsByType)" [series]="[{ label: 'Open events', data: values(d.qualityEventsByType), color: '--series-1' }]"
                [height]="220" categoryLabel="Type" ariaLabel="Open quality events by type" (pick)="drill(d.qualityEventsByType[$event.index].drill)" /></div>
            </div>
            <div class="card">
              <div class="card-head"><div><h3>Expiry risk</h3><p>Time left before the batch drops below its market's minimum shelf life at dispatch</p></div></div>
              <div class="card-body chart-pad"><brq-chart [labels]="labels(d.expiryRisk)" [series]="[{ label: 'Batches', data: values(d.expiryRisk), color: '--status-neutral', colors: expiryTones }]"
                [height]="220" categoryLabel="Shelf life" ariaLabel="Expiry risk" (pick)="drill(d.expiryRisk[$event.index].drill)" /></div>
            </div>
            <div class="card">
              <div class="card-head"><div><h3>Warehouse aging</h3><p>Released but not dispatched – days since QA release</p></div></div>
              <div class="card-body chart-pad"><brq-chart [labels]="labels(d.warehouseAging)" [series]="[{ label: 'Batches', data: values(d.warehouseAging), color: '--ordinal-3', colors: ordinal }]"
                [height]="220" categoryLabel="Age" ariaLabel="Warehouse aging" (pick)="drill(d.warehouseAging[$event.index].drill)" /></div>
            </div>
          </div>

          <div class="grid breakdown-row">
            <div class="card">
              <div class="card-head"><div><h3>Breakdown by {{ dimension().toLowerCase() }}</h3><p>Released, ready and still in progress</p></div>
                <div class="seg">@for (dim of dims; track dim) {<button [class.on]="dimension() === dim" (click)="dimension.set(dim)">{{ dim }}</button>}</div></div>
              <div class="card-body chart-pad"><brq-chart [horizontal]="true" [stacked]="true" [labels]="breakLabels()" [series]="breakSeries()" [height]="chartHeight(breakLabels().length, 34)"
                [categoryLabel]="dimension()" ariaLabel="Breakdown" (pick)="pickBreakdown($event)" /></div>
              <div class="table-scroll">
                <table class="data compact">
                  <thead><tr><th>{{ dimension() }}</th><th class="num">Total</th><th class="num">In pipeline</th><th class="num">Ready</th><th class="num">Released</th><th class="num">Overdue</th></tr></thead>
                  <tbody>
                    @for (r of breakRows(); track r.key) {
                      <tr><td>{{ r.label }}</td>
                        <td class="num"><button class="link" (click)="drill(r.drill)">{{ r.total }}</button></td>
                        <td class="num"><button class="link" (click)="drill(r.drill + '|active')">{{ r.active }}</button></td>
                        <td class="num"><button class="link" (click)="drill(r.drill + '|ready')">{{ r.ready }}</button></td>
                        <td class="num"><button class="link" (click)="drill(r.drill + '|released')">{{ r.released }}</button></td>
                        <td class="num"><button class="link" [class.bad]="r.overdue > 0" (click)="drill(r.drill + '|overdue')">{{ r.overdue }}</button></td></tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
            <div class="card">
              <div class="card-head"><div><h3>Batch status</h3><p>Current disposition of batches in scope</p></div></div>
              <div class="card-body chart-pad"><brq-chart [horizontal]="true" [labels]="labels(d.dispositions)" [series]="[{ label: 'Batches', data: values(d.dispositions), color: '--series-1' }]"
                [height]="chartHeight(d.dispositions.length)" categoryLabel="Status" ariaLabel="Batch status" (pick)="drill(d.dispositions[$event.index].drill)" /></div>
            </div>
          </div>
        </div>
      }

      <brq-modal [open]="!!mapBatch() || mapLoading()" [wide]="true" [title]="mapBatch() ? 'Batch map – ' + mapBatch()!.summary.batchNo : 'Loading batch…'" (close)="mapBatch.set(null)">
        @if (mapBatch(); as m) {
          <div class="map-head">
            <div>
              <div><b>{{ m.details.product.name }} {{ m.details.product.strength }}{{ m.details.product.strengthUnit }}</b> · {{ m.details.site.name }}</div>
              <div class="small secondary">{{ m.summary.explanation }}</div>
            </div>
            <div class="row"><brq-status [value]="m.summary.disposition" /><brq-readiness [value]="m.summary.readiness" />
              <a class="btn sm primary" [routerLink]="['/batches', m.summary.id]">Open Batch 360</a></div>
          </div>
          <brq-batch-map [batch]="m" (navigate)="mapNavigate($event)" />
        }
        @if (mapError()) {<p class="error">{{ mapError() }}</p>}
      </brq-modal>
    </div>`,
  styles: [`
    .grid { margin-bottom: 16px; }
    .top-row .span-2 { grid-column: span 2; }
    @media (max-width: 1100px) { .top-row .span-2 { grid-column: auto; } }
    .breakdown-row { grid-template-columns: minmax(0, 2fr) minmax(0, 1fr); }
    @media (max-width: 1100px) { .breakdown-row { grid-template-columns: minmax(0, 1fr); } }
    .chart-pad { padding-top: 34px; }
    .kpi-value.bad, .link.bad { color: var(--status-critical-ink); }
    .seg { display: inline-flex; border: 1px solid var(--border-strong); border-radius: 6px; overflow: hidden; }
    .seg button { background: var(--surface-1); border: 0; padding: 4px 10px; font: inherit; font-size: 12px; cursor: pointer; color: var(--text-secondary); }
    .seg button + button { border-left: 1px solid var(--border-strong); }
    .seg button.on { background: var(--accent-soft); color: var(--accent); font-weight: 600; }
    .sub { margin: 16px 0 6px; }
    .explorer .inline-pair input { min-width: 0; }
    .map-head { display: flex; justify-content: space-between; gap: 12px; flex-wrap: wrap; margin-bottom: 12px; padding-bottom: 12px; border-bottom: 1px solid var(--border); }`]
})
export class DashboardComponent implements OnInit {
  private api = inject(Api);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  d = signal<Dashboard | null>(null);
  loading = signal(false);
  error = signal('');
  filter: BatchFilter = filterFromParams(this.route.snapshot.queryParams);
  period = signal<'week' | 'month'>('month');
  dims = ['Site', 'Product', 'Market', 'Customer'];
  dimension = signal('Site');
  expiryTones = EXPIRY_TONE;
  ordinal = ORDINAL;
  query = '';
  hits = signal<any[]>([]);
  mapBatch = signal<Batch360 | null>(null);
  mapLoading = signal(false);
  mapError = signal('');

  stageLabels = computed(() => this.d()?.stages.map(s => s.stage) ?? []);
  stageWaiting = computed(() => this.d()?.stages.map(s => s.waitingNow) ?? []);
  stageDays = computed(() => this.d()?.stages.map(s => s.avgDays) ?? []);
  stageCleared = computed(() => this.d()?.stages.map(s => s.cleared) ?? []);
  points = computed(() => (this.period() === 'week' ? this.d()?.weekly : this.d()?.monthly) ?? []);
  periodLabels = computed(() => this.points().map(p => p.label));
  periodSeries = computed<Series[]>(() => [
    { label: 'Planned', data: this.points().map(p => p.planned), color: '--series-1' },
    { label: 'Released', data: this.points().map(p => p.released), color: '--series-2' },
  ]);
  breakRows = computed(() => (this.d()?.breakdown ?? []).filter(r => r.dimension === this.dimension()).slice(0, 12));
  breakLabels = computed(() => this.breakRows().map(r => r.label));
  breakSeries = computed<Series[]>(() => [
    { label: 'Released', data: this.breakRows().map(r => r.released), color: '--series-1' },
    { label: 'Ready for release', data: this.breakRows().map(r => r.ready), color: '--series-2' },
    { label: 'In progress (not ready)', data: this.breakRows().map(r => r.active - r.ready), color: '--series-3' },
  ]);

  ngOnInit() { this.load(); }
  labels(b: { label: string }[]) { return b.map(x => x.label); }
  values(b: { value: number }[]) { return b.map(x => x.value); }
  chartHeight(n: number, per = 30) { return Math.max(160, n * per + 40); }

  onFilter(f: BatchFilter) {
    this.filter = f;
    this.router.navigate([], { queryParams: clean(f), replaceUrl: true });
    this.load();
  }

  async load() {
    this.loading.set(true); this.error.set('');
    try { this.d.set(await this.api.get<Dashboard>('dashboard', clean(this.filter))); }
    catch (e) { this.error.set(errorMessage(e)); }
    finally { this.loading.set(false); }
  }

  /** Every chart element drills down to the batch list with the same filters plus the element's metric key. */
  drill(metric: string) {
    if (metric.startsWith('batch:')) { this.openMap(Number(metric.split(':')[1])); return; }
    this.router.navigate(['/batches'], { queryParams: { ...clean(this.filter), metric } });
  }
  pickPeriod(p: ChartPick) { const pt = this.points()[p.index]; this.drill(p.series === 0 ? pt.plannedDrill : pt.releasedDrill); }
  pickBreakdown(p: ChartPick) { const r = this.breakRows()[p.index]; this.drill(r.drill + ['|released', '|ready', '|active'][p.series]); }

  async suggest() {
    if (this.query.trim().length < 2) return;
    const r = await this.api.get<any>('batches', { search: this.query.trim(), pageSize: 10 });
    this.hits.set(r.items);
  }
  async searchBatch() {
    const q = this.query.trim();
    if (!q) return;
    const r = await this.api.get<any>('batches', { search: q, pageSize: 10 });
    const exact = r.items.find((b: any) => b.batchNo.toUpperCase() === q.toUpperCase()) ?? r.items[0];
    if (exact) this.openMap(exact.id); else this.mapError.set('No batch found.');
  }

  async openMap(id: number) {
    this.mapLoading.set(true); this.mapError.set(''); this.mapBatch.set(null);
    try { this.mapBatch.set(await this.api.get<Batch360>(`batches/${id}`)); }
    catch (e) { this.mapError.set(errorMessage(e)); }
    finally { this.mapLoading.set(false); }
  }
  mapNavigate(t: MapTarget) {
    if (t.tab === 'map' && t.batchId !== this.mapBatch()?.summary.id) { this.openMap(t.batchId); return; }
    this.router.navigate(['/batches', t.batchId], { queryParams: { tab: t.tab, focus: t.focus ?? null } });
  }
}
