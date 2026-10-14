using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Api.Controllers;

[ApiController, Route("api/lookups"), Authorize]
public class LookupsController : ControllerBase
{
    readonly AppDbContext _db;
    public LookupsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<object> Get() => new
    {
        products = await _db.Products.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
        sites = await _db.Sites.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
        markets = await _db.Markets.AsNoTracking().OrderBy(x => x.Code).ToListAsync(),
        customers = await _db.Customers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
        departments = await _db.Departments.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
        externalParties = await _db.ExternalParties.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
        enums = new
        {
            batchTypes = Enum.GetNames<BatchType>(), dispositions = Enum.GetNames<BatchDisposition>(),
            itemTypes = Enum.GetNames<ChecklistItemType>(), itemStatuses = Enum.GetNames<ChecklistItemStatus>(),
            dataSources = Enum.GetNames<DataSource>(), qualityEventTypes = Enum.GetNames<QualityEventType>(),
            criticalities = Enum.GetNames<QualityEventCriticality>(), regulatoryStatuses = Enum.GetNames<RegulatoryStatus>(),
            movementPurposes = Enum.GetNames<MovementPurpose>(), movementStatuses = Enum.GetNames<MovementStatus>(),
            releaseKinds = Enum.GetNames<ReleaseKind>(), siteTypes = Enum.GetNames<SiteType>(),
            partyTypes = Enum.GetNames<ExternalPartyType>(), changeTypes = Enum.GetNames<ChangeType>()
        }
    };
}
