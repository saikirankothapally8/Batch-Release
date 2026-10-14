using BatchReleaseIQ.Domain.Entities;

namespace BatchReleaseIQ.Domain.Engine;

public record TemplateCriteria(int ProductId, string DosageForm, IReadOnlyCollection<int> MarketIds, int? CustomerId,
    BatchType BatchType, SiteType SiteType);

public record TemplateMatch(ChecklistTemplate Template, int Score);

public static class TemplateMatcher
{
    /// <summary>
    /// A template is a candidate when every criterion it sets matches the batch. The most specific candidate
    /// (most criteria set) wins; ties go to the higher priority, then the most recently updated template.
    /// </summary>
    public static List<TemplateMatch> Candidates(IEnumerable<ChecklistTemplate> templates, TemplateCriteria c)
    {
        var result = new List<TemplateMatch>();
        foreach (var t in templates.Where(t => t.IsActive))
        {
            var score = 0;
            if (t.ProductId != null) { if (t.ProductId != c.ProductId) continue; score += 4; }
            if (t.CustomerId != null) { if (t.CustomerId != c.CustomerId) continue; score += 3; }
            if (t.MarketId != null) { if (!c.MarketIds.Contains(t.MarketId.Value)) continue; score += 2; }
            if (t.BatchType != null) { if (t.BatchType != c.BatchType) continue; score += 3; }
            if (t.SiteType != null) { if (t.SiteType != c.SiteType) continue; score += 2; }
            if (t.DosageForm != null) { if (!string.Equals(t.DosageForm, c.DosageForm, StringComparison.OrdinalIgnoreCase)) continue; score += 1; }
            result.Add(new TemplateMatch(t, score));
        }
        return result.OrderByDescending(m => m.Score).ThenByDescending(m => m.Template.Priority).ThenByDescending(m => m.Template.UpdatedAt).ToList();
    }
}
