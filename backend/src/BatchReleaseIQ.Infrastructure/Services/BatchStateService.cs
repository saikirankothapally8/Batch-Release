using BatchReleaseIQ.Domain.Engine;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Infrastructure.Services;

/// <summary>Loads batches with everything the engine needs and keeps the stored derived fields current.</summary>
public class BatchStateService
{
    readonly AppDbContext _db;
    readonly IClock _clock;
    PrioritySettings? _settings;
    Dictionary<string, decimal>? _averages;

    public BatchStateService(AppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public IQueryable<Batch> FullBatches(bool tracking = false)
    {
        var q = _db.Batches
            .Include(b => b.Product).Include(b => b.Site).Include(b => b.Customer).Include(b => b.ParentBatch)
            .Include(b => b.Markets).ThenInclude(m => m.Market)
            .Include(b => b.Items).ThenInclude(i => i.Department)
            .Include(b => b.Items).ThenInclude(i => i.Market)
            .Include(b => b.QualityEvents)
            .Include(b => b.Movements)
            .Include(b => b.Releases)
            .Include(b => b.Dispatches)
            .Include(b => b.ChangeRequests)
            .AsSplitQuery();
        return tracking ? q : q.AsNoTracking();
    }

    public async Task<PrioritySettings> SettingsAsync() =>
        _settings ??= await _db.PrioritySettings.AsNoTracking().FirstOrDefaultAsync() ?? new PrioritySettings();

    /// <summary>Historical average duration (days) per checklist item code, over the last 12 months.</summary>
    public async Task<Dictionary<string, decimal>> AverageDurationsAsync()
    {
        if (_averages != null) return _averages;
        var since = _clock.Now.AddDays(-365);
        var rows = await _db.ChecklistItems.AsNoTracking()
            .Where(i => i.Status == Domain.ChecklistItemStatus.Completed && i.CompletedAt != null && i.CompletedAt > since && !i.IsRemoved)
            .Select(i => new { i.Code, i.StartedAt, i.CompletedAt, i.Batch!.CreatedAt })
            .ToListAsync();
        _averages = rows
            .Select(r => new { r.Code, Days = (decimal)(r.CompletedAt!.Value - (r.StartedAt ?? r.CreatedAt)).TotalDays })
            .Where(r => r.Days >= 0)
            .GroupBy(r => r.Code)
            .ToDictionary(g => g.Key, g => Math.Round(g.Average(r => r.Days), 2));
        return _averages;
    }

    public async Task<BatchAssessment> AssessAsync(Batch b) =>
        ReadinessEngine.Assess(b, await SettingsAsync(), _clock.Today, await AverageDurationsAsync());

    public async Task<List<BatchAssessment>> AssessAllAsync(IEnumerable<Batch> batches)
    {
        var s = await SettingsAsync();
        var avg = await AverageDurationsAsync();
        var today = _clock.Today;
        return batches.Select(b => ReadinessEngine.Assess(b, s, today, avg)).ToList();
    }

    /// <summary>Recalculates the stored derived fields (used for filtering and sorting in SQL).</summary>
    public async Task RecalculateAsync(Batch b)
    {
        var a = ReadinessEngine.Assess(b, await SettingsAsync(), _clock.Today);
        b.ReadinessPct = a.ReadinessPct;
        b.IsReadyForRelease = a.IsReadyForRelease;
        b.CurrentStage = a.CurrentStage;
        b.LastProgressAt = a.LastProgressAt;
        b.DispatchByDate = a.DispatchBy;
        b.Deadline = a.Deadline;
        b.DeadlineType = a.DeadlineType;
    }

    public async Task RecalculateAllAsync()
    {
        _settings = null;
        var batches = await FullBatches(tracking: true).ToListAsync();
        foreach (var b in batches) await RecalculateAsync(b);
        await _db.SaveChangesAsync();
    }
}
