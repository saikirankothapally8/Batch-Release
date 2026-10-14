import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Api, errorMessage } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { todayIso } from '../core/dates';
import { Batch360, Item, Lookups, P } from '../core/models';
import { DmyPipe, DmyTimePipe, LabelPipe, humanize } from '../shared/pipes';
import { BatchMapComponent, MapTarget } from '../shared/batch-map.component';
import { DateInputComponent, FlagsComponent, ModalComponent, ReadinessComponent, SignatureComponent, StatusComponent, Toasts } from '../shared/ui';

type Tab = 'map' | 'checklist' | 'markets' | 'quality' | 'movements' | 'releases' | 'genealogy' | 'changes' | 'audit';

@Component({
  selector: 'brq-batch360',
  imports: [FormsModule, RouterLink, BatchMapComponent, DecimalPipe, DmyPipe, DmyTimePipe, LabelPipe, DateInputComponent, FlagsComponent, ModalComponent,
    ReadinessComponent, SignatureComponent, StatusComponent],
  templateUrl: './batch360.component.html',
  styleUrl: './batch360.component.scss'
})
export class Batch360Component implements OnInit {
  P = P;
  private api = inject(Api);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private toasts = inject(Toasts);
  auth = inject(AuthService);
  humanize = humanize;

  id = Number(this.route.snapshot.paramMap.get('id'));
  focus = signal<string | null>(null);
  b = signal<Batch360 | null>(null);
  lk = signal<Lookups | null>(null);
  audit = signal<any[]>([]);
  tab = signal<Tab>('checklist');
  error = signal('');
  busy = signal(false);
  modalError = signal('');

  stages = computed(() => {
    const items = this.b()?.items ?? [];
    const groups: { stage: string; sequence: number; items: Item[] }[] = [];
    for (const i of items) {
      let g = groups.find(x => x.stage === i.stage && x.sequence === i.sequence);
      if (!g) { g = { stage: i.stage, sequence: i.sequence, items: [] }; groups.push(g); }
      g.items.push(i);
    }
    return groups.sort((a, b) => a.sequence - b.sequence);
  });
  pendingMandatory = computed(() => (this.b()?.items ?? []).filter(i => i.isMandatory && !i.isFinalRelease && !['Completed', 'NotApplicable'].includes(i.status)));
  openEvents = computed(() => (this.b()?.qualityEvents ?? []).filter(e => e.status === 'Open'));
  pendingChanges = computed(() => (this.b()?.changeRequests ?? []).filter(c => c.status === 'Pending'));
  active = computed(() => { const d = this.b()?.summary.disposition; return !this.b()?.summary.isReleased && d !== 'Rejected' && d !== 'Recalled'; });
  remainingQty = computed(() => { const b = this.b(); return b ? b.details.batchSize - b.releasedQuantity : 0; });
  undispatched = computed(() => { const b = this.b(); return b ? b.releasedQuantity - b.dispatchedQuantity : 0; });

  // ---- item status modal
  itemTarget = signal<Item | null>(null);
  itemAction = '';
  itemForm = { comments: '', justification: '', blockedReason: '', evidenceReference: '', reason: '' };
  // ---- release
  releaseOpen = signal(false);
  release = { kind: 'Full', quantity: 0, marketId: null as number | null, justification: '' };
  // ---- disposition
  dispoOpen = signal(false);
  dispo = { disposition: 'OnHold', reason: '', password: '' };
  // ---- dispatch
  dispatchOpen = signal(false);
  dispatch = { quantity: 0, marketId: null as number | null, dispatchedOn: todayIso(), reference: '' };
  // ---- change request
  changeOpen = signal(false);
  change: any = {};
  // ---- change decision
  decision = signal<{ cr: any; approve: boolean } | null>(null);
  // ---- quality event
  qeOpen = signal(false);
  qe: any = {};
  // ---- movement
  mvOpen = signal(false);
  mv: any = {};
  // ---- market
  mkOpen = signal(false);
  mk: any = {};

  async ngOnInit() {
    this.lk.set(await this.api.loadLookups());
    // The component is reused when navigating between batches (e.g. parent/child in the batch map).
    this.route.paramMap.subscribe(async pm => {
      const id = Number(pm.get('id'));
      if (id !== this.id || !this.b()) { this.id = id; this.b.set(null); await this.load(); }
      this.applyQuery();
      if (this.route.snapshot.queryParamMap.get('action') === 'release') this.openRelease();
    });
    this.route.queryParamMap.subscribe(() => { if (this.b()) this.applyQuery(); });
  }

  private applyQuery() {
    const q = this.route.snapshot.queryParamMap;
    const tab = q.get('tab') as Tab | null;
    if (tab) this.setTab(tab);
    const focus = q.get('focus');
    this.focus.set(focus);
    if (focus) setTimeout(() => document.getElementById(focus)?.scrollIntoView({ behavior: 'smooth', block: 'center' }), 80);
  }

  onMap(t: MapTarget) {
    this.router.navigate(['/batches', t.batchId], { queryParams: { tab: t.tab, focus: t.focus ?? null } });
  }

  async load() {
    try { this.b.set(await this.api.get<Batch360>(`batches/${this.id}`)); this.error.set(''); }
    catch (e) { this.error.set(errorMessage(e)); }
    if (this.tab() === 'audit') this.loadAudit();
  }

  async loadAudit() {
    if (!this.auth.has(P.audit)) return;
    this.audit.set(await this.api.get<any[]>(`batches/${this.id}/audit`));
  }

  setTab(t: Tab) {
    this.tab.set(t);
    if (t === 'audit') this.loadAudit();
  }
  pickTab(t: Tab) { this.router.navigate([], { queryParams: { tab: t }, replaceUrl: true }); }

  private async run(fn: () => Promise<unknown>, ok: string, close: () => void) {
    this.busy.set(true); this.modalError.set('');
    try { await fn(); this.toasts.show(ok); close(); await this.load(); }
    catch (e) { this.modalError.set(errorMessage(e)); }
    finally { this.busy.set(false); }
  }

  // ---------------- checklist items
  openItem(i: Item, action: string) {
    this.itemTarget.set(i); this.itemAction = action; this.modalError.set('');
    this.itemForm = { comments: i.comments ?? '', justification: '', blockedReason: i.blockedReason ?? '', evidenceReference: i.evidenceReference ?? '', reason: '' };
  }
  itemTitle() { return { InProgress: 'Start item', Completed: 'Complete item', NotApplicable: 'Mark not applicable', Blocked: 'Mark blocked', NotStarted: 'Reopen item' }[this.itemAction] ?? 'Update item'; }
  saveItem() {
    const i = this.itemTarget()!;
    this.run(() => this.api.put(`checklist-items/${i.id}/status`, { status: this.itemAction, ...this.itemForm }),
      `${i.name}: ${humanize(this.itemAction)}`, () => this.itemTarget.set(null));
  }
  async upload(i: Item, ev: Event) {
    const file = (ev.target as HTMLInputElement).files?.[0];
    if (!file) return;
    try { await this.api.upload(`checklist-items/${i.id}/evidence`, file); this.toasts.show(`Evidence attached to ${i.name}`); await this.load(); }
    catch (e) { this.toasts.show(errorMessage(e), 'error'); }
  }
  downloadEvidence(e: any) { this.api.download(`checklist-items/evidence/${e.id}`, e.fileName); }
  canAct(i: Item) { return i.canUpdate && this.active() && !(this.b()!.summary.isReleased && ['Completed', 'NotApplicable'].includes(i.status)); }
  isDone(i: Item) { return i.status === 'Completed' || i.status === 'NotApplicable'; }

  // ---------------- release
  openRelease() {
    const b = this.b(); if (!b) return;
    this.release = { kind: b.summary.isReady && this.openEvents().length === 0 ? 'Full' : 'Conditional', quantity: this.remainingQty(), marketId: null, justification: '' };
    this.modalError.set(''); this.releaseOpen.set(true);
  }
  releaseMeaning() {
    return { Full: 'Approved for release', Partial: `Approved for partial release (${this.release.quantity})`,
      Conditional: 'Approved for conditional release with pending items', FurtherProcessing: 'Released for further processing' }[this.release.kind] ?? '';
  }
  doRelease(sig: { password: string }) {
    this.run(() => this.api.post(`batches/${this.id}/release`, { ...this.release, quantity: this.release.kind === 'Partial' ? this.release.quantity : null, password: sig.password }),
      `${this.b()!.summary.batchNo}: ${this.releaseMeaning()}`, () => this.releaseOpen.set(false));
  }

  // ---------------- disposition
  openDispo(d: string) { this.dispo = { disposition: d, reason: '', password: '' }; this.modalError.set(''); this.dispoOpen.set(true); }
  needsSignature() { return ['Rejected', 'Recalled'].includes(this.dispo.disposition); }
  doDispo() {
    this.run(() => this.api.post(`batches/${this.id}/disposition`, this.dispo), `Batch set to ${humanize(this.dispo.disposition)}`, () => this.dispoOpen.set(false));
  }

  // ---------------- dispatch
  openDispatch() { this.dispatch = { quantity: this.undispatched(), marketId: null, dispatchedOn: todayIso(), reference: '' }; this.modalError.set(''); this.dispatchOpen.set(true); }
  doDispatch() { this.run(() => this.api.post(`batches/${this.id}/dispatch`, this.dispatch), 'Dispatch recorded', () => this.dispatchOpen.set(false)); }

  // ---------------- checklist change control
  openChange(type: string, item?: Item) {
    this.change = { changeType: type, checklistItemId: item?.id ?? null, name: type === 'EditItem' ? item?.name : '', stage: item?.stage ?? 'Additional checks',
      departmentId: item?.departmentId ?? null, itemType: item?.itemType ?? 'DataCheck', isMandatory: item?.isMandatory ?? true, weight: null,
      sequence: item?.sequence ?? null, dueDate: item?.dueDate ?? null, reason: '' };
    this.modalError.set(''); this.changeOpen.set(true);
  }
  doChange() {
    const c = { ...this.change };
    if (c.changeType === 'RemoveItem') { c.name = null; c.departmentId = null; c.itemType = null; c.isMandatory = null; c.sequence = null; c.dueDate = null; c.stage = null; }
    this.run(() => this.api.post(`batches/${this.id}/change-requests`, c), 'Change request submitted for approval', () => this.changeOpen.set(false));
  }
  decide(sig: { password: string; comment: string }) {
    const d = this.decision()!;
    this.run(() => this.api.post(`change-requests/${d.cr.id}/${d.approve ? 'approve' : 'reject'}`, { password: sig.password, comment: sig.comment || null }),
      d.approve ? 'Change approved and applied' : 'Change rejected', () => this.decision.set(null));
  }

  // ---------------- quality events
  openQe(e?: any) {
    this.qe = e ? { ...e, eventType: e.eventType, reason: '' } :
      { eventType: 'Deviation', number: '', description: '', status: 'Open', criticality: 'Minor', openedOn: todayIso(), closedOn: null, reason: '' };
    this.modalError.set(''); this.qeOpen.set(true);
  }
  saveQe() {
    const url = this.qe.id ? `batches/${this.id}/quality-events/${this.qe.id}` : `batches/${this.id}/quality-events`;
    this.run(() => this.qe.id ? this.api.put(url, this.qe) : this.api.post(url, this.qe), 'Quality event saved', () => this.qeOpen.set(false));
  }

  // ---------------- movements
  openMv(m?: any) {
    this.mv = m ? { ...m, reason: '' } : { externalPartyId: null, purpose: 'ContractTesting', status: 'Planned', description: '', quantity: null, unit: 'Tablets',
      reference: '', sentOn: null, receivedByVendorOn: null, expectedReturnOn: null, returnedOn: null, reason: '' };
    this.modalError.set(''); this.mvOpen.set(true);
  }
  saveMv() {
    const url = this.mv.id ? `batches/${this.id}/movements/${this.mv.id}` : `batches/${this.id}/movements`;
    this.run(() => this.mv.id ? this.api.put(url, this.mv) : this.api.post(url, this.mv), 'Movement saved', () => this.mvOpen.set(false));
  }
  approvedParties() { return (this.lk()?.externalParties ?? []).filter((p: any) => p.isActive); }

  // ---------------- markets
  openMk(m?: any) {
    this.mk = m ? { marketId: m.marketId, regulatoryStatus: m.regulatoryStatus, approvalReference: m.approvalReference ?? '', approvedOn: m.approvedAt?.slice(0, 10) ?? null, reason: '', isNew: false }
      : { marketId: null, regulatoryStatus: 'Pending', approvalReference: '', approvedOn: null, reason: '', isNew: true };
    this.modalError.set(''); this.mkOpen.set(true);
  }
  saveMk() {
    const body = { ...this.mk, approvedAt: this.mk.approvedOn ? `${this.mk.approvedOn}T12:00:00Z` : null };
    this.run(() => this.api.post(`batches/${this.id}/markets`, body), 'Market updated', () => this.mkOpen.set(false));
  }
  availableMarkets() { const used = (this.b()?.markets ?? []).map(m => m.marketId); return (this.lk()?.markets ?? []).filter((m: any) => !used.includes(m.id)); }

  itemName(id: number) { return this.b()?.items.find(i => i.id === id)?.name ?? ''; }
  newChild(type: string) { this.router.navigate(['/batches/new'], { queryParams: { parent: this.id, type } }); }
  print() { window.print(); }
}
