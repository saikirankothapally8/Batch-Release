namespace BatchReleaseIQ.Domain;

public enum SiteType { Own, ContractManufacturer }

public enum ExternalPartyType { ContractManufacturer, ContractLab, PackagingVendor, LabellingVendor, Distributor }

public enum BatchType { Commercial, Validation, Exhibit, Rework, Reprocess, Repack, Relabel }

/// <summary>Physical/QA disposition of a batch. Readiness is tracked separately on the checklist.</summary>
public enum BatchDisposition
{
    InProgress,
    Quarantine,
    UnderInvestigation,
    OnHold,
    ReleasedForFurtherProcessing,
    PartiallyReleased,
    ConditionallyReleased,
    Released,
    Rejected,
    Recalled
}

public enum ChecklistItemType { Approval, DataCheck, Document, ExternalResult, PhysicalActivity }

public enum ChecklistItemStatus { NotStarted, InProgress, Completed, NotApplicable, Blocked }

/// <summary>Where a value came from. Manual = typed by a user; System = derived by this application; others = integration adapters.</summary>
public enum DataSource { Manual, System, LIMS, QMS, RIMS, ERP, WMS }

public enum QualityEventType { Deviation, OOS, OOT, CAPA, ChangeControl }

public enum QualityEventStatus { Open, Closed }

public enum QualityEventCriticality { Minor, Major, Critical }

public enum RegulatoryStatus { NotRequired, Pending, Submitted, Approved, Rejected }

public enum MovementPurpose { ContractManufacturing, ContractTesting, Packaging, Labelling, Relabelling, Other }

public enum MovementStatus { Planned, SentOut, AtVendor, Returned, Closed }

public enum ReleaseKind { Full, Partial, Conditional, FurtherProcessing }

public enum ChangeType { AddItem, RemoveItem, EditItem }

public enum ChangeRequestStatus { Pending, Approved, Rejected }

public enum DeadlineType { TargetRelease, ShelfLifeDispatchLimit, Expiry }

public enum BatchFlag { Overdue, AtRisk, ExpiryNear, ShelfLifeBreached, Expired, Stuck, Blocked, OnHold, OpenQualityEvents, AtVendor, PendingChange }
