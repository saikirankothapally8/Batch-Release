using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Data;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Tests;

class TestUser : ICurrentUser
{
    public int? UserId { get; set; }
    public string Username { get; set; } = "system";
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();
    public int? DepartmentId { get; set; }
}

class FixedClock : IClock
{
    public DateTimeOffset Now => new(2026, 10, 14, 6, 0, 0, TimeSpan.Zero);
    public DateOnly Today => new(2026, 10, 14);
}

/// <summary>Business rules against the full sample data set (EF in-memory provider).</summary>
public class ServiceTests
{
    readonly AppDbContext _db;
    readonly TestUser _user = new();
    readonly AuditScope _scope = new();
    readonly IClock _clock = new FixedClock();
    readonly PasswordService _passwords = new();

    public ServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new AppDbContext(options, _user, _clock, _scope);
        new SampleData(_db, _passwords, _clock, batchCount: 40).SeedAsync().GetAwaiter().GetResult();
    }

    BatchService Service()
    {
        var state = new BatchStateService(_db, _clock);
        return new BatchService(_db, state, new SignatureService(_db, _passwords, _clock, _user), _user, _clock, _scope);
    }

    void LoginAs(string username)
    {
        var u = _db.Users.Include(x => x.UserRoles).ThenInclude(r => r.Role!).ThenInclude(r => r.Permissions).Single(x => x.Username == username);
        _user.UserId = u.Id; _user.Username = u.Username; _user.DepartmentId = u.DepartmentId;
        _user.Permissions = u.UserRoles.SelectMany(r => r.Role!.Permissions).Select(p => p.PermissionCode).ToHashSet();
        _scope.Reason = null;
    }

    async Task<Batch> CreateUsBatchAsync()
    {
        LoginAs("qa.reviewer");
        var product = _db.Products.Single(p => p.Code == "MET500");
        var request = new CreateBatchRequest(null, product.Id, _db.Sites.Single(s => s.Code == "HYD1").Id, null, BatchType.Commercial, null,
            "Commercial", 900000, "Tablets", "Block A", null, _clock.Today.AddDays(-3), null, _clock.Today.AddDays(20), null, null, null,
            new List<int> { _db.Markets.Single(m => m.Code == "US").Id, _db.Markets.Single(m => m.Code == "UK").Id }, null);
        var b = await Service().CreateAsync(request);
        return await _db.Batches.Include(x => x.Items).ThenInclude(i => i.Dependencies).Include(x => x.Template).SingleAsync(x => x.Id == b.Id);
    }

    [Fact]
    public async Task Creating_a_batch_applies_the_most_specific_template_and_per_market_items()
    {
        var b = await CreateUsBatchAsync();
        Assert.Equal("US market – own site tablets", b.Template!.Name);
        Assert.Contains(b.Items, i => i.Code == "SERIALIZATION");
        Assert.Equal(2, b.Items.Count(i => i.Code == "RA_APPROVAL"));
        Assert.Single(b.Items, i => i.IsFinalRelease);
        Assert.StartsWith("MET26N", b.BatchNo);
        Assert.Equal(new DateOnly(2029, 9, 30), b.ExpiryDate);
        Assert.Contains(_db.AuditEntries, a => a.EntityType == "Batch" && a.EntityId == b.Id.ToString() && a.Action == "Create");
    }

    [Fact]
    public async Task Item_cannot_start_before_its_dependencies_and_other_departments_cannot_update_it()
    {
        var b = await CreateUsBatchAsync();
        var qc = b.Items.First(i => i.Code == "QC_LIMS");

        LoginAs("mfg.user");
        var ex = await Assert.ThrowsAsync<DomainException>(() => Service().UpdateItemStatusAsync(qc.Id, new(ChecklistItemStatus.Completed, null, null, null, null, null)));
        Assert.Equal(403, ex.StatusCode);

        LoginAs("qc.user");
        ex = await Assert.ThrowsAsync<DomainException>(() => Service().UpdateItemStatusAsync(qc.Id, new(ChecklistItemStatus.Completed, null, null, null, null, null)));
        Assert.Contains("Cannot start until complete", ex.Message);

        LoginAs("mfg.user");
        await Service().UpdateItemStatusAsync(b.Items.First(i => i.Code == "MFG_BMR").Id, new(ChecklistItemStatus.Completed, "BMR reviewed", null, null, null, null));
        LoginAs("qc.user");
        var done = await Service().UpdateItemStatusAsync(qc.Id, new(ChecklistItemStatus.Completed, null, null, null, null, null));
        Assert.Equal(ChecklistItemStatus.Completed, done.Status);
        Assert.Equal(DataSource.Manual, done.Source); // LIMS item completed by hand is marked manual
        Assert.Contains("Manual entry", done.SourceReference);
    }

    [Fact]
    public async Task Not_applicable_and_blocked_need_a_reason_which_is_audited()
    {
        var b = await CreateUsBatchAsync();
        LoginAs("qa.head");
        var item = b.Items.First(i => i.Code == "ART_VERIFY");
        await Assert.ThrowsAsync<DomainException>(() => Service().UpdateItemStatusAsync(item.Id, new(ChecklistItemStatus.NotApplicable, null, null, null, null, null)));
        await Service().UpdateItemStatusAsync(item.Id, new(ChecklistItemStatus.NotApplicable, null, "Artwork unchanged since last approved batch", null, null, null));
        Assert.Contains(_db.AuditEntries, a => a.EntityType == "ChecklistItem" && a.EntityId == item.Id.ToString() && a.FieldName == "Status"
                                              && a.NewValue == "NotApplicable" && a.Reason == "Artwork unchanged since last approved batch");
    }

    [Fact]
    public async Task Checklist_change_needs_approval_by_someone_else_with_e_signature()
    {
        var b = await CreateUsBatchAsync();
        var cr = await Service().RequestChangeAsync(b.Id, new(ChangeType.AddItem, null, "Customer-specific CoA format check", "QA Review",
            _db.Departments.Single(d => d.Code == "QA").Id, ChecklistItemType.Document, true, null, 3, null, "Customer quality agreement clause 7.2"));

        LoginAs("qa.head");
        var svc = Service();
        await Assert.ThrowsAsync<DomainException>(() => svc.DecideChangeAsync(cr.Id, true, new SignRequest("wrong-password", null)));
        await Service().DecideChangeAsync(cr.Id, true, new SignRequest(SampleData.DemoPassword, "Agreed"));

        var items = _db.ChecklistItems.Where(i => i.BatchId == b.Id && !i.IsRemoved).ToList();
        Assert.Contains(items, i => i.Name == "Customer-specific CoA format check");
        Assert.Contains(_db.Signatures, s => s.EntityType == nameof(ChecklistChangeRequest) && s.Meaning == "Checklist change approved");
    }

    [Fact]
    public async Task Requester_cannot_approve_own_change()
    {
        var b = await CreateUsBatchAsync();
        LoginAs("qa.head");
        var item = b.Items.First(i => i.Code == "ART_VERIFY");
        var cr = await Service().RequestChangeAsync(b.Id, new(ChangeType.RemoveItem, item.Id, null, null, null, null, null, null, null, null, "Not required"));
        var ex = await Assert.ThrowsAsync<DomainException>(() => Service().DecideChangeAsync(cr.Id, true, new SignRequest(SampleData.DemoPassword, null)));
        Assert.Contains("Segregation of duties", ex.Message);
    }

    [Fact]
    public async Task Release_rules_readiness_segregation_conditional_and_partial()
    {
        var b = await CreateUsBatchAsync();
        LoginAs("qa.head");
        var ex = await Assert.ThrowsAsync<DomainException>(() => Service().ReleaseAsync(b.Id, new(ReleaseKind.Full, null, null, null, SampleData.DemoPassword)));
        Assert.Contains("not ready", ex.Message);

        ex = await Assert.ThrowsAsync<DomainException>(() => Service().ReleaseAsync(b.Id, new(ReleaseKind.Conditional, null, null, null, SampleData.DemoPassword)));
        Assert.Contains("reason is required", ex.Message);

        // Complete everything except final release.
        foreach (var i in _db.ChecklistItems.Where(i => i.BatchId == b.Id && !i.IsFinalRelease).OrderBy(i => i.Sequence).ToList())
            await Service().UpdateItemStatusAsync(i.Id, new(ChecklistItemStatus.Completed, null, null, null, null, null));

        // The creator may not release.
        LoginAs("qa.reviewer");
        _user.Permissions = _user.Permissions.Append(Permissions.BatchRelease).ToHashSet();
        ex = await Assert.ThrowsAsync<DomainException>(() => Service().ReleaseAsync(b.Id, new(ReleaseKind.Full, null, null, null, SampleData.DemoPassword)));
        Assert.Contains("Segregation of duties", ex.Message);

        LoginAs("qa.head");
        await Service().ReleaseAsync(b.Id, new(ReleaseKind.Partial, 300000, null, "Urgent order", SampleData.DemoPassword));
        var partial = await _db.Batches.SingleAsync(x => x.Id == b.Id);
        Assert.Equal(BatchDisposition.PartiallyReleased, partial.Disposition);
        Assert.Null(partial.ReleasedAt);

        await Service().ReleaseAsync(b.Id, new(ReleaseKind.Full, null, null, null, SampleData.DemoPassword));
        var released = await _db.Batches.Include(x => x.Items).Include(x => x.Releases).SingleAsync(x => x.Id == b.Id);
        Assert.Equal(BatchDisposition.Released, released.Disposition);
        Assert.NotNull(released.ReleasedAt);
        Assert.Equal(900000, released.Releases.Sum(r => r.Quantity));
        Assert.Equal(ChecklistItemStatus.Completed, released.Items.Single(i => i.IsFinalRelease).Status);
        Assert.Equal(100m, released.ReadinessPct);
    }

    [Fact]
    public async Task Account_locks_after_repeated_failed_signatures()
    {
        LoginAs("qa.head");
        var svc = new SignatureService(_db, _passwords, _clock, _user);
        for (var i = 0; i < SignatureService.MaxFailedAttempts; i++)
            await Assert.ThrowsAsync<DomainException>(() => svc.VerifyCredentialsAsync("qa.head", "nope"));
        var ex = await Assert.ThrowsAsync<DomainException>(() => svc.VerifyCredentialsAsync("qa.head", SampleData.DemoPassword));
        Assert.Equal(423, ex.StatusCode);
    }

    [Fact]
    public async Task Sample_data_covers_every_state()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, new TestUser(), _clock, new AuditScope());
        await new SampleData(db, _passwords, _clock).SeedAsync();
        var batches = await new BatchStateService(db, _clock).FullBatches().ToListAsync();
        foreach (var d in new[] { BatchDisposition.InProgress, BatchDisposition.Released, BatchDisposition.OnHold, BatchDisposition.Quarantine,
                     BatchDisposition.UnderInvestigation, BatchDisposition.PartiallyReleased, BatchDisposition.ConditionallyReleased,
                     BatchDisposition.Rejected, BatchDisposition.Recalled })
            Assert.Contains(batches, b => b.Disposition == d);
        Assert.Contains(batches, b => b.BatchType == BatchType.Relabel && b.ParentBatchId != null);
        Assert.Contains(batches, b => b.BatchType == BatchType.Rework && b.ParentBatchId != null);
        Assert.Contains(batches, b => b.Movements.Any(m => m.Purpose == MovementPurpose.ContractManufacturing));
        Assert.Contains(batches, b => b.Movements.Any(m => m.Purpose == MovementPurpose.ContractTesting));
        Assert.Contains(batches, b => b.IsReadyForRelease);
    }
}
