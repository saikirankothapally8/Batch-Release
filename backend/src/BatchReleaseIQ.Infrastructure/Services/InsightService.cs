using System.Globalization;
using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Domain.Engine;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Infrastructure.Services;

public record BatchFilter
{
    public int? ProductId { get; init; }
    public string? ProductCode { get; init; }
    public int? SiteId { get; init; }
    public int? MarketId { get; init; }
    public int? CustomerId { get; init; }
    public BatchDisposition? Disposition { get; init; }
    public BatchType? BatchType { get; init; }
    public string? Stage { get; init; }
    /// <summary>target | manufacturing | released | expiry | created</summary>
    public string? DateField { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string? Search { get; init; }
    /// <summary>Drill-down key produced by a chart, e.g. "ready", "expiry:Within 30 days", "site:2|overdue".</summary>
    public string? Metric { get; init; }
}

public record Bucket(string Label, decimal Value, string Drill, string? Group = null);
public record Kpi(string Key, string Label, decimal Value, string Unit, string Drill, string Definition);
public record StageStat(string Stage, int Cleared, int WaitingNow, decimal AvgDays, int Samples, string Drill);
public record PeriodPoint(string Period, string Label, int Planned, int Released, int ReleasedOnTime, string PlannedDrill, string ReleasedDrill);
public record BreakdownRow(string Dimension, string Key, string Label, int Total, int Active, int Ready, int Released, int Overdue, string Drill);

public record DashboardData(
    DateOnly Today, List<Kpi> Kpis, List<StageStat> Stages, List<Bucket> QualityEventsByType, List<Bucket> QualityEventsByBatch,
    List<Bucket> ExpiryRisk, List<Bucket> WarehouseAging, List<PeriodPoint> Weekly, List<PeriodPoint> Monthly,
    List<BreakdownRow> Breakdown, List<Bucket> Dispositions);

public record WorkItem(int ItemId, string ItemName, string Stage, string Department, string? Market, ChecklistItemStatus Status,
    DateOnly? DueDate, int? DaysOverdue, string? BlockedReason, int BatchId, string BatchNo, string Product, decimal Readiness,
    DateOnly Deadline, string Priority, bool DependenciesOpen);

/// <summary>
/// Read side. Every list, KPI and chart starts from <see cref="ScopeAsync"/> and is computed with
/// <see cref="Metrics"/>, so a chart bar and the list behind it always agree.
/// </summary>
public class InsightService
{
    readonly AppDbContext _db;
    readonly BatchStateService _state;
    readonly IClock _clock;

    public InsightService(AppDbContext db, BatchStateService state, IClock clock) { _db = db; _state = state; _clock = clock; }

    public async Task<List<BatchAssessment>> ScopeAsync(BatchFilter f)
    {
        var q = _state.FullBatches();
        if (f.ProductId != null) q = q.Where(b => b.ProductId == f.ProductId);
        if (!string.IsNullOrWhiteSpace(f.ProductCode)) q = q.Where(b => b.Product!.Code == f.ProductCode);
        if (f.SiteId != null) q = q.Where(b => b.SiteId == f.SiteId);
        if (f.CustomerId != null) q = q.Where(b => b.CustomerId == f.CustomerId);
        if (f.MarketId != null) q = q.Where(b => b.Markets.Any(m => m.MarketId == f.MarketId));
        if (f.Disposition != null) q = q.Where(b => b.Disposition == f.Disposition);
        if (f.BatchType != null) q = q.Where(b => b.BatchType == f.BatchType);
        if (!string.IsNullOrWhiteSpace(f.Stage)) q = q.Where(b => b.CurrentStage == f.Stage);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim().ToUpper();
            q = q.Where(b => b.BatchNo.ToUpper().Contains(s) || b.Product!.Name.ToUpper().Contains(s) || b.Product.Code.ToUpper().Contains(s));
        }
        if (f.From != null || f.To != null)
        {
            var from = f.From ?? DateOnly.MinValue;
            var to = f.To ?? DateOnly.MaxValue;
            q = (f.DateField ?? "target") switch
            {
                "manufacturing" => q.Where(b => b.ManufacturingDate >= from && b.ManufacturingDate <= to),
                "expiry" => q.Where(b => b.ExpiryDate >= from && b.ExpiryDate <= to),
                "created" => q.Where(b => DateOnly.FromDateTime(b.CreatedAt.Date) >= from && DateOnly.FromDateTime(b.CreatedAt.Date) <= to),
                "released" => q.Where(b => b.ReleasedAt != null),
                _ => q.Where(b => b.TargetReleaseDate >= from && b.TargetReleaseDate <= to)
            };
        }
        var batches = await q.ToListAsync();
        if (f.DateField == "released" && (f.From != null || f.To != null))
            batches = batches.Where(b => InRange(DateOnly.FromDateTime(b.ReleasedAt!.Value.Date), f.From, f.To)).ToList();

        var assessed = await _state.AssessAllAsync(batches);
        if (string.IsNullOrWhiteSpace(f.Metric)) return assessed;
        var settings = await _state.SettingsAsync();
        var today = _clock.Today;
        var parts = f.Metric.Split('|', StringSplitOptions.RemoveEmptyEntries);
        return assessed.Where(a => parts.All(p => Matches(a, p, today, settings))).ToList();
    }

    static bool InRange(DateOnly d, DateOnly? from, DateOnly? to) => (from == null || d >= from) && (to == null || d <= to);

    /// <summary>Drill-down keys. Each one corresponds to exactly one chart element or KPI.</summary>
    public static bool Matches(BatchAssessment a, string metric, DateOnly today, PrioritySettings s)
    {
        var b = a.Batch;
        var idx = metric.IndexOf(':');
        var key = idx < 0 ? metric : metric[..idx];
        var arg = idx < 0 ? "" : metric[(idx + 1)..];
        return key switch
        {
            "total" => true,
            "active" => Metrics.IsActive(b),
            "ready" => Metrics.IsReadyForRelease(b),
            "released" => Metrics.IsReleased(b),
            "partial" => Metrics.IsPartiallyReleased(b),
            "overdue" => Metrics.IsOpenOverdue(b, today),
            "ontime" => Metrics.IsOnTime(b),
            "late" => Metrics.IsReleased(b) && !Metrics.IsOnTime(b),
            "stuck" => Metrics.IsStuck(b, today, s.StuckDays),
            "atrisk" => a.Flags.Contains(BatchFlag.AtRisk),
            "blocked" => a.Flags.Contains(BatchFlag.Blocked),
            "onhold" => a.Flags.Contains(BatchFlag.OnHold),
            "atvendor" => a.Flags.Contains(BatchFlag.AtVendor),
            "rejected" => b.Disposition == BatchDisposition.Rejected,
            "disposition" => b.Disposition.ToString() == arg,
            "qe" => b.QualityEvents.Any(e => e.Status == QualityEventStatus.Open && (arg == "" || e.EventType.ToString() == arg)),
            "expiry" => (Metrics.IsActive(b) || Metrics.UndispatchedQuantity(b) > 0) && Metrics.ExpiryBucket(b, today) == arg,
            "warehouse" => Metrics.WarehouseAgeDays(b, today) is { } age && (arg == "" || Metrics.AgingBucket(age) == arg),
            "stage" => Metrics.IsActive(b) && a.CurrentStage == arg,
            "cleared" => ClearedStages(b).Contains(arg),
            "planned-month" => b.TargetReleaseDate.ToString("yyyy-MM") == arg,
            "released-month" => b.ReleasedAt != null && b.ReleasedAt.Value.ToString("yyyy-MM") == arg,
            "planned-week" => WeekStart(b.TargetReleaseDate).ToString("yyyy-MM-dd") == arg,
            "released-week" => b.ReleasedAt != null && WeekStart(DateOnly.FromDateTime(b.ReleasedAt.Value.Date)).ToString("yyyy-MM-dd") == arg,
            "site" => b.SiteId.ToString() == arg,
            "product" => b.ProductId.ToString() == arg,
            "customer" => (b.CustomerId?.ToString() ?? "none") == arg,
            "market" => b.Markets.Any(m => m.MarketId.ToString() == arg),
            "batch" => b.Id.ToString() == arg,
            _ => true
        };
    }

    public static DateOnly WeekStart(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    /// <summary>Stages whose mandatory items are all done.</summary>
    static HashSet<string> ClearedStages(Batch b)
    {
        var items = Metrics.LiveItems(b).Where(i => i.IsMandatory).ToList();
        return items.GroupBy(i => i.Stage).Where(g => g.All(i => i.IsDone)).Select(g => g.Key).ToHashSet();
    }

    // ------------------------------------------------------------------ queue & workbench

    public async Task<List<BatchAssessment>> QueueAsync(BatchFilter f)
    {
        var scope = await ScopeAsync(f);
        return ReadinessEngine.Rank(scope.Where(a => Metrics.IsActive(a.Batch)));
    }

    public async Task<List<WorkItem>> WorkItemsAsync(int? departmentId, bool mine)
    {
        var today = _clock.Today;
        var batches = await _state.FullBatches()
            .Where(b => b.ReleasedAt == null && b.Disposition != BatchDisposition.Rejected && b.Disposition != BatchDisposition.Recalled)
            .ToListAsync();
        var deps = await _db.ChecklistItemDependencies.AsNoTracking().ToListAsync();
        var depsByItem = deps.GroupBy(d => d.ChecklistItemId).ToDictionary(g => g.Key, g => g.Select(x => x.DependsOnItemId).ToList());
        var assessed = (await _state.AssessAllAsync(batches)).ToDictionary(a => a.Batch.Id);

        var rows = new List<WorkItem>();
        foreach (var b in batches)
        {
            var doneIds = b.Items.Where(i => i.IsDone).Select(i => i.Id).ToHashSet();
            foreach (var i in Metrics.LiveItems(b).Where(i => !i.IsDone && !i.IsFinalRelease))
            {
                if (mine ? i.DepartmentId != departmentId : i.DepartmentId == departmentId) continue;
                var a = assessed[b.Id];
                var depsOpen = depsByItem.TryGetValue(i.Id, out var d) && d.Any(x => !doneIds.Contains(x));
                rows.Add(new WorkItem(i.Id, i.Name, i.Stage, i.Department?.Name ?? "", i.Market?.Code, i.Status, i.DueDate,
                    i.DueDate != null && i.DueDate < today ? today.DayNumber - i.DueDate.Value.DayNumber : null,
                    i.BlockedReason, b.Id, b.BatchNo, b.Product!.Name, a.ReadinessPct, a.Deadline, a.Priority.ToString(), depsOpen));
            }
        }
        return rows.OrderBy(r => r.DependenciesOpen).ThenBy(r => r.Priority switch { "Critical" => 0, "High" => 1, "Medium" => 2, _ => 3 })
            .ThenBy(r => r.DueDate ?? DateOnly.MaxValue).ToList();
    }

    // ------------------------------------------------------------------ management dashboard

    public async Task<DashboardData> DashboardAsync(BatchFilter f)
    {
        var scope = await ScopeAsync(f with { Metric = null });
        var s = await _state.SettingsAsync();
        var today = _clock.Today;
        var batches = scope.Select(a => a.Batch).ToList();
        int Count(string metric) => scope.Count(a => Matches(a, metric, today, s));

        var released = Count("released");
        var onTime = Count("ontime");
        var kpis = new List<Kpi>
        {
            new("total", "Total batches", scope.Count, "", "total", "All batches in the current filter"),
            new("active", "In pipeline", Count("active"), "", "active", "Not released, rejected or recalled"),
            new("ready", "Ready for release", Count("ready"), "", "ready", "All mandatory items except final QA release are completed"),
            new("released", "Released", released, "", "released", "Final QA release completed (full or conditional)"),
            new("overdue", "Overdue", Count("overdue"), "", "overdue", "Not released and past the target release date"),
            new("ontime", "On-time release", released == 0 ? 0 : Math.Round(onTime * 100m / released, 1), "%", "ontime",
                "Released batches whose final QA release date ≤ target release date"),
            new("stuck", "Stuck", Count("stuck"), "", "stuck", $"Active and no checklist item completed for more than {s.StuckDays} days"),
            new("qe", "Open quality events", batches.Sum(Metrics.OpenQualityEvents), "", "qe", "Deviation, OOS, OOT, CAPA and Change Control with status Open"),
        };

        // Stage order: by the average position of the stage across checklists.
        var stageOrder = batches.SelectMany(Metrics.LiveItems).Where(i => i.IsMandatory)
            .GroupBy(i => i.Stage).OrderBy(g => g.Average(i => i.Sequence)).ThenBy(g => g.Key).Select(g => g.Key).ToList();
        var stages = stageOrder.Select(stage =>
        {
            var durations = batches.SelectMany(b => Metrics.LiveItems(b).Where(i => i.Stage == stage)
                .Select(i => Metrics.ItemDurationDays(i, b))).Where(d => d != null).Select(d => d!.Value).ToList();
            return new StageStat(stage,
                scope.Count(a => Matches(a, "cleared:" + stage, today, s)),
                scope.Count(a => Matches(a, "stage:" + stage, today, s)),
                durations.Count == 0 ? 0 : Math.Round(durations.Average(), 1), durations.Count, "stage:" + stage);
        }).ToList();

        var openEvents = batches.SelectMany(b => b.QualityEvents.Where(e => e.Status == QualityEventStatus.Open).Select(e => (b, e))).ToList();
        var qeByType = Enum.GetValues<QualityEventType>()
            .Select(t => new Bucket(Label(t), openEvents.Count(x => x.e.EventType == t), "qe:" + t)).ToList();
        var qeByBatch = openEvents.GroupBy(x => x.b).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Deadline).Take(10)
            .Select(g => new Bucket(g.Key.BatchNo, g.Count(), "batch:" + g.Key.Id, string.Join(", ", g.Select(x => x.e.EventType).Distinct()))).ToList();

        var expiry = Metrics.ExpiryBuckets.Select(bk => new Bucket(bk, Count("expiry:" + bk), "expiry:" + bk)).ToList();
        var aging = Metrics.AgingBuckets.Select(bk => new Bucket(bk, Count("warehouse:" + bk), "warehouse:" + bk)).ToList();

        var weekly = Periods(scope, today, weekly: true);
        var monthly = Periods(scope, today, weekly: false);

        var breakdown = new List<BreakdownRow>();
        void AddDim(string dim, IEnumerable<IGrouping<(string Key, string Label), BatchAssessment>> groups, string prefix)
        {
            foreach (var g in groups.OrderByDescending(g => g.Count()))
                breakdown.Add(new BreakdownRow(dim, g.Key.Key, g.Key.Label, g.Count(),
                    g.Count(a => Metrics.IsActive(a.Batch)), g.Count(a => Metrics.IsReadyForRelease(a.Batch)),
                    g.Count(a => Metrics.IsReleased(a.Batch)), g.Count(a => Metrics.IsOpenOverdue(a.Batch, today)), $"{prefix}:{g.Key.Key}"));
        }
        AddDim("Site", scope.GroupBy(a => (a.Batch.SiteId.ToString(), a.Batch.Site!.Name)), "site");
        AddDim("Product", scope.GroupBy(a => (a.Batch.ProductId.ToString(), $"{a.Batch.Product!.Name} {a.Batch.Product.Strength}{a.Batch.Product.StrengthUnit}")), "product");
        AddDim("Customer", scope.GroupBy(a => (a.Batch.CustomerId?.ToString() ?? "none", a.Batch.Customer?.Name ?? "(none)")), "customer");
        AddDim("Market", scope.SelectMany(a => a.Batch.Markets.Select(m => (a, m)))
            .GroupBy(x => (x.m.MarketId.ToString(), x.m.Market!.Name), x => x.a), "market");

        var dispositions = scope.GroupBy(a => a.Batch.Disposition).OrderBy(g => g.Key)
            .Select(g => new Bucket(Label(g.Key), g.Count(), "disposition:" + g.Key)).ToList();

        return new DashboardData(today, kpis, stages, qeByType, qeByBatch, expiry, aging, weekly, monthly, breakdown, dispositions);
    }

    static List<PeriodPoint> Periods(List<BatchAssessment> scope, DateOnly today, bool weekly)
    {
        var points = new List<PeriodPoint>();
        if (weekly)
        {
            var start = WeekStart(today).AddDays(-7 * 11);
            for (var w = 0; w < 16; w++)
            {
                var ws = start.AddDays(7 * w);
                var key = ws.ToString("yyyy-MM-dd");
                var planned = scope.Count(a => WeekStart(a.Batch.TargetReleaseDate) == ws);
                var rel = scope.Where(a => a.Batch.ReleasedAt != null && WeekStart(DateOnly.FromDateTime(a.Batch.ReleasedAt.Value.Date)) == ws).ToList();
                points.Add(new PeriodPoint(key, ws.ToString("dd-MM", CultureInfo.InvariantCulture), planned, rel.Count,
                    rel.Count(a => Metrics.IsOnTime(a.Batch)), "planned-week:" + key, "released-week:" + key));
            }
        }
        else
        {
            var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
            for (var m = 0; m < 14; m++)
            {
                var ms = first.AddMonths(m);
                var key = ms.ToString("yyyy-MM");
                var planned = scope.Count(a => a.Batch.TargetReleaseDate.ToString("yyyy-MM") == key);
                var rel = scope.Where(a => a.Batch.ReleasedAt != null && a.Batch.ReleasedAt.Value.ToString("yyyy-MM") == key).ToList();
                points.Add(new PeriodPoint(key, ms.ToString("MMM yyyy", CultureInfo.InvariantCulture), planned, rel.Count,
                    rel.Count(a => Metrics.IsOnTime(a.Batch)), "planned-month:" + key, "released-month:" + key));
            }
        }
        return points;
    }

    public static string Label(Enum e) => e.ToString() switch
    {
        "ChangeControl" => "Change Control",
        "InProgress" => "In Progress",
        "UnderInvestigation" => "Under Investigation",
        "OnHold" => "On Hold",
        "ReleasedForFurtherProcessing" => "Released for Further Processing",
        "PartiallyReleased" => "Partially Released",
        "ConditionallyReleased" => "Conditionally Released",
        var x => x
    };
}
