# 8. Roadmap

## Delivered in the MVP
Checklist templates with matching and versioning · batch creation with template suggestion · Batch 360 · Batch map
· prioritised QA queue with explanations and estimates · workbench · management dashboard with drill-down ·
change control with approval · e-signatures · partial / conditional / further-processing release · holds,
rejection, recall · dispatch and warehouse aging · CMO, contract testing, relabelling/repacking and rework
scenarios · roles & activities · users · master data · audit trail · PostgreSQL and SQL Server · realistic sample data.

## Phase 2
1. **Integration host** with LIMS, QMS, RIMS, ERP/MES and WMS adapters, scheduled + on-demand sync, exception queue (see architecture).
2. **Notifications & escalations** – email/in-app for due items, blocked items, overdue batches, pending approvals; escalation after N days.
3. **EF Core migrations**, deployment packages (container image, IIS package), backup/restore guidance.
4. **SSO** (OIDC / Azure AD / ADFS) as an alternative to built-in passwords.
5. **Multi-tenant SaaS** operations: database-per-tenant provisioning, tenant admin.
6. **Advanced scenarios** from the suggestions list (stability commitments, skip-lot, QP import testing, quality agreement expiry blocking, retest dates).
7. **Trend snapshots** for long-range analytics, scheduled PDF reports.
