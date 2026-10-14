namespace BatchReleaseIQ.Domain.Entities;

public class Department
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class Site
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public SiteType SiteType { get; set; }
    /// <summary>For contract manufacturing sites: the external party that operates the site.</summary>
    public int? ExternalPartyId { get; set; }
    public ExternalParty? ExternalParty { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Contract manufacturer, contract testing lab, packaging/labelling vendor or distributor.</summary>
public class ExternalParty
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public ExternalPartyType PartyType { get; set; }
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public bool IsApproved { get; set; } = true;
    public DateOnly? QualityAgreementValidUntil { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Product
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Strength { get; set; } = "";
    public string StrengthUnit { get; set; } = "mg";
    public string DosageForm { get; set; } = "Tablet";
    public string ProductType { get; set; } = "Generic";
    public string PackSize { get; set; } = "";
    public int ShelfLifeMonths { get; set; } = 24;
    /// <summary>Prefix used to build batch numbers, e.g. MET.</summary>
    public string BatchPrefix { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Market
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string RegulatoryAuthority { get; set; } = "";
    /// <summary>Minimum remaining shelf life (percent of total) required at dispatch.</summary>
    public int MinShelfLifePctAtDispatch { get; set; } = 75;
    public bool IsActive { get; set; } = true;
}

public class Customer
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Country { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
