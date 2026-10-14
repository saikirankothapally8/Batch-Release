export interface Me { id: number; username: string; fullName: string; departmentId: number | null; department: string | null; roles: string[]; permissions: string[]; }

export interface BatchRow {
  id: number; batchNo: string; product: string; productCode: string; strength: string; site: string; customer: string | null; markets: string[];
  batchType: string; disposition: string; currentStage: string; readiness: number; isReady: boolean; isReleased: boolean;
  manufacturingDate: string; targetReleaseDate: string; expiryDate: string; dispatchBy: string; deadline: string; deadlineType: string;
  daysToDeadline: number; releasedAt: string | null; estimatedDaysToRelease: number; estimatedReleaseDate: string | null;
  flags: string[]; priority: string; explanation: string; pendingCount: number; openQualityEvents: number;
  parentBatchId: number | null; parentBatchNo: string | null; rank?: number;
}

export interface Item {
  id: number; code: string; name: string; stage: string; departmentId: number; department: string; itemType: string; isMandatory: boolean;
  weight: number; sequence: number; isFinalRelease: boolean; marketId: number | null; market: string | null; status: string; dueDate: string | null;
  startedAt: string | null; completedAt: string | null; completedBy: string | null; comments: string | null; notApplicableJustification: string | null;
  blockedReason: string | null; evidenceReference: string | null; source: string; sourceReference: string | null; lastSyncedAt: string | null;
  dependsOn: number[]; openDependencies: string[]; externalMovementId: number | null;
  evidence: { id: number; fileName: string; sizeBytes: number; sha256: string; uploadedAt: string }[]; canUpdate: boolean;
}

export interface Batch360 {
  summary: BatchRow; details: any; items: Item[]; markets: any[]; qualityEvents: any[]; movements: any[]; releases: any[]; dispatches: any[];
  changeRequests: any[]; signatures: any[]; children: BatchRow[]; parent: BatchRow | null; releasedQuantity: number; dispatchedQuantity: number;
  remainingShelfLifePct: number; minShelfLifePct: number; completionPct: number; penalty: number; warehouseAgeDays: number | null; templateName: string | null;
}

export interface Lookups {
  products: any[]; sites: any[]; markets: any[]; customers: any[]; departments: any[]; externalParties: any[];
  enums: Record<string, string[]>;
}

export interface Bucket { label: string; value: number; drill: string; group?: string; }
export interface Kpi { key: string; label: string; value: number; unit: string; drill: string; definition: string; }
export interface StageStat { stage: string; cleared: number; waitingNow: number; avgDays: number; samples: number; drill: string; }
export interface PeriodPoint { period: string; label: string; planned: number; released: number; releasedOnTime: number; plannedDrill: string; releasedDrill: string; }
export interface BreakdownRow { dimension: string; key: string; label: string; total: number; active: number; ready: number; released: number; overdue: number; drill: string; }
export interface Dashboard {
  today: string; kpis: Kpi[]; stages: StageStat[]; qualityEventsByType: Bucket[]; qualityEventsByBatch: Bucket[]; expiryRisk: Bucket[];
  warehouseAging: Bucket[]; weekly: PeriodPoint[]; monthly: PeriodPoint[]; breakdown: BreakdownRow[]; dispositions: Bucket[];
}

export interface WorkItem {
  itemId: number; itemName: string; stage: string; department: string; market: string | null; status: string; dueDate: string | null;
  daysOverdue: number | null; blockedReason: string | null; batchId: number; batchNo: string; product: string; readiness: number;
  deadline: string; priority: string; dependenciesOpen: boolean;
}

export interface BatchFilter {
  productId?: number | null; productCode?: string | null; siteId?: number | null; marketId?: number | null; customerId?: number | null;
  disposition?: string | null; stage?: string | null; dateField?: string | null; from?: string | null; to?: string | null;
  search?: string | null; metric?: string | null; batchType?: string | null;
}

export const P = {
  batchView: 'batch.view', batchCreate: 'batch.create', batchEdit: 'batch.edit', itemOwn: 'checklist.update.own', itemAny: 'checklist.update.any',
  changeRequest: 'checklist.change.request', changeApprove: 'checklist.change.approve', quality: 'quality.manage', movement: 'movement.manage',
  market: 'market.manage', hold: 'batch.hold', reject: 'batch.reject', release: 'batch.release', releaseConditional: 'batch.release.conditional',
  dispatch: 'batch.dispatch', queue: 'queue.view', dashboard: 'dashboard.view', audit: 'audit.view', template: 'template.manage',
  masterData: 'masterdata.manage', settings: 'settings.manage', users: 'user.manage', roles: 'role.manage'
} as const;
