using System.Globalization;
using System.Text.Json;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BatchReleaseIQ.Infrastructure.Data;

public class AppDbContext : DbContext
{
    readonly ICurrentUser _user;
    readonly IClock _clock;
    readonly AuditScope _scope;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser user, IClock clock, AuditScope scope) : base(options)
    {
        _user = user;
        _clock = clock;
        _scope = scope;
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<ExternalParty> ExternalParties => Set<ExternalParty>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Market> Markets => Set<Market>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<ElectronicSignature> Signatures => Set<ElectronicSignature>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<PrioritySettings> PrioritySettings => Set<PrioritySettings>();
    public DbSet<ChecklistTemplate> Templates => Set<ChecklistTemplate>();
    public DbSet<TemplateItem> TemplateItems => Set<TemplateItem>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<BatchMarket> BatchMarkets => Set<BatchMarket>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<ChecklistItemDependency> ChecklistItemDependencies => Set<ChecklistItemDependency>();
    public DbSet<ChecklistEvidence> Evidence => Set<ChecklistEvidence>();
    public DbSet<QualityEvent> QualityEvents => Set<QualityEvent>();
    public DbSet<ExternalMovement> Movements => Set<ExternalMovement>();
    public DbSet<BatchRelease> Releases => Set<BatchRelease>();
    public DbSet<Dispatch> Dispatches => Set<Dispatch>();
    public DbSet<ChecklistChangeRequest> ChangeRequests => Set<ChecklistChangeRequest>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Department>().HasIndex(x => x.Code).IsUnique();
        m.Entity<Site>().HasIndex(x => x.Code).IsUnique();
        m.Entity<ExternalParty>().HasIndex(x => x.Code).IsUnique();
        m.Entity<Product>().HasIndex(x => x.Code).IsUnique();
        m.Entity<Market>().HasIndex(x => x.Code).IsUnique();
        m.Entity<Customer>().HasIndex(x => x.Code).IsUnique();
        m.Entity<User>().HasIndex(x => x.Username).IsUnique();
        m.Entity<Role>().HasIndex(x => x.Name).IsUnique();
        m.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionCode });
        m.Entity<Role>().HasMany(x => x.Permissions).WithOne().HasForeignKey(x => x.RoleId);
        m.Entity<UserRole>().HasKey(x => new { x.UserId, x.RoleId });
        m.Entity<User>().HasMany(x => x.UserRoles).WithOne().HasForeignKey(x => x.UserId);

        m.Entity<AuditEntry>().HasIndex(x => new { x.BatchId, x.Timestamp });
        m.Entity<AuditEntry>().HasIndex(x => new { x.EntityType, x.EntityId });

        m.Entity<TemplateItem>().Property(x => x.Weight).HasPrecision(6, 2);
        m.Entity<ChecklistTemplate>().HasMany(x => x.Items).WithOne().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);

        m.Entity<Batch>(b =>
        {
            b.HasIndex(x => x.BatchNo).IsUnique();
            b.HasIndex(x => x.TargetReleaseDate);
            b.HasIndex(x => x.Disposition);
            b.HasOne(x => x.ParentBatch).WithMany(x => x.ChildBatches).HasForeignKey(x => x.ParentBatchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.BatchSize).HasPrecision(18, 3);
            b.Property(x => x.ReadinessPct).HasPrecision(5, 1);
            b.HasMany(x => x.Items).WithOne(x => x.Batch).HasForeignKey(x => x.BatchId);
            b.HasMany(x => x.Markets).WithOne().HasForeignKey(x => x.BatchId);
            b.HasMany(x => x.QualityEvents).WithOne().HasForeignKey(x => x.BatchId);
            b.HasMany(x => x.Movements).WithOne().HasForeignKey(x => x.BatchId);
            b.HasMany(x => x.Releases).WithOne().HasForeignKey(x => x.BatchId);
            b.HasMany(x => x.Dispatches).WithOne().HasForeignKey(x => x.BatchId);
            b.HasMany(x => x.ChangeRequests).WithOne(x => x.Batch).HasForeignKey(x => x.BatchId);
        });
        m.Entity<BatchMarket>().HasIndex(x => new { x.BatchId, x.MarketId }).IsUnique();

        m.Entity<ChecklistItem>(i =>
        {
            i.Property(x => x.Weight).HasPrecision(6, 2);
            i.HasOne(x => x.CompletedBy).WithMany().HasForeignKey(x => x.CompletedById).OnDelete(DeleteBehavior.Restrict);
            i.HasMany(x => x.Dependencies).WithOne().HasForeignKey(x => x.ChecklistItemId);
            i.HasMany(x => x.Evidence).WithOne().HasForeignKey(x => x.ChecklistItemId);
            i.Ignore(x => x.IsDone);
        });
        m.Entity<ChecklistItemDependency>().HasKey(x => new { x.ChecklistItemId, x.DependsOnItemId });
        m.Entity<ChecklistItemDependency>().HasOne<ChecklistItem>().WithMany().HasForeignKey(x => x.DependsOnItemId).OnDelete(DeleteBehavior.Restrict);

        m.Entity<QualityEvent>().HasIndex(x => x.Number);
        m.Entity<ExternalMovement>().Property(x => x.Quantity).HasPrecision(18, 3);
        m.Entity<BatchRelease>().Property(x => x.Quantity).HasPrecision(18, 3);
        m.Entity<Dispatch>().Property(x => x.Quantity).HasPrecision(18, 3);
        m.Entity<ChecklistChangeRequest>().Property(x => x.ProposedWeight).HasPrecision(6, 2);
        m.Entity<ChecklistChangeRequest>().HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ChecklistChangeRequest>().HasOne(x => x.DecidedBy).WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.Restrict);

        foreach (var p in new[] { nameof(Domain.Entities.PrioritySettings.BlockedItemPenalty), nameof(Domain.Entities.PrioritySettings.OpenEventPenalty),
                     nameof(Domain.Entities.PrioritySettings.CriticalEventPenalty), nameof(Domain.Entities.PrioritySettings.MaxPenalty),
                     nameof(Domain.Entities.PrioritySettings.MinStageWeight), nameof(Domain.Entities.PrioritySettings.MaxStageWeight),
                     nameof(Domain.Entities.PrioritySettings.DefaultItemDurationDays) })
            m.Entity<PrioritySettings>().Property(p).HasPrecision(8, 2);
        m.Entity<PrioritySettings>().Property(x => x.Id).ValueGeneratedNever();

        // Enums are stored as their names so the database stays readable for inspectors and reports.
        foreach (var entity in m.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
            {
                var t = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
                if (t.IsEnum) { prop.SetProviderClrType(typeof(string)); prop.SetMaxLength(40); }
                // Timestamps are always stored in UTC; presentation converts to the site time zone.
                if (prop.ClrType == typeof(DateTimeOffset)) prop.SetValueConverter(UtcConverter);
                if (prop.ClrType == typeof(DateTimeOffset?)) prop.SetValueConverter(NullableUtcConverter);
            }
    }

    static readonly ValueConverter<DateTimeOffset, DateTimeOffset> UtcConverter = new(v => v.ToUniversalTime(), v => v);
    static readonly ValueConverter<DateTimeOffset?, DateTimeOffset?> NullableUtcConverter = new(v => v == null ? v : v.Value.ToUniversalTime(), v => v);

    // ------------------------------------------------------------------ audit trail

    /// <summary>Derived or security-internal fields that are not business data and are not audited field by field.</summary>
    static readonly HashSet<string> NotAudited = new()
    {
        "Batch.ReadinessPct", "Batch.IsReadyForRelease", "Batch.CurrentStage", "Batch.LastProgressAt",
        "Batch.DispatchByDate", "Batch.Deadline", "Batch.DeadlineType",
        "User.FailedLoginCount", "User.LastLoginAt", "User.LockedUntil", "ChecklistEvidence.Content"
    };

    public void AddAudit(string action, string entityType, string entityId, int? batchId = null,
        string? field = null, string? oldValue = null, string? newValue = null, string? reason = null)
    {
        AuditEntries.Add(new AuditEntry
        {
            Timestamp = _clock.Now, UserId = _user.UserId, Username = _user.Username, Action = action,
            EntityType = entityType, EntityId = entityId, BatchId = batchId, FieldName = field,
            OldValue = oldValue, NewValue = newValue, Reason = reason ?? _scope.Reason
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess).GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        ChangeTracker.DetectChanges();
        var pending = ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditEntry && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => new PendingAudit(e, e.State, CaptureChanges(e)))
            .ToList();
        if (pending.Count == 0) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);

        var ownTransaction = Database.CurrentTransaction == null && Database.IsRelational();
        await using var tx = ownTransaction ? await Database.BeginTransactionAsync(ct) : null;
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);

        var now = _clock.Now;
        foreach (var p in pending)
        {
            var typeName = p.Entry.Metadata.ClrType.Name;
            var id = KeyOf(p.Entry);
            var batchId = BatchIdOf(p.Entry);
            var action = p.State switch { EntityState.Added => "Create", EntityState.Deleted => "Delete", _ => "Update" };
            foreach (var (field, oldValue, newValue) in p.Changes)
                AuditEntries.Add(new AuditEntry
                {
                    Timestamp = now, UserId = _user.UserId, Username = _user.Username, Action = action,
                    EntityType = typeName, EntityId = id, BatchId = batchId, FieldName = field,
                    OldValue = oldValue, NewValue = p.State == EntityState.Added ? newValue ?? Snapshot(p.Entry) : newValue,
                    Reason = _scope.Reason
                });
        }
        await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
        if (tx != null) await tx.CommitAsync(ct);
        return result;
    }

    record PendingAudit(EntityEntry Entry, EntityState State, List<(string? Field, string? Old, string? New)> Changes);

    List<(string?, string?, string?)> CaptureChanges(EntityEntry e)
    {
        var typeName = e.Metadata.ClrType.Name;
        if (e.State is EntityState.Added or EntityState.Deleted)
            return new() { (null, e.State == EntityState.Deleted ? Snapshot(e) : null, null) };
        return e.Properties
            .Where(p => p.IsModified && !NotAudited.Contains($"{typeName}.{p.Metadata.Name}") && !Equals(p.OriginalValue, p.CurrentValue))
            .Select(p => ((string?)p.Metadata.Name,
                p.Metadata.Name == "PasswordHash" ? "********" : Format(p.OriginalValue),
                p.Metadata.Name == "PasswordHash" ? "(changed)" : Format(p.CurrentValue)))
            .ToList();
    }

    static string Snapshot(EntityEntry e)
    {
        var typeName = e.Metadata.ClrType.Name;
        var values = e.Properties
            .Where(p => !NotAudited.Contains($"{typeName}.{p.Metadata.Name}") && p.Metadata.Name != "PasswordHash" && p.CurrentValue != null)
            .ToDictionary(p => p.Metadata.Name, p => Format(p.CurrentValue));
        return JsonSerializer.Serialize(values);
    }

    static string KeyOf(EntityEntry e) =>
        string.Join("/", e.Metadata.FindPrimaryKey()!.Properties.Select(p => e.Property(p.Name).CurrentValue?.ToString()));

    int? BatchIdOf(EntityEntry e)
    {
        switch (e.Entity)
        {
            case Batch b: return b.Id;
            case ChecklistEvidence ev: return ChecklistItems.Local.FirstOrDefault(i => i.Id == ev.ChecklistItemId)?.BatchId;
            case ChecklistItemDependency d: return ChecklistItems.Local.FirstOrDefault(i => i.Id == d.ChecklistItemId)?.BatchId;
        }
        var prop = e.Metadata.FindProperty("BatchId");
        return prop == null ? null : e.Property("BatchId").CurrentValue as int?;
    }

    public static string? Format(object? v) => v switch
    {
        null => null,
        DateOnly d => d.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
        DateTimeOffset dt => dt.ToUniversalTime().ToString("dd-MM-yyyy HH:mm:ss 'UTC'", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        byte[] => "(binary)",
        _ => Convert.ToString(v, CultureInfo.InvariantCulture)
    };
}
