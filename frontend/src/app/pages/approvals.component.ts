import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api, errorMessage } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { P } from '../core/models';
import { DmyPipe, DmyTimePipe, LabelPipe } from '../shared/pipes';
import { SignatureComponent, StatusComponent, Toasts } from '../shared/ui';

@Component({
  selector: 'brq-approvals',
  imports: [RouterLink, DmyPipe, DmyTimePipe, LabelPipe, SignatureComponent, StatusComponent],
  template: `
    <div class="page">
      <div class="page-head"><div><h1>Approvals</h1><p>Checklist changes waiting for a decision. You cannot approve a change you requested.</p></div>
        <div class="tabs"><button [class.active]="status() === 'Pending'" (click)="setStatus('Pending')">Pending</button>
          <button [class.active]="status() === 'Approved'" (click)="setStatus('Approved')">Approved</button>
          <button [class.active]="status() === 'Rejected'" (click)="setStatus('Rejected')">Rejected</button></div></div>
      @if (error()) {<p class="callout danger">{{ error() }}</p>}
      <div class="card">
        <table class="data">
          <thead><tr><th>Batch</th><th>Change</th><th>Details</th><th>Reason</th><th>Requested</th><th>Status</th><th></th></tr></thead>
          <tbody>
            @for (c of rows(); track c.id) {
              <tr>
                <td><a [routerLink]="['/batches', c.batchId]" [queryParams]="{ tab: 'changes' }"><b>{{ c.batchNo }}</b></a></td>
                <td>{{ c.changeType | label }}</td>
                <td><b>{{ c.proposedName || c.itemName }}</b>
                  @if (c.changeType !== 'RemoveItem') {<div class="small muted">@if (c.proposedItemType) {{{ c.proposedItemType | label }}}@if (c.proposedIsMandatory === false) { · optional}@if (c.proposedDueDate) { · due {{ c.proposedDueDate | dmy }}}</div>}</td>
                <td class="small">{{ c.reason }}</td>
                <td class="nowrap">{{ c.requestedAt | dmyt }}<div class="small muted">{{ c.requestedBy }}</div></td>
                <td><brq-status [value]="c.status" />@if (c.decidedBy) {<div class="small muted">{{ c.decidedBy }}</div>}</td>
                <td class="nowrap">@if (c.status === 'Pending' && canApprove) {
                  @if (c.requestedById === auth.user()?.id) {<span class="small muted">Your request</span>}
                  @else {<button class="btn sm primary" (click)="open(c, true)">Approve</button> <button class="btn sm" (click)="open(c, false)">Reject</button>}}</td>
              </tr>
            } @empty {<tr><td colspan="7" class="empty">Nothing here.</td></tr>}
          </tbody>
        </table>
      </div>
      <brq-sign [open]="!!target()" [title]="approve ? 'Approve change' : 'Reject change'" [meaning]="approve ? 'Checklist change approved' : 'Checklist change rejected'"
                [signer]="auth.user()?.fullName ?? ''" commentLabel="Comment" [commentRequired]="!approve" [busy]="busy()" [error]="signError()"
                (signed)="decide($event)" (cancel)="target.set(null)">
        @if (target(); as c) {<p>{{ c.batchNo }} – {{ c.changeType | label }}: <b>{{ c.proposedName || c.itemName }}</b></p>}
      </brq-sign>
    </div>`,
})
export class ApprovalsComponent implements OnInit {
  private api = inject(Api);
  private toasts = inject(Toasts);
  auth = inject(AuthService);
  canApprove = this.auth.has(P.changeApprove);
  rows = signal<any[]>([]);
  status = signal('Pending');
  error = signal('');
  target = signal<any>(null);
  approve = true;
  busy = signal(false);
  signError = signal('');

  ngOnInit() { this.load(); }
  setStatus(s: string) { this.status.set(s); this.load(); }
  async load() {
    try { this.rows.set(await this.api.get<any[]>('change-requests', { status: this.status() })); }
    catch (e) { this.error.set(errorMessage(e)); }
  }
  open(c: any, approve: boolean) { this.approve = approve; this.signError.set(''); this.target.set(c); }
  async decide(s: { password: string; comment: string }) {
    this.busy.set(true); this.signError.set('');
    try {
      await this.api.post(`change-requests/${this.target().id}/${this.approve ? 'approve' : 'reject'}`, { password: s.password, comment: s.comment || null });
      this.toasts.show(this.approve ? 'Change approved and applied to the batch' : 'Change rejected');
      this.target.set(null); this.load();
    } catch (e) { this.signError.set(errorMessage(e)); }
    finally { this.busy.set(false); }
  }
}
