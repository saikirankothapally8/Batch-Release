namespace BatchReleaseIQ.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? PasswordChangedAt { get; set; }
    public List<UserRole> UserRoles { get; set; } = new();
}

/// <summary>An administrator-defined role. What it may do is the set of permission codes attached to it.</summary>
public class Role
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsSystem { get; set; }
    public List<RolePermission> Permissions { get; set; } = new();
}

public class RolePermission
{
    public int RoleId { get; set; }
    public string PermissionCode { get; set; } = "";
}

public class UserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public Role? Role { get; set; }
}

/// <summary>
/// 21 CFR Part 11 / Annex 11 electronic signature. The signer's name and the meaning are stored with the
/// record (not looked up later) and RecordHash binds the signature to the content that was signed.
/// </summary>
public class ElectronicSignature
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Meaning { get; set; } = "";
    public DateTimeOffset SignedAt { get; set; }
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public int? BatchId { get; set; }
    public string RecordHash { get; set; } = "";
}

/// <summary>Append-only audit trail. Never updated or deleted (enforced by a database trigger as well).</summary>
public class AuditEntry
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public int? UserId { get; set; }
    public string Username { get; set; } = "";
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public int? BatchId { get; set; }
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Admin-configurable prioritisation and alerting parameters (single row).</summary>
public class PrioritySettings
{
    public int Id { get; set; } = 1;
    /// <summary>Batches whose readiness falls in the same band are ordered by deadline.</summary>
    public int ReadinessBandSize { get; set; } = 10;
    public decimal BlockedItemPenalty { get; set; } = 10;
    public decimal OpenEventPenalty { get; set; } = 5;
    public decimal CriticalEventPenalty { get; set; } = 15;
    public decimal MaxPenalty { get; set; } = 40;
    /// <summary>Default weight of the first and last stage when a template item has no explicit weight.</summary>
    public decimal MinStageWeight { get; set; } = 1;
    public decimal MaxStageWeight { get; set; } = 3;
    public int StuckDays { get; set; } = 7;
    public int ExpiryWarningDays { get; set; } = 30;
    public int AtRiskBufferDays { get; set; } = 2;
    public decimal DefaultItemDurationDays { get; set; } = 2;
    public bool EnforceSegregationOfDuties { get; set; } = true;
}
