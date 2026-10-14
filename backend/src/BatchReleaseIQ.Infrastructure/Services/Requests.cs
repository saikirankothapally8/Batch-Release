using BatchReleaseIQ.Domain;

namespace BatchReleaseIQ.Infrastructure.Services;

public record CreateBatchRequest(
    string? BatchNo, int ProductId, int SiteId, int? CustomerId, BatchType BatchType, int? ParentBatchId,
    string LotType, decimal BatchSize, string BatchSizeUnit, string? ManufacturingBlock,
    DateOnly? ProcessOrderDate, DateOnly ManufacturingDate, DateOnly? BatchCompletionDate, DateOnly TargetReleaseDate,
    int? ShelfLifeMonths, DateOnly? ExpiryDate, string? ExpiryOverrideReason, List<int> MarketIds, int? TemplateId);

public record UpdateBatchRequest(
    int? CustomerId, string LotType, decimal BatchSize, string BatchSizeUnit, string? ManufacturingBlock,
    DateOnly? ProcessOrderDate, DateOnly ManufacturingDate, DateOnly? BatchCompletionDate, DateOnly TargetReleaseDate,
    DateOnly ExpiryDate, string Reason);

public record ItemStatusRequest(ChecklistItemStatus Status, string? Comments, string? Justification,
    string? BlockedReason, string? EvidenceReference, string? Reason);

public record ChangeRequestInput(ChangeType ChangeType, int? ChecklistItemId, string? Name, string? Stage,
    int? DepartmentId, ChecklistItemType? ItemType, bool? IsMandatory, decimal? Weight, int? Sequence,
    DateOnly? DueDate, string Reason);

public record SignRequest(string Password, string? Comment);

public record ReleaseRequest(ReleaseKind Kind, decimal? Quantity, int? MarketId, string? Justification, string Password);

public record DispositionRequest(BatchDisposition Disposition, string Reason, string? Password);

public record DispatchRequest(decimal Quantity, int? MarketId, DateOnly DispatchedOn, string? Reference);

public record QualityEventInput(QualityEventType EventType, string Number, string Description, QualityEventStatus Status,
    QualityEventCriticality Criticality, DateOnly OpenedOn, DateOnly? ClosedOn, string? Reason);

public record MovementInput(int ExternalPartyId, MovementPurpose Purpose, MovementStatus Status, string Description,
    decimal? Quantity, string? Unit, string? Reference, DateOnly? SentOn, DateOnly? ReceivedByVendorOn,
    DateOnly? ExpectedReturnOn, DateOnly? ReturnedOn, string? Reason);

public record MarketInput(int MarketId, RegulatoryStatus RegulatoryStatus, string? ApprovalReference, DateTimeOffset? ApprovedAt, string? Reason);
