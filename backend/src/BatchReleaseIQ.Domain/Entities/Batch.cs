namespace BatchReleaseIQ.Domain.Entities;

/// <summary>The centre of the model: everything else hangs off a Batch.</summary>
public class Batch
{
    public int Id { get; set; }
    public string BatchNo { get; set; } = "";
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int SiteId { get; set; }
    public Site? Site { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public BatchType BatchType { get; set; }

    /// <summary>Parent for rework, reprocess, repack and relabel batches.</summary>
    public int? ParentBatchId { get; set; }
    public Batch? ParentBatch { get; set; }
    public List<Batch> ChildBatches { get; set; } = new();

    public string LotType { get; set; } = "Commercial";
    public decimal BatchSize { get; set; }
    public string BatchSizeUnit { get; set; } = "Tablets";
    public string ManufacturingBlock { get; set; } = "";
    public DateOnly? ProcessOrderDate { get; set; }
    public DateOnly ManufacturingDate { get; set; }
    public DateOnly? BatchCompletionDate { get; set; }
    public DateOnly TargetReleaseDate { get; set; }
    public int ShelfLifeMonths { get; set; }
    public DateOnly ExpiryDate { get; set; }
    /// <summary>Set when the expiry date was changed from the calculated value (shelf-life extension).</summary>
    public string? ExpiryOverrideReason { get; set; }

    public BatchDisposition Disposition { get; set; } = BatchDisposition.InProgress;
    public string? DispositionReason { get; set; }

    public int? TemplateId { get; set; }
    public ChecklistTemplate? Template { get; set; }
    public int? TemplateVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public int CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    // ---- Derived state, recalculated by BatchStateService whenever the batch changes ----
    public decimal ReadinessPct { get; set; }
    public bool IsReadyForRelease { get; set; }
    public string CurrentStage { get; set; } = "";
    public DateTimeOffset? LastProgressAt { get; set; }
    public DateOnly DispatchByDate { get; set; }
    public DateOnly Deadline { get; set; }
    public DeadlineType DeadlineType { get; set; }
    /// <summary>Timestamp of final QA release (full or conditional). Null until then.</summary>
    public DateTimeOffset? ReleasedAt { get; set; }

    public List<BatchMarket> Markets { get; set; } = new();
    public List<ChecklistItem> Items { get; set; } = new();
    public List<QualityEvent> QualityEvents { get; set; } = new();
    public List<ExternalMovement> Movements { get; set; } = new();
    public List<BatchRelease> Releases { get; set; } = new();
    public List<Dispatch> Dispatches { get; set; } = new();
    public List<ChecklistChangeRequest> ChangeRequests { get; set; } = new();
}

/// <summary>A target market of the batch, with its own regulatory status.</summary>
public class BatchMarket
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public int MarketId { get; set; }
    public Market? Market { get; set; }
    public RegulatoryStatus RegulatoryStatus { get; set; } = RegulatoryStatus.Pending;
    public string? ApprovalReference { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DataSource Source { get; set; } = DataSource.Manual;
    public DateTimeOffset? LastSyncedAt { get; set; }
}

public class ChecklistItem
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public Batch? Batch { get; set; }
    public int? TemplateItemId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Stage { get; set; } = "";
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public ChecklistItemType ItemType { get; set; }
    public bool IsMandatory { get; set; } = true;
    public decimal Weight { get; set; } = 1;
    public int Sequence { get; set; }
    public bool IsFinalRelease { get; set; }
    public int? MarketId { get; set; }
    public Market? Market { get; set; }
    public int? ExternalMovementId { get; set; }

    public ChecklistItemStatus Status { get; set; } = ChecklistItemStatus.NotStarted;
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? CompletedById { get; set; }
    public User? CompletedBy { get; set; }
    public string? Comments { get; set; }
    public string? NotApplicableJustification { get; set; }
    public string? BlockedReason { get; set; }
    public string? EvidenceReference { get; set; }

    public DataSource Source { get; set; } = DataSource.Manual;
    public string? SourceReference { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }

    public bool IsRemoved { get; set; }
    public List<ChecklistItemDependency> Dependencies { get; set; } = new();
    public List<ChecklistEvidence> Evidence { get; set; } = new();

    public bool IsDone => Status is ChecklistItemStatus.Completed or ChecklistItemStatus.NotApplicable;
}

public class ChecklistItemDependency
{
    public int ChecklistItemId { get; set; }
    public int DependsOnItemId { get; set; }
}

public class ChecklistEvidence
{
    public int Id { get; set; }
    public int ChecklistItemId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public DateTimeOffset UploadedAt { get; set; }
    public int UploadedById { get; set; }
}

public class QualityEvent
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public QualityEventType EventType { get; set; }
    public string Number { get; set; } = "";
    public string Description { get; set; } = "";
    public QualityEventStatus Status { get; set; } = QualityEventStatus.Open;
    public QualityEventCriticality Criticality { get; set; } = QualityEventCriticality.Minor;
    public DateOnly OpenedOn { get; set; }
    public DateOnly? ClosedOn { get; set; }
    public DataSource Source { get; set; } = DataSource.Manual;
    public DateTimeOffset? LastSyncedAt { get; set; }
}

/// <summary>Material leaving the site: to a contract manufacturer, contract lab, packaging or labelling vendor.</summary>
public class ExternalMovement
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public int ExternalPartyId { get; set; }
    public ExternalParty? ExternalParty { get; set; }
    public MovementPurpose Purpose { get; set; }
    public MovementStatus Status { get; set; } = MovementStatus.Planned;
    public string Description { get; set; } = "";
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Reference { get; set; }
    public DateOnly? SentOn { get; set; }
    public DateOnly? ReceivedByVendorOn { get; set; }
    public DateOnly? ExpectedReturnOn { get; set; }
    public DateOnly? ReturnedOn { get; set; }
}

/// <summary>A signed release decision. Several per batch when it is released in parts.</summary>
public class BatchRelease
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public ReleaseKind Kind { get; set; }
    public decimal Quantity { get; set; }
    public int? MarketId { get; set; }
    public Market? Market { get; set; }
    public string? Justification { get; set; }
    public string? PendingItemsSummary { get; set; }
    public DateTimeOffset ReleasedAt { get; set; }
    public int ReleasedById { get; set; }
    public int SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }
}

public class Dispatch
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public decimal Quantity { get; set; }
    public int? MarketId { get; set; }
    public Market? Market { get; set; }
    public DateOnly DispatchedOn { get; set; }
    public string? Reference { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public int RecordedById { get; set; }
}

/// <summary>A change to a batch's checklist after creation. Takes effect only when someone else approves it.</summary>
public class ChecklistChangeRequest
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public Batch? Batch { get; set; }
    public ChangeType ChangeType { get; set; }
    public int? ChecklistItemId { get; set; }
    public string? ProposedName { get; set; }
    public string? ProposedStage { get; set; }
    public int? ProposedDepartmentId { get; set; }
    public ChecklistItemType? ProposedItemType { get; set; }
    public bool? ProposedIsMandatory { get; set; }
    public decimal? ProposedWeight { get; set; }
    public int? ProposedSequence { get; set; }
    public DateOnly? ProposedDueDate { get; set; }
    public string Reason { get; set; } = "";
    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Pending;
    public int RequestedById { get; set; }
    public User? RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public int? DecidedById { get; set; }
    public User? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionComment { get; set; }
    public int? SignatureId { get; set; }
    public ElectronicSignature? Signature { get; set; }
}
