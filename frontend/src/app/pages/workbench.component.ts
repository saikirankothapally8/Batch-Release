import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Api, errorMessage } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { BatchFilter, BatchRow, P, WorkItem } from '../core/models';
import { FilterBarComponent, clean, filterFromParams } from '../shared/filter-bar.component';
import { DmyPipe, LabelPipe } from '../shared/pipes';
import { FlagsComponent, ModalComponent, ReadinessComponent, StatusComponent, Toasts } from '../shared/ui';

type Tab = 'queue' | 'mine' | 'others' | 'stuck';

@Component({
  selector: 'brq-workbench',
  imports: [FormsModule, NgTemplateOutlet, RouterLink, FilterBarComponent, DmyPipe, LabelPipe, FlagsComponent, ReadinessComponent, StatusComponent, ModalComponent],
  template: `
    <div class="page">
      <div class="page-head">
        <div>
          <h1>QA workbench</h1>
          <p>Batches ranked by how close they are to release, then by the earliest deadline. Each row says why it is where it is.</p>
        </div>
        <div class="row summary">
          <button class="pill" (click)="tab.set('queue')"><b>{{ readyCount() }}</b> ready to release</button>
          <button class="pill crit" (click)="tab.set('queue')"><b>{{ count('Overdue') }}</b> overdue</button>
          <button class="pill warn" (click)="tab.set('queue')"><b>{{ count('AtRisk') }}</b> at risk</button>
          <button class="pill" (click)="tab.set('stuck')"><b>{{ stuck().length }}</b> stuck</button>
        </div>
      </div>

      <brq-filter-bar [value]="filter" [showStatus]="false" (changed)="onFilter($event)" />

      <div class="tabs" role="tablist">
        <button role="tab" [class.active]="tab() === 'queue'" (click)="tab.set('queue')">Next to release<span class="count">{{ queue().length }}</span></button>
        <button role="tab" [class.active]="tab() === 'mine'" (click)="tab.set('mine')">My department's items<span class="count">{{ mine().length }}</span></button>
        <button role="tab" [class.active]="tab() === 'others'" (click)="tab.set('others')">Waiting on other departments<span class="count">{{ others().length }}</span></button>
        <button role="tab" [class.active]="tab() === 'stuck'" (click)="tab.set('stuck')">Stuck batches<span class="count">{{ stuck().length }}</span></button>
      </div>

      @if (error()) {<p class="callout danger">{{ error() }}</p>}

      @switch (tab()) {
        @case ('queue') {
          <div class="card" [style.opacity]="loading() ? 0.6 : 1">
            <div class="table-scroll">
              <table class="data">
                <thead><tr>
                  <th>#</th><th>Batch</th><th>Readiness</th><th>Priority</th><th>Deadline</th><th>Est. release</th><th>Why it is here</th><th></th>
                </tr></thead>
                <tbody>
                  @for (b of queue(); track b.id) {
                    <tr class="clickable" (click)="open(b)">
                      <td><span class="rank" [class.top]="b.isReady">{{ b.rank }}</span></td>
                      <td class="nowrap">
                        <a [routerLink]="['/batches', b.id]" (click)="$event.stopPropagation()"><b>{{ b.batchNo }}</b></a>
                        <div class="small secondary">{{ b.product }} {{ b.strength }}</div>
                        <div class="small muted">{{ b.site }} · @for (m of b.markets; track m) {<span class="tag">{{ m }}</span>}</div>
                      </td>
                      <td><brq-readiness [value]="b.readiness" /><div class="small muted">{{ b.currentStage }}</div></td>
                      <td><brq-status [value]="b.priority" /></td>
                      <td class="nowrap">
                        <div>{{ b.deadline | dmy }}</div>
                        <div class="small" [class.overdue]="b.daysToDeadline < 0">{{ daysText(b.daysToDeadline) }}</div>
                        <div class="small muted">{{ b.deadlineType | label }}</div>
                      </td>
                      <td class="nowrap">
                        @if (b.estimatedReleaseDate) {<div>{{ b.estimatedReleaseDate | dmy }}</div><div class="small muted">~{{ b.estimatedDaysToRelease }} days</div>}
                      </td>
                      <td><div class="why">{{ b.explanation }}</div><div class="flags"><brq-flags [flags]="b.flags" /></div></td>
                      <td class="nowrap" (click)="$event.stopPropagation()">
                        @if (b.isReady && canRelease) {<a class="btn primary sm" [routerLink]="['/batches', b.id]" [queryParams]="{ action: 'release' }">Release</a>}
                        @else {<a class="btn sm" [routerLink]="['/batches', b.id]">Open</a>}
                      </td>
                    </tr>
                  } @empty {<tr><td colspan="8" class="empty">No batches in the release pipeline for this filter.</td></tr>}
                </tbody>
              </table>
            </div>
          </div>
        }
        @case ('mine') { <ng-container *ngTemplateOutlet="items; context: { rows: mine(), mineTab: true }" /> }
        @case ('others') { <ng-container *ngTemplateOutlet="items; context: { rows: others(), mineTab: false }" /> }
        @case ('stuck') {
          <div class="card">
            <div class="card-head"><div><h3>No checklist item completed for more than {{ stuckDays() }} days</h3><p>Threshold is configurable in Prioritisation settings.</p></div></div>
            <div class="table-scroll">
              <table class="data">
                <thead><tr><th>Batch</th><th class="num">Days without progress</th><th>Current stage</th><th>Waiting for</th><th>Target release</th><th>Flags</th></tr></thead>
                <tbody>
                  @for (s of stuck(); track s.row.id) {
                    <tr class="clickable" (click)="open(s.row)">
                      <td><a [routerLink]="['/batches', s.row.id]"><b>{{ s.row.batchNo }}</b></a><div class="small secondary">{{ s.row.product }}</div></td>
                      <td class="num"><b>{{ s.daysSinceProgress }}</b></td>
                      <td>{{ s.row.currentStage }}</td>
                      <td class="small">@for (w of s.waitingFor; track w) {<div>{{ w }}</div>}</td>
                      <td>{{ s.row.targetReleaseDate | dmy }}</td>
                      <td><brq-flags [flags]="s.row.flags" /></td>
                    </tr>
                  } @empty {<tr><td colspan="6" class="empty">Nothing stuck.</td></tr>}
                </tbody>
              </table>
            </div>
          </div>
        }
      }

      <ng-template #items let-rows="rows" let-mineTab="mineTab">
        <div class="card">
          <div class="table-scroll">
            <table class="data">
              <thead><tr><th>Item</th><th>Batch</th>@if (!mineTab) {<th>Department</th>}<th>Status</th><th>Due</th><th>Batch priority</th><th></th></tr></thead>
              <tbody>
                @for (w of rows; track w.itemId) {
                  <tr>
                    <td><b>{{ w.itemName }}</b>@if (w.market) { <span class="tag">{{ w.market }}</span>}
                      <div class="small muted">{{ w.stage }}@if (w.dependenciesOpen) { · waiting on earlier step}</div>
                      @if (w.blockedReason) {<div class="small error">{{ w.blockedReason }}</div>}</td>
                    <td class="nowrap"><a [routerLink]="['/batches', w.batchId]">{{ w.batchNo }}</a><div class="small secondary">{{ w.product }}</div></td>
                    @if (!mineTab) {<td>{{ w.department }}</td>}
                    <td><brq-status [value]="w.status" /></td>
                    <td class="nowrap">{{ w.dueDate | dmy }}@if (w.daysOverdue) {<div class="small overdue">{{ w.daysOverdue }} days overdue</div>}</td>
                    <td><brq-status [value]="w.priority" /> <span class="small muted">{{ w.readiness }}%</span></td>
                    <td class="nowrap">
                      @if (mineTab && canUpdate && !w.dependenciesOpen) {
                        @if (w.status === 'NotStarted') {<button class="btn sm" (click)="quick(w, 'InProgress')">Start</button>}
                        @if (w.status !== 'Blocked') {<button class="btn sm primary" (click)="quick(w, 'Completed')">Complete</button>}
                        <button class="btn sm" (click)="blockTarget.set(w)">{{ w.status === 'Blocked' ? 'Update block' : 'Block' }}</button>
                      } @else {<a class="btn sm" [routerLink]="['/batches', w.batchId]">Open</a>}
                    </td>
                  </tr>
                } @empty {<tr><td [attr.colspan]="mineTab ? 6 : 7" class="empty">No open items.</td></tr>}
              </tbody>
            </table>
          </div>
        </div>
      </ng-template>

      <brq-modal [open]="!!blockTarget()" title="Mark item blocked" (close)="blockTarget.set(null)">
        <form class="form" (ngSubmit)="block()">
          <p><b>{{ blockTarget()?.itemName }}</b> – {{ blockTarget()?.batchNo }}</p>
          <label class="field"><span>Reason<em class="req">*</em></span><textarea name="r" [(ngModel)]="blockReason" rows="3" required></textarea></label>
          <div class="actions"><button type="button" class="btn" (click)="blockTarget.set(null)">Cancel</button>
            <button class="btn primary" [disabled]="!blockReason.trim()">Mark blocked</button></div>
        </form>
      </brq-modal>
    </div>`,
  styles: [`
    .summary .pill { border: 1px solid var(--border); background: var(--surface-1); border-radius: 16px; padding: 4px 12px; cursor: pointer; font: inherit; color: var(--text-secondary); }
    .summary .pill b { color: var(--text-primary); font-size: 15px; margin-right: 2px; }
    .summary .pill.crit b { color: var(--status-critical-ink); } .summary .pill.warn b { color: var(--status-serious-ink); }
    .overdue { color: var(--status-critical-ink); font-weight: 600; }
    .flags { margin-top: 4px; }`]
})
export class WorkbenchComponent implements OnInit {
  private api = inject(Api);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toasts = inject(Toasts);
  auth = inject(AuthService);
  tab = signal<Tab>('queue');
  queue = signal<BatchRow[]>([]);
  mine = signal<WorkItem[]>([]);
  others = signal<WorkItem[]>([]);
  stuck = signal<any[]>([]);
  stuckDays = signal(7);
  loading = signal(false);
  error = signal('');
  blockTarget = signal<WorkItem | null>(null);
  blockReason = '';
  filter: BatchFilter = filterFromParams(this.route.snapshot.queryParams);
  canRelease = this.auth.has(P.release);
  canUpdate = this.auth.has(P.itemOwn, P.itemAny);
  readyCount = computed(() => this.queue().filter(b => b.isReady).length);

  ngOnInit() { this.load(); }
  count(flag: string) { return this.queue().filter(b => b.flags.includes(flag)).length; }

  onFilter(f: BatchFilter) {
    this.filter = f;
    this.router.navigate([], { queryParams: clean(f), replaceUrl: true });
    this.load();
  }

  async load() {
    this.loading.set(true); this.error.set('');
    try {
      const [q, w] = await Promise.all([this.api.get<any>('queue', clean(this.filter)), this.api.get<any>('workbench')]);
      this.queue.set(q.items);
      this.mine.set(w.mine); this.others.set(w.waitingOnOthers); this.stuck.set(w.stuck); this.stuckDays.set(w.stuckDays);
    } catch (e) { this.error.set(errorMessage(e)); }
    finally { this.loading.set(false); }
  }

  open(b: BatchRow) { this.router.navigate(['/batches', b.id]); }

  daysText(d: number) {
    if (d < 0) return `${-d} day${d === -1 ? '' : 's'} late`;
    if (d === 0) return 'today';
    return `in ${d} day${d === 1 ? '' : 's'}`;
  }

  async quick(w: WorkItem, status: string) {
    try {
      await this.api.put(`checklist-items/${w.itemId}/status`, { status });
      this.toasts.show(`${w.itemName} – ${status === 'Completed' ? 'completed' : 'started'} (${w.batchNo})`);
      this.load();
    } catch (e) { this.toasts.show(errorMessage(e), 'error'); }
  }

  async block() {
    const w = this.blockTarget()!;
    try {
      await this.api.put(`checklist-items/${w.itemId}/status`, { status: 'Blocked', blockedReason: this.blockReason, reason: this.blockReason });
      this.toasts.show(`${w.itemName} marked blocked`);
      this.blockTarget.set(null); this.blockReason = '';
      this.load();
    } catch (e) { this.toasts.show(errorMessage(e), 'error'); }
  }
}
