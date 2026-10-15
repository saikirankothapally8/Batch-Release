# Batch Release IQ

Tells QA and operations **which batch can be released next, what is stopping it, and how quickly it can be released** –
for every batch, across manufacturing, QC/LIMS, QMS, regulatory/RIMS, external parties and the warehouse.

- **Prioritised release queue** – readiness (weighted checklist completion), earliest deadline (target release,
  75 % shelf-life dispatch limit, expiry), flags, a plain-language reason for every rank and an estimated release date.
- **Flexible checklists** – admin templates matched to each batch; per-batch changes under approval.
- **Batch 360 and Batch map** – the complete picture of one batch; the map shows every stage as a swimlane and
  every box links to the record behind it.
- **Management dashboard** – KPIs, planned vs released, bottlenecks, quality events, expiry risk, warehouse aging,
  breakdowns; everything drills down to batches.
- **GxP** – admin-defined roles and activities, append-only audit trail, Part 11 / Annex 11 e-signatures, ALCOA+.
- **Real-world scenarios** – contract manufacturing, contract testing, relabelling/repacking at a vendor, rework,
  partial and conditional release, multiple markets, holds, rejection, recall.

## Documentation
| | |
|---|---|
| [Requirements & user stories](docs/01-requirements-and-user-stories.md) | [Data model](docs/02-data-model.md) |
| [Architecture, tech stack & integration layer](docs/03-architecture.md) | [Prioritisation engine & metric definitions](docs/04-prioritisation-and-metrics.md) |
| [Scenarios (CMO, contract testing, relabelling, …)](docs/05-scenarios.md) | [Key screens](docs/06-screens.md) |
| [Compliance mapping](docs/07-compliance.md) | [Roadmap](docs/08-roadmap.md) |
| [Hosting on IIS](docs/09-deploy-iis.md) | [Database scripts](database/) |

## Run it locally
Prerequisites: .NET 8 SDK, Node.js 20+, PostgreSQL 14+ (or SQL Server 2019+).

```bash
# 1. API (creates the database and loads sample data on first start)
cd backend/src/BatchReleaseIQ.Api
dotnet run                     # http://localhost:5080, Swagger at /swagger
# connection string: appsettings.json → ConnectionStrings:Default (default: postgres/postgres on localhost)

# 2. Frontend (dev server proxies /api to :5080)
cd frontend
npm install
npm start                      # http://localhost:4200
```

Single deployable: `cd frontend && npm run publish:api` copies the production build into the API's `wwwroot`;
the API then serves the app at http://localhost:5080.

### Database
Create an empty database and put its name in the connection string. On first start the API creates all tables,
indexes and the audit-trail protection triggers inside it. If your DBA prefers to create the schema by hand, run
`database/postgresql-schema.sql` or `database/sqlserver-schema.sql` in the empty database first (regenerate them
from `backend/src/BatchReleaseIQ.Api` with `dotnet run -- --export-schema ../../../database`).

### Demo accounts (password `Passw0rd!`)
| User | Role |
|---|---|
| `qa.head`, `qa.lead` | QA Release Authority – release, approve changes, dispositions, dashboard |
| `qa.reviewer` | QA Reviewer – create batches, QA items, request changes |
| `mfg.user`, `pkg.user`, `qc.user`, `qms.user`, `ra.user`, `wh.user` | Department users |
| `mgmt.user` | Management – dashboard |
| `admin` | Administrator – everything incl. roles, users, templates, settings |

### Removing or loading the sample data
From the API folder (or the IIS site folder, using `dotnet BatchReleaseIQ.Api.dll` instead of `dotnet run --`):
```bash
dotnet run -- --remove-sample-data   # empty system: roles, departments, settings and an admin user
dotnet run -- --load-sample-data     # full demo data again
```
Both delete all data, audit trail included, and ask for the database name as confirmation (`--confirm <name>` for scripts).
See [Hosting on IIS](docs/09-deploy-iis.md#6-removing-or-loading-the-sample-data).

### Configuration
| Key | Meaning |
|---|---|
| `Database:Provider` | `PostgreSQL` (default) or `SqlServer` |
| `ConnectionStrings:Default` | Database connection string |
| `Database:SeedSampleData` / `Database:SampleBatchCount` | Load demo data into an empty database (default true / 250). With `false`, an empty database gets roles, departments, settings and an `admin` user only |
| `Bootstrap:AdminPassword` | Password of the `admin` account created when `SeedSampleData` is false |
| `PathBase` | Sub-path when hosted behind a reverse proxy (IIS virtual directories set it automatically) |
| `Jwt:SigningKey` | **Required**, ≥ 32 characters (development key in `appsettings.Development.json` only) |
| `Jwt:ExpiryMinutes` | Session lifetime (default 60) |
| `Site:TimeZone` | Business-date time zone (default `Asia/Kolkata`) |
| `Cors:Origins` | Allowed browser origins when the frontend is served separately |

## Tests
```bash
cd backend && dotnet test      # engine (readiness, ranking, deadlines, estimates, metrics) and business rules
```
