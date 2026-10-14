using BatchReleaseIQ.Api.Security;
using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Infrastructure.Data;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Api.Controllers;

[ApiController, Route("api/checklist-items"), Requires(Permissions.ItemUpdateOwnDepartment, Permissions.ItemUpdateAny)]
public class ChecklistController : ControllerBase
{
    readonly BatchService _batches;
    readonly AppDbContext _db;
    public ChecklistController(BatchService batches, AppDbContext db) { _batches = batches; _db = db; }

    [HttpPut("{id:int}/status")]
    public async Task<object> SetStatus(int id, ItemStatusRequest r)
    {
        var i = await _batches.UpdateItemStatusAsync(id, r);
        return new { i.Id, status = i.Status.ToString() };
    }

    [HttpPost("{id:int}/evidence"), RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<object> Upload(int id, IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var ev = await _batches.AddEvidenceAsync(id, file.FileName, file.ContentType, ms.ToArray());
        return new { ev.Id, ev.FileName, ev.Sha256 };
    }

    [HttpGet("evidence/{evidenceId:int}"), Requires(Permissions.BatchView)]
    public async Task<IActionResult> Download(int evidenceId)
    {
        var ev = await _db.Evidence.AsNoTracking().FirstOrDefaultAsync(e => e.Id == evidenceId);
        return ev == null ? NotFound() : File(ev.Content, ev.ContentType, ev.FileName);
    }
}
