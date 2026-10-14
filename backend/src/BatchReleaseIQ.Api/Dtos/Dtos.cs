using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Domain.Engine;
using BatchReleaseIQ.Domain.Entities;

namespace BatchReleaseIQ.Api.Dtos;

public record LoginRequest(string Username, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record MeDto(int Id, string Username, string FullName, int? DepartmentId, string? Department, List<string> Roles, List<string> Permissions);
public record LoginResponse(string Token, DateTime ExpiresAt, MeDto User);

public record IdName(int Id, string Code, string Name);

public record BatchRowDto(
    int Id, string BatchNo, string Product, string ProductCode, string Strength, string Site, string? Customer, List<string> Markets,
    string BatchType, string Disposition, string CurrentStage, decimal Readiness, bool IsReady, bool IsReleased,
    DateOnly ManufacturingDate, DateOnly TargetReleaseDate, DateOnly ExpiryDate, DateOnly DispatchBy, DateOnly Deadline, string DeadlineType,
    int DaysToDeadline, DateTimeOffset? ReleasedAt, decimal EstimatedDaysToRelease, DateOnly? EstimatedReleaseDate,
    List<string> Flags, string Priority, string Explanation, int PendingCount, int OpenQualityEvents, int? ParentBatchId, string? ParentBatchNo,
    int? Rank = null)
{
    public static BatchRowDto From(BatchAssessment a, int? rank = null)
    {
        var b = a.Batch;
        return new BatchRowDto(b.Id, b.BatchNo, b.Product!.Name, b.Product.Code, $"{b.Product.Strength} {b.Product.StrengthUnit}", b.Site!.Name,
            b.Customer?.Name, b.Markets.Select(m => m.Market!.Code).ToList(), b.BatchType.ToString(), b.Disposition.ToString(), a.CurrentStage,
            a.ReadinessPct, a.IsReadyForRelease, a.IsReleased, b.ManufacturingDate, b.TargetReleaseDate, b.ExpiryDate, a.DispatchBy, a.Deadline,
            a.DeadlineType.ToString(), a.DaysToDeadline, b.ReleasedAt, a.EstimatedDaysToRelease, a.EstimatedReleaseDate,
            a.Flags.Select(f => f.ToString()).ToList(), a.Priority.ToString(), a.Explanation,
            a.PendingItems.Count, a.OpenQualityEvents, b.ParentBatchId, b.ParentBatch?.BatchNo, rank);
    }
}

public record ItemDto(int Id, string Code, string Name, string Stage, int DepartmentId, string Department, string ItemType, bool IsMandatory,
    decimal Weight, int Sequence, bool IsFinalRelease, int? MarketId, string? Market, string Status, DateOnly? DueDate,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? CompletedBy, string? Comments, string? NotApplicableJustification,
    string? BlockedReason, string? EvidenceReference, string Source, string? SourceReference, DateTimeOffset? LastSyncedAt,
    List<int> DependsOn, List<string> OpenDependencies, int? ExternalMovementId, List<EvidenceDto> Evidence, bool CanUpdate);

public record EvidenceDto(int Id, string FileName, long SizeBytes, string Sha256, DateTimeOffset UploadedAt);

public record Batch360Dto(BatchRowDto Summary, object Details, List<ItemDto> Items, object Markets, object QualityEvents, object Movements,
    object Releases, object Dispatches, object ChangeRequests, object Signatures, object Children, object? Parent,
    decimal ReleasedQuantity, decimal DispatchedQuantity, decimal RemainingShelfLifePct, int MinShelfLifePct,
    decimal CompletionPct, decimal Penalty, int? WarehouseAgeDays, string? TemplateName);

public record RoleDto(int Id, string Name, string Description, bool IsSystem, List<string> Permissions, int UserCount);
public record RoleInput(string Name, string Description, List<string> Permissions);
public record UserDto(int Id, string Username, string FullName, string Email, int? DepartmentId, string? Department, bool IsActive,
    bool IsLocked, DateTimeOffset? LastLoginAt, List<int> RoleIds, List<string> Roles);
public record UserInput(string Username, string FullName, string Email, int? DepartmentId, bool IsActive, List<int> RoleIds, string? Password);

public record TemplateItemInput(int? Id, string Code, string Name, string Stage, int DepartmentId, ChecklistItemType ItemType, bool IsMandatory,
    decimal? Weight, int Sequence, int DueOffsetDays, bool IsFinalRelease, bool IsPerMarket, DataSource DefaultSource, string DependsOn);
public record TemplateInput(string Name, string Description, bool IsActive, int Priority, int? ProductId, string? DosageForm, int? MarketId, int? CustomerId,
    BatchType? BatchType, SiteType? SiteType, List<TemplateItemInput> Items, string Reason);
