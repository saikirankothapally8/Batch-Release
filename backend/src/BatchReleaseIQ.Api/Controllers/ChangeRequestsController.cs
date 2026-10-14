using BatchReleaseIQ.Api.Security;
using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Data;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Api.Controllers;

[ApiController, Route("api/change-requests"), Requires(Permissions.BatchView)]
public class ChangeRequestsController : ControllerBase
{
    readonly AppDbContext _db;
    readonly BatchService _batches;
    public ChangeRequestsController(AppDbContext db, BatchService batches) { _db = db; _batches = batches; }

    public static object ToDto(ChecklistChangeRequest c, Batch? b) => new
    {
        c.Id, c.BatchId, batchNo = b?.BatchNo ?? c.Batch?.BatchNo, changeType = c.ChangeType.ToString(), c.ChecklistItemId,
        itemName = b?.Items.FirstOrDefault(i => i.Id == c.ChecklistItemId)?.Name ?? c.Batch?.Items.FirstOrDefault(i => i.Id == c.ChecklistItemId)?.Name,
        c.ProposedName, c.ProposedStage, c.ProposedDepartmentId, proposedItemType = c.ProposedItemType?.ToString(), c.ProposedIsMandatory,
        c.ProposedWeight, c.ProposedSequence, c.ProposedDueDate, c.Reason, status = c.Status.ToString(),
        requestedBy = c.RequestedBy?.FullName, c.RequestedById, c.RequestedAt, decidedBy = c.DecidedBy?.FullName, c.DecidedAt, c.DecisionComment
    };

    [HttpGet]
    public async Task<object> List([FromQuery] ChangeRequestStatus? status = ChangeRequestStatus.Pending)
    {
        var q = _db.ChangeRequests.AsNoTracking().Include(c => c.Batch!).ThenInclude(b => b.Items)
            .Include(c => c.RequestedBy).Include(c => c.DecidedBy).AsQueryable();
        if (status != null) q = q.Where(c => c.Status == status);
        var list = await q.OrderByDescending(c => c.RequestedAt).Take(200).ToListAsync();
        return list.Select(c => ToDto(c, null));
    }

    [HttpPost("{id:int}/approve"), Requires(Permissions.ChangeApprove)]
    public async Task<object> Approve(int id, SignRequest r) { var c = await _batches.DecideChangeAsync(id, true, r); return new { c.Id, status = c.Status.ToString() }; }

    [HttpPost("{id:int}/reject"), Requires(Permissions.ChangeApprove)]
    public async Task<object> Reject(int id, SignRequest r) { var c = await _batches.DecideChangeAsync(id, false, r); return new { c.Id, status = c.Status.ToString() }; }
}
