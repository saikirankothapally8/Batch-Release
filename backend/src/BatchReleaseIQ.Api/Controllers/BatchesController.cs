using System.Globalization;
using BatchReleaseIQ.Api.Dtos;
using BatchReleaseIQ.Api.Security;
using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Domain.Engine;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Data;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Api.Controllers;

[ApiController, Route("api/batches"), Requires(Permissions.BatchView)]
public class BatchesController : ControllerBase
{
    readonly AppDbContext _db;
    readonly BatchService _batches;
    readonly BatchStateService _state;
    readonly InsightService _insight;
    readonly CurrentUserState _me;
    readonly IClock _clock;

    public BatchesController(AppDbContext db, BatchService batches, BatchStateService state, InsightService insight, CurrentUserState me, IClock clock)
    {
        _db = db; _batches = batches; _state = state; _insight = insight; _me = me; _clock = clock;
    }

    /// <summary>Batch list; also the drill-down target of every chart (pass the chart's metric key).</summary>
    [HttpGet]
    public async Task<object> List([FromQuery] BatchFilter f, [FromQuery] string? sort = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var rows = await _insight.ScopeAsync(f);
        IEnumerable<BatchAssessment> ordered = sort switch
        {
            "readiness" => rows.OrderByDescending(a => a.ReadinessPct),
            "batchNo" => rows.OrderBy(a => a.Batch.BatchNo),
            "target" => rows.OrderBy(a => a.Batch.TargetReleaseDate),
            "released" => rows.OrderByDescending(a => a.Batch.ReleasedAt),
            _ => rows.OrderBy(a => Metrics.IsActive(a.Batch) ? 0 : 1).ThenBy(a => a.Deadline)
        };
        pageSize = Math.Clamp(pageSize, 1, 500);
        return new { total = rows.Count, page, pageSize, items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(a => BatchRowDto.From(a)) };
    }

    [HttpGet("{id:int}")]
    public async Task<Batch360Dto> Get(int id)
    {
        var b = await _state.FullBatches().Include(x => x.Items).ThenInclude(i => i.CompletedBy)
                    .Include(x => x.Items).ThenInclude(i => i.Dependencies)
                    .Include(x => x.Items).ThenInclude(i => i.Evidence)
                    .Include(x => x.Movements).ThenInclude(m => m.ExternalParty)
                    .Include(x => x.Releases).ThenInclude(r => r.Signature)
                    .Include(x => x.Releases).ThenInclude(r => r.Market)
                    .Include(x => x.Dispatches).ThenInclude(d => d.Market)
                    .Include(x => x.ChangeRequests).ThenInclude(c => c.RequestedBy)
                    .Include(x => x.ChangeRequests).ThenInclude(c => c.DecidedBy)
                    .Include(x => x.Template)
                    .Include(x => x.CreatedBy)
                    .FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Batch not found.", 404);
        var a = await _state.AssessAsync(b);
        var today = _clock.Today;
        var children = await _state.FullBatches().Where(x => x.ParentBatchId == id).ToListAsync();
        var childRows = (await _state.AssessAllAsync(children)).Select(c => BatchRowDto.From(c)).ToList();
        object? parent = null;
        if (b.ParentBatchId != null)
        {
            var p = await _state.FullBatches().FirstAsync(x => x.Id == b.ParentBatchId);
            parent = BatchRowDto.From(await _state.AssessAsync(p));
        }
        var signatures = await _db.Signatures.AsNoTracking().Where(s => s.BatchId == id).OrderBy(s => s.SignedAt).ToListAsync();
        var doneIds = b.Items.Where(i => i.IsDone).Select(i => i.Id).ToHashSet();

        var items = b.Items.Where(i => !i.IsRemoved).OrderBy(i => i.Sequence).ThenBy(i => i.Stage).ThenBy(i => i.Name).Select(i => new ItemDto(
            i.Id, i.Code, i.Name, i.Stage, i.DepartmentId, i.Department!.Name, i.ItemType.ToString(), i.IsMandatory, i.Weight, i.Sequence,
            i.IsFinalRelease, i.MarketId, i.Market?.Code, i.Status.ToString(), i.DueDate, i.StartedAt, i.CompletedAt, i.CompletedBy?.FullName,
            i.Comments, i.NotApplicableJustification, i.BlockedReason, i.EvidenceReference, i.Source.ToString(), i.SourceReference, i.LastSyncedAt,
            i.Dependencies.Select(d => d.DependsOnItemId).ToList(),
            i.Dependencies.Where(d => !doneIds.Contains(d.DependsOnItemId)).Select(d => b.Items.First(x => x.Id == d.DependsOnItemId).Name).ToList(),
            i.ExternalMovementId,
            i.Evidence.Select(e => new EvidenceDto(e.Id, e.FileName, e.SizeBytes, e.Sha256, e.UploadedAt)).ToList(),
            !i.IsFinalRelease && (_me.Has(Permissions.ItemUpdateAny) || (_me.Has(Permissions.ItemUpdateOwnDepartment) && _me.DepartmentId == i.DepartmentId))
        )).ToList();

        return new Batch360Dto(
            BatchRowDto.From(a),
            new
            {
                b.Id, b.BatchNo, product = new { b.Product!.Id, b.Product.Code, b.Product.Name, b.Product.Strength, b.Product.StrengthUnit, b.Product.DosageForm, b.Product.ProductType, b.Product.PackSize },
                site = new { b.Site!.Id, b.Site.Code, b.Site.Name, siteType = b.Site.SiteType.ToString() },
                customer = b.Customer == null ? null : new { b.Customer.Id, b.Customer.Name },
                batchType = b.BatchType.ToString(), b.LotType, b.BatchSize, b.BatchSizeUnit, b.ManufacturingBlock, b.ProcessOrderDate,
                b.ManufacturingDate, b.BatchCompletionDate, b.TargetReleaseDate, b.ShelfLifeMonths, b.ExpiryDate, b.ExpiryOverrideReason,
                disposition = b.Disposition.ToString(), b.DispositionReason, b.CreatedAt, createdBy = b.CreatedBy?.FullName, b.TemplateVersion
            },
            items,
            b.Markets.Select(m => new { m.Id, m.MarketId, m.Market!.Code, m.Market.Name, m.Market.RegulatoryAuthority, m.Market.MinShelfLifePctAtDispatch,
                regulatoryStatus = m.RegulatoryStatus.ToString(), m.ApprovalReference, m.ApprovedAt, source = m.Source.ToString(), m.LastSyncedAt }),
            b.QualityEvents.OrderBy(e => e.Status).ThenByDescending(e => e.OpenedOn).Select(e => new { e.Id, eventType = e.EventType.ToString(), e.Number, e.Description,
                status = e.Status.ToString(), criticality = e.Criticality.ToString(), e.OpenedOn, e.ClosedOn, source = e.Source.ToString(), e.LastSyncedAt,
                ageDays = (e.ClosedOn ?? today).DayNumber - e.OpenedOn.DayNumber }),
            b.Movements.OrderBy(m => m.SentOn).Select(m => new { m.Id, m.ExternalPartyId, party = m.ExternalParty!.Name, partyType = m.ExternalParty.PartyType.ToString(),
                purpose = m.Purpose.ToString(), status = m.Status.ToString(), m.Description, m.Quantity, m.Unit, m.Reference, m.SentOn, m.ReceivedByVendorOn,
                m.ExpectedReturnOn, m.ReturnedOn, daysAtVendor = m.SentOn == null ? (int?)null : (m.ReturnedOn ?? today).DayNumber - m.SentOn.Value.DayNumber,
                overdue = m.ReturnedOn == null && m.ExpectedReturnOn != null && m.ExpectedReturnOn < today }),
            b.Releases.OrderBy(r => r.ReleasedAt).Select(r => new { r.Id, kind = r.Kind.ToString(), r.Quantity, market = r.Market?.Code, r.Justification,
                r.PendingItemsSummary, r.ReleasedAt, signedBy = r.Signature?.FullName, meaning = r.Signature?.Meaning }),
            b.Dispatches.OrderBy(d => d.DispatchedOn).Select(d => new { d.Id, d.Quantity, market = d.Market?.Code, d.DispatchedOn, d.Reference }),
            b.ChangeRequests.OrderByDescending(c => c.RequestedAt).Select(c => ChangeRequestsController.ToDto(c, b)),
            signatures.Select(s => new { s.Id, s.FullName, s.Username, s.Meaning, s.SignedAt, s.EntityType, s.RecordHash }),
            childRows, parent,
            Metrics.ReleasedQuantity(b), Metrics.DispatchedQuantity(b), ShelfLife.RemainingPct(b.ManufacturingDate, b.ExpiryDate, today),
            Metrics.MinShelfLifePct(b), a.CompletionPct, a.Penalty, Metrics.WarehouseAgeDays(b, today), b.Template?.Name);
    }

    [HttpGet("template-match")]
    public async Task<object> TemplateMatch([FromQuery] int productId, [FromQuery] int siteId, [FromQuery] int[] marketIds, [FromQuery] int? customerId, [FromQuery] BatchType batchType)
    {
        var matches = await _batches.MatchTemplatesAsync(productId, siteId, marketIds, customerId, batchType);
        return matches.Select(m => new
        {
            m.Template.Id, m.Template.Name, m.Template.Description, m.Template.Version, m.Template.Priority, m.Score,
            items = m.Template.Items.OrderBy(i => i.Sequence).Select(i => new { i.Code, i.Name, i.Stage, department = i.Department?.Name, itemType = i.ItemType.ToString(),
                i.IsMandatory, i.Sequence, i.IsPerMarket, i.IsFinalRelease, i.DueOffsetDays })
        });
    }

    [HttpGet("suggest")]
    public async Task<object> Suggest([FromQuery] int productId, [FromQuery] DateOnly manufacturingDate, [FromQuery] int? shelfLifeMonths)
    {
        var p = await _db.Products.FindAsync(productId) ?? throw new DomainException("Unknown product.");
        var no = BatchService.SuggestBatchNo(p, manufacturingDate, await _db.Batches.Select(b => b.BatchNo).ToListAsync());
        var shelf = shelfLifeMonths ?? p.ShelfLifeMonths;
        var expiry = ShelfLife.CalculateExpiry(manufacturingDate, shelf);
        return new { batchNo = no, shelfLifeMonths = shelf, expiryDate = expiry, dispatchBy = ShelfLife.DispatchBy(manufacturingDate, expiry, Metrics.DefaultMinShelfLifePct) };
    }

    [HttpPost, Requires(Permissions.BatchCreate)]
    public async Task<object> Create(CreateBatchRequest r)
    {
        var b = await _batches.CreateAsync(r);
        return new { b.Id, b.BatchNo };
    }

    [HttpPut("{id:int}"), Requires(Permissions.BatchEdit)]
    public async Task<object> Update(int id, UpdateBatchRequest r) { await _batches.UpdateAsync(id, r); return new { id }; }

    [HttpPost("{id:int}/disposition"), Requires(Permissions.BatchHold, Permissions.BatchReject)]
    public async Task<object> Disposition(int id, DispositionRequest r) { var b = await _batches.SetDispositionAsync(id, r); return new { b.Id, disposition = b.Disposition.ToString() }; }

    [HttpPost("{id:int}/release"), Requires(Permissions.BatchRelease)]
    public async Task<object> Release(int id, ReleaseRequest r) { var rel = await _batches.ReleaseAsync(id, r); return new { rel.Id, kind = rel.Kind.ToString(), rel.Quantity }; }

    [HttpPost("{id:int}/dispatch"), Requires(Permissions.BatchDispatch)]
    public async Task<object> Dispatch(int id, DispatchRequest r) { var d = await _batches.DispatchAsync(id, r); return new { d.Id }; }

    [HttpPost("{id:int}/markets"), Requires(Permissions.MarketManage)]
    public async Task<object> Market(int id, MarketInput r) { var m = await _batches.UpsertMarketAsync(id, r); return new { m.Id }; }

    [HttpPost("{id:int}/quality-events"), Requires(Permissions.QualityEventManage)]
    public async Task<object> AddEvent(int id, QualityEventInput r) { var e = await _batches.UpsertQualityEventAsync(id, null, r); return new { e.Id }; }

    [HttpPut("{id:int}/quality-events/{eventId:int}"), Requires(Permissions.QualityEventManage)]
    public async Task<object> UpdateEvent(int id, int eventId, QualityEventInput r) { var e = await _batches.UpsertQualityEventAsync(id, eventId, r); return new { e.Id }; }

    [HttpPost("{id:int}/movements"), Requires(Permissions.MovementManage)]
    public async Task<object> AddMovement(int id, MovementInput r) { var m = await _batches.UpsertMovementAsync(id, null, r); return new { m.Id }; }

    [HttpPut("{id:int}/movements/{movementId:int}"), Requires(Permissions.MovementManage)]
    public async Task<object> UpdateMovement(int id, int movementId, MovementInput r) { var m = await _batches.UpsertMovementAsync(id, movementId, r); return new { m.Id }; }

    [HttpPost("{id:int}/change-requests"), Requires(Permissions.ChangeRequest)]
    public async Task<object> RequestChange(int id, ChangeRequestInput r) { var c = await _batches.RequestChangeAsync(id, r); return new { c.Id }; }

    [HttpGet("{id:int}/audit"), Requires(Permissions.AuditView)]
    public async Task<object> Audit(int id) =>
        await _db.AuditEntries.AsNoTracking().Where(a => a.BatchId == id).OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id).Take(1000).ToListAsync();
}
