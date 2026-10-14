using BatchReleaseIQ.Domain;
using BatchReleaseIQ.Domain.Engine;
using BatchReleaseIQ.Domain.Entities;
using BatchReleaseIQ.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace BatchReleaseIQ.Infrastructure.Data;

/// <summary>
/// Realistic demo data: tablet products, three own sites plus a contract manufacturer, six markets, and batches in
/// every state. Progress is simulated in time (each item takes a realistic number of days), so stage durations,
/// delays, stuck batches and historical estimates are coherent rather than random.
/// </summary>
public class SampleData
{
    public const string DemoPassword = "Passw0rd!";

    readonly AppDbContext _db;
    readonly PasswordService _passwords;
    readonly IClock _clock;
    readonly Random _rnd = new(20261014);
    DateTimeOffset _now;
    DateOnly _today;

    readonly int _batchCount;

    public SampleData(AppDbContext db, PasswordService passwords, IClock clock, int batchCount = 250)
    {
        _db = db; _passwords = passwords; _clock = clock; _batchCount = batchCount;
    }

    Dictionary<string, Department> _dept = new();
    Dictionary<string, Site> _sites = new();
    Dictionary<string, ExternalParty> _parties = new();
    Dictionary<string, Product> _products = new();
    Dictionary<string, Market> _markets = new();
    Dictionary<string, Customer> _customers = new();
    Dictionary<string, User> _users = new();
    List<ChecklistTemplate> _templates = new();
    PrioritySettings _settings = new();
    int _eventSeq = 1;

    static readonly Dictionary<string, double> MeanDays = new()
    {
        ["MFG_BMR"] = 3, ["PKG_BPR"] = 2.5, ["QC_LIMS"] = 6, ["QC_RETAIN"] = 1, ["QMS_CHECK"] = 2, ["RA_APPROVAL"] = 4,
        ["ART_VERIFY"] = 1.5, ["SERIALIZATION"] = 1.5, ["QA_REVIEW"] = 2, ["QA_RELEASE"] = 1,
        ["CMO_BMR"] = 3, ["CMO_COA"] = 2, ["CMO_QA_AGREEMENT"] = 1, ["GRN_RECEIPT"] = 2, ["QC_ID_TEST"] = 3,
        ["CT_SAMPLES_SENT"] = 1, ["CT_COA"] = 8, ["PARENT_RELEASE"] = 0.5, ["LBL_ARTWORK"] = 3, ["LBL_SENT"] = 1,
        ["LBL_RECON"] = 4, ["LBL_RETURN"] = 2, ["INV_APPROVED"] = 6, ["RW_PROTOCOL"] = 3, ["RW_BMR"] = 3,
    };

    public async Task SeedAsync()
    {
        if (await _db.Users.AnyAsync()) return;
        _now = _clock.Now;
        _today = _clock.Today;

        SeedDepartments();
        SeedMasterData();
        SeedSecurity(demoUsers: true, DemoPassword);
        _db.PrioritySettings.Add(_settings);
        await _db.SaveChangesAsync();
        SeedTemplates();
        await _db.SaveChangesAsync();
        await SeedBatchesAsync();
    }

    /// <summary>
    /// For a real installation (no demo data): departments, the standard roles, prioritisation settings and one
    /// administrator, so that someone can sign in and set up master data, templates and users. Runs only on an
    /// empty database.
    /// </summary>
    public async Task SeedEssentialsAsync(string adminPassword)
    {
        if (await _db.Users.AnyAsync()) return;
        PasswordService.ValidatePolicy(adminPassword);
        _now = _clock.Now;
        _today = _clock.Today;
        SeedDepartments();
        SeedSecurity(demoUsers: false, adminPassword);
        _db.PrioritySettings.Add(_settings);
        await _db.SaveChangesAsync();
    }

    // ------------------------------------------------------------------ master data

    void SeedDepartments()
    {
        foreach (var (code, name) in new[] { ("MFG", "Manufacturing"), ("PKG", "Packaging"), ("QC", "Quality Control"),
                     ("QMS", "Quality Systems (QMS)"), ("RA", "Regulatory Affairs"), ("QA", "Quality Assurance"),
                     ("WH", "Warehouse"), ("SCM", "Supply Chain") })
            _dept[code] = new Department { Code = code, Name = name };
        _db.Departments.AddRange(_dept.Values);
    }

    void SeedMasterData()
    {

        _parties["CMO1"] = new ExternalParty { Code = "CMO1", Name = "Medisynth Contract Pharma", PartyType = ExternalPartyType.ContractManufacturer, City = "Ahmedabad", Country = "India", QualityAgreementValidUntil = _today.AddMonths(14) };
        _parties["LAB1"] = new ExternalParty { Code = "LAB1", Name = "Spectra Analytical Labs", PartyType = ExternalPartyType.ContractLab, City = "Hyderabad", Country = "India", QualityAgreementValidUntil = _today.AddMonths(8) };
        _parties["LAB2"] = new ExternalParty { Code = "LAB2", Name = "MicroBio Testing Services", PartyType = ExternalPartyType.ContractLab, City = "Bengaluru", Country = "India", QualityAgreementValidUntil = _today.AddMonths(20) };
        _parties["LBL1"] = new ExternalParty { Code = "LBL1", Name = "PrimePack Labelling Services", PartyType = ExternalPartyType.LabellingVendor, City = "Pune", Country = "India", QualityAgreementValidUntil = _today.AddMonths(11) };
        _parties["PKG1"] = new ExternalParty { Code = "PKG1", Name = "Coastal Blister Packaging", PartyType = ExternalPartyType.PackagingVendor, City = "Visakhapatnam", Country = "India", QualityAgreementValidUntil = _today.AddMonths(5) };
        _parties["PKG2"] = new ExternalParty { Code = "PKG2", Name = "Unity Cartons (audit pending)", PartyType = ExternalPartyType.PackagingVendor, City = "Chennai", Country = "India", IsApproved = false };
        _db.ExternalParties.AddRange(_parties.Values);

        _sites["HYD1"] = new Site { Code = "HYD1", Name = "Hyderabad Unit-I", City = "Hyderabad", Country = "India", SiteType = SiteType.Own };
        _sites["VZG2"] = new Site { Code = "VZG2", Name = "Visakhapatnam Unit-II", City = "Visakhapatnam", Country = "India", SiteType = SiteType.Own };
        _sites["BDI3"] = new Site { Code = "BDI3", Name = "Baddi Unit-III", City = "Baddi", Country = "India", SiteType = SiteType.Own };
        _sites["CMO1"] = new Site { Code = "CMO-AHD", Name = "Medisynth (CMO) Ahmedabad", City = "Ahmedabad", Country = "India", SiteType = SiteType.ContractManufacturer, ExternalParty = _parties["CMO1"] };
        _db.Sites.AddRange(_sites.Values);

        void P(string code, string name, string strength, string pack, int shelf, string prefix, string type = "Generic Rx") =>
            _products[code] = new Product { Code = code, Name = name, Strength = strength, StrengthUnit = "mg", DosageForm = "Tablet", ProductType = type, PackSize = pack, ShelfLifeMonths = shelf, BatchPrefix = prefix };
        P("MET500", "Metformin Hydrochloride Tablets", "500", "10 x 10 blister", 36, "MET");
        P("CTZ10", "Cetirizine Dihydrochloride Tablets", "10", "3 x 10 blister", 36, "CTZ", "OTC");
        P("PCM500", "Paracetamol Tablets", "500", "Bottle of 100", 36, "PCM", "OTC");
        P("PNT40", "Pantoprazole Gastro-resistant Tablets", "40", "3 x 10 blister", 24, "PNT");
        P("AZM500", "Azithromycin Tablets", "500", "1 x 3 blister", 24, "AZM");
        P("ATV20", "Atorvastatin Calcium Tablets", "20", "Bottle of 30", 24, "ATV");
        P("AML5", "Amlodipine Besylate Tablets", "5", "Bottle of 90", 36, "AML");
        P("LSN50", "Losartan Potassium Tablets", "50", "Bottle of 30", 24, "LSN");
        _db.Products.AddRange(_products.Values);

        void M(string code, string name, string authority) => _markets[code] = new Market { Code = code, Name = name, RegulatoryAuthority = authority, MinShelfLifePctAtDispatch = 75 };
        M("US", "United States", "US FDA"); M("UK", "United Kingdom", "MHRA"); M("EU", "European Union", "EMA / National CAs");
        M("IN", "India", "CDSCO"); M("ZA", "South Africa", "SAHPRA"); M("AU", "Australia", "TGA");
        _db.Markets.AddRange(_markets.Values);

        void C(string code, string name, string country) => _customers[code] = new Customer { Code = code, Name = name, Country = country };
        C("MERIDIAN", "Meridian Generics Inc.", "United States"); C("NORTHBRIDGE", "Northbridge Health Distributors", "United Kingdom");
        C("HELIOS", "Helios Pharma Trading GmbH", "Germany"); C("SUNRISE", "Sunrise Medicare Pvt Ltd", "India");
        C("CAPE", "Cape Health Supplies", "South Africa"); C("SOUTHERNX", "Southern Cross Pharma Pty", "Australia");
        _db.Customers.AddRange(_customers.Values);
    }

    void SeedSecurity(bool demoUsers, string password)
    {
        var roles = new Dictionary<string, Role>();
        Role R(string name, string desc, params string[] perms)
        {
            var r = new Role { Name = name, Description = desc, IsSystem = name == "Administrator",
                Permissions = perms.Select(p => new RolePermission { PermissionCode = p }).ToList() };
            roles[name] = r;
            return r;
        }
        var all = Permissions.All.Select(p => p.Code).ToArray();
        string[] viewer = { Permissions.BatchView, Permissions.QueueView };
        R("Administrator", "Full system administration", all);
        R("QA Release Authority", "Works the release queue and signs release decisions", viewer.Concat(new[] {
            Permissions.BatchCreate, Permissions.BatchEdit, Permissions.ItemUpdateAny, Permissions.ChangeRequest, Permissions.ChangeApprove,
            Permissions.QualityEventManage, Permissions.MovementManage, Permissions.MarketManage, Permissions.BatchHold, Permissions.BatchReject,
            Permissions.BatchRelease, Permissions.BatchReleaseConditional, Permissions.DashboardView, Permissions.AuditView }).ToArray());
        R("QA Reviewer", "Creates batches, reviews records, raises checklist changes", viewer.Concat(new[] {
            Permissions.BatchCreate, Permissions.BatchEdit, Permissions.ItemUpdateOwnDepartment, Permissions.ChangeRequest,
            Permissions.QualityEventManage, Permissions.MovementManage, Permissions.BatchHold, Permissions.AuditView }).ToArray());
        R("Department User", "Completes checklist items owned by their department", viewer.Concat(new[] {
            Permissions.ItemUpdateOwnDepartment, Permissions.ChangeRequest }).ToArray());
        R("QMS Coordinator", "Maintains quality events and the QMS check", viewer.Concat(new[] {
            Permissions.ItemUpdateOwnDepartment, Permissions.QualityEventManage, Permissions.ChangeRequest }).ToArray());
        R("Regulatory Affairs", "Maintains market approvals", viewer.Concat(new[] {
            Permissions.ItemUpdateOwnDepartment, Permissions.MarketManage, Permissions.ChangeRequest }).ToArray());
        R("Warehouse", "Records movements, receipts and dispatch", viewer.Concat(new[] {
            Permissions.ItemUpdateOwnDepartment, Permissions.MovementManage, Permissions.BatchDispatch }).ToArray());
        R("Management", "Monitors throughput, bottlenecks and risk", viewer.Concat(new[] { Permissions.DashboardView, Permissions.AuditView }).ToArray());
        _db.Roles.AddRange(roles.Values);

        void U(string username, string fullName, string dept, params string[] roleNames)
        {
            var u = new User { Username = username, FullName = fullName, Email = $"{username}@example.com", Department = _dept[dept], PasswordChangedAt = _now };
            u.PasswordHash = _passwords.Hash(u, password);
            foreach (var rn in roleNames) u.UserRoles.Add(new UserRole { Role = roles[rn] });
            _users[username] = u;
        }
        U("admin", "System Administrator", "QA", "Administrator");
        if (!demoUsers) { _db.Users.AddRange(_users.Values); return; }
        U("qa.head", "Anita Rao", "QA", "QA Release Authority");
        U("qa.lead", "Vikram Shetty", "QA", "QA Release Authority");
        U("qa.reviewer", "Priya Nair", "QA", "QA Reviewer");
        U("mfg.user", "Rahul Verma", "MFG", "Department User");
        U("pkg.user", "Sneha Iyer", "PKG", "Department User");
        U("qc.user", "Arjun Mehta", "QC", "Department User");
        U("qms.user", "Fatima Khan", "QMS", "QMS Coordinator");
        U("ra.user", "Daniel Joseph", "RA", "Regulatory Affairs");
        U("wh.user", "Suresh Kumar", "WH", "Warehouse");
        U("mgmt.user", "Meera Krishnan", "SCM", "Management");
        _db.Users.AddRange(_users.Values);
    }

    // ------------------------------------------------------------------ templates

    TemplateItem TI(string code, string name, string stage, string dept, ChecklistItemType type, int seq, int dueOffset,
        bool mandatory = true, bool final = false, bool perMarket = false, DataSource source = DataSource.Manual, string dependsOn = "") =>
        new() { Code = code, Name = name, Stage = stage, Department = _dept[dept], ItemType = type, Sequence = seq, DueOffsetDays = dueOffset,
            IsMandatory = mandatory, IsFinalRelease = final, IsPerMarket = perMarket, DefaultSource = source, DependsOn = dependsOn };

    List<TemplateItem> StandardItems(string finalName = "Final QA release") => new()
    {
        TI("MFG_BMR", "Manufacturing batch record review", "Mfg/Pack", "MFG", ChecklistItemType.Approval, 1, -14, source: DataSource.ERP),
        TI("PKG_BPR", "Packaging batch record review", "Mfg/Pack", "PKG", ChecklistItemType.Approval, 1, -12),
        TI("QC_LIMS", "QC release testing – LIMS results approved", "QC/LIMS", "QC", ChecklistItemType.ExternalResult, 2, -6, source: DataSource.LIMS, dependsOn: "MFG_BMR"),
        TI("QC_RETAIN", "Retention samples logged", "QC/LIMS", "QC", ChecklistItemType.PhysicalActivity, 2, -6, mandatory: false),
        TI("QMS_CHECK", "QMS check – no open Deviation, OOS, OOT, CAPA or Change Control", "QMS", "QMS", ChecklistItemType.DataCheck, 3, -3, source: DataSource.QMS),
        TI("RA_APPROVAL", "Regulatory approval for target market", "Regulatory", "RA", ChecklistItemType.ExternalResult, 3, -3, perMarket: true, source: DataSource.RIMS),
        TI("ART_VERIFY", "Artwork and label text verification", "QA Review", "QA", ChecklistItemType.Document, 3, -3),
        TI("QA_REVIEW", "QA batch documentation review", "QA Review", "QA", ChecklistItemType.Approval, 4, -1, dependsOn: "QC_LIMS,QMS_CHECK"),
        TI("QA_RELEASE", finalName, "QA Release", "QA", ChecklistItemType.Approval, 5, 0, final: true, dependsOn: "QA_REVIEW"),
    };

    void SeedTemplates()
    {
        ChecklistTemplate T(string name, string desc, List<TemplateItem> items, int? productId = null, Market? market = null,
            BatchType? batchType = null, SiteType? siteType = null, string? dosageForm = "Tablet", Product? product = null)
        {
            var t = new ChecklistTemplate { Name = name, Description = desc, Items = items, Product = product, Market = market,
                BatchType = batchType, SiteType = siteType, DosageForm = dosageForm, UpdatedAt = _now };
            _templates.Add(t);
            return t;
        }

        T("Standard release – own site tablets", "Default checklist for tablets made at our own sites.", StandardItems(), siteType: SiteType.Own);

        var us = StandardItems();
        us.Insert(2, TI("SERIALIZATION", "Serialization / DSCSA data verified", "Mfg/Pack", "PKG", ChecklistItemType.DataCheck, 1, -10, source: DataSource.ERP));
        T("US market – own site tablets", "Adds DSCSA serialization verification for US-bound batches.", us, market: _markets["US"], siteType: SiteType.Own).Priority = 30;

        T("EU market – QP certification", "Final step is Qualified Person certification (EU GMP Annex 16).",
            StandardItems("QP certification and release"), market: _markets["EU"], siteType: SiteType.Own).Priority = 10;
        T("UK market – QP certification", "Final step is UK Qualified Person certification.",
            StandardItems("UK QP certification and release"), market: _markets["UK"], siteType: SiteType.Own).Priority = 20;

        T("Contract manufactured tablets (CMO)", "Batches manufactured by a contract manufacturer and received into our warehouse.", new()
        {
            TI("CMO_QA_AGREEMENT", "CMO approved status and quality agreement valid", "CMO Review", "QA", ChecklistItemType.DataCheck, 1, -14),
            TI("CMO_BMR", "CMO executed batch record review", "CMO Review", "QA", ChecklistItemType.Document, 1, -12),
            TI("CMO_COA", "CMO Certificate of Analysis received and reviewed", "CMO Review", "QC", ChecklistItemType.ExternalResult, 1, -12),
            TI("GRN_RECEIPT", "Goods receipt and transport temperature log reviewed", "Receipt", "WH", ChecklistItemType.PhysicalActivity, 2, -8, source: DataSource.WMS),
            TI("QC_ID_TEST", "Identity testing on receipt – LIMS", "QC/LIMS", "QC", ChecklistItemType.ExternalResult, 3, -5, source: DataSource.LIMS, dependsOn: "GRN_RECEIPT"),
            TI("QMS_CHECK", "QMS check incl. CMO deviations", "QMS", "QMS", ChecklistItemType.DataCheck, 3, -3, source: DataSource.QMS),
            TI("RA_APPROVAL", "Regulatory approval for target market (CMO site registered)", "Regulatory", "RA", ChecklistItemType.ExternalResult, 3, -3, perMarket: true, source: DataSource.RIMS),
            TI("QA_REVIEW", "QA review of CMO batch documentation", "QA Review", "QA", ChecklistItemType.Approval, 4, -1, dependsOn: "CMO_BMR,CMO_COA,QC_ID_TEST"),
            TI("QA_RELEASE", "Final QA release", "QA Release", "QA", ChecklistItemType.Approval, 5, 0, final: true, dependsOn: "QA_REVIEW"),
        }, siteType: SiteType.ContractManufacturer);

        var ct = StandardItems();
        ct.Insert(4, TI("CT_SAMPLES_SENT", "Samples sent to contract lab (microbial assay)", "QC/LIMS", "QC", ChecklistItemType.PhysicalActivity, 2, -10));
        ct.Insert(5, TI("CT_COA", "Contract lab CoA received and reviewed", "QC/LIMS", "QC", ChecklistItemType.ExternalResult, 2, -5, dependsOn: "CT_SAMPLES_SENT"));
        ct.Single(i => i.Code == "QA_REVIEW").DependsOn = "QC_LIMS,QMS_CHECK,CT_COA";
        T("Azithromycin – outsourced microbial assay", "Own-site batches where the microbial assay is tested at a contract lab.", ct,
            product: _products["AZM500"], siteType: SiteType.Own);

        List<TemplateItem> ChildItems(string verb, string vendorStage) => new()
        {
            TI("PARENT_RELEASE", "Parent batch released for further processing", "Parent", "QA", ChecklistItemType.DataCheck, 1, -14),
            TI("LBL_ARTWORK", $"Market-specific artwork approved for {verb}", "Parent", "RA", ChecklistItemType.Document, 1, -12),
            TI("LBL_SENT", $"Material sent to {vendorStage} vendor", "At vendor", "WH", ChecklistItemType.PhysicalActivity, 2, -10, dependsOn: "PARENT_RELEASE"),
            TI("LBL_RECON", $"{char.ToUpper(verb[0]) + verb[1..]} record and label reconciliation at vendor", "At vendor", "QA", ChecklistItemType.Document, 3, -5),
            TI("LBL_RETURN", "Returned and receipt inspection", "At vendor", "WH", ChecklistItemType.PhysicalActivity, 3, -4),
            TI("RA_APPROVAL", "Regulatory approval for target market", "Regulatory", "RA", ChecklistItemType.ExternalResult, 4, -2, perMarket: true, source: DataSource.RIMS),
            TI("QA_REVIEW", $"QA review of {verb} documentation", "QA Review", "QA", ChecklistItemType.Approval, 4, -1, dependsOn: "LBL_RECON,LBL_RETURN"),
            TI("QA_RELEASE", "Final QA release", "QA Release", "QA", ChecklistItemType.Approval, 5, 0, final: true, dependsOn: "QA_REVIEW"),
        };
        T("Relabelling at external vendor", "Child batch relabelled for a market by a labelling vendor.", ChildItems("relabelling", "labelling"), batchType: BatchType.Relabel, dosageForm: null);
        T("Repacking at external vendor", "Child batch repacked (e.g. bulk to blister) by a packaging vendor.", ChildItems("repacking", "packaging"), batchType: BatchType.Repack, dosageForm: null);

        T("Rework / reprocess", "Rework under an approved deviation and protocol.", new()
        {
            TI("INV_APPROVED", "Investigation closed and rework approved", "Investigation", "QMS", ChecklistItemType.Approval, 1, -14),
            TI("RW_PROTOCOL", "Rework protocol approved", "Investigation", "QA", ChecklistItemType.Document, 1, -12),
            TI("RW_BMR", "Rework batch record review", "Mfg/Pack", "MFG", ChecklistItemType.Approval, 2, -9, dependsOn: "RW_PROTOCOL"),
            TI("QC_LIMS", "Re-testing – LIMS results approved", "QC/LIMS", "QC", ChecklistItemType.ExternalResult, 3, -5, source: DataSource.LIMS, dependsOn: "RW_BMR"),
            TI("QMS_CHECK", "QMS check – no open events", "QMS", "QMS", ChecklistItemType.DataCheck, 3, -3, source: DataSource.QMS),
            TI("RA_APPROVAL", "Regulatory approval for target market", "Regulatory", "RA", ChecklistItemType.ExternalResult, 3, -3, perMarket: true, source: DataSource.RIMS),
            TI("QA_REVIEW", "QA review incl. stability commitment", "QA Review", "QA", ChecklistItemType.Approval, 4, -1, dependsOn: "QC_LIMS,QMS_CHECK"),
            TI("QA_RELEASE", "Final QA release", "QA Release", "QA", ChecklistItemType.Approval, 5, 0, final: true, dependsOn: "QA_REVIEW"),
        }, batchType: BatchType.Rework, dosageForm: null);

        _db.Templates.AddRange(_templates);
    }

    // ------------------------------------------------------------------ batches

    record Plan(Product Product, Site Site, DateOnly Mfg, List<Market> Markets, Customer? Customer, BatchType Type,
        double Speed, int? StallGroup, int StallDays);

    double Rand(double min, double max) => min + _rnd.NextDouble() * (max - min);
    T Pick<T>(IReadOnlyList<T> list) => list[_rnd.Next(list.Count)];
    bool Chance(double p) => _rnd.NextDouble() < p;

    Customer? CustomerFor(Market m) => m.Code switch
    {
        "US" => _customers["MERIDIAN"], "UK" => _customers["NORTHBRIDGE"], "EU" => _customers["HELIOS"],
        "IN" => _customers["SUNRISE"], "ZA" => _customers["CAPE"], _ => _customers["SOUTHERNX"]
    };

    async Task SeedBatchesAsync()
    {
        var products = _products.Values.ToList();
        var own = new[] { _sites["HYD1"], _sites["VZG2"], _sites["BDI3"] };
        var marketSets = new List<string[]> { new[] { "US" }, new[] { "UK" }, new[] { "EU" }, new[] { "IN" }, new[] { "UK", "EU" },
            new[] { "US" }, new[] { "IN", "ZA" }, new[] { "AU" }, new[] { "UK", "EU", "AU" }, new[] { "IN" } };

        var plans = new List<Plan>();
        for (var i = 0; i < _batchCount; i++)
        {
            var ageDays = (int)Math.Round(Math.Pow(_rnd.NextDouble(), 1.5) * 250) + 1;
            var product = Pick(products);
            var site = Chance(0.12) ? _sites["CMO1"] : Pick(own);
            var markets = Pick(marketSets).Select(c => _markets[c]).ToList();
            var speed = Chance(0.2) ? Rand(1.4, 2.4) : Rand(0.7, 1.15);
            int? stall = Chance(0.16) ? _rnd.Next(1, 5) : null;
            plans.Add(new Plan(product, site, _today.AddDays(-ageDays), markets, CustomerFor(markets[0]),
                Chance(0.05) ? BatchType.Validation : BatchType.Commercial, speed, stall, stall == null ? 0 : _rnd.Next(25, 120)));
        }
        // Old batches still in the warehouse to exercise the expiry and shelf-life views.
        plans.Add(new Plan(_products["PNT40"], _sites["HYD1"], _today.AddMonths(-25), new() { _markets["IN"] }, _customers["SUNRISE"], BatchType.Exhibit, 1, 4, 400));
        plans.Add(new Plan(_products["AZM500"], _sites["VZG2"], _today.AddMonths(-23).AddDays(-10), new() { _markets["ZA"] }, _customers["CAPE"], BatchType.Commercial, 1, null, 0));
        plans.Add(new Plan(_products["LSN50"], _sites["BDI3"], _today.AddMonths(-5).AddDays(-20), new() { _markets["UK"] }, _customers["NORTHBRIDGE"], BatchType.Commercial, 1, null, 0));

        var batches = new List<(Batch Batch, ChecklistTemplate Template, Plan Plan)>();
        foreach (var plan in plans.OrderBy(p => p.Mfg))
            batches.Add(BuildBatch(plan, batches.Select(b => b.Batch.BatchNo)));

        // Children: relabel/repack at a vendor, and rework.
        var parents = batches.Where(b => b.Batch.ReleasedAt != null && b.Plan.Product.Code is "PCM500" or "CTZ10" or "MET500")
            .OrderByDescending(b => b.Batch.ReleasedAt).Take(7).ToList();
        var childN = 0;
        foreach (var parent in parents)
        {
            var type = childN % 3 == 2 ? BatchType.Repack : BatchType.Relabel;
            var market = childN % 2 == 0 ? _markets["ZA"] : _markets["AU"];
            var mfg = DateOnly.FromDateTime(parent.Batch.ReleasedAt!.Value.Date).AddDays(1);
            var plan = new Plan(parent.Plan.Product, _sites["HYD1"], mfg, new() { market }, CustomerFor(market), type,
                childN == 4 ? 2.5 : Rand(0.8, 1.3), null, 0);
            batches.Add(BuildBatch(plan, batches.Select(b => b.Batch.BatchNo), parent.Batch));
            childN++;
        }
        var investigated = batches.First(b => b.Batch.ReleasedAt == null && b.Batch.BatchType == BatchType.Commercial && b.Plan.Site.SiteType == SiteType.Own
                                              && (_today.DayNumber - b.Plan.Mfg.DayNumber) > 40);
        investigated.Batch.Disposition = BatchDisposition.UnderInvestigation;
        investigated.Batch.DispositionReason = "Dissolution OOS – root cause blending time; rework batch raised";
        AddEvent(investigated.Batch, QualityEventType.OOS, "Dissolution below Q at L2 stage", QualityEventCriticality.Major, investigated.Plan.Mfg.AddDays(8), null);
        AddEvent(investigated.Batch, QualityEventType.Deviation, "Blending time exceeded validated range", QualityEventCriticality.Major, investigated.Plan.Mfg.AddDays(9), null);
        var rwPlan = new Plan(investigated.Plan.Product, investigated.Plan.Site, _today.AddDays(-12), investigated.Plan.Markets, investigated.Plan.Customer, BatchType.Rework, 1.1, null, 0);
        batches.Add(BuildBatch(rwPlan, batches.Select(b => b.Batch.BatchNo), investigated.Batch));

        ApplyScenarios(batches);

        // Signatures are append-only (database trigger), so they are inserted once with their final batch reference.
        var releases = batches.ToDictionary(x => x.Batch, x => x.Batch.Releases.ToList());
        foreach (var (b, _, _) in batches) { b.Releases.Clear(); _db.Batches.Add(b); }
        await _db.SaveChangesAsync();
        foreach (var (b, list) in releases)
            foreach (var r in list)
            {
                r.Signature!.BatchId = b.Id;
                r.Signature.EntityId = b.Id.ToString();
                b.Releases.Add(r);
            }
        foreach (var (item, mv) in _pendingLinks) item.ExternalMovementId = mv.Id;

        foreach (var (b, t, _) in batches)
        {
            _db.ChecklistItemDependencies.AddRange(BatchService.BuildDependencies(b, t));
            var a = ReadinessEngine.Assess(b, _settings, _today);
            b.ReadinessPct = a.ReadinessPct; b.IsReadyForRelease = a.IsReadyForRelease; b.CurrentStage = a.CurrentStage;
            b.LastProgressAt = a.LastProgressAt; b.DispatchByDate = a.DispatchBy; b.Deadline = a.Deadline; b.DeadlineType = a.DeadlineType;
        }
        await _db.SaveChangesAsync();
    }

    User CompleterFor(ChecklistItem i) => i.Department!.Code switch
    {
        "MFG" => _users["mfg.user"], "PKG" => _users["pkg.user"], "QC" => _users["qc.user"], "QMS" => _users["qms.user"],
        "RA" => _users["ra.user"], "WH" => _users["wh.user"], _ => _users["qa.reviewer"]
    };

    (Batch, ChecklistTemplate, Plan) BuildBatch(Plan plan, IEnumerable<string> existingNos, Batch? parent = null)
    {
        var product = plan.Product;
        var batchNo = parent == null ? BatchService.SuggestBatchNo(product, plan.Mfg, existingNos)
            : $"{parent.BatchNo}-{plan.Type switch { BatchType.Relabel => "RL", BatchType.Repack => "RP", _ => "RW" }}1";
        var completion = plan.Mfg.AddDays(_rnd.Next(2, 5));
        var createdAt = new DateTimeOffset(plan.Mfg.ToDateTime(new TimeOnly(9, _rnd.Next(0, 59))), TimeSpan.FromHours(5.5));
        var expiry = parent != null && plan.Type != BatchType.Rework ? parent.ExpiryDate : ShelfLife.CalculateExpiry(plan.Mfg, product.ShelfLifeMonths);
        var size = product.Code switch { "PCM500" => 1_200_000m, "MET500" => 900_000m, "AZM500" => 300_000m, _ => 600_000m };

        var b = new Batch
        {
            BatchNo = batchNo, Product = product, Site = plan.Site, Customer = plan.Customer, BatchType = plan.Type, ParentBatch = parent,
            LotType = plan.Type == BatchType.Commercial ? "Commercial" : plan.Type.ToString(), BatchSize = parent?.BatchSize / (plan.Type == BatchType.Rework ? 1 : 4) ?? size,
            BatchSizeUnit = "Tablets", ManufacturingBlock = plan.Site.SiteType == SiteType.ContractManufacturer ? "CMO Block C" : $"Block {"ABCD"[_rnd.Next(4)]}",
            ProcessOrderDate = plan.Mfg.AddDays(-_rnd.Next(3, 10)), ManufacturingDate = plan.Mfg,
            BatchCompletionDate = completion <= _today ? completion : null,
            TargetReleaseDate = completion.AddDays(_rnd.Next(18, 30)), ShelfLifeMonths = product.ShelfLifeMonths, ExpiryDate = expiry,
            ExpiryOverrideReason = parent != null && plan.Type != BatchType.Rework ? "Inherited from parent batch" : null,
            CreatedAt = createdAt, CreatedBy = _users[Chance(0.7) ? "qa.reviewer" : "mfg.user"],
            Markets = plan.Markets.Select(m => new BatchMarket { Market = m, MarketId = 0 }).ToList()
        };
        if (plan.Type == BatchType.Rework) b.TargetReleaseDate = _today.AddDays(14);

        var template = Choose(plan, product);
        b.Template = template; b.TemplateVersion = template.Version;
        b.Items = BatchService.BuildChecklist(template, b, plan.Markets, _settings);
        foreach (var i in b.Items) i.Department = _dept.Values.First(d => d.Id == i.DepartmentId);

        Simulate(b, plan, completion, parent);
        return (b, template, plan);
    }

    ChecklistTemplate Choose(Plan plan, Product product)
    {
        if (plan.Type is BatchType.Relabel) return _templates.Single(t => t.BatchType == BatchType.Relabel);
        if (plan.Type is BatchType.Repack) return _templates.Single(t => t.BatchType == BatchType.Repack);
        if (plan.Type is BatchType.Rework) return _templates.Single(t => t.BatchType == BatchType.Rework);
        if (plan.Site.SiteType == SiteType.ContractManufacturer) return _templates.Single(t => t.SiteType == SiteType.ContractManufacturer);
        if (product.Code == "AZM500") return _templates.Single(t => t.Product == product);
        var codes = plan.Markets.Select(m => m.Code).ToList();
        if (codes.Contains("US")) return _templates.Single(t => t.Market?.Code == "US");
        if (codes.Contains("UK")) return _templates.Single(t => t.Market?.Code == "UK");
        if (codes.Contains("EU")) return _templates.Single(t => t.Market?.Code == "EU");
        return _templates.First();
    }

    /// <summary>Walks the checklist in sequence groups, completing items at realistic times up to "now".</summary>
    void Simulate(Batch b, Plan plan, DateOnly start, Batch? parent)
    {
        var cursor = new DateTimeOffset(start.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(5.5));
        if (parent != null && plan.Type != BatchType.Rework) cursor = parent.ReleasedAt ?? cursor;
        var groups = b.Items.GroupBy(i => i.Sequence).OrderBy(g => g.Key).ToList();
        var groupIndex = 0;
        foreach (var g in groups)
        {
            groupIndex++;
            if (plan.StallGroup == groupIndex) cursor = cursor.AddDays(plan.StallDays);
            var groupEnd = cursor;
            foreach (var item in g)
            {
                var mean = MeanDays.GetValueOrDefault(item.Code, 2);
                var startAt = cursor.AddHours(Rand(1, 20));
                var doneAt = startAt.AddDays(mean * plan.Speed * Rand(0.6, 1.5));
                if (item.Code == "QC_RETAIN" && Chance(0.3)) continue;
                if (startAt < _now) { item.StartedAt = startAt; item.Status = ChecklistItemStatus.InProgress; }
                if (doneAt < _now)
                {
                    item.Status = ChecklistItemStatus.Completed;
                    item.CompletedAt = doneAt;
                    item.CompletedBy = item.IsFinalRelease ? _users[Chance(0.6) ? "qa.head" : "qa.lead"] : CompleterFor(item);
                    if (item.Source is not DataSource.Manual) { item.LastSyncedAt = doneAt.AddMinutes(_rnd.Next(5, 90)); item.SourceReference = $"{item.Source}-{_rnd.Next(100000, 999999)}"; }
                }
                if (doneAt > groupEnd) groupEnd = doneAt;
            }
            cursor = groupEnd;
        }

        foreach (var m in b.Markets)
        {
            var ra = b.Items.FirstOrDefault(i => i.Code == "RA_APPROVAL" && i.Market == m.Market);
            if (ra?.Status == ChecklistItemStatus.Completed)
            {
                m.RegulatoryStatus = RegulatoryStatus.Approved; m.ApprovedAt = ra.CompletedAt; m.Source = DataSource.RIMS;
                m.ApprovalReference = $"{m.Market!.Code}-MA-{_rnd.Next(1000, 9999)}"; m.LastSyncedAt = ra.LastSyncedAt;
            }
            else m.RegulatoryStatus = ra?.Status == ChecklistItemStatus.InProgress ? RegulatoryStatus.Submitted : RegulatoryStatus.Pending;
        }

        var final = b.Items.Single(i => i.IsFinalRelease);
        if (final.Status == ChecklistItemStatus.Completed)
        {
            b.ReleasedAt = final.CompletedAt;
            b.Disposition = BatchDisposition.Released;
            var sig = Sign(final.CompletedBy!, "Approved for release", b, final.CompletedAt!.Value);
            b.Releases.Add(new BatchRelease { Kind = ReleaseKind.Full, Quantity = b.BatchSize, ReleasedAt = final.CompletedAt.Value, ReleasedById = final.CompletedBy!.Id, Signature = sig });
            final.SourceReference = $"E-signature by {sig.FullName}";
            var dispatchOn = DateOnly.FromDateTime(final.CompletedAt.Value.Date).AddDays(_rnd.Next(1, 40));
            if (dispatchOn <= _today && !Chance(0.04) && _today.DayNumber - plan.Mfg.DayNumber < 300)
            {
                var split = Chance(0.2);
                AddDispatch(b, split ? Math.Round(b.BatchSize * 0.6m) : b.BatchSize, dispatchOn);
                var second = dispatchOn.AddDays(_rnd.Next(7, 35));
                if (split && second <= _today) AddDispatch(b, b.BatchSize - Math.Round(b.BatchSize * 0.6m), second);
            }
        }

        // Contract manufacturing: API and materials supplied to the CMO, finished goods returned on receipt.
        if (plan.Site.SiteType == SiteType.ContractManufacturer)
        {
            var grn = b.Items.First(i => i.Code == "GRN_RECEIPT");
            var mv = new ExternalMovement
            {
                ExternalParty = _parties["CMO1"], Purpose = MovementPurpose.ContractManufacturing, Description = "API and excipients supplied; finished goods returned",
                SentOn = plan.Mfg.AddDays(-8), ReceivedByVendorOn = plan.Mfg.AddDays(-6), ExpectedReturnOn = plan.Mfg.AddDays(8),
                Quantity = b.BatchSize, Unit = "Tablets", Reference = $"PO-CMO-{_rnd.Next(1000, 9999)}",
                ReturnedOn = grn.CompletedAt == null ? null : DateOnly.FromDateTime(grn.CompletedAt.Value.Date),
                Status = grn.CompletedAt == null ? MovementStatus.AtVendor : MovementStatus.Returned
            };
            b.Movements.Add(mv);
            _pendingLinks.Add((grn, mv));
        }
        // Contract testing: samples to the contract lab, CoA back.
        var sent = b.Items.FirstOrDefault(i => i.Code == "CT_SAMPLES_SENT");
        if (sent?.CompletedAt != null)
        {
            var coa = b.Items.First(i => i.Code == "CT_COA");
            var mv = new ExternalMovement
            {
                ExternalParty = _parties[Chance(0.5) ? "LAB1" : "LAB2"], Purpose = MovementPurpose.ContractTesting, Description = "Samples for microbial assay",
                SentOn = DateOnly.FromDateTime(sent.CompletedAt.Value.Date), ReceivedByVendorOn = DateOnly.FromDateTime(sent.CompletedAt.Value.Date).AddDays(1),
                ExpectedReturnOn = DateOnly.FromDateTime(sent.CompletedAt.Value.Date).AddDays(10), Quantity = 120, Unit = "Tablets",
                Reference = $"CT-{_rnd.Next(1000, 9999)}",
                ReturnedOn = coa.CompletedAt == null ? null : DateOnly.FromDateTime(coa.CompletedAt.Value.Date),
                Status = coa.CompletedAt == null ? MovementStatus.AtVendor : MovementStatus.Returned
            };
            b.Movements.Add(mv);
            _pendingLinks.Add((coa, mv));
        }
        // Relabel / repack at vendor.
        var lsent = b.Items.FirstOrDefault(i => i.Code == "LBL_SENT");
        if (lsent?.CompletedAt != null)
        {
            var ret = b.Items.First(i => i.Code == "LBL_RETURN");
            var vendor = plan.Type == BatchType.Repack ? _parties["PKG1"] : _parties["LBL1"];
            var mv = new ExternalMovement
            {
                ExternalParty = vendor, Purpose = plan.Type == BatchType.Repack ? MovementPurpose.Packaging : MovementPurpose.Relabelling,
                Description = plan.Type == BatchType.Repack ? "Repacking into market pack" : $"Relabelling for {b.Markets[0].Market!.Name}",
                SentOn = DateOnly.FromDateTime(lsent.CompletedAt.Value.Date), ReceivedByVendorOn = DateOnly.FromDateTime(lsent.CompletedAt.Value.Date).AddDays(1),
                ExpectedReturnOn = DateOnly.FromDateTime(lsent.CompletedAt.Value.Date).AddDays(7), Quantity = b.BatchSize, Unit = "Tablets",
                Reference = $"DC-{_rnd.Next(1000, 9999)}",
                ReturnedOn = ret.CompletedAt == null ? null : DateOnly.FromDateTime(ret.CompletedAt.Value.Date),
                Status = ret.CompletedAt == null ? MovementStatus.AtVendor : MovementStatus.Returned
            };
            b.Movements.Add(mv);
            _pendingLinks.Add((ret, mv));
        }
    }

    readonly List<(ChecklistItem Item, ExternalMovement Movement)> _pendingLinks = new();

    void AddDispatch(Batch b, decimal qty, DateOnly on) =>
        b.Dispatches.Add(new Dispatch { Quantity = qty, DispatchedOn = on, Market = b.Markets[_rnd.Next(b.Markets.Count)].Market,
            Reference = $"INV-{_rnd.Next(10000, 99999)}", RecordedById = _users["wh.user"].Id,
            RecordedAt = new DateTimeOffset(on.ToDateTime(new TimeOnly(16, 0)), TimeSpan.FromHours(5.5)) });

    void CompleteNow(ChecklistItem i, double hoursAgo)
    {
        i.StartedAt ??= _now.AddHours(-hoursAgo - 20);
        i.Status = ChecklistItemStatus.Completed;
        i.CompletedAt = _now.AddHours(-hoursAgo);
        i.CompletedBy = CompleterFor(i);
        i.BlockedReason = null;
        if (i.Source is not DataSource.Manual) { i.LastSyncedAt = i.CompletedAt; i.SourceReference = $"{i.Source}-{_rnd.Next(100000, 999999)}"; }
    }

    static List<ChecklistItem> PendingNonFinal(Batch b) => b.Items.Where(i => i.IsMandatory && !i.IsDone && !i.IsFinalRelease).ToList();

    ElectronicSignature Sign(User u, string meaning, Batch b, DateTimeOffset at) => new()
    {
        UserId = u.Id, Username = u.Username, FullName = u.FullName, Meaning = meaning, SignedAt = at, EntityType = nameof(Batch), EntityId = b.BatchNo,
        RecordHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{b.BatchNo}|{meaning}|{at:O}|{u.Username}")))
    };

    void AddEvent(Batch b, QualityEventType type, string description, QualityEventCriticality crit, DateOnly opened, DateOnly? closed)
    {
        var prefix = type switch { QualityEventType.Deviation => "DEV", QualityEventType.ChangeControl => "CC", _ => type.ToString() };
        b.QualityEvents.Add(new QualityEvent
        {
            EventType = type, Number = $"{prefix}-{opened:yyyy}-{_eventSeq++:0000}", Description = description, Criticality = crit,
            OpenedOn = opened, ClosedOn = closed, Status = closed == null ? QualityEventStatus.Open : QualityEventStatus.Closed,
            Source = DataSource.QMS, LastSyncedAt = _now.AddMinutes(-_rnd.Next(10, 300))
        });
    }

    static readonly (QualityEventType Type, string Text)[] EventTexts =
    {
        (QualityEventType.Deviation, "Compression room RH excursion above 60% for 40 min"),
        (QualityEventType.Deviation, "Blister sealing temperature alarm during packing"),
        (QualityEventType.Deviation, "Label roll mix-up detected at line clearance"),
        (QualityEventType.OOS, "Assay result 93.8% (limit 95.0–105.0%)"),
        (QualityEventType.OOS, "Related substances: unknown impurity above limit"),
        (QualityEventType.OOT, "Dissolution trending low vs last 10 batches"),
        (QualityEventType.OOT, "Hardness trending towards lower limit"),
        (QualityEventType.CAPA, "CAPA from granulation endpoint deviation"),
        (QualityEventType.CAPA, "CAPA: retraining on line clearance"),
        (QualityEventType.ChangeControl, "Alternate API source qualification"),
        (QualityEventType.ChangeControl, "Change in blister foil supplier"),
    };

    /// <summary>Quality events, blocks, holds, rejections, conditional and partial releases, recall, shelf-life extension.</summary>
    void ApplyScenarios(List<(Batch Batch, ChecklistTemplate Template, Plan Plan)> all)
    {
        foreach (var (b, _, plan) in all)
        {
            if (!Chance(0.28)) continue;
            var (type, text) = Pick(EventTexts);
            var opened = plan.Mfg.AddDays(_rnd.Next(1, 8));
            if (opened > _today) continue;
            var qms = b.Items.FirstOrDefault(i => i.Code == "QMS_CHECK");
            var closedBefore = qms?.CompletedAt;
            if (closedBefore != null || b.ReleasedAt != null)
            {
                var closeOn = DateOnly.FromDateTime((closedBefore ?? b.ReleasedAt!.Value).Date).AddDays(-_rnd.Next(1, 3));
                AddEvent(b, type, text, QualityEventCriticality.Minor, opened, closeOn < opened ? opened : closeOn);
            }
            else
            {
                var crit = type == QualityEventType.OOS ? QualityEventCriticality.Major : Chance(0.15) ? QualityEventCriticality.Critical : QualityEventCriticality.Minor;
                AddEvent(b, type, text, crit, opened, null);
                if (qms != null && Chance(0.5) && b.Items.Where(i => i.Sequence < qms.Sequence).All(i => i.IsDone))
                {
                    qms.Status = ChecklistItemStatus.Blocked;
                    qms.StartedAt ??= _now.AddDays(-3);
                    qms.BlockedReason = $"{b.QualityEvents.Last().Number} open";
                }
            }
        }

        var active = all.Where(x => x.Batch.ReleasedAt == null && x.Batch.Disposition == BatchDisposition.InProgress && x.Batch.BatchType != BatchType.Rework).Select(x => x.Batch).ToList();
        var released = all.Where(x => x.Batch.ReleasedAt != null && x.Batch.ParentBatch == null).Select(x => x.Batch).ToList();

        // Regulatory approvals blocked.
        foreach (var b in active.Where(b => b.Items.Any(i => i.Code == "RA_APPROVAL" && i.Status == ChecklistItemStatus.InProgress)).Take(4))
        {
            var ra = b.Items.First(i => i.Code == "RA_APPROVAL" && i.Status == ChecklistItemStatus.InProgress);
            ra.Status = ChecklistItemStatus.Blocked;
            ra.BlockedReason = $"Awaiting {ra.Market?.RegulatoryAuthority} variation approval for new API source";
        }

        // Holds.
        var holdCandidates = active.Where(b => b.QualityEvents.Count == 0).ToList();
        void Hold(Batch b, BatchDisposition d, string why) { b.Disposition = d; b.DispositionReason = why; }
        if (holdCandidates.Count > 4)
        {
            Hold(holdCandidates[0], BatchDisposition.OnHold, "Customer requested hold pending artwork change");
            Hold(holdCandidates[1], BatchDisposition.Quarantine, "Received from CMO – awaiting temperature excursion assessment");
            Hold(holdCandidates[2], BatchDisposition.OnHold, "QA hold: environmental monitoring excursion in compression area");
            Hold(holdCandidates[3], BatchDisposition.UnderInvestigation, "Market complaint on sister batch under investigation");
        }

        // Rejections.
        foreach (var b in active.Where(b => b.Disposition == BatchDisposition.InProgress && b.Markets.Count == 1 && b.QualityEvents.Count == 0
                     && (_today.DayNumber - b.ManufacturingDate.DayNumber) > 20).Take(2))
        {
            b.Disposition = BatchDisposition.Rejected;
            b.DispositionReason = "Confirmed OOS for assay; batch rejected per SOP QA-045";
            AddEvent(b, QualityEventType.OOS, "Assay OOS confirmed on re-test", QualityEventCriticality.Critical, b.ManufacturingDate.AddDays(6), b.ManufacturingDate.AddDays(30));
        }

        bool Clean(Batch b) => b.Disposition == BatchDisposition.InProgress && b.QualityEvents.All(e => e.Status == QualityEventStatus.Closed)
                               && b.ParentBatch == null && b.Items.All(i => i.Status != ChecklistItemStatus.Blocked);
        var nearlyDone = active.Where(b => Clean(b) && PendingNonFinal(b).Count is > 0 and <= 3)
            .OrderBy(b => b.TargetReleaseDate).ToList();

        // Conditional release: one market's regulatory approval still pending, released for the approved market(s).
        foreach (var b in nearlyDone.Where(b => b.Markets.Count > 1).Take(2).ToList())
        {
            var keep = PendingNonFinal(b).FirstOrDefault(i => i.Code == "RA_APPROVAL") ?? b.Items.First(i => i.Code == "RA_APPROVAL");
            if (keep.IsDone) { keep.Status = ChecklistItemStatus.InProgress; keep.CompletedAt = null; keep.CompletedBy = null; }
            foreach (var i in PendingNonFinal(b).Where(i => i != keep)) CompleteNow(i, _rnd.Next(30, 70));
            b.Markets.First(m => m.Market == keep.Market).RegulatoryStatus = RegulatoryStatus.Submitted;
            ReleaseNow(b, ReleaseKind.Conditional, $"Release for approved market(s); {keep.Market!.Code} approval pending – stock for {keep.Market.Code} segregated in warehouse");
            nearlyDone.Remove(b);
        }

        // Ready for release: everything done except the final QA release (these head the queue).
        foreach (var b in nearlyDone.Take(9).ToList())
        {
            foreach (var i in PendingNonFinal(b)) CompleteNow(i, _rnd.Next(2, 60));
            foreach (var m in b.Markets.Where(m => m.RegulatoryStatus != RegulatoryStatus.Approved))
            { m.RegulatoryStatus = RegulatoryStatus.Approved; m.ApprovedAt = _now.AddHours(-30); m.Source = DataSource.RIMS; m.ApprovalReference = $"{m.Market!.Code}-MA-{_rnd.Next(1000, 9999)}"; }
            nearlyDone.Remove(b);
        }

        // Partial release: part released for an urgent order, balance held.
        foreach (var b in active.Where(b => Clean(b) && Metrics.IsReadyForRelease(b)).Skip(5).Take(2).ToList())
            ReleaseNow(b, ReleaseKind.Partial, "Release 60% for urgent customer order; balance held for additional stability pull", 0.6m);

        // Recall of a dispatched batch.
        var recalled = released.FirstOrDefault(b => b.Dispatches.Count > 0 && b.Releases.Count > 0);
        if (recalled != null)
        {
            recalled.Disposition = BatchDisposition.Recalled;
            recalled.DispositionReason = "Class II recall: mislabelled strength on outer carton (market complaint)";
            AddEvent(recalled, QualityEventType.Deviation, "Market complaint: carton strength misprint", QualityEventCriticality.Critical, _today.AddDays(-9), null);
            AddEvent(recalled, QualityEventType.CAPA, "CAPA: artwork proof-reading with 100% vision check", QualityEventCriticality.Major, _today.AddDays(-7), null);
        }

        // Shelf-life extension on an older active batch.
        var ext = active.FirstOrDefault(b => b.Product!.ShelfLifeMonths == 24 && b.Disposition == BatchDisposition.InProgress && b.ParentBatch == null);
        if (ext != null)
        {
            ext.ShelfLifeMonths = 36;
            ext.ExpiryDate = ShelfLife.CalculateExpiry(ext.ManufacturingDate, 36);
            ext.ExpiryOverrideReason = "Shelf life extended from 24 to 36 months based on long-term stability data (CC-2026-0042)";
        }

        // Released-for-further-processing parents for relabel children not yet released.
        foreach (var child in all.Where(x => x.Batch.ParentBatch != null && x.Batch.BatchType is BatchType.Relabel or BatchType.Repack))
        {
            var pr = child.Batch.Items.First(i => i.Code == "PARENT_RELEASE");
            pr.Source = DataSource.System;
            pr.SourceReference = $"Parent batch {child.Batch.ParentBatch!.BatchNo} released";
        }
    }

    void ReleaseNow(Batch b, ReleaseKind kind, string justification, decimal fraction = 1)
    {
        var at = _now.AddDays(-_rnd.Next(1, 4)).AddHours(-_rnd.Next(1, 6));
        var signer = _users["qa.head"];
        var meaning = kind == ReleaseKind.Partial ? $"Approved for partial release ({b.BatchSize * fraction:0} {b.BatchSizeUnit})" : "Approved for conditional release with pending items";
        var sig = Sign(signer, meaning, b, at);
        var pending = b.Items.Where(i => i.IsMandatory && !i.IsDone && !i.IsFinalRelease).Select(i => i.Name + (i.Market == null ? "" : $" ({i.Market.Code})"));
        b.Releases.Add(new BatchRelease { Kind = kind, Quantity = Math.Round(b.BatchSize * fraction), ReleasedAt = at, Signature = sig, ReleasedById = signer.Id,
            Justification = justification, PendingItemsSummary = string.Join("; ", pending) });
        var final = b.Items.Single(i => i.IsFinalRelease);
        if (kind == ReleaseKind.Partial)
        {
            b.Disposition = BatchDisposition.PartiallyReleased;
            final.Status = ChecklistItemStatus.InProgress; final.StartedAt = at;
        }
        else
        {
            b.Disposition = BatchDisposition.ConditionallyReleased; b.ReleasedAt = at;
            final.Status = ChecklistItemStatus.Completed; final.StartedAt = at; final.CompletedAt = at; final.CompletedBy = signer;
            final.SourceReference = $"E-signature by {signer.FullName}";
        }
    }
}
