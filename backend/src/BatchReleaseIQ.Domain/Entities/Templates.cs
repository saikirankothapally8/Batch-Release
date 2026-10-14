namespace BatchReleaseIQ.Domain.Entities;

/// <summary>
/// A reusable checklist. Null match criteria mean "any". When a batch is created, every active template whose
/// non-null criteria all match is a candidate, and the one with the most matching criteria wins.
/// </summary>
public class ChecklistTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    /// <summary>Tie-break when several equally specific templates match (e.g. a batch for two markets). Higher wins.</summary>
    public int Priority { get; set; }

    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public string? DosageForm { get; set; }
    public int? MarketId { get; set; }
    public Market? Market { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public BatchType? BatchType { get; set; }
    public SiteType? SiteType { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
    public List<TemplateItem> Items { get; set; } = new();
}

public class TemplateItem
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    /// <summary>Stable key, used for dependencies and for historical duration estimates.</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Stage { get; set; } = "";
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    public ChecklistItemType ItemType { get; set; }
    public bool IsMandatory { get; set; } = true;
    /// <summary>Null = derived from the stage position (later stages weigh more).</summary>
    public decimal? Weight { get; set; }
    /// <summary>Items with the same sequence may run in parallel.</summary>
    public int Sequence { get; set; }
    /// <summary>Due date relative to the batch target release date (negative = before).</summary>
    public int DueOffsetDays { get; set; }
    public bool IsFinalRelease { get; set; }
    /// <summary>Create one item per target market of the batch (e.g. regulatory approval).</summary>
    public bool IsPerMarket { get; set; }
    public DataSource DefaultSource { get; set; } = DataSource.Manual;
    /// <summary>Comma-separated codes of items that must be complete before this one can start.</summary>
    public string DependsOn { get; set; } = "";
}
