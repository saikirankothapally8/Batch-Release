using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Domain.Engine;
using BatchReleaseIQ.Domain.Entities;

namespace BatchReleaseIQ.Tests;

public class EngineTests
{
    static readonly DateOnly Today = new(2026, 10, 14);
    static readonly PrioritySettings S = new();

    static Batch NewBatch(string no, DateOnly target, params (string Code, int Seq, decimal Weight, ChecklistItemStatus Status, bool Final)[] items)
    {
        var mfg = Today.AddDays(-20);
        var b = new Batch
        {
            BatchNo = no, ManufacturingDate = mfg, ExpiryDate = ShelfLife.CalculateExpiry(mfg, 24), ShelfLifeMonths = 24,
            TargetReleaseDate = target, CreatedAt = new DateTimeOffset(mfg.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), BatchSize = 1000
        };
        var id = 1;
        foreach (var (code, seq, w, st, final) in items)
            b.Items.Add(new ChecklistItem { Id = id++, Code = code, Name = code, Stage = code, Sequence = seq, Weight = w, Status = st, IsFinalRelease = final,
                CompletedAt = st == ChecklistItemStatus.Completed ? new DateTimeOffset(Today.AddDays(-2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null });
        return b;
    }

    const ChecklistItemStatus Done = ChecklistItemStatus.Completed, Open = ChecklistItemStatus.NotStarted;

    [Fact]
    public void Expiry_is_last_day_of_month_before_mfg_month_plus_shelf_life()
    {
        Assert.Equal(new DateOnly(2027, 12, 31), ShelfLife.CalculateExpiry(new DateOnly(2026, 1, 15), 24));
        Assert.Equal(new DateOnly(2029, 2, 28), ShelfLife.CalculateExpiry(new DateOnly(2026, 3, 1), 36));
    }

    [Fact]
    public void Dispatch_by_keeps_required_share_of_shelf_life()
    {
        var mfg = new DateOnly(2026, 1, 1);
        var exp = new DateOnly(2027, 12, 31); // 729 days
        var by = ShelfLife.DispatchBy(mfg, exp, 75);
        Assert.Equal(exp.AddDays(-547), by); // ceil(729 * 0.75) = 547
        Assert.True(ShelfLife.RemainingPct(mfg, exp, by) >= 75);
    }

    [Fact]
    public void Readiness_is_weighted_and_excludes_final_release()
    {
        var b = NewBatch("A", Today.AddDays(10), ("MFG", 1, 1, Done, false), ("QC", 2, 2, Done, false), ("QA", 3, 3, Open, false), ("REL", 4, 3, Open, true));
        var a = ReadinessEngine.Assess(b, S, Today);
        Assert.Equal(50m, a.ReadinessPct); // (1+2)/(1+2+3)
        Assert.False(a.IsReadyForRelease);

        b.Items.Single(i => i.Code == "QA").Status = Done;
        a = ReadinessEngine.Assess(b, S, Today);
        Assert.Equal(100m, a.ReadinessPct);
        Assert.True(a.IsReadyForRelease);
        Assert.Equal("Ready for release", a.CurrentStage);
        Assert.Contains("only final QA release pending", a.Explanation);
    }

    [Fact]
    public void Blocked_items_and_open_events_lower_readiness()
    {
        var b = NewBatch("A", Today.AddDays(10), ("MFG", 1, 1, Done, false), ("QC", 2, 1, ChecklistItemStatus.Blocked, false), ("REL", 3, 1, Open, true));
        b.Items[1].BlockedReason = "Awaiting OOS investigation";
        b.QualityEvents.Add(new QualityEvent { Number = "DEV-1", Status = QualityEventStatus.Open, Criticality = QualityEventCriticality.Minor });
        var a = ReadinessEngine.Assess(b, S, Today);
        Assert.Equal(50m - S.BlockedItemPenalty - S.OpenEventPenalty, a.ReadinessPct);
        Assert.Contains(BatchFlag.Blocked, a.Flags);
        Assert.Contains(BatchFlag.OpenQualityEvents, a.Flags);
        Assert.Contains("Awaiting OOS investigation", a.Explanation);
        Assert.Contains("DEV-1", a.Explanation);
    }

    [Fact]
    public void Queue_ranks_by_readiness_band_then_deadline_and_puts_holds_last()
    {
        var nearReadyLate = NewBatch("NEAR-LATE", Today.AddDays(20), ("A", 1, 1, Done, false), ("B", 2, 1, Done, false), ("C", 2, 1, Done, false), ("D", 3, 1, Open, false), ("R", 4, 1, Open, true)); // 75%
        var nearReadySoon = NewBatch("NEAR-SOON", Today.AddDays(5), ("A", 1, 1, Done, false), ("B", 2, 1, Done, false), ("C", 2, 1, Done, false), ("D", 3, 1, Open, false), ("R", 4, 1, Open, true)); // 75%
        var earlyUrgent = NewBatch("EARLY", Today.AddDays(1), ("A", 1, 1, Open, false), ("R", 2, 1, Open, true)); // 0%
        var ready = NewBatch("READY", Today.AddDays(30), ("A", 1, 1, Done, false), ("R", 2, 1, Open, true)); // 100%
        var held = NewBatch("HELD", Today.AddDays(30), ("A", 1, 1, Done, false), ("R", 2, 1, Open, true));
        held.Disposition = BatchDisposition.OnHold;

        var ranked = ReadinessEngine.Rank(new[] { earlyUrgent, nearReadyLate, held, ready, nearReadySoon }.Select(b => ReadinessEngine.Assess(b, S, Today)));
        Assert.Equal(new[] { "READY", "NEAR-SOON", "NEAR-LATE", "EARLY", "HELD" }, ranked.Select(a => a.Batch.BatchNo));
    }

    [Fact]
    public void Deadline_is_shelf_life_dispatch_limit_when_earlier_than_target()
    {
        var b = NewBatch("A", Today.AddDays(400), ("A", 1, 1, Open, false), ("R", 2, 1, Open, true));
        var a = ReadinessEngine.Assess(b, S, Today);
        Assert.Equal(DeadlineType.ShelfLifeDispatchLimit, a.DeadlineType);
        Assert.Equal(Metrics.DispatchBy(b), a.Deadline);
    }

    [Fact]
    public void Estimate_runs_parallel_items_concurrently_using_history()
    {
        var b = NewBatch("A", Today.AddDays(10), ("QC", 1, 1, Open, false), ("RA", 1, 1, Open, false), ("QA", 2, 1, Open, false), ("REL", 3, 1, Open, true));
        var avg = new Dictionary<string, decimal> { ["QC"] = 6, ["RA"] = 4, ["QA"] = 2, ["REL"] = 1 };
        var a = ReadinessEngine.Assess(b, S, Today, avg);
        Assert.Equal(9m, a.EstimatedDaysToRelease); // max(6,4) + 2 + 1
        Assert.Equal(Today.AddDays(9), a.EstimatedReleaseDate);
    }

    [Fact]
    public void At_risk_when_estimate_passes_target()
    {
        var b = NewBatch("A", Today.AddDays(3), ("QC", 1, 1, Open, false), ("REL", 2, 1, Open, true));
        var a = ReadinessEngine.Assess(b, S, Today, new Dictionary<string, decimal> { ["QC"] = 6 });
        Assert.Contains(BatchFlag.AtRisk, a.Flags);
        Assert.DoesNotContain(BatchFlag.Overdue, a.Flags);
    }

    [Fact]
    public void Metric_definitions_for_overdue_on_time_and_stuck()
    {
        var b = NewBatch("A", Today.AddDays(-1), ("A", 1, 1, Done, false), ("R", 2, 1, Open, true));
        Assert.True(Metrics.IsOpenOverdue(b, Today));
        Assert.True(Metrics.IsReadyForRelease(b));
        Assert.False(Metrics.IsStuck(b, Today, 7)); // last completion 2 days ago

        b.ReleasedAt = new DateTimeOffset(Today.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
        Assert.True(Metrics.IsReleased(b));
        Assert.False(Metrics.IsOnTime(b));
        Assert.False(Metrics.IsOpenOverdue(b, Today));
        Assert.True(Metrics.IsOverdue(b, Today));

        var stale = NewBatch("B", Today.AddDays(5), ("A", 1, 1, Open, false), ("R", 2, 1, Open, true));
        Assert.True(Metrics.IsStuck(stale, Today, 7)); // created 20 days ago, nothing completed
    }

    [Fact]
    public void Default_weight_rises_towards_final_release()
    {
        Assert.Equal(1m, ReadinessEngine.DefaultWeight(1, 1, 5, S));
        Assert.Equal(2m, ReadinessEngine.DefaultWeight(3, 1, 5, S));
        Assert.Equal(3m, ReadinessEngine.DefaultWeight(5, 1, 5, S));
    }

    [Fact]
    public void Most_specific_matching_template_wins()
    {
        var generic = new ChecklistTemplate { Id = 1, Name = "Generic", DosageForm = "Tablet", SiteType = SiteType.Own };
        var us = new ChecklistTemplate { Id = 2, Name = "US", DosageForm = "Tablet", SiteType = SiteType.Own, MarketId = 10 };
        var cmo = new ChecklistTemplate { Id = 3, Name = "CMO", SiteType = SiteType.ContractManufacturer };
        var c = new TemplateCriteria(1, "Tablet", new[] { 10, 11 }, null, BatchType.Commercial, SiteType.Own);
        var matches = TemplateMatcher.Candidates(new[] { generic, us, cmo }, c);
        Assert.Equal(new[] { "US", "Generic" }, matches.Select(m => m.Template.Name));
    }
}
