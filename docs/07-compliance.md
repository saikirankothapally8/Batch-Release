# 7. Compliance (GxP, 21 CFR Part 11, EU Annex 11, ALCOA+)

| Requirement | Implementation |
|---|---|
| Access control (Part 11 §11.10(d), Annex 11 §12) | Unique user IDs (never reused), PBKDF2 password hashes, password policy, lockout after 5 failed attempts (30 min), 60-min token expiry, 30-min idle logoff, admin-defined roles with an activity catalogue checked on every API call |
| Audit trail (§11.10(e), Annex 11 §9) | Captured automatically in `AppDbContext.SaveChanges` for every create/update/delete: who, when (UTC), what (entity, field), old and new value, why (reason). Written in the same transaction as the change. **Append-only** – database triggers reject UPDATE/DELETE on `AuditEntries` and `Signatures` (PostgreSQL and SQL Server). Logins, failed logins, lockouts and signatures are audited too |
| Reason for change | Required for reopening items, N/A, blocking, editing batch data, expiry overrides, template changes, quality-event changes, change requests, holds, rejections and recalls |
| Electronic signatures (§11.50, §11.70, §11.100, §11.200) | Password re-entry at every signing; the manifest (printed name, date/time, meaning) is shown before signing and stored with the record; signatures are bound to the signed content with a SHA-256 hash; a user can only sign as themselves |
| Segregation of duties | Configurable: the batch creator cannot release it; a requester cannot approve their own checklist change |
| Operational checks (§11.10(f)) | Dependencies enforce sequence; final release only through the signed release action; full release blocked while items are pending or events open |
| ALCOA+ | **Attributable** (user on every change and signature) · **Legible** (enums stored as text, DD-MM-YYYY display) · **Contemporaneous** (server-side UTC timestamps) · **Original** (manual vs integrated source recorded; integration bypass flagged) · **Accurate** (validation, proper date fields) · **Complete / Consistent / Enduring / Available** (no deletes, master data deactivated, metric dictionary, exports) |
| Data integrity of integrations | `Source`, `SourceReference`, `LastSyncedAt` on items, markets and events |
| Evidence | Files stored with SHA-256 checksum |

## Still to do before a validated release (phase 2)
- EF Core **migrations** per provider (the MVP creates the schema on first start).
- Electronic-signature **certificate of identity** process, password ageing / history policy, SSO option.
- Notifications and escalations (email / in-app) for due dates, blocked items, overdue batches.
- Validation package: URS / FS traceability, IQ/OQ scripts (the unit tests are a basis), risk assessment.
- Time synchronisation and backup/restore procedures per deployment.
