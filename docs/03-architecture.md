# 3. Architecture and technology

## Tech stack and why
| Layer | Choice | Why |
|---|---|---|
| Frontend | **Angular 19** (standalone components, signals), Chart.js 4 | Your team already uses Angular; typed forms and strict templates suit validated software; Chart.js is small and accessible with a table view for every chart |
| API | **ASP.NET Core 8** (LTS), REST + JWT | Your team uses .NET; LTS support window fits a validated product; runs on Windows/IIS and Linux/containers |
| Data access | **EF Core 8** | One model for **PostgreSQL** and **SQL Server**, chosen by configuration (`Database:Provider`) |
| Database | PostgreSQL 16 (cloud, Linux on-prem) or SQL Server 2019+ (Windows on-prem) | Both are common in pharma IT; triggers make the audit trail append-only in either |
| Auth | Built-in users with PBKDF2 hashing, lockout, idle logoff; admin-defined roles | Works on-prem without an identity provider; SSO (Azure AD / ADFS / OIDC) is a phase-2 adapter |
| Tests | xUnit (engine + business rules), Playwright for UI checks | Engine rules are the core IP and must be provable for CSV |

## Solution layout
```
backend/
  src/BatchReleaseIQ.Domain          entities, enums, permission catalogue,
                                     Engine/ (ReadinessEngine, Metrics, ShelfLife, TemplateMatcher) – pure, unit-tested
  src/BatchReleaseIQ.Infrastructure  AppDbContext (audit capture, UTC, enum-as-text), DatabaseSetup (append-only triggers),
                                     Services/ (BatchService, InsightService, BatchStateService, SignatureService), SampleData
  src/BatchReleaseIQ.Api             controllers, JWT, permission filter, DTOs; serves the Angular build from wwwroot
  tests/BatchReleaseIQ.Tests         engine and service tests
frontend/                            Angular app (core/, shared/, pages/)
docs/                                this documentation
```

## Request flow
```
Angular ──JWT──▶ API controller ──[Requires(permission)]──▶ BatchService / InsightService
                                                        │
                    ReadinessEngine + Metrics (Domain) ◀┘
                                                        ▼
                         AppDbContext.SaveChanges ──▶ entity changes + AuditEntry rows (same transaction)
```
- **One definition of every metric** (`Domain/Engine/Metrics.cs`). The queue, batch list, KPIs, charts and
  drill-downs all call it. A chart element carries a *drill key* (e.g. `expiry:Within 30 days`, `site:2|overdue`);
  the batch list applies the same key, so the bar and the list behind it always agree.
- **Derived state**: `BatchStateService.RecalculateAsync` stores readiness, current stage, deadline, dispatch-by
  on the batch after every change; settings changes trigger a full recalculation.

## Integration layer (phase 2 – designed now, manual entry in the MVP)
```
                ┌──────────────── Integration host (background service) ───────────────┐
 LIMS  ───────▶ │ LimsAdapter   ┐                                                       │
 QMS   ───────▶ │ QmsAdapter    │  IIntegrationAdapter                                  │
 RIMS  ───────▶ │ RimsAdapter   ├─ FetchChanges(since) → ExternalRecord[]  ─▶ Mapper ─▶ BatchService
 ERP/MES ─────▶ │ ErpAdapter    │  (scheduled sync + on-demand refresh)    (by Batch No)   (same rules,
 WMS   ───────▶ │ WmsAdapter    ┘                                                         audit, recalculation)
                │ SyncRun log: system, started, finished, records, errors                │
                └────────────────────────────────────────────────────────────────────────┘
```
- One adapter per system implementing `IIntegrationAdapter { FetchChangesAsync(since); HealthAsync(); }`.
  Transport is per customer (REST, database view, file drop, message queue).
- Records are mapped to batches **by Batch No** (plus market for RIMS). Unmatched records go to an exception queue.
- The adapter writes through the same services as users, so business rules, audit trail and recalculation apply.
  Every value carries `Source` and `LastSyncedAt`, shown next to it in the UI.
- Examples of automatic completion: LIMS result approved → `QC_LIMS` completed; QMS event closed → event status;
  all events closed → `QMS_CHECK` completed; RIMS approval → market approved and its regulatory item completed
  (already implemented for manual approvals); WMS goods receipt → movement returned and its linked item completed
  (implemented for manual returns).
- **Manual fallback**: when an integration is down, an authorised user completes the item; it is stored with
  `Source = Manual` and the reference "Manual entry (integration X bypassed)", visibly flagged in the UI.
- Approval dates/times are stored in proper date fields (`ApprovedAt`, `CompletedAt`), never parsed from text.

## Deployment
| Mode | Topology |
|---|---|
| **Cloud (SaaS)** | API + static Angular in containers behind a load balancer; managed PostgreSQL per customer (database-per-tenant for data segregation and per-customer validation); blob storage for evidence |
| **On-premise** | IIS or Windows service + SQL Server, or Linux + PostgreSQL; single tenant; integration host on the customer network next to LIMS/QMS |

The same build is used in both; configuration selects provider, connection string, time zone and signing key.

## Scale (1,000 batches/month)
- Active pipeline is typically a few hundred to ~2,000 batches; the queue and dashboard evaluate those in memory in
  milliseconds. Historical dashboards are scoped by date range.
- For very large histories the next step is a nightly `BatchSnapshot` table (one row per batch per day) for trend
  charts, and caching of historical item durations (already computed once per request).
