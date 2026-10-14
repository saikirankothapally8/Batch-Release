import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Api, errorMessage } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { BatchFilter, BatchRow, P } from '../core/models';
import { FilterBarComponent, clean, filterFromParams } from '../shared/filter-bar.component';
import { DmyPipe, humanize } from '../shared/pipes';
import { FlagsComponent, ReadinessComponent, StatusComponent } from '../shared/ui';
import { isoToDmy } from '../core/dates';

/** Human description of a drill-down key, so the user always knows which slice they are looking at. */
export function describeMetric(m: string | null | undefined, lookups?: any): string {
  if (!m) return '';
  return m.split('|').map(part => {
    const [k, ...rest] = part.split(':');
    const a = rest.join(':');
    const named = (list: any[] | undefined, id: string) => list?.find((x: any) => String(x.id) === id)?.name ?? id;
    switch (k) {
      case 'total': return 'All batches';
      case 'active': return 'In pipeline';
      case 'ready': return 'Ready for release';
      case 'released': return 'Released';
      case 'partial': return 'Partially released';
      case 'overdue': return 'Overdue (not released, past target)';
      case 'ontime': return 'Released on time';
      case 'late': return 'Released after target';
      case 'stuck': return 'Stuck';
      case 'atrisk': return 'At risk';
      case 'blocked': return 'Has a blocked item';
      case 'onhold': return 'On hold / quarantine / investigation';
      case 'atvendor': return 'Material at an external party';
      case 'qe': return a ? `Open ${humanize(a)}` : 'Open quality events';
      case 'expiry': return `Shelf life: ${a}`;
      case 'warehouse': return `Released, in warehouse ${a}`;
      case 'stage': return `Currently at ${a}`;
      case 'cleared': return `Cleared ${a}`;
      case 'planned-month': return `Target release in ${a}`;
      case 'released-month': return `Released in ${a}`;
      case 'planned-week': return `Target release in week of ${isoToDmy(a)}`;
      case 'released-week': return `Released in week of ${isoToDmy(a)}`;
      case 'disposition': return humanize(a);
      case 'site': return `Site: ${named(lookups?.sites, a)}`;
      case 'product': return `Product: ${named(lookups?.products, a)}`;
      case 'customer': return `Customer: ${a === 'none' ? '(none)' : named(lookups?.customers, a)}`;
      case 'market': return `Market: ${named(lookups?.markets, a)}`;
      case 'batch': return 'Single batch';
      default: return part;
    }
  }).join(' · ');
}

@Component({
  selector: 'brq-batches',
  imports: [RouterLink, FilterBarComponent, DmyPipe, FlagsComponent, ReadinessComponent, StatusComponent],
  template: `
    <div class="page">
      <div class="page-head">
        <div><h1>Batches</h1><p>Every batch, with its readiness, deadline and flags. Charts on the dashboard drill down to this list.</p></div>
        <div class="row">
          <button class="btn" (click)="exportCsv()">Export CSV</button>
          @if (auth.has(P.batchCreate)) {<a class="btn primary" routerLink="/batches/new">+ New batch</a>}
        </div>
      </div>
      <brq-filter-bar [value]="filter" [showSearch]="true" (changed)="onFilter($event)" />
      @if (filter.metric) {
        <div class="drill row">
          <span class="muted">Showing</span><b>{{ metricText() }}</b>
          <button class="link small" (click)="clearMetric()">Show all</button>
        </div>
      }
      @if (error()) {<p class="callout danger">{{ error() }}</p>}
      <div class="card" [style.opacity]="loading() ? 0.6 : 1">
        <div class="card-head"><h3>{{ total() }} batch{{ total() === 1 ? '' : 'es' }}</h3>
          <label class="row small">Sort
            <select (change)="sort = $any($event.target).value; page = 1; load()">
              <option value="">Active first, by deadline</option><option value="readiness">Readiness</option>
              <option value="target">Target release date</option><option value="released">Most recently released</option><option value="batchNo">Batch number</option>
            </select></label>
        </div>
        <div class="table-scroll">
          <table class="data">
            <thead><tr><th>Batch</th><th>Product</th><th>Site / markets</th><th>Status</th><th>Readiness</th><th>Target release</th><th>Expiry</th><th>Released</th><th>Flags</th></tr></thead>
            <tbody>
              @for (b of rows(); track b.id) {
                <tr class="clickable" (click)="router.navigate(['/batches', b.id])">
                  <td class="nowrap"><a [routerLink]="['/batches', b.id]"><b>{{ b.batchNo }}</b></a>
                    @if (b.batchType !== 'Commercial') {<div class="small muted">{{ b.batchType }}@if (b.parentBatchNo) { of {{ b.parentBatchNo }}}</div>}</td>
                  <td>{{ b.product }}<div class="small muted">{{ b.productCode }} · {{ b.strength }}</div></td>
                  <td>{{ b.site }}<div>@for (m of b.markets; track m) {<span class="tag">{{ m }}</span>}</div></td>
                  <td><brq-status [value]="b.disposition" /><div class="small muted">{{ b.currentStage }}</div></td>
                  <td><brq-readiness [value]="b.readiness" /></td>
                  <td class="nowrap">{{ b.targetReleaseDate | dmy }}</td>
                  <td class="nowrap">{{ b.expiryDate | dmy }}</td>
                  <td class="nowrap">{{ b.releasedAt | dmy }}</td>
                  <td><brq-flags [flags]="b.flags" /></td>
                </tr>
              } @empty {<tr><td colspan="9" class="empty">No batches match.</td></tr>}
            </tbody>
          </table>
        </div>
        @if (total() > pageSize) {
          <div class="row pager">
            <button class="btn sm" [disabled]="page === 1" (click)="page = page - 1; load()">Previous</button>
            <span class="small muted">Page {{ page }} of {{ pages() }}</span>
            <button class="btn sm" [disabled]="page >= pages()" (click)="page = page + 1; load()">Next</button>
          </div>
        }
      </div>
    </div>`,
  styles: [`.drill { margin: -6px 0 12px; padding: 8px 12px; background: var(--accent-soft); border-radius: 6px; } .pager { padding: 10px 16px; justify-content: flex-end; }`]
})
export class BatchesComponent implements OnInit {
  P = P;
  api = inject(Api);
  auth = inject(AuthService);
  router = inject(Router);
  private route = inject(ActivatedRoute);
  rows = signal<BatchRow[]>([]);
  total = signal(0);
  loading = signal(false);
  error = signal('');
  metricText = signal('');
  filter: BatchFilter = filterFromParams(this.route.snapshot.queryParams);
  sort = '';
  page = 1;
  pageSize = 50;
  pages = () => Math.max(1, Math.ceil(this.total() / this.pageSize));

  async ngOnInit() {
    this.load();
    const lk = await this.api.loadLookups();
    this.metricText.set(describeMetric(this.filter.metric, lk));
  }

  onFilter(f: BatchFilter) {
    this.filter = { ...f, metric: this.filter.metric };
    this.page = 1;
    this.router.navigate([], { queryParams: clean(this.filter), replaceUrl: true });
    this.load();
  }

  clearMetric() { this.filter = { ...this.filter, metric: null }; this.onFilter(this.filter); }

  async load() {
    this.loading.set(true); this.error.set('');
    try {
      const r = await this.api.get<any>('batches', { ...clean(this.filter), sort: this.sort, page: this.page, pageSize: this.pageSize });
      this.rows.set(r.items); this.total.set(r.total);
    } catch (e) { this.error.set(errorMessage(e)); }
    finally { this.loading.set(false); }
  }

  async exportCsv() {
    const r = await this.api.get<any>('batches', { ...clean(this.filter), sort: this.sort, page: 1, pageSize: 500 });
    const cols: [string, (b: BatchRow) => unknown][] = [
      ['Batch No', b => b.batchNo], ['Product', b => b.product], ['Product Code', b => b.productCode], ['Site', b => b.site],
      ['Markets', b => b.markets.join(' ')], ['Customer', b => b.customer], ['Status', b => humanize(b.disposition)], ['Stage', b => b.currentStage],
      ['Readiness %', b => b.readiness], ['Manufacturing Date', b => isoToDmy(b.manufacturingDate)], ['Target Release Date', b => isoToDmy(b.targetReleaseDate)],
      ['Expiry Date', b => isoToDmy(b.expiryDate)], ['Released On', b => isoToDmy(b.releasedAt)], ['Flags', b => b.flags.join(' ')], ['Explanation', b => b.explanation],
    ];
    const esc = (v: unknown) => `"${String(v ?? '').replace(/"/g, '""')}"`;
    const csv = [cols.map(c => esc(c[0])).join(','), ...r.items.map((b: BatchRow) => cols.map(c => esc(c[1](b))).join(','))].join('\r\n');
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob(['﻿' + csv], { type: 'text/csv' }));
    a.download = 'batches.csv';
    a.click();
  }
}
