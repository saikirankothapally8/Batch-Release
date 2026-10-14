# 6. Key screens

| Screen | Who | What it does |
|---|---|---|
| **QA workbench** | QA / release authority | *Next to release* queue (rank, readiness, priority, deadline, estimated release, why, quick Release); *My department's items* with Start / Complete / Block; *Waiting on other departments*; *Stuck batches* with what they wait for |
| **Batches** | All | Filterable list (product, product code, site, market, customer, status, date range, search); target of every dashboard drill-down with a visible "Showing: …" label; CSV export |
| **New batch** | QA reviewer | Product/batch form with DD-MM-YYYY dates, auto batch number, calculated expiry and dispatch-by; matching template previewed live with alternatives; child batches from a parent |
| **Batch 360** | All | Header with readiness, explanation, deadline, shelf life, actions (Release, Hold, Reject, Recall, Dispatch, Request change). Tabs: **Batch map**, Checklist, Markets & regulatory, Quality events, External parties, Release & dispatch, Genealogy, Changes & signatures, Audit trail |
| **Batch map** | All | Swimlanes per stage (Batch & genealogy, Mfg/Pack, QC/LIMS, QMS, Regulatory/RIMS, external parties, QA, release & warehouse, change control). Each box is coloured Completed / In progress / Not started / Blocked / N/A and lists its records (items, QE numbers, approvals, movements, dispatches). **Clicking any box or line opens that section of Batch 360 and highlights the record.** Available from the dashboard (Batch explorer, or any batch in "Most open quality events") and as a Batch 360 tab |
| **Management dashboard** | Management | KPI tiles with definitions; Planned vs released (weekly/monthly); Where batches are now; Average days per stage; Stage funnel; Open QE by type; Expiry risk; Warehouse aging; Breakdown by site/product/market/customer; Batch status. Every tile, bar and table cell drills down to the batches behind it |
| **Approvals** | Approvers | Pending checklist change requests with signed approve/reject |
| **Checklist templates** | Admin | Criteria, priority, items (step, code, stage, department, type, mandatory, weight, due offset, per market, final, source, dependencies); versioned with reason |
| **Roles & activities** | Admin | Create roles and tick activities from the catalogue |
| **Users** | Admin | Users, departments, roles, activation, password reset, lock status |
| **Master data** | Admin | Products, markets (min shelf life %), sites, external parties (approval, quality agreement), customers, departments |
| **Prioritisation** | Admin | Weights, penalties, band size, stuck days, warnings, segregation of duties |
| **Audit trail** | QA / auditors | System-wide filterable audit trail |

All charts follow one design system: fixed categorical palette (validated for colour-vision deficiency), status
colours reserved for meaning and always paired with a label, thin marks, hairline grid, tooltips, a **table view on
every chart**, and designed light and dark themes.
