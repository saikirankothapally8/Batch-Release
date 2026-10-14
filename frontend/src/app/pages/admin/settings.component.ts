import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api, errorMessage } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { P } from '../../core/models';
import { Toasts } from '../../shared/ui';

@Component({
  selector: 'brq-settings',
  imports: [FormsModule],
  template: `
    <div class="page">
      <div class="page-head"><div><h1>Prioritisation settings</h1><p>How the release queue is ranked. Saving recalculates every batch; the change is audited.</p></div></div>
      @if (s(); as s) {
        <form class="grid cols-2" (ngSubmit)="save()">
          <div class="card"><div class="card-head"><h3>Readiness</h3></div><div class="card-body form">
            <p class="small secondary">Readiness = weighted share of mandatory items done (final release excluded) − penalties. Item weights default from the stage position: first stage = min weight, last stage = max weight. A template item can override its weight.</p>
            <div class="form-grid">
              <label class="field span-2"><span>Min stage weight</span><input type="number" step="0.1" name="a" [(ngModel)]="s.minStageWeight" [disabled]="!canEdit" /></label>
              <label class="field span-2"><span>Max stage weight</span><input type="number" step="0.1" name="b" [(ngModel)]="s.maxStageWeight" [disabled]="!canEdit" /></label>
              <label class="field span-2"><span>Penalty per blocked item (points)</span><input type="number" name="c" [(ngModel)]="s.blockedItemPenalty" [disabled]="!canEdit" /></label>
              <label class="field span-2"><span>Penalty per open quality event</span><input type="number" name="d" [(ngModel)]="s.openEventPenalty" [disabled]="!canEdit" /></label>
              <label class="field span-2"><span>Penalty per critical open event</span><input type="number" name="e" [(ngModel)]="s.criticalEventPenalty" [disabled]="!canEdit" /></label>
              <label class="field span-2"><span>Maximum total penalty</span><input type="number" name="f" [(ngModel)]="s.maxPenalty" [disabled]="!canEdit" /></label>
            </div></div></div>
          <div class="card"><div class="card-head"><h3>Ranking and flags</h3></div><div class="card-body form">
            <p class="small secondary">Batches whose readiness falls in the same band are ordered by the earliest deadline: target release date, the {{ '' }}minimum-shelf-life dispatch date, or expiry.</p>
            <div class="form-grid">
              <label class="field span-2"><span>Readiness band size (%)</span><input type="number" name="g" [(ngModel)]="s.readinessBandSize" [disabled]="!canEdit" /></label>
              <label class="field span-2"><span>Stuck after (days without progress)</span><input type="number" name="h" [(ngModel)]="s.stuckDays" [disabled]="!canEdit" /></label>
              <label class="field span-2"><span>Shelf-life warning (days before dispatch limit)</span><input type="number" name="i" [(ngModel)]="s.expiryWarningDays" [disabled]="!canEdit" /></label>
              <label class="field span-2"><span>At-risk buffer (days before target)</span><input type="number" name="j" [(ngModel)]="s.atRiskBufferDays" [disabled]="!canEdit" /></label>
              <label class="field span-2"><span>Default item duration (days, no history)</span><input type="number" step="0.5" name="k" [(ngModel)]="s.defaultItemDurationDays" [disabled]="!canEdit" /></label>
              <label class="checkbox span-4"><input type="checkbox" name="l" [(ngModel)]="s.enforceSegregationOfDuties" [disabled]="!canEdit" /> Enforce segregation of duties (creator cannot release; requester cannot approve own change)</label>
            </div>
            @if (canEdit) {<div class="actions"><button class="btn primary" [disabled]="busy()">{{ busy() ? 'Recalculating…' : 'Save and recalculate' }}</button></div>}
          </div></div>
        </form>
      }
      @if (error()) {<p class="callout danger">{{ error() }}</p>}
    </div>`,
})
export class SettingsComponent implements OnInit {
  private api = inject(Api);
  private toasts = inject(Toasts);
  canEdit = inject(AuthService).has(P.settings);
  s = signal<any>(null);
  busy = signal(false);
  error = signal('');
  async ngOnInit() { try { this.s.set(await this.api.get('admin/settings')); } catch (e) { this.error.set(errorMessage(e)); } }
  async save() {
    this.busy.set(true);
    try { this.s.set(await this.api.put('admin/settings', this.s())); this.toasts.show('Settings saved; all batches recalculated'); }
    catch (e) { this.toasts.show(errorMessage(e), 'error'); }
    finally { this.busy.set(false); }
  }
}
