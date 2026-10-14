# 1. Requirements summary and user stories

## Purpose
Batch Release IQ answers one question for QA and operations: **which batch can I release next, what is stopping
it, and how quickly can I release it?** Every record is linked to a Batch No, and opening a batch shows its
complete picture in one place (Batch 360 and the Batch map).

## Decisions taken with the customer
| # | Topic | Decision |
|---|-------|----------|
| 1 | Product type | Standalone product sold to several pharma companies |
| 2 | Hosting | Cloud **and** on-premise; .NET + Angular |
| 3 | Volume | ~1,000 batches per month per customer |
| 4 | Priority driver | Status of the checklist stages (not the content of test results) |
| 5 | Checklist changes | Allowed per batch after creation, **with approval** |
| 6 | Four-eyes | Yes – the app is mainly used by QA / release authority, operations and management to move product |
| 7 | Weighting | Configurable; default weight rises with the stage position (proposal accepted) |
| 8 | Shelf life | At least **75 %** of shelf life must remain at dispatch (configurable per market) |
| 9 | Conditional release | Decided by the operations user holding the permission, with justification |
| 10 | Split batches | Partial release by quantity under the parent batch |
| 11 | Security in MVP | Login, admin-defined roles and activities, e-signatures included now |
| 12 | Data | Real database (PostgreSQL or SQL Server) |
| 13 | Outsourcing | Contract testing, contract manufacturing and relabelling at another party must be handled |

## Functional requirements
- **Flexible checklists, not a fixed workflow.** Admins define templates with match criteria (product, dosage form,
  market, customer, batch type, site type). The most specific match is applied automatically; ties go to the
  template priority. Items carry owner department, type, mandatory flag, weight, step (parallel items share a
  step), due offset, dependencies, per-market expansion and default data source.
- **Per-batch changes** to the checklist go through a change request approved by a second person with an e-signature.
- **Item status**: Not Started, In Progress, Completed, Not Applicable (justification), Blocked (reason). Completion
  time and person are recorded in proper timestamp fields.
- **Prioritised release queue** with readiness, deadline, flags, the reason for each rank and an estimated release date.
- **Workbench**: my department's items, items waiting on other departments, stuck batches.
- **Management dashboard**: KPIs, planned vs released, stage bottlenecks and funnel, open quality events,
  expiry risk, warehouse aging, breakdowns by site / product / market / customer, every element drillable.
- **Batch 360 and Batch map** for one batch: checklist, markets & regulatory, quality events, external parties,
  release & dispatch, genealogy, changes & signatures, audit trail.
- **Release**: full, partial (quantity), conditional (pending items + justification) and for further processing
  (relabelling / repacking), each with an e-signature.
- **Dispositions**: hold, quarantine, investigation, rejection, recall.
- **Dates** entered and shown as DD-MM-YYYY; stored as ISO date or UTC timestamp.

## Non-functional requirements
- GxP: role-based access, immutable audit trail, Part 11 / Annex 11 e-signatures, ALCOA+.
- 1,000 batches/month: queue and dashboard computations stay in memory over the active scope; derived fields are
  stored on the batch for SQL filtering (see architecture).
- Runs on PostgreSQL (cloud, Linux on-prem) or SQL Server (Windows on-prem) from the same build.

## User stories

### QA reviewer / release authority
- As a release authority I see a ranked list of batches with *why* each one is where it is, so I know what to release next.
- I open a batch and see every check, event, approval, movement and signature in one place.
- I release a ready batch with my e-signature; if something is pending I can release conditionally with a justification.
- I release part of a batch for an urgent order and hold the rest.
- I place a batch on hold / quarantine / investigation, and reject or recall with a signature.
- I approve or reject checklist changes requested by colleagues (never my own).
- I see which batches are stuck and which department they are waiting for.

### Department users (Manufacturing, Packaging, QC, QMS, Regulatory, Warehouse)
- As a department user I see the items my department owns, ordered by batch priority and due date.
- I start, complete, block or mark items not applicable, with comments and evidence.
- I cannot start an item whose dependencies are open, and cannot touch another department's items.
- Regulatory: I update market approvals; approving a market completes its regulatory item.
- QMS: I record or update Deviation, OOS, OOT, CAPA and Change Control linked to batches.
- Warehouse: I record material sent to and returned from external parties and dispatch of released stock.

### Management
- As a manager I see throughput, on-time %, bottlenecks, risk and breakdowns, and drill into any number down to the batch.
- I open any batch as a map to see where it stands across all departments.

### Administrator
- As an administrator I create roles and tick the activities each may perform.
- I manage users (never reused IDs, deactivate rather than delete), master data and checklist templates.
- I tune the prioritisation weights and thresholds; all batches are recalculated and the change is audited.
