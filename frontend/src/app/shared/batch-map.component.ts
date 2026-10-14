import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { Batch360 } from '../core/models';
import { DmyPipe, DmyTimePipe, LabelPipe } from './pipes';

export type MapState = 'done' | 'wip' | 'todo' | 'blocked' | 'na';
export interface MapLine { text: string; state?: MapState; tab: string; focus?: string; batchId?: number; }
export interface MapNode { title: string; state: MapState; lines: MapLine[]; tab: string; focus?: string; batchId?: number; meta?: string; }
export interface MapLane { key: string; title: string; system?: string; nodes: MapNode[]; state: MapState; }
export interface MapTarget { batchId: number; tab: string; focus?: string; }

/**
 * Batch map: every stage of a batch as a swimlane, left-to-right within a lane and top-to-bottom between lanes,
 * coloured by state. Every node and every line inside a node is a link to the section of Batch 360 that holds it.
 */
@Component({
  selector: 'brq-batch-map',
  imports: [LabelPipe],
  template: `
    <div class="map">
      <div class="legend" aria-label="Legend">
        <span><i class="sw done"></i>Completed</span><span><i class="sw wip"></i>In progress</span>
        <span><i class="sw todo"></i>Not started</span><span><i class="sw blocked"></i>Blocked / open issue</span><span><i class="sw na"></i>Not applicable</span>
      </div>
      @for (lane of lanes; track lane.key; let last = $last) {
        <section class="lane" [class]="'lane lane-' + lane.state">
          <header class="lane-title"><span>{{ lane.title }}</span>@if (lane.system) {<small>{{ lane.system }}</small>}</header>
          <div class="lane-flow">
            @for (n of lane.nodes; track $index; let lastNode = $last) {
              <div class="node {{ n.state }}" role="link" tabindex="0" (click)="go(n)" (keydown.enter)="go(n)" [attr.aria-label]="n.title + ', ' + (n.state | label)">
                <div class="node-title">{{ n.title }}</div>
                @if (n.meta) {<div class="node-meta">{{ n.meta }}</div>}
                @for (l of n.lines; track $index) {
                  <button type="button" class="node-line {{ l.state ?? '' }}" (click)="$event.stopPropagation(); go(l)">{{ l.text }}</button>
                }
              </div>
              @if (!lastNode) {<div class="arrow" aria-hidden="true">→</div>}
            } @empty {<div class="node na"><div class="node-title">Nothing recorded</div></div>}
          </div>
        </section>
        @if (!last) {<div class="lane-link" aria-hidden="true">↓</div>}
      }
    </div>`,
  styles: [`
    .map { display: flex; flex-direction: column; }
    .legend { display: flex; flex-wrap: wrap; gap: 14px; font-size: 12px; color: var(--text-secondary); margin-bottom: 10px; }
    .legend span { display: inline-flex; align-items: center; gap: 6px; }
    .sw { width: 14px; height: 12px; border-radius: 3px; display: inline-block; border: 1px solid var(--border-strong); }
    .lane { display: grid; grid-template-columns: 132px minmax(0, 1fr); border: 1px solid var(--border-strong); border-radius: 8px; background: var(--surface-1); overflow: hidden; }
    .lane-title { display: flex; flex-direction: column; justify-content: center; gap: 2px; padding: 10px 12px; background: var(--surface-2); border-right: 1px solid var(--border-strong);
      font-weight: 600; font-size: 13px; box-shadow: inset 4px 0 0 var(--lane-accent, var(--status-neutral)); }
    .lane-title small { font-weight: 500; color: var(--text-muted); font-size: 11px; }
    .lane-done { --lane-accent: var(--status-good); } .lane-wip { --lane-accent: var(--accent); } .lane-blocked { --lane-accent: var(--status-critical); }
    .lane-flow { display: flex; flex-wrap: wrap; align-items: stretch; row-gap: 12px; padding: 12px; }
    .arrow { align-self: center; padding: 0 8px; color: var(--text-muted); font-size: 18px; flex-shrink: 0; }
    .lane-link { text-align: center; color: var(--text-muted); line-height: 1; padding: 3px 0; margin-left: 66px; width: 0; }
    .node { min-width: 190px; max-width: 260px; flex-shrink: 0; border-radius: 10px; padding: 10px 12px; border: 1px solid var(--border-strong); cursor: pointer;
      display: flex; flex-direction: column; gap: 3px; transition: transform .1s, box-shadow .1s; }
    .node:hover, .node:focus-visible { transform: translateY(-1px); box-shadow: var(--shadow); }
    .node-title { font-weight: 600; font-size: 13px; text-align: center; margin-bottom: 2px; }
    .node-meta { font-size: 11px; color: var(--text-secondary); text-align: center; margin-bottom: 2px; }
    .node-line { background: none; border: 0; padding: 1px 0; font: inherit; font-size: 12px; color: var(--text-primary); text-align: left; cursor: pointer;
      text-decoration: underline; text-decoration-color: var(--border-strong); text-underline-offset: 2px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .node-line:hover { text-decoration-color: currentColor; }
    .node-line.blocked { color: var(--status-critical-ink); font-weight: 600; } .node-line.done { color: var(--status-good-ink); }
    .done, .sw.done { background: var(--status-good-bg); border-color: color-mix(in srgb, var(--status-good) 45%, transparent); }
    .wip, .sw.wip { background: var(--status-info-bg); border-color: color-mix(in srgb, var(--accent) 45%, transparent); }
    .todo, .sw.todo { background: var(--surface-sunk); }
    .blocked, .sw.blocked { background: var(--status-critical-bg); border-color: color-mix(in srgb, var(--status-critical) 55%, transparent); }
    .na, .sw.na { background: transparent; border-style: dashed; }
    .node-line.done, .node-line.blocked, .node-line.wip, .node-line.todo, .node-line.na { background: none; border: 0; }
    @media (max-width: 700px) { .lane { grid-template-columns: minmax(0, 1fr); } .lane-title { border-right: 0; border-bottom: 1px solid var(--border-strong); } }`]
})
export class BatchMapComponent implements OnChanges {
  @Input({ required: true }) batch!: Batch360;
  @Output() navigate = new EventEmitter<MapTarget>();
  lanes: MapLane[] = [];

  ngOnChanges() { this.lanes = buildLanes(this.batch); }

  go(t: { tab: string; focus?: string; batchId?: number }) {
    this.navigate.emit({ batchId: t.batchId ?? this.batch.summary.id, tab: t.tab, focus: t.focus });
  }
}

const dmy = new DmyPipe();
const dmyt = new DmyTimePipe();
const label = new LabelPipe();

function itemState(status: string): MapState {
  return ({ Completed: 'done', NotApplicable: 'na', InProgress: 'wip', Blocked: 'blocked' } as Record<string, MapState>)[status] ?? 'todo';
}

function combine(states: MapState[]): MapState {
  const s = states.filter(x => x !== 'na');
  if (!s.length) return states.length ? 'na' : 'todo';
  if (s.includes('blocked')) return 'blocked';
  if (s.every(x => x === 'done')) return 'done';
  if (s.some(x => x === 'done' || x === 'wip')) return 'wip';
  return 'todo';
}

const SYSTEM: Record<string, string> = { 'QC/LIMS': 'LIMS', QMS: 'QMS', Regulatory: 'RIMS', Receipt: 'WMS', 'Mfg/Pack': 'ERP / MES' };

/** Builds swimlanes from the Batch 360 payload. Exported for reuse and testing. */
export function buildLanes(b: Batch360): MapLane[] {
  const lanes: MapLane[] = [];
  const id = b.summary.id;

  // Lane 0: the batch itself and its genealogy
  const genealogy: MapNode[] = [];
  if (b.parent) genealogy.push({ title: `Parent ${b.parent.batchNo}`, state: b.parent.isReleased || b.parent.disposition === 'ReleasedForFurtherProcessing' ? 'done' : 'wip',
    meta: label.transform(b.parent.disposition), lines: [{ text: 'Open parent batch map', tab: 'map', batchId: b.parent.id }], tab: 'map', batchId: b.parent.id });
  genealogy.push({ title: b.summary.batchNo, state: ['Rejected', 'Recalled'].includes(b.summary.disposition) ? 'blocked' : b.summary.isReleased ? 'done' : 'wip',
    meta: `${label.transform(b.summary.disposition)} · ${b.summary.readiness}% ready`,
    lines: [
      { text: `${b.details.product.name} ${b.details.product.strength} ${b.details.product.strengthUnit}`, tab: 'checklist' },
      { text: `Mfg ${dmy.transform(b.details.manufacturingDate)} · Exp ${dmy.transform(b.details.expiryDate)}`, tab: 'checklist' },
      { text: `Target release ${dmy.transform(b.details.targetReleaseDate)}`, tab: 'checklist' },
    ], tab: 'checklist' });
  for (const c of b.children)
    genealogy.push({ title: `${c.batchType} ${c.batchNo}`, state: c.isReleased ? 'done' : 'wip', meta: `${label.transform(c.disposition)} · ${c.readiness}% ready`,
      lines: [{ text: 'Open child batch map', tab: 'map', batchId: c.id }], tab: 'map', batchId: c.id });
  lanes.push({ key: 'batch', title: 'Batch', system: b.details.site.name, nodes: genealogy, state: combine(genealogy.map(n => n.state)) });

  // Checklist stages in sequence order, enriched with the data that belongs to them
  // Stages ordered by their average step, so a stage spanning steps 3-4 follows one that sits wholly in step 3.
  const seqs = new Map<string, number[]>();
  for (const i of b.items) seqs.set(i.stage, [...(seqs.get(i.stage) ?? []), i.sequence]);
  const avg = (st: string) => { const v = seqs.get(st)!; return v.reduce((a, c) => a + c, 0) / v.length; };
  const stageOrder = [...seqs.keys()].sort((x, y) => avg(x) - avg(y));
  const placedMovements = new Set<number>();
  let qmsPlaced = false, marketsPlaced = false;

  for (const stage of stageOrder) {
    const items = b.items.filter(i => i.stage === stage && i.code !== 'RA_APPROVAL').sort((x, y) => x.sequence - y.sequence || x.id - y.id);
    const nodes: MapNode[] = items.map(i => ({
      title: i.name + (i.market ? ` (${i.market})` : ''), state: itemState(i.status), tab: 'checklist', focus: `item-${i.id}`,
      meta: `${i.department}${i.isMandatory ? '' : ' · optional'}`,
      lines: [
        { text: i.completedAt ? `${label.transform(i.status)} ${dmyt.transform(i.completedAt)}${i.completedBy ? ' · ' + i.completedBy : ''}` : `${label.transform(i.status)} · due ${dmy.transform(i.dueDate)}`,
          tab: 'checklist', focus: `item-${i.id}`, state: itemState(i.status) },
        ...(i.source !== 'Manual' && i.source !== 'System' ? [{ text: `Source ${i.source}${i.lastSyncedAt ? ' · sync ' + dmyt.transform(i.lastSyncedAt) : ''}`, tab: 'checklist', focus: `item-${i.id}` }] : []),
        ...(i.blockedReason ? [{ text: i.blockedReason, tab: 'checklist', focus: `item-${i.id}`, state: 'blocked' as MapState }] : []),
        ...(i.openDependencies.length ? [{ text: `Waiting on ${i.openDependencies.join(', ')}`, tab: 'checklist', focus: `item-${i.id}` }] : []),
      ]
    }));

    // QMS: quality events grouped by type
    if (stage === 'QMS' || (stage.includes('QMS') && !qmsPlaced)) {
      qmsPlaced = true;
      nodes.push(...qmsNodes(b));
    }
    // Regulatory: one node per target market with its RIMS status
    if (b.items.some(i => i.stage === stage && i.code === 'RA_APPROVAL') && !marketsPlaced) {
      marketsPlaced = true;
      nodes.push(...marketNodes(b));
    }
    // External movements linked to an item in this stage
    for (const m of b.movements) {
      if (b.items.some(i => i.stage === stage && i.externalMovementId === m.id)) { nodes.push(movementNode(m)); placedMovements.add(m.id); }
    }
    lanes.push({ key: stage, title: stage, system: SYSTEM[stage], nodes, state: combine(nodes.map(n => n.state)) });
  }

  if (!qmsPlaced && b.qualityEvents.length) {
    const nodes = qmsNodes(b);
    lanes.splice(Math.min(lanes.length, 3), 0, { key: 'qms', title: 'QMS', system: 'QMS', nodes, state: combine(nodes.map(n => n.state)) });
  }
  if (!marketsPlaced && b.markets.length) {
    const nodes = marketNodes(b);
    lanes.splice(Math.max(1, lanes.length - 1), 0, { key: 'ra', title: 'Regulatory', system: 'RIMS', nodes, state: combine(nodes.map(n => n.state)) });
  }
  const loose = b.movements.filter(m => !placedMovements.has(m.id)).map(movementNode);
  if (loose.length) lanes.splice(2, 0, { key: 'ext', title: 'External parties', system: 'CMO / lab / vendor', nodes: loose, state: combine(loose.map(n => n.state)) });

  // Warehouse: release decisions and dispatch
  const wh: MapNode[] = b.releases.map(r => ({
    title: `${label.transform(r.kind)} release`, state: 'done' as MapState, tab: 'releases', meta: `${r.quantity.toLocaleString()} · ${dmyt.transform(r.releasedAt)}`,
    lines: [{ text: `${r.signedBy} – ${r.meaning}`, tab: 'releases' }, ...(r.pendingItemsSummary ? [{ text: `Pending: ${r.pendingItemsSummary}`, tab: 'releases', state: 'blocked' as MapState }] : [])]
  }));
  const undispatched = b.releasedQuantity - b.dispatchedQuantity;
  if (b.releasedQuantity > 0)
    wh.push({ title: 'Dispatch', state: undispatched <= 0 ? 'done' : b.dispatchedQuantity > 0 ? 'wip' : 'todo', tab: 'releases',
      meta: `${b.dispatchedQuantity.toLocaleString()} of ${b.releasedQuantity.toLocaleString()} dispatched`,
      lines: [...b.dispatches.map(d => ({ text: `${dmy.transform(d.dispatchedOn)} · ${d.quantity.toLocaleString()}${d.market ? ' · ' + d.market : ''}`, tab: 'releases' })),
        ...(b.warehouseAgeDays !== null ? [{ text: `In warehouse ${b.warehouseAgeDays} days since release`, tab: 'releases', state: (b.warehouseAgeDays > 30 ? 'blocked' : undefined) as MapState | undefined }] : []),
        { text: `Dispatch by ${dmy.transform(b.summary.dispatchBy)} (${b.minShelfLifePct}% shelf life)`, tab: 'releases' }] });
  if (wh.length) lanes.push({ key: 'wh', title: 'Release & warehouse', system: 'WMS', nodes: wh, state: combine(wh.map(n => n.state)) });

  if (b.changeRequests.some(c => c.status === 'Pending'))
    lanes.push({ key: 'cr', title: 'Change control', nodes: b.changeRequests.filter(c => c.status === 'Pending').map(c => ({
      title: `${label.transform(c.changeType)}`, state: 'wip' as MapState, tab: 'changes', meta: c.proposedName || c.itemName,
      lines: [{ text: `Requested by ${c.requestedBy}`, tab: 'changes' }, { text: c.reason, tab: 'changes' }] })), state: 'wip' });
  return lanes;
}

function qmsNodes(b: Batch360): MapNode[] {
  const types = ['Deviation', 'OOS', 'OOT', 'CAPA', 'ChangeControl'];
  return types.map(t => {
    const evs = b.qualityEvents.filter(e => e.eventType === t);
    const open = evs.filter(e => e.status === 'Open');
    return {
      title: label.transform(t), tab: 'quality', state: (evs.length === 0 ? 'na' : open.length ? 'blocked' : 'done') as MapState,
      meta: evs.length ? `${open.length} open · ${evs.length - open.length} closed` : 'None linked',
      lines: evs.map(e => ({ text: `${e.number} · ${label.transform(e.status)}`, tab: 'quality', focus: `qe-${e.id}`, state: (e.status === 'Open' ? 'blocked' : 'done') as MapState }))
    };
  });
}

function marketNodes(b: Batch360): MapNode[] {
  return b.markets.map(m => {
    const item = b.items.find(i => i.code === 'RA_APPROVAL' && i.marketId === m.marketId);
    const state: MapState = m.regulatoryStatus === 'Approved' ? 'done' : item?.status === 'Blocked' || m.regulatoryStatus === 'Rejected' ? 'blocked'
      : m.regulatoryStatus === 'Submitted' ? 'wip' : 'todo';
    return {
      title: `${m.code} – ${m.regulatoryAuthority}`, tab: 'markets', focus: `mk-${m.marketId}`, state, meta: label.transform(m.regulatoryStatus),
      lines: [
        ...(m.approvalReference ? [{ text: `${m.approvalReference} · ${dmy.transform(m.approvedAt)}`, tab: 'markets', focus: `mk-${m.marketId}` }] : []),
        ...(item ? [{ text: `Checklist: ${label.transform(item.status)}`, tab: 'checklist', focus: `item-${item.id}`, state: itemState(item.status) }] : []),
        ...(item?.blockedReason ? [{ text: item.blockedReason, tab: 'checklist', focus: `item-${item.id}`, state: 'blocked' as MapState }] : []),
        { text: `Min ${m.minShelfLifePctAtDispatch}% shelf life · ${m.source}`, tab: 'markets', focus: `mk-${m.marketId}` },
      ]
    };
  });
}

function movementNode(m: any): MapNode {
  const state: MapState = m.status === 'Returned' || m.status === 'Closed' ? 'done' : m.status === 'Planned' ? 'todo' : m.overdue ? 'blocked' : 'wip';
  return {
    title: m.party, tab: 'movements', focus: `mv-${m.id}`, state, meta: `${label.transform(m.purpose)} · ${label.transform(m.status)}`,
    lines: [
      { text: `Sent ${dmy.transform(m.sentOn)}${m.returnedOn ? ' · back ' + dmy.transform(m.returnedOn) : m.expectedReturnOn ? ' · due ' + dmy.transform(m.expectedReturnOn) : ''}`, tab: 'movements', focus: `mv-${m.id}` },
      ...(m.daysAtVendor !== null ? [{ text: `${m.daysAtVendor} days out${m.overdue ? ' – return overdue' : ''}`, tab: 'movements', focus: `mv-${m.id}`, state: (m.overdue ? 'blocked' : undefined) as MapState | undefined }] : []),
      ...(m.reference ? [{ text: m.reference, tab: 'movements', focus: `mv-${m.id}` }] : []),
    ]
  };
}
