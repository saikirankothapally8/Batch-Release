using BatchReleaseIQ.Domain.Entities;

namespace BatchReleaseIQ.Domain.Engine;

/// <summary>
/// The single definition of every metric. KPIs, charts, drill-down lists and the queue all call these,
/// so a number on one screen always matches the same number on another.
/// </summary>
public static class Metrics
{
    public const int DefaultMinShelfLifePct = 75;

    /// <summary>Released = final QA release completed (full or conditional).</summary>
    public static bool IsReleased(Batch b) => b.ReleasedAt != null;

    /// <summary>Partially released: some quantity released, final QA release not yet completed.</summary>
    public static bool IsPartiallyReleased(Batch b) => !IsReleased(b) && b.Disposition == BatchDisposition.PartiallyReleased;

    public static bool IsClosedWithoutRelease(Batch b) => b.Disposition is BatchDisposition.Rejected;

    /// <summary>Active = still on its way to release (not released, not rejected).</summary>
    public static bool IsActive(Batch b) => !IsReleased(b) && !IsClosedWithoutRelease(b) && b.Disposition != BatchDisposition.Recalled;

    /// <summary>Ready for Release = every mandatory item except final QA release is Completed or Not Applicable.</summary>
    public static bool IsReadyForRelease(Batch b)
    {
        if (!IsActive(b)) return false;
        var mandatory = LiveItems(b).Where(i => i.IsMandatory && !i.IsFinalRelease).ToList();
        return mandatory.Count > 0 && mandatory.All(i => i.IsDone);
    }

    /// <summary>Overdue = not released on or before the target release date.</summary>
    public static bool IsOverdue(Batch b, DateOnly today) =>
        IsReleased(b) ? DateOnly.FromDateTime(b.ReleasedAt!.Value.Date) > b.TargetReleaseDate
                      : IsActive(b) && today > b.TargetReleaseDate;

    /// <summary>Currently overdue: active and past the target release date.</summary>
    public static bool IsOpenOverdue(Batch b, DateOnly today) => IsActive(b) && today > b.TargetReleaseDate;

    /// <summary>On-time release = final QA release date on or before the target release date.</summary>
    public static bool IsOnTime(Batch b) =>
        IsReleased(b) && DateOnly.FromDateTime(b.ReleasedAt!.Value.Date) <= b.TargetReleaseDate;

    /// <summary>Stuck = active and no checklist item completed for more than N days.</summary>
    public static bool IsStuck(Batch b, DateOnly today, int stuckDays) =>
        IsActive(b) && DaysSinceProgress(b, today) > stuckDays;

    public static int DaysSinceProgress(Batch b, DateOnly today)
    {
        var last = LastProgress(b);
        return today.DayNumber - DateOnly.FromDateTime(last.Date).DayNumber;
    }

    public static DateTimeOffset LastProgress(Batch b) =>
        LiveItems(b).Where(i => i.CompletedAt != null).Select(i => i.CompletedAt!.Value)
            .DefaultIfEmpty(b.CreatedAt).Max();

    public static bool IsExpired(Batch b, DateOnly today) => today > b.ExpiryDate;

    public static int MinShelfLifePct(Batch b) =>
        b.Markets.Count == 0 ? DefaultMinShelfLifePct
            : b.Markets.Max(m => m.Market?.MinShelfLifePctAtDispatch ?? DefaultMinShelfLifePct);

    public static DateOnly DispatchBy(Batch b) => ShelfLife.DispatchBy(b.ManufacturingDate, b.ExpiryDate, MinShelfLifePct(b));

    public static decimal ReleasedQuantity(Batch b) => b.Releases.Where(r => r.Kind != ReleaseKind.FurtherProcessing).Sum(r => r.Quantity);

    public static decimal DispatchedQuantity(Batch b) => b.Dispatches.Sum(d => d.Quantity);

    /// <summary>Released (fully or partly) quantity still sitting in the warehouse.</summary>
    public static decimal UndispatchedQuantity(Batch b) => Math.Max(0, ReleasedQuantity(b) - DispatchedQuantity(b));

    /// <summary>Warehouse age = days since first release decision, while released stock is undispatched.</summary>
    public static int? WarehouseAgeDays(Batch b, DateOnly today)
    {
        if (UndispatchedQuantity(b) <= 0) return null;
        var first = b.Releases.Where(r => r.Kind != ReleaseKind.FurtherProcessing).Min(r => (DateTimeOffset?)r.ReleasedAt);
        return first == null ? null : today.DayNumber - DateOnly.FromDateTime(first.Value.Date).DayNumber;
    }

    public static int OpenQualityEvents(Batch b) => b.QualityEvents.Count(e => e.Status == QualityEventStatus.Open);

    public static IEnumerable<ChecklistItem> LiveItems(Batch b) => b.Items.Where(i => !i.IsRemoved);

    // ---- Buckets used by charts; drill-down uses the same functions ----

    public static readonly string[] ExpiryBuckets = { "Expired", "Shelf-life limit passed", "Within 30 days", "31-60 days", "61-90 days", "Over 90 days" };

    /// <summary>Bucket by days remaining until the shelf-life dispatch limit (or expiry once that has passed).</summary>
    public static string ExpiryBucket(Batch b, DateOnly today)
    {
        if (IsExpired(b, today)) return ExpiryBuckets[0];
        var days = DispatchBy(b).DayNumber - today.DayNumber;
        if (days < 0) return ExpiryBuckets[1];
        if (days <= 30) return ExpiryBuckets[2];
        if (days <= 60) return ExpiryBuckets[3];
        if (days <= 90) return ExpiryBuckets[4];
        return ExpiryBuckets[5];
    }

    public static readonly string[] AgingBuckets = { "0-3 days", "4-7 days", "8-14 days", "15-30 days", "Over 30 days" };

    public static string AgingBucket(int days) => days switch
    {
        <= 3 => AgingBuckets[0],
        <= 7 => AgingBuckets[1],
        <= 14 => AgingBuckets[2],
        <= 30 => AgingBuckets[3],
        _ => AgingBuckets[4]
    };

    /// <summary>Duration of a completed item in days: from start (or batch creation) to completion.</summary>
    public static decimal? ItemDurationDays(ChecklistItem i, Batch b)
    {
        if (i.CompletedAt == null || i.Status != ChecklistItemStatus.Completed) return null;
        var start = i.StartedAt ?? b.CreatedAt;
        var days = (decimal)(i.CompletedAt.Value - start).TotalDays;
        return days < 0 ? null : Math.Round(days, 2);
    }
}
