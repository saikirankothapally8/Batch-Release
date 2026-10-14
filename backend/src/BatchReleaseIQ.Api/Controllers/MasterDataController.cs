using BatchReleaseIQ.Api.Security;
using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Data;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Api.Controllers;

/// <summary>Create/update for master data. Records are deactivated, never deleted, so history stays intact.</summary>
public abstract class MasterDataController<T> : ControllerBase where T : class
{
    protected readonly AppDbContext Db;
    readonly AuditScope _scope;
    protected MasterDataController(AppDbContext db, AuditScope scope) { Db = db; _scope = scope; }

    [HttpGet, Requires(Permissions.BatchView)]
    public async Task<List<T>> List() => await Db.Set<T>().AsNoTracking().ToListAsync();

    [HttpPost, Requires(Permissions.MasterDataManage)]
    public async Task<T> Create(T item)
    {
        Db.Entry(item).Property("Id").CurrentValue = 0;
        _scope.Reason = $"{typeof(T).Name} created";
        Db.Set<T>().Add(item);
        await Db.SaveChangesAsync();
        return item;
    }

    [HttpPut("{id:int}"), Requires(Permissions.MasterDataManage)]
    public async Task<T> Update(int id, T item)
    {
        var current = await Db.Set<T>().FindAsync(id) ?? throw new DomainException("Not found.", 404);
        _scope.Reason = $"{typeof(T).Name} updated";
        Db.Entry(current).CurrentValues.SetValues(item);
        Db.Entry(current).Property("Id").CurrentValue = id;
        await Db.SaveChangesAsync();
        return current;
    }
}

[ApiController, Route("api/master/products")] public class ProductsController : MasterDataController<Product> { public ProductsController(AppDbContext d, AuditScope s) : base(d, s) { } }
[ApiController, Route("api/master/sites")] public class SitesController : MasterDataController<Site> { public SitesController(AppDbContext d, AuditScope s) : base(d, s) { } }
[ApiController, Route("api/master/markets")] public class MarketsController : MasterDataController<Market> { public MarketsController(AppDbContext d, AuditScope s) : base(d, s) { } }
[ApiController, Route("api/master/customers")] public class CustomersController : MasterDataController<Customer> { public CustomersController(AppDbContext d, AuditScope s) : base(d, s) { } }
[ApiController, Route("api/master/departments")] public class DepartmentsController : MasterDataController<Department> { public DepartmentsController(AppDbContext d, AuditScope s) : base(d, s) { } }
[ApiController, Route("api/master/external-parties")] public class ExternalPartiesController : MasterDataController<ExternalParty> { public ExternalPartiesController(AppDbContext d, AuditScope s) : base(d, s) { } }
