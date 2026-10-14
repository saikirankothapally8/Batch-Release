using System.Text.Json;
using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Domain.Engine;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Infrastructure.Services;

/// <summary>Business rules for batches. Every state change is audited by the DbContext with the request's reason.</summary>
public class BatchService
{
    readonly AppDbContext _db;
    readonly BatchStateService _state;
    readonly SignatureService _signatures;
    readonly ICurrentUser _user;
    readonly IClock _clock;
    readonly AuditScope _scope;

    public BatchService(AppDbContext db, BatchStateService state, SignatureService signatures, ICurrentUser user, IClock clock, AuditScope scope)
    {
        _db = db; _state = state; _signatures = signatures; _user = user; _clock = clock; _scope = scope;
    }

    int Me => _user.UserId ?? throw new DomainException("Not authenticated.", 401);

    async Task<Batch> LoadAsync(int id) =>
        await _state.FullBatches(tracking: true).FirstOrDefaultAsync(b => b.Id == id)
        ?? throw new DomainException("Batch not found.", 404);

    static void RequireReason(string? reason, string what)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException($"A reason is required to {what}.");
    }

    // ------------------------------------------------------------------ templates

    public async Task<List<TemplateMatch>> MatchTemplatesAsync(int productId, int siteId, IReadOnlyCollection<int> marketIds, int? customerId, BatchType type)
    {
        var product = await _db.Products.FindAsync(productId) ?? throw new DomainException("Unknown product.");
        var site = await _db.Sites.FindAsync(siteId) ?? throw new DomainException("Unknown site.");
        var templates = await _db.Templates.Include(t => t.Items).ThenInclude(i => i.Department).AsNoTracking().ToListAsync();
        return TemplateMatcher.Candidates(templates, new TemplateCriteria(productId, product.DosageForm, marketIds, customerId, type, site.SiteType));
    }

    public static string SuggestBatchNo(Product p, DateOnly mfg, IEnumerable<string> existing)
    {
        var prefix = $"{p.BatchPrefix}{mfg:yy}N";
        var max = existing.Where(n => n.StartsWith(prefix) && n.Length == prefix.Length + 3)
            .Select(n => int.TryParse(n[prefix.Length..], out var x) ? x : 0).DefaultIfEmpty(100).Max();
        return $"{prefix}{max + 1:000}";
    }

    // ------------------------------------------------------------------ create / edit

    public async Task<Batch> CreateAsync(CreateBatchRequest r)
    {
        var product = await _db.Products.FindAsync(r.ProductId) ?? throw new DomainException("Unknown product.");
        var site = await _db.Sites.FindAsync(r.SiteId) ?? throw new DomainException("Unknown site.");
        if (r.MarketIds.Count == 0) throw new DomainException("At least one target market is required.");
        if (r.BatchSize <= 0) throw new DomainException("Batch size must be greater than zero.");

        Batch? parent = null;
        var needsParent = r.BatchType is BatchType.Rework or BatchType.Reprocess or BatchType.Repack or BatchType.Relabel;
        if (needsParent)
        {
            if (r.ParentBatchId == null) throw new DomainException($"{r.BatchType} batches must be linked to a parent batch.");
            parent = await _db.Batches.FindAsync(r.ParentBatchId) ?? throw new DomainException("Parent batch not found.");
        }

        var shelfLife = r.ShelfLifeMonths ?? product.ShelfLifeMonths;
        var calculated = ShelfLife.CalculateExpiry(r.ManufacturingDate, shelfLife);
        var expiry = r.ExpiryDate ?? calculated;
        if (parent != null && r.BatchType is BatchType.Repack or BatchType.Relabel)
        {
            // Repacked/relabelled material keeps the parent's manufacturing date and can never outlive it.
            if (r.ExpiryDate == null) expiry = parent.ExpiryDate;
            if (expiry > parent.ExpiryDate) throw new DomainException("A repacked or relabelled batch cannot expire after its parent batch.");
        }
        if (expiry != calculated && parent == null) RequireReason(r.ExpiryOverrideReason, "override the calculated expiry date");
        if (r.TargetReleaseDate < r.ManufacturingDate) throw new DomainException("Target release date cannot be before the manufacturing date.");

        var batchNo = string.IsNullOrWhiteSpace(r.BatchNo)
            ? parent != null ? await ChildBatchNoAsync(parent, r.BatchType)
                : SuggestBatchNo(product, r.ManufacturingDate, await _db.Batches.Select(b => b.BatchNo).ToListAsync())
            : r.BatchNo.Trim().ToUpperInvariant();
        if (await _db.Batches.AnyAsync(b => b.BatchNo == batchNo)) throw new DomainException($"Batch number {batchNo} already exists.");

        var markets = await _db.Markets.Where(m => r.MarketIds.Contains(m.Id)).ToListAsync();
        var templates = await _db.Templates.Include(t => t.Items).ToListAsync();
        var template = r.TemplateId != null
            ? templates.FirstOrDefault(t => t.Id == r.TemplateId) ?? throw new DomainException("Template not found.")
            : TemplateMatcher.Candidates(templates, new TemplateCriteria(product.Id, product.DosageForm, r.MarketIds, r.CustomerId, r.BatchType, site.SiteType))
                .FirstOrDefault()?.Template ?? throw new DomainException("No checklist template matches this batch. Ask an administrator to create one.");

        var batch = new Batch
        {
            BatchNo = batchNo, ProductId = product.Id, Product = product, SiteId = site.Id, Site = site, CustomerId = r.CustomerId,
            BatchType = r.BatchType, ParentBatchId = parent?.Id, LotType = r.LotType, BatchSize = r.BatchSize,
            BatchSizeUnit = r.BatchSizeUnit, ManufacturingBlock = r.ManufacturingBlock ?? "",
            ProcessOrderDate = r.ProcessOrderDate, ManufacturingDate = r.ManufacturingDate, BatchCompletionDate = r.BatchCompletionDate,
            TargetReleaseDate = r.TargetReleaseDate, ShelfLifeMonths = shelfLife, ExpiryDate = expiry,
            ExpiryOverrideReason = expiry != calculated ? r.ExpiryOverrideReason ?? "Inherited from parent batch" : null,
            TemplateId = template.Id, TemplateVersion = template.Version, CreatedAt = _clock.Now, CreatedById = Me,
            Markets = markets.Select(m => new BatchMarket { MarketId = m.Id, Market = m }).ToList()
        };
        var settings = await _state.SettingsAsync();
        batch.Items = BuildChecklist(template, batch, markets, settings);
        if (parent != null && parent.ReleasedAt != null || parent?.Disposition == BatchDisposition.ReleasedForFurtherProcessing)
            foreach (var i in batch.Items.Where(i => i.Code == "PARENT_RELEASE"))
                CompleteBySystem(i, $"Parent batch {parent!.BatchNo} released");

        await _state.RecalculateAsync(batch);
        _db.Batches.Add(batch);
        _scope.Reason ??= $"Batch created; template '{template.Name}' v{template.Version} applied";
        await _db.SaveChangesAsync();
        await LinkDependenciesAsync(batch, template);
        return batch;
    }

    async Task<string> ChildBatchNoAsync(Batch parent, BatchType type)
    {
        var suffix = type switch { BatchType.Relabel => "RL", BatchType.Repack => "RP", BatchType.Rework => "RW", _ => "RX" };
        var n = await _db.Batches.CountAsync(b => b.ParentBatchId == parent.Id && b.BatchType == type) + 1;
        return $"{parent.BatchNo}-{suffix}{n}";
    }

    public static List<ChecklistItem> BuildChecklist(ChecklistTemplate t, Batch batch, IReadOnlyList<Market> markets, PrioritySettings s)
    {
        var minSeq = t.Items.Count == 0 ? 0 : t.Items.Min(i => i.Sequence);
        var maxSeq = t.Items.Count == 0 ? 0 : t.Items.Max(i => i.Sequence);
        var items = new List<ChecklistItem>();
        foreach (var ti in t.Items.OrderBy(i => i.Sequence))
        {
            foreach (var market in ti.IsPerMarket ? markets.Cast<Market?>() : new Market?[] { null })
                items.Add(new ChecklistItem
                {
                    TemplateItemId = ti.Id, Code = ti.Code, Name = ti.Name, Stage = ti.Stage, DepartmentId = ti.DepartmentId,
                    ItemType = ti.ItemType, IsMandatory = ti.IsMandatory,
                    Weight = ti.Weight ?? ReadinessEngine.DefaultWeight(ti.Sequence, minSeq, maxSeq, s),
                    Sequence = ti.Sequence, IsFinalRelease = ti.IsFinalRelease, MarketId = market?.Id, Market = market,
                    DueDate = batch.TargetReleaseDate.AddDays(ti.DueOffsetDays), Source = ti.DefaultSource
                });
        }
        return items;
    }

    async Task LinkDependenciesAsync(Batch batch, ChecklistTemplate t)
    {
        var links = BuildDependencies(batch, t);
        if (links.Count == 0) return;
        _db.ChecklistItemDependencies.AddRange(links);
        await _db.SaveChangesAsync();
    }

    /// <summary>Resolves template dependency codes to item ids (items must already be saved).</summary>
    public static List<ChecklistItemDependency> BuildDependencies(Batch batch, ChecklistTemplate t)
    {
        var byCode = batch.Items.GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.ToList());
        var links = new List<ChecklistItemDependency>();
        foreach (var item in batch.Items)
        {
            var ti = t.Items.FirstOrDefault(x => x.Id == item.TemplateItemId);
            if (ti == null || string.IsNullOrWhiteSpace(ti.DependsOn)) continue;
            foreach (var code in ti.DependsOn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (byCode.TryGetValue(code, out var targets))
                    links.AddRange(targets.Select(target => new ChecklistItemDependency { ChecklistItemId = item.Id, DependsOnItemId = target.Id }));
        }
        return links;
    }

    public async Task<Batch> UpdateAsync(int id, UpdateBatchRequest r)
    {
        RequireReason(r.Reason, "change batch details");
        var b = await LoadAsync(id);
        if (Metrics.IsReleased(b)) throw new DomainException("Released batches cannot be edited.");
        _scope.Reason = r.Reason;
        b.CustomerId = r.CustomerId; b.LotType = r.LotType; b.BatchSize = r.BatchSize; b.BatchSizeUnit = r.BatchSizeUnit;
        b.ManufacturingBlock = r.ManufacturingBlock ?? ""; b.ProcessOrderDate = r.ProcessOrderDate;
        b.ManufacturingDate = r.ManufacturingDate; b.BatchCompletionDate = r.BatchCompletionDate;
        b.TargetReleaseDate = r.TargetReleaseDate;
        if (b.ExpiryDate != r.ExpiryDate)
        {
            b.ExpiryDate = r.ExpiryDate;
            b.ExpiryOverrideReason = r.Reason;
        }
        await _state.RecalculateAsync(b);
        await _db.SaveChangesAsync();
        return b;
    }

    // ------------------------------------------------------------------ checklist items

    public async Task<ChecklistItem> UpdateItemStatusAsync(int itemId, ItemStatusRequest r)
    {
        var item = await _db.ChecklistItems.Include(i => i.Dependencies).FirstOrDefaultAsync(i => i.Id == itemId && !i.IsRemoved)
                   ?? throw new DomainException("Checklist item not found.", 404);
        var b = await LoadAsync(item.BatchId);
        item = b.Items.First(i => i.Id == itemId);

        if (!_user.Has(Permissions.ItemUpdateAny) && !(_user.Has(Permissions.ItemUpdateOwnDepartment) && _user.DepartmentId == item.DepartmentId))
            throw new DomainException("This item is owned by another department.", 403);
        if (item.IsFinalRelease) throw new DomainException("Final QA release is performed through the signed release action.");
        if (b.Disposition is BatchDisposition.Rejected or BatchDisposition.Recalled)
            throw new DomainException($"Batch is {b.Disposition}; its checklist is closed.");
        if (Metrics.IsReleased(b) && item.IsDone)
            throw new DomainException("This item was closed at release and cannot be reopened.");

        if (r.Status is ChecklistItemStatus.InProgress or ChecklistItemStatus.Completed)
        {
            var depIds = item.Dependencies.Select(d => d.DependsOnItemId).ToHashSet();
            var openDeps = b.Items.Where(i => depIds.Contains(i.Id) && !i.IsRemoved && !i.IsDone).Select(i => i.Name).ToList();
            if (openDeps.Count > 0) throw new DomainException($"Cannot start until complete: {string.Join(", ", openDeps)}.");
        }

        var reopening = item.IsDone && r.Status is not (ChecklistItemStatus.Completed or ChecklistItemStatus.NotApplicable);
        if (reopening) RequireReason(r.Reason, "reopen a completed item");
        if (r.Status == ChecklistItemStatus.NotApplicable && string.IsNullOrWhiteSpace(r.Justification))
            throw new DomainException("A justification is required to mark an item Not Applicable.");
        if (r.Status == ChecklistItemStatus.Blocked && string.IsNullOrWhiteSpace(r.BlockedReason))
            throw new DomainException("A reason is required to mark an item Blocked.");

        _scope.Reason = r.Reason ?? r.Justification ?? r.BlockedReason;
        var now = _clock.Now;
        item.Status = r.Status;
        if (r.Comments != null) item.Comments = r.Comments;
        if (r.EvidenceReference != null) item.EvidenceReference = r.EvidenceReference;
        item.NotApplicableJustification = r.Status == ChecklistItemStatus.NotApplicable ? r.Justification : null;
        item.BlockedReason = r.Status == ChecklistItemStatus.Blocked ? r.BlockedReason : null;
        switch (r.Status)
        {
            case ChecklistItemStatus.InProgress:
                item.StartedAt ??= now; item.CompletedAt = null; item.CompletedById = null; break;
            case ChecklistItemStatus.Completed:
            case ChecklistItemStatus.NotApplicable:
                item.StartedAt ??= now; item.CompletedAt = now; item.CompletedById = Me; break;
            default:
                item.CompletedAt = null; item.CompletedById = null; break;
        }
        // A manual action on an integrated item is marked as manual entry, keeping the source traceable.
        if (item.Source != DataSource.Manual && item.Source != DataSource.System)
        {
            item.SourceReference = $"Manual entry (integration {item.Source} bypassed)";
            item.Source = DataSource.Manual;
        }
        await _state.RecalculateAsync(b);
        await _db.SaveChangesAsync();
        return item;
    }

    static void CompleteBySystem(ChecklistItem i, string reference)
    {
        if (i.IsDone) return;
        i.Status = ChecklistItemStatus.Completed;
        i.CompletedAt = DateTimeOffset.UtcNow;
        i.StartedAt ??= i.CompletedAt;
        i.Source = DataSource.System;
        i.SourceReference = reference;
        i.LastSyncedAt = i.CompletedAt;
    }

    public async Task<ChecklistEvidence> AddEvidenceAsync(int itemId, string fileName, string contentType, byte[] content)
    {
        var item = await _db.ChecklistItems.FirstOrDefaultAsync(i => i.Id == itemId) ?? throw new DomainException("Item not found.", 404);
        if (content.Length > 10 * 1024 * 1024) throw new DomainException("Evidence files are limited to 10 MB.");
        var ev = new ChecklistEvidence
        {
            ChecklistItemId = item.Id, FileName = Path.GetFileName(fileName), ContentType = contentType, SizeBytes = content.Length,
            Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)),
            Content = content, UploadedAt = _clock.Now, UploadedById = Me
        };
        _db.Evidence.Add(ev);
        await _db.SaveChangesAsync();
        return ev;
    }

    // ------------------------------------------------------------------ checklist change control

    public async Task<ChecklistChangeRequest> RequestChangeAsync(int batchId, ChangeRequestInput r)
    {
        RequireReason(r.Reason, "change a batch checklist");
        var b = await LoadAsync(batchId);
        if (!Metrics.IsActive(b)) throw new DomainException("The checklist of a closed batch cannot be changed.");
        ChecklistItem? item = null;
        if (r.ChangeType != ChangeType.AddItem)
        {
            item = b.Items.FirstOrDefault(i => i.Id == r.ChecklistItemId && !i.IsRemoved) ?? throw new DomainException("Item not found.");
            if (item.IsFinalRelease && r.ChangeType == ChangeType.RemoveItem) throw new DomainException("Final QA release cannot be removed.");
            if (item.IsDone && r.ChangeType == ChangeType.RemoveItem) throw new DomainException("A completed item cannot be removed.");
        }
        else if (string.IsNullOrWhiteSpace(r.Name) || r.DepartmentId == null)
            throw new DomainException("A new item needs a name and an owning department.");

        if (b.ChangeRequests.Any(c => c.Status == ChangeRequestStatus.Pending && c.ChecklistItemId != null && c.ChecklistItemId == r.ChecklistItemId))
            throw new DomainException("There is already a pending change for this item.");

        var cr = new ChecklistChangeRequest
        {
            BatchId = b.Id, ChangeType = r.ChangeType, ChecklistItemId = item?.Id, ProposedName = r.Name, ProposedStage = r.Stage,
            ProposedDepartmentId = r.DepartmentId, ProposedItemType = r.ItemType, ProposedIsMandatory = r.IsMandatory,
            ProposedWeight = r.Weight, ProposedSequence = r.Sequence, ProposedDueDate = r.DueDate, Reason = r.Reason,
            RequestedById = Me, RequestedAt = _clock.Now
        };
        _scope.Reason = r.Reason;
        _db.ChangeRequests.Add(cr);
        await _db.SaveChangesAsync();
        return cr;
    }

    public async Task<ChecklistChangeRequest> DecideChangeAsync(int requestId, bool approve, SignRequest sign)
    {
        var cr = await _db.ChangeRequests.FirstOrDefaultAsync(c => c.Id == requestId) ?? throw new DomainException("Change request not found.", 404);
        if (cr.Status != ChangeRequestStatus.Pending) throw new DomainException("This change request has already been decided.");
        var settings = await _state.SettingsAsync();
        if (settings.EnforceSegregationOfDuties && cr.RequestedById == Me)
            throw new DomainException("Segregation of duties: you cannot approve your own change request.", 403);
        if (!approve && string.IsNullOrWhiteSpace(sign.Comment)) throw new DomainException("A comment is required to reject a change.");

        var b = await LoadAsync(cr.BatchId);
        _scope.Reason = sign.Comment ?? cr.Reason;
        var sig = await _signatures.SignAsync(sign.Password, approve ? "Checklist change approved" : "Checklist change rejected",
            nameof(ChecklistChangeRequest), cr.Id.ToString(), b.Id, JsonSerializer.Serialize(new { cr.ChangeType, cr.ChecklistItemId, cr.ProposedName, cr.Reason }));

        cr.Status = approve ? ChangeRequestStatus.Approved : ChangeRequestStatus.Rejected;
        cr.DecidedById = Me; cr.DecidedAt = _clock.Now; cr.DecisionComment = sign.Comment; cr.Signature = sig;
        if (approve) await ApplyChangeAsync(b, cr);
        await _state.RecalculateAsync(b);
        await _db.SaveChangesAsync();
        return cr;
    }

    async Task ApplyChangeAsync(Batch b, ChecklistChangeRequest cr)
    {
        var settings = await _state.SettingsAsync();
        switch (cr.ChangeType)
        {
            case ChangeType.AddItem:
                var seq = cr.ProposedSequence ?? b.Items.Where(i => !i.IsFinalRelease).Select(i => i.Sequence).DefaultIfEmpty(1).Max();
                var seqs = b.Items.Select(i => i.Sequence).Append(seq).ToList();
                var item = new ChecklistItem
                {
                    BatchId = b.Id, Code = "CUSTOM_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Name = cr.ProposedName!,
                    Stage = cr.ProposedStage ?? "Additional checks", DepartmentId = cr.ProposedDepartmentId!.Value,
                    ItemType = cr.ProposedItemType ?? ChecklistItemType.DataCheck, IsMandatory = cr.ProposedIsMandatory ?? true,
                    Weight = cr.ProposedWeight ?? ReadinessEngine.DefaultWeight(seq, seqs.Min(), seqs.Max(), settings),
                    Sequence = seq, DueDate = cr.ProposedDueDate ?? b.TargetReleaseDate
                };
                b.Items.Add(item);
                break;
            case ChangeType.RemoveItem:
                var removed = b.Items.First(i => i.Id == cr.ChecklistItemId);
                if (removed.IsDone) throw new DomainException("The item was completed after the request was raised and can no longer be removed.");
                removed.IsRemoved = true;
                break;
            case ChangeType.EditItem:
                var e = b.Items.First(i => i.Id == cr.ChecklistItemId);
                if (cr.ProposedName != null) e.Name = cr.ProposedName;
                if (cr.ProposedStage != null) e.Stage = cr.ProposedStage;
                if (cr.ProposedDepartmentId != null) e.DepartmentId = cr.ProposedDepartmentId.Value;
                if (cr.ProposedItemType != null) e.ItemType = cr.ProposedItemType.Value;
                if (cr.ProposedIsMandatory != null && !e.IsFinalRelease) e.IsMandatory = cr.ProposedIsMandatory.Value;
                if (cr.ProposedWeight != null) e.Weight = cr.ProposedWeight.Value;
                if (cr.ProposedSequence != null) e.Sequence = cr.ProposedSequence.Value;
                if (cr.ProposedDueDate != null) e.DueDate = cr.ProposedDueDate;
                break;
        }
    }

    // ------------------------------------------------------------------ disposition, release, dispatch

    public async Task<Batch> SetDispositionAsync(int id, DispositionRequest r)
    {
        RequireReason(r.Reason, $"set the batch to {r.Disposition}");
        var b = await LoadAsync(id);
        _scope.Reason = r.Reason;
        switch (r.Disposition)
        {
            case BatchDisposition.OnHold or BatchDisposition.Quarantine or BatchDisposition.UnderInvestigation:
                if (!_user.Has(Permissions.BatchHold)) throw new DomainException("Not permitted to hold batches.", 403);
                if (b.Disposition is BatchDisposition.Rejected or BatchDisposition.Recalled) throw new DomainException($"Batch is already {b.Disposition}.");
                break;
            case BatchDisposition.InProgress:
                if (!_user.Has(Permissions.BatchHold)) throw new DomainException("Not permitted to lift holds.", 403);
                if (b.Disposition is not (BatchDisposition.OnHold or BatchDisposition.Quarantine or BatchDisposition.UnderInvestigation))
                    throw new DomainException("Only a held batch can be returned to In Progress.");
                break;
            case BatchDisposition.Rejected or BatchDisposition.Recalled:
                if (!_user.Has(Permissions.BatchReject)) throw new DomainException("Not permitted to reject or recall batches.", 403);
                if (r.Disposition == BatchDisposition.Recalled && Metrics.ReleasedQuantity(b) == 0 && b.ReleasedAt == null)
                    throw new DomainException("Only a released batch can be recalled; reject it instead.");
                if (r.Disposition == BatchDisposition.Rejected && b.ReleasedAt != null)
                    throw new DomainException("A released batch cannot be rejected; recall it instead.");
                await _signatures.SignAsync(r.Password ?? "", r.Disposition == BatchDisposition.Rejected ? "Batch rejected" : "Batch recalled",
                    nameof(Batch), b.Id.ToString(), b.Id, $"{b.BatchNo}|{r.Disposition}|{r.Reason}");
                break;
            default:
                throw new DomainException("Use the release action to release a batch.");
        }

        b.Disposition = r.Disposition == BatchDisposition.InProgress
            ? Metrics.ReleasedQuantity(b) > 0 ? BatchDisposition.PartiallyReleased : BatchDisposition.InProgress
            : r.Disposition;
        b.DispositionReason = r.Disposition == BatchDisposition.InProgress ? null : r.Reason;
        await _state.RecalculateAsync(b);
        await _db.SaveChangesAsync();
        return b;
    }

    public async Task<BatchRelease> ReleaseAsync(int id, ReleaseRequest r)
    {
        var b = await LoadAsync(id);
        var settings = await _state.SettingsAsync();
        var today = _clock.Today;
        if (!_user.Has(Permissions.BatchRelease)) throw new DomainException("Not permitted to release batches.", 403);
        if (b.ReleasedAt != null) throw new DomainException("Batch has already been released.");
        if (b.Disposition is BatchDisposition.OnHold or BatchDisposition.Quarantine or BatchDisposition.UnderInvestigation
            or BatchDisposition.Rejected or BatchDisposition.Recalled)
            throw new DomainException($"Batch is {b.Disposition} and cannot be released.");
        if (Metrics.IsExpired(b, today)) throw new DomainException("Batch has expired and cannot be released.");
        if (settings.EnforceSegregationOfDuties && b.CreatedById == Me)
            throw new DomainException("Segregation of duties: the person who created the batch cannot release it.", 403);

        var ready = Metrics.IsReadyForRelease(b);
        var openEvents = b.QualityEvents.Where(e => e.Status == QualityEventStatus.Open).ToList();
        var pending = Metrics.LiveItems(b).Where(i => i.IsMandatory && !i.IsFinalRelease && !i.IsDone).ToList();
        var remainingQty = b.BatchSize - Metrics.ReleasedQuantity(b);

        if (r.Kind == ReleaseKind.Conditional)
        {
            if (!_user.Has(Permissions.BatchReleaseConditional)) throw new DomainException("Not permitted to release conditionally.", 403);
            RequireReason(r.Justification, "release with pending items");
        }
        else if (!ready || openEvents.Count > 0)
            throw new DomainException(
                $"Batch is not ready: {pending.Count} mandatory item(s) pending, {openEvents.Count} open quality event(s). Use a conditional release with justification if appropriate.");

        if (today > Metrics.DispatchBy(b)) RequireReason(r.Justification,
            $"release below the {Metrics.MinShelfLifePct(b)}% remaining shelf-life requirement");

        var qty = r.Kind == ReleaseKind.Partial ? r.Quantity ?? 0 : remainingQty;
        if (qty <= 0 || qty > remainingQty) throw new DomainException($"Quantity must be between 0 and {remainingQty:0.###} {b.BatchSizeUnit}.");
        if (r.Kind == ReleaseKind.Partial && qty == remainingQty) r = r with { Kind = ReleaseKind.Full };

        _scope.Reason = r.Justification;
        var meaning = r.Kind switch
        {
            ReleaseKind.Full => "Approved for release",
            ReleaseKind.Partial => $"Approved for partial release ({qty:0.###} {b.BatchSizeUnit})",
            ReleaseKind.Conditional => "Approved for conditional release with pending items",
            _ => "Released for further processing"
        };
        var sig = await _signatures.SignAsync(r.Password, meaning, nameof(Batch), b.Id.ToString(), b.Id,
            JsonSerializer.Serialize(new { b.BatchNo, r.Kind, qty, r.MarketId, r.Justification, Pending = pending.Select(p => p.Name) }));

        var release = new BatchRelease
        {
            BatchId = b.Id, Kind = r.Kind, Quantity = qty, MarketId = r.MarketId, Justification = r.Justification,
            PendingItemsSummary = pending.Count == 0 ? null : string.Join("; ", pending.Select(p => p.Name)),
            ReleasedAt = _clock.Now, ReleasedById = Me, Signature = sig
        };
        b.Releases.Add(release);

        var final = Metrics.LiveItems(b).FirstOrDefault(i => i.IsFinalRelease);
        switch (r.Kind)
        {
            case ReleaseKind.Partial:
                b.Disposition = BatchDisposition.PartiallyReleased;
                if (final != null) { final.Status = ChecklistItemStatus.InProgress; final.StartedAt ??= _clock.Now; }
                break;
            case ReleaseKind.FurtherProcessing:
                b.Disposition = BatchDisposition.ReleasedForFurtherProcessing;
                break;
            default:
                b.Disposition = r.Kind == ReleaseKind.Conditional ? BatchDisposition.ConditionallyReleased : BatchDisposition.Released;
                b.ReleasedAt = _clock.Now;
                if (final != null)
                {
                    final.Status = ChecklistItemStatus.Completed; final.StartedAt ??= _clock.Now;
                    final.CompletedAt = _clock.Now; final.CompletedById = Me; final.SourceReference = $"E-signature by {sig.FullName} at {sig.SignedAt:dd-MM-yyyy HH:mm} UTC";
                }
                break;
        }

        // Child relabel/repack batches waiting on this parent can now proceed.
        var children = await _db.ChecklistItems.Include(i => i.Batch)
            .Where(i => i.Batch!.ParentBatchId == b.Id && i.Code == "PARENT_RELEASE" && i.Status != ChecklistItemStatus.Completed).ToListAsync();
        foreach (var c in children) CompleteBySystem(c, $"Parent batch {b.BatchNo} {b.Disposition}");

        await _state.RecalculateAsync(b);
        await _db.SaveChangesAsync();
        foreach (var childId in children.Select(c => c.BatchId).Distinct())
        {
            var child = await LoadAsync(childId);
            await _state.RecalculateAsync(child);
        }
        await _db.SaveChangesAsync();
        return release;
    }

    public async Task<Dispatch> DispatchAsync(int id, DispatchRequest r)
    {
        var b = await LoadAsync(id);
        var available = Metrics.UndispatchedQuantity(b);
        if (b.Disposition == BatchDisposition.Recalled) throw new DomainException("Recalled batches cannot be dispatched.");
        if (r.Quantity <= 0 || r.Quantity > available)
            throw new DomainException($"Only {available:0.###} {b.BatchSizeUnit} is released and not yet dispatched.");
        if (r.DispatchedOn > _clock.Today) throw new DomainException("Dispatch date cannot be in the future.");
        var d = new Dispatch
        {
            BatchId = b.Id, Quantity = r.Quantity, MarketId = r.MarketId, DispatchedOn = r.DispatchedOn,
            Reference = r.Reference, RecordedAt = _clock.Now, RecordedById = Me
        };
        b.Dispatches.Add(d);
        await _db.SaveChangesAsync();
        return d;
    }

    // ------------------------------------------------------------------ markets, quality events, movements

    public async Task<BatchMarket> UpsertMarketAsync(int batchId, MarketInput r)
    {
        var b = await LoadAsync(batchId);
        _scope.Reason = r.Reason;
        var bm = b.Markets.FirstOrDefault(m => m.MarketId == r.MarketId);
        if (bm == null)
        {
            RequireReason(r.Reason, "add a target market");
            var market = await _db.Markets.FindAsync(r.MarketId) ?? throw new DomainException("Unknown market.");
            bm = new BatchMarket { MarketId = market.Id, Market = market };
            b.Markets.Add(bm);
            // New market => new per-market items from the template, through the normal path (no approval needed
            // because the market decision itself is the controlled change and is audited with its reason).
            if (b.TemplateId != null)
            {
                var t = await _db.Templates.Include(x => x.Items).FirstAsync(x => x.Id == b.TemplateId);
                var clone = new ChecklistTemplate { Items = t.Items.Where(i => i.IsPerMarket).ToList() };
                foreach (var item in BuildChecklist(clone, b, new[] { market }, await _state.SettingsAsync()))
                {
                    var siblings = b.Items.Where(i => i.Code == item.Code).ToList();
                    if (siblings.Count > 0) { item.Weight = siblings[0].Weight; item.Sequence = siblings[0].Sequence; }
                    b.Items.Add(item);
                }
            }
        }
        if (bm.RegulatoryStatus != RegulatoryStatus.Approved && r.RegulatoryStatus == RegulatoryStatus.Approved)
        {
            if (string.IsNullOrWhiteSpace(r.ApprovalReference)) throw new DomainException("An approval reference is required.");
            bm.ApprovedAt = r.ApprovedAt ?? _clock.Now;
            foreach (var i in b.Items.Where(i => i.MarketId == bm.MarketId && i.ItemType == ChecklistItemType.ExternalResult && !i.IsRemoved))
                CompleteBySystem(i, $"Regulatory approval {r.ApprovalReference} ({bm.Source})");
        }
        bm.RegulatoryStatus = r.RegulatoryStatus;
        bm.ApprovalReference = r.ApprovalReference;
        await _state.RecalculateAsync(b);
        await _db.SaveChangesAsync();
        return bm;
    }

    public async Task<QualityEvent> UpsertQualityEventAsync(int batchId, int? eventId, QualityEventInput r)
    {
        var b = await LoadAsync(batchId);
        _scope.Reason = r.Reason;
        var e = eventId == null ? new QualityEvent { BatchId = b.Id } : b.QualityEvents.FirstOrDefault(x => x.Id == eventId)
                                                                         ?? throw new DomainException("Quality event not found.", 404);
        if (eventId != null) RequireReason(r.Reason, "change a quality event");
        if (r.Status == QualityEventStatus.Closed && r.ClosedOn == null) throw new DomainException("Closed events need a closure date.");
        e.EventType = r.EventType; e.Number = r.Number.Trim(); e.Description = r.Description; e.Status = r.Status;
        e.Criticality = r.Criticality; e.OpenedOn = r.OpenedOn; e.ClosedOn = r.Status == QualityEventStatus.Closed ? r.ClosedOn : null;
        if (eventId == null) b.QualityEvents.Add(e);
        await _state.RecalculateAsync(b);
        await _db.SaveChangesAsync();
        return e;
    }

    public async Task<ExternalMovement> UpsertMovementAsync(int batchId, int? movementId, MovementInput r)
    {
        var b = await LoadAsync(batchId);
        _scope.Reason = r.Reason;
        var party = await _db.ExternalParties.FindAsync(r.ExternalPartyId) ?? throw new DomainException("Unknown external party.");
        if (!party.IsApproved) throw new DomainException($"{party.Name} is not an approved external party.");
        var mv = movementId == null ? new ExternalMovement { BatchId = b.Id } : b.Movements.FirstOrDefault(x => x.Id == movementId)
                                                                                ?? throw new DomainException("Movement not found.", 404);
        var wasReturned = mv.Status is MovementStatus.Returned or MovementStatus.Closed;
        mv.ExternalPartyId = party.Id; mv.Purpose = r.Purpose; mv.Status = r.Status; mv.Description = r.Description;
        mv.Quantity = r.Quantity; mv.Unit = r.Unit; mv.Reference = r.Reference; mv.SentOn = r.SentOn;
        mv.ReceivedByVendorOn = r.ReceivedByVendorOn; mv.ExpectedReturnOn = r.ExpectedReturnOn; mv.ReturnedOn = r.ReturnedOn;
        if (mv.Status is MovementStatus.SentOut or MovementStatus.AtVendor && mv.SentOn == null) throw new DomainException("Sent-out date is required.");
        if (mv.Status is MovementStatus.Returned && mv.ReturnedOn == null) throw new DomainException("Return date is required.");
        if (movementId == null) b.Movements.Add(mv);
        await _db.SaveChangesAsync();

        if (!wasReturned && mv.Status is MovementStatus.Returned or MovementStatus.Closed)
            foreach (var i in b.Items.Where(i => i.ExternalMovementId == mv.Id && !i.IsRemoved))
                CompleteBySystem(i, $"Returned from {party.Name} on {ReadinessEngine.FormatDate(mv.ReturnedOn!.Value)}");
        await _state.RecalculateAsync(b);
        await _db.SaveChangesAsync();
        return mv;
    }
}
