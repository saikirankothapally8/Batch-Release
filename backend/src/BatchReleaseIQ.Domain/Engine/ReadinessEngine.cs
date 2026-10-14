using System.Globalization;
using BatchReleaseIQ.Domain.Entities;

namespace BatchReleaseIQ.Domain.Engine;

public enum PriorityLevel { Critical, High, Medium, Low }

public record PendingItemInfo(int Id, string Name, string Stage, string Department, string? Market,
    ChecklistItemStatus Status, DateOnly? DueDate, bool IsFinalRelease, string? BlockedReason);

public record BatchAssessment
{
    public required Batch Batch { get; init; }
    public decimal ReadinessPct { get; init; }
    public decimal CompletionPct { get; init; }
    public decimal Penalty { get; init; }
    public bool IsReleased { get; init; }
    public bool IsReadyForRelease { get; init; }
    public string CurrentStage { get; init; } = "";
    public DateOnly DispatchBy { get; init; }
    public DateOnly Deadline { get; init; }
    public DeadlineType DeadlineType { get; init; }
    public int DaysToDeadline { get; init; }
    public decimal EstimatedDaysToRelease { get; init; }
    public DateOnly? EstimatedReleaseDate { get; init; }
    public IReadOnlyList<BatchFlag> Flags { get; init; } = Array.Empty<BatchFlag>();
    public IReadOnlyList<PendingItemInfo> PendingItems { get; init; } = Array.Empty<PendingItemInfo>();
    public int OpenQualityEvents { get; init; }
    public PriorityLevel Priority { get; init; }
    public int Band { get; init; }
    public string Explanation { get; init; } = "";
    public DateTimeOffset LastProgressAt { get; init; }
}

/// <summary>
/// Readiness and prioritisation. Readiness is the weighted share of mandatory checklist items done (excluding the
/// final release step itself), minus
/// penalties for blocked items and open quality events. The queue is ordered by readiness band first and
/// then by the earliest deadline (target release date, or the date the batch drops below the market's
/// minimum shelf life at dispatch, or expiry).
/// </summary>
public static class ReadinessEngine
{
    public static BatchAssessment Assess(Batch b, PrioritySettings s, DateOnly today,
        IReadOnlyDictionary<string, decimal>? averageDaysByCode = null)
    {
        averageDaysByCode ??= new Dictionary<string, decimal>();
        var items = Metrics.LiveItems(b).ToList();
        var mandatory = items.Where(i => i.IsMandatory).ToList();
        var released = Metrics.IsReleased(b);

        // The final release step is the action this score supports, so it is not part of the score:
        // 100% means "everything needed for release is done – release now".
        var scored = mandatory.Where(i => !i.IsFinalRelease).ToList();
        var totalWeight = scored.Sum(i => i.Weight);
        var doneWeight = scored.Where(i => i.IsDone).Sum(i => i.Weight);
        var completion = totalWeight == 0 ? 0 : doneWeight * 100m / totalWeight;

        var openEvents = b.QualityEvents.Where(e => e.Status == QualityEventStatus.Open).ToList();
        var blocked = mandatory.Where(i => i.Status == ChecklistItemStatus.Blocked).ToList();
        var penalty = blocked.Count * s.BlockedItemPenalty
                      + openEvents.Sum(e => e.Criticality == QualityEventCriticality.Critical ? s.CriticalEventPenalty : s.OpenEventPenalty);
        penalty = Math.Min(penalty, s.MaxPenalty);

        var readiness = released ? 100m : Math.Max(0, completion - penalty);
        readiness = Math.Round(readiness, 1);

        var pending = mandatory.Where(i => !i.IsDone)
            .OrderBy(i => i.Sequence).ThenBy(i => i.DueDate ?? DateOnly.MaxValue).ThenBy(i => i.Name)
            .Select(i => new PendingItemInfo(i.Id, i.Name, i.Stage, i.Department?.Name ?? "", i.Market?.Code,
                i.Status, i.DueDate, i.IsFinalRelease, i.BlockedReason))
            .ToList();

        var ready = Metrics.IsReadyForRelease(b);
        var dispatchBy = Metrics.DispatchBy(b);
        var (deadline, deadlineType) = Deadline(b.TargetReleaseDate, dispatchBy, b.ExpiryDate);
        var daysToDeadline = deadline.DayNumber - today.DayNumber;

        var estimate = released ? 0 : EstimateDays(mandatory.Where(i => !i.IsDone), s, averageDaysByCode, today);
        DateOnly? estimatedRelease = released ? null : today.AddDays((int)Math.Ceiling(estimate));

        var flags = new List<BatchFlag>();
        var active = Metrics.IsActive(b);
        var overdue = Metrics.IsOpenOverdue(b, today);
        if (overdue) flags.Add(BatchFlag.Overdue);
        if (active && !overdue && estimatedRelease != null &&
            (estimatedRelease > b.TargetReleaseDate ||
             (b.TargetReleaseDate.DayNumber - today.DayNumber <= s.AtRiskBufferDays && pending.Count(p => !p.IsFinalRelease) > 0)))
            flags.Add(BatchFlag.AtRisk);

        var stillInWarehouse = active || Metrics.UndispatchedQuantity(b) > 0;
        if (Metrics.IsExpired(b, today)) flags.Add(BatchFlag.Expired);
        else if (stillInWarehouse && today > dispatchBy) flags.Add(BatchFlag.ShelfLifeBreached);
        else if (stillInWarehouse && dispatchBy.DayNumber - today.DayNumber <= s.ExpiryWarningDays) flags.Add(BatchFlag.ExpiryNear);

        if (Metrics.IsStuck(b, today, s.StuckDays)) flags.Add(BatchFlag.Stuck);
        if (items.Any(i => i.Status == ChecklistItemStatus.Blocked)) flags.Add(BatchFlag.Blocked);
        if (b.Disposition is BatchDisposition.OnHold or BatchDisposition.Quarantine or BatchDisposition.UnderInvestigation)
            flags.Add(BatchFlag.OnHold);
        if (openEvents.Count > 0) flags.Add(BatchFlag.OpenQualityEvents);
        if (b.Movements.Any(m => m.Status is MovementStatus.SentOut or MovementStatus.AtVendor)) flags.Add(BatchFlag.AtVendor);
        if (b.ChangeRequests.Any(c => c.Status == ChangeRequestStatus.Pending)) flags.Add(BatchFlag.PendingChange);

        var priority = PriorityFor(flags, daysToDeadline, active);
        var band = s.ReadinessBandSize <= 0 ? (int)readiness : (int)Math.Floor(readiness / s.ReadinessBandSize);

        var assessment = new BatchAssessment
        {
            Batch = b,
            ReadinessPct = readiness,
            CompletionPct = Math.Round(completion, 1),
            Penalty = released ? 0 : penalty,
            IsReleased = released,
            IsReadyForRelease = ready,
            CurrentStage = CurrentStage(b, items, released, ready),
            DispatchBy = dispatchBy,
            Deadline = deadline,
            DeadlineType = deadlineType,
            DaysToDeadline = daysToDeadline,
            EstimatedDaysToRelease = Math.Round(estimate, 1),
            EstimatedReleaseDate = estimatedRelease,
            Flags = flags,
            PendingItems = pending,
            OpenQualityEvents = openEvents.Count,
            Priority = priority,
            Band = band,
            LastProgressAt = Metrics.LastProgress(b)
        };
        return assessment with { Explanation = Explain(assessment, openEvents, today) };
    }

    /// <summary>Queue order: releasable before held, then readiness band (high first), then deadline, then readiness.</summary>
    public static List<BatchAssessment> Rank(IEnumerable<BatchAssessment> assessments) =>
        assessments
            .OrderBy(a => a.Flags.Contains(BatchFlag.OnHold) ? 1 : 0)
            .ThenByDescending(a => a.Band)
            .ThenBy(a => a.Deadline)
            .ThenByDescending(a => a.ReadinessPct)
            .ThenBy(a => a.Batch.BatchNo, StringComparer.Ordinal)
            .ToList();

    /// <summary>Default weight for an item: rises linearly from the first to the last stage.</summary>
    public static decimal DefaultWeight(int sequence, int minSequence, int maxSequence, PrioritySettings s)
    {
        if (maxSequence <= minSequence) return s.MaxStageWeight;
        var position = (decimal)(sequence - minSequence) / (maxSequence - minSequence);
        return Math.Round(s.MinStageWeight + (s.MaxStageWeight - s.MinStageWeight) * position, 2);
    }

    public static (DateOnly, DeadlineType) Deadline(DateOnly target, DateOnly dispatchBy, DateOnly expiry)
    {
        var result = (target, DeadlineType.TargetRelease);
        if (dispatchBy < result.target) result = (dispatchBy, DeadlineType.ShelfLifeDispatchLimit);
        if (expiry < result.Item1) result = (expiry, DeadlineType.Expiry);
        return result;
    }

    /// <summary>
    /// Remaining items are grouped by sequence: items in a group run in parallel, so the group takes as long as its
    /// slowest item; groups run one after another. Item durations are historical averages for the same item code.
    /// </summary>
    public static decimal EstimateDays(IEnumerable<ChecklistItem> remaining, PrioritySettings s,
        IReadOnlyDictionary<string, decimal> averageDaysByCode, DateOnly today)
    {
        return remaining.GroupBy(i => i.Sequence).Sum(g => g.Max(i =>
        {
            var avg = averageDaysByCode.TryGetValue(i.Code, out var d) ? d : s.DefaultItemDurationDays;
            if (i.Status == ChecklistItemStatus.InProgress && i.StartedAt != null)
            {
                var elapsed = today.DayNumber - DateOnly.FromDateTime(i.StartedAt.Value.Date).DayNumber;
                avg = Math.Max(0.5m, avg - elapsed);
            }
            return avg;
        }));
    }

    static string CurrentStage(Batch b, List<ChecklistItem> items, bool released, bool ready)
    {
        if (b.Disposition == BatchDisposition.Rejected) return "Rejected";
        if (b.Disposition == BatchDisposition.Recalled) return "Recalled";
        if (released) return "Released";
        if (ready) return "Ready for release";
        var next = items.Where(i => i.IsMandatory && !i.IsDone).OrderBy(i => i.Sequence).FirstOrDefault();
        return next == null ? "Not started" : next.Stage;
    }

    static PriorityLevel PriorityFor(List<BatchFlag> flags, int daysToDeadline, bool active)
    {
        if (!active) return PriorityLevel.Low;
        if (flags.Contains(BatchFlag.Overdue) || flags.Contains(BatchFlag.Expired) ||
            flags.Contains(BatchFlag.ShelfLifeBreached) || daysToDeadline <= 3) return PriorityLevel.Critical;
        if (flags.Contains(BatchFlag.AtRisk) || flags.Contains(BatchFlag.ExpiryNear) || daysToDeadline <= 7) return PriorityLevel.High;
        if (daysToDeadline <= 21) return PriorityLevel.Medium;
        return PriorityLevel.Low;
    }

    public static string FormatDate(DateOnly d) => d.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    static string Days(int n) => n == 1 ? "1 day" : $"{n} days";

    static string Explain(BatchAssessment a, List<QualityEvent> openEvents, DateOnly today)
    {
        var b = a.Batch;
        if (b.Disposition == BatchDisposition.Rejected) return $"Rejected: {b.DispositionReason}";
        if (a.IsReleased)
            return $"Released {FormatDate(DateOnly.FromDateTime(b.ReleasedAt!.Value.Date))}" +
                   (Metrics.IsOnTime(b) ? " (on time)" : " (after target)");

        var parts = new List<string> { $"{a.ReadinessPct:0.#}% ready" };
        var notFinal = a.PendingItems.Where(p => !p.IsFinalRelease).ToList();
        if (a.IsReadyForRelease) parts.Add("only final QA release pending");
        else if (notFinal.Count > 0)
        {
            var names = notFinal.Take(2).Select(p => p.Market == null ? p.Name : $"{p.Name} ({p.Market})");
            var more = notFinal.Count > 2 ? $" +{notFinal.Count - 2} more" : "";
            parts.Add($"{notFinal.Count} item{(notFinal.Count == 1 ? "" : "s")} pending: {string.Join(", ", names)}{more}");
        }

        var blocked = a.PendingItems.FirstOrDefault(p => p.Status == ChecklistItemStatus.Blocked);
        if (blocked != null) parts.Add($"blocked: {blocked.Name}{(string.IsNullOrEmpty(blocked.BlockedReason) ? "" : " – " + blocked.BlockedReason)}");

        if (openEvents.Count > 0)
            parts.Add($"{openEvents.Count} open quality event{(openEvents.Count == 1 ? "" : "s")} ({string.Join(", ", openEvents.Take(2).Select(e => e.Number))})");

        var nextDue = notFinal.Where(p => p.DueDate != null).OrderBy(p => p.DueDate).FirstOrDefault();
        if (nextDue != null)
        {
            var d = nextDue.DueDate!.Value.DayNumber - today.DayNumber;
            parts.Add(d < 0 ? $"{nextDue.Name} overdue by {Days(-d)}" : d == 0 ? $"{nextDue.Name} due today" : $"{nextDue.Name} due in {Days(d)}");
        }

        var what = a.DeadlineType switch
        {
            DeadlineType.TargetRelease => "target release",
            DeadlineType.ShelfLifeDispatchLimit => $"{Metrics.MinShelfLifePct(b)}% shelf-life dispatch limit",
            _ => "expiry"
        };
        parts.Add(a.DaysToDeadline < 0
            ? $"{what} {FormatDate(a.Deadline)} passed {Days(-a.DaysToDeadline)} ago"
            : $"{what} {FormatDate(a.Deadline)} in {Days(a.DaysToDeadline)}");

        if (b.Disposition is BatchDisposition.OnHold or BatchDisposition.Quarantine or BatchDisposition.UnderInvestigation)
            parts.Insert(0, $"{b.Disposition}: {b.DispositionReason}");
        return string.Join(" · ", parts);
    }
}
