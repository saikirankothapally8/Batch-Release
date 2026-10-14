namespace BatchReleaseIQ.Domain.Engine;

public static class ShelfLife
{
    /// <summary>
    /// Expiry is the last day of the month before (manufacturing month + shelf life),
    /// e.g. manufactured 15-01-2026 with 24 months shelf life expires 31-12-2027.
    /// </summary>
    public static DateOnly CalculateExpiry(DateOnly manufacturingDate, int shelfLifeMonths)
    {
        var firstOfMfgMonth = new DateOnly(manufacturingDate.Year, manufacturingDate.Month, 1);
        return firstOfMfgMonth.AddMonths(shelfLifeMonths).AddDays(-1);
    }

    /// <summary>Latest date the batch can be dispatched while keeping <paramref name="minRemainingPct"/> of its shelf life.</summary>
    public static DateOnly DispatchBy(DateOnly manufacturingDate, DateOnly expiryDate, int minRemainingPct)
    {
        var totalDays = expiryDate.DayNumber - manufacturingDate.DayNumber;
        var requiredDays = (int)Math.Ceiling(totalDays * minRemainingPct / 100m);
        return expiryDate.AddDays(-requiredDays);
    }

    public static decimal RemainingPct(DateOnly manufacturingDate, DateOnly expiryDate, DateOnly onDate)
    {
        var total = expiryDate.DayNumber - manufacturingDate.DayNumber;
        if (total <= 0) return 0;
        var remaining = Math.Max(0, expiryDate.DayNumber - onDate.DayNumber);
        return Math.Round(remaining * 100m / total, 1);
    }
}
