namespace BatchReleaseIQ.Domain;

/// <summary>
/// The catalogue of activities a role can be granted. Roles themselves are data: administrators create them and
/// tick the activities each may perform.
/// </summary>
public static class Permissions
{
    public const string BatchView = "batch.view";
    public const string BatchCreate = "batch.create";
    public const string BatchEdit = "batch.edit";
    public const string ItemUpdateOwnDepartment = "checklist.update.own";
    public const string ItemUpdateAny = "checklist.update.any";
    public const string ChangeRequest = "checklist.change.request";
    public const string ChangeApprove = "checklist.change.approve";
    public const string QualityEventManage = "quality.manage";
    public const string MovementManage = "movement.manage";
    public const string MarketManage = "market.manage";
    public const string BatchHold = "batch.hold";
    public const string BatchReject = "batch.reject";
    public const string BatchRelease = "batch.release";
    public const string BatchReleaseConditional = "batch.release.conditional";
    public const string BatchDispatch = "batch.dispatch";
    public const string QueueView = "queue.view";
    public const string DashboardView = "dashboard.view";
    public const string AuditView = "audit.view";
    public const string TemplateManage = "template.manage";
    public const string MasterDataManage = "masterdata.manage";
    public const string SettingsManage = "settings.manage";
    public const string UserManage = "user.manage";
    public const string RoleManage = "role.manage";

    public record PermissionInfo(string Code, string Group, string Description);

    public static readonly IReadOnlyList<PermissionInfo> All = new[]
    {
        new PermissionInfo(BatchView, "Batches", "View batches and Batch 360"),
        new PermissionInfo(BatchCreate, "Batches", "Create batches"),
        new PermissionInfo(BatchEdit, "Batches", "Edit batch details"),
        new PermissionInfo(MarketManage, "Batches", "Manage target markets and regulatory status"),
        new PermissionInfo(QualityEventManage, "Batches", "Record and update quality events"),
        new PermissionInfo(MovementManage, "Batches", "Record movements to and from external parties"),
        new PermissionInfo(ItemUpdateOwnDepartment, "Checklist", "Update checklist items owned by own department"),
        new PermissionInfo(ItemUpdateAny, "Checklist", "Update any checklist item"),
        new PermissionInfo(ChangeRequest, "Checklist", "Request checklist changes for a batch"),
        new PermissionInfo(ChangeApprove, "Checklist", "Approve or reject checklist changes"),
        new PermissionInfo(BatchHold, "Disposition", "Place batches on hold / quarantine / investigation"),
        new PermissionInfo(BatchReject, "Disposition", "Reject batches"),
        new PermissionInfo(BatchRelease, "Disposition", "Release batches (full or partial), with e-signature"),
        new PermissionInfo(BatchReleaseConditional, "Disposition", "Release with pending items under justification"),
        new PermissionInfo(BatchDispatch, "Disposition", "Record dispatch from warehouse"),
        new PermissionInfo(QueueView, "Insight", "View the QA release queue and workbench"),
        new PermissionInfo(DashboardView, "Insight", "View the management dashboard"),
        new PermissionInfo(AuditView, "Insight", "View the audit trail"),
        new PermissionInfo(TemplateManage, "Administration", "Manage checklist templates"),
        new PermissionInfo(MasterDataManage, "Administration", "Manage master data"),
        new PermissionInfo(SettingsManage, "Administration", "Manage prioritisation settings"),
        new PermissionInfo(UserManage, "Administration", "Manage users"),
        new PermissionInfo(RoleManage, "Administration", "Manage roles and their activities"),
    };
}
