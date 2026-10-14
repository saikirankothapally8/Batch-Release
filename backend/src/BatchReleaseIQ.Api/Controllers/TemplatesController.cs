using BatchReleaseIQ.Api.Dtos;
using BatchReleaseIQ.Api.Security;
using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Data;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Api.Controllers;

/// <summary>
/// Checklist templates. Editing a template bumps its version; batches keep the copy of the checklist they were
/// created with, so a template change never silently alters a batch in progress.
/// </summary>
[ApiController, Route("api/templates"), Requires(Permissions.BatchView)]
public class TemplatesController : ControllerBase
{
    readonly AppDbContext _db;
    readonly AuditScope _scope;
    readonly IClock _clock;
    public TemplatesController(AppDbContext db, AuditScope scope, IClock clock) { _db = db; _scope = scope; _clock = clock; }

    static object ToDto(ChecklistTemplate t, int batchCount) => new
    {
        t.Id, t.Name, t.Description, t.Version, t.IsActive, t.Priority, t.ProductId, product = t.Product?.Name, t.DosageForm, t.MarketId, market = t.Market?.Code,
        t.CustomerId, customer = t.Customer?.Name, batchType = t.BatchType?.ToString(), siteType = t.SiteType?.ToString(), t.UpdatedAt, batchCount,
        items = t.Items.OrderBy(i => i.Sequence).ThenBy(i => i.Id).Select(i => new
        {
            i.Id, i.Code, i.Name, i.Stage, i.DepartmentId, department = i.Department?.Name, itemType = i.ItemType.ToString(), i.IsMandatory, i.Weight,
            i.Sequence, i.DueOffsetDays, i.IsFinalRelease, i.IsPerMarket, defaultSource = i.DefaultSource.ToString(), i.DependsOn
        })
    };

    IQueryable<ChecklistTemplate> Full => _db.Templates.Include(t => t.Items).ThenInclude(i => i.Department)
        .Include(t => t.Product).Include(t => t.Market).Include(t => t.Customer);

    [HttpGet]
    public async Task<object> List()
    {
        var list = await Full.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
        var counts = await _db.Batches.Where(b => b.TemplateId != null).GroupBy(b => b.TemplateId!.Value)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        return list.Select(t => ToDto(t, counts.GetValueOrDefault(t.Id)));
    }

    [HttpGet("{id:int}")]
    public async Task<object> Get(int id) =>
        ToDto(await Full.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id) ?? throw new DomainException("Template not found.", 404),
            await _db.Batches.CountAsync(b => b.TemplateId == id));

    [HttpPost, Requires(Permissions.TemplateManage)]
    public async Task<object> Create(TemplateInput r)
    {
        Validate(r);
        var t = new ChecklistTemplate { UpdatedAt = _clock.Now };
        Apply(t, r);
        _scope.Reason = string.IsNullOrWhiteSpace(r.Reason) ? "Template created" : r.Reason;
        _db.Templates.Add(t);
        await _db.SaveChangesAsync();
        return new { t.Id };
    }

    [HttpPut("{id:int}"), Requires(Permissions.TemplateManage)]
    public async Task<object> Update(int id, TemplateInput r)
    {
        Validate(r);
        if (string.IsNullOrWhiteSpace(r.Reason)) throw new DomainException("A reason is required to change a template.");
        var t = await _db.Templates.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Template not found.", 404);
        _scope.Reason = r.Reason;
        Apply(t, r);
        t.Version++;
        t.UpdatedAt = _clock.Now;
        await _db.SaveChangesAsync();
        return new { t.Id, t.Version };
    }

    void Apply(ChecklistTemplate t, TemplateInput r)
    {
        t.Name = r.Name.Trim(); t.Description = r.Description; t.IsActive = r.IsActive; t.Priority = r.Priority; t.ProductId = r.ProductId;
        t.DosageForm = string.IsNullOrWhiteSpace(r.DosageForm) ? null : r.DosageForm; t.MarketId = r.MarketId; t.CustomerId = r.CustomerId;
        t.BatchType = r.BatchType; t.SiteType = r.SiteType;
        var keep = r.Items.Where(i => i.Id != null).Select(i => i.Id!.Value).ToHashSet();
        foreach (var removed in t.Items.Where(i => !keep.Contains(i.Id)).ToList()) t.Items.Remove(removed);
        foreach (var input in r.Items)
        {
            var item = input.Id == null ? null : t.Items.FirstOrDefault(i => i.Id == input.Id);
            if (item == null) { item = new TemplateItem(); t.Items.Add(item); }
            item.Code = input.Code.Trim().ToUpperInvariant(); item.Name = input.Name; item.Stage = input.Stage; item.DepartmentId = input.DepartmentId;
            item.ItemType = input.ItemType; item.IsMandatory = input.IsFinalRelease || input.IsMandatory; item.Weight = input.Weight;
            item.Sequence = input.Sequence; item.DueOffsetDays = input.DueOffsetDays; item.IsFinalRelease = input.IsFinalRelease;
            item.IsPerMarket = input.IsPerMarket; item.DefaultSource = input.DefaultSource; item.DependsOn = input.DependsOn ?? "";
        }
    }

    static void Validate(TemplateInput r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new DomainException("Template name is required.");
        if (r.Items.Count == 0) throw new DomainException("A template needs at least one item.");
        if (r.Items.Count(i => i.IsFinalRelease) != 1) throw new DomainException("A template must have exactly one final release item.");
        var dup = r.Items.GroupBy(i => i.Code.Trim().ToUpperInvariant()).FirstOrDefault(g => g.Count() > 1);
        if (dup != null) throw new DomainException($"Item code {dup.Key} is used more than once.");
        var codes = r.Items.Select(i => i.Code.Trim().ToUpperInvariant()).ToHashSet();
        foreach (var i in r.Items)
            foreach (var d in (i.DependsOn ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (!codes.Contains(d.ToUpperInvariant())) throw new DomainException($"{i.Code} depends on unknown item {d}.");
        var final = r.Items.Single(i => i.IsFinalRelease);
        if (r.Items.Any(i => i.Sequence > final.Sequence)) throw new DomainException("Final release must be the last step.");
    }
}
