import { Pipe, PipeTransform } from '@angular/core';
import { isoToDmy, isoToDmyTime } from '../core/dates';

@Pipe({ name: 'dmy' })
export class DmyPipe implements PipeTransform {
  transform(v: string | null | undefined): string { return isoToDmy(v) || '—'; }
}

@Pipe({ name: 'dmyt' })
export class DmyTimePipe implements PipeTransform {
  transform(v: string | null | undefined): string { return isoToDmyTime(v) || '—'; }
}

const LABELS: Record<string, string> = {
  ChangeControl: 'Change Control', OOS: 'OOS', OOT: 'OOT', CAPA: 'CAPA', LIMS: 'LIMS', QMS: 'QMS', RIMS: 'RIMS', ERP: 'ERP', WMS: 'WMS',
  ShelfLifeBreached: 'Shelf-life limit passed', ExpiryNear: 'Shelf-life limit near', OpenQualityEvents: 'Open QE', AtRisk: 'At risk',
  AtVendor: 'At vendor', PendingChange: 'Change pending', OnHold: 'Held', TargetRelease: 'Target release',
  ShelfLifeDispatchLimit: 'Shelf-life dispatch limit', ReleasedForFurtherProcessing: 'Released for further processing',
  FurtherProcessing: 'For further processing', NotApplicable: 'Not applicable', ContractManufacturer: 'Contract manufacturer',
};

/** Turns enum names into readable labels ("UnderInvestigation" → "Under investigation"). */
export function humanize(v: string | null | undefined): string {
  if (!v) return '';
  if (LABELS[v]) return LABELS[v];
  const s = v.replace(/([a-z])([A-Z])/g, '$1 $2');
  return s.charAt(0) + s.slice(1).toLowerCase();
}

@Pipe({ name: 'label' })
export class LabelPipe implements PipeTransform {
  transform(v: string | null | undefined): string { return humanize(v); }
}
