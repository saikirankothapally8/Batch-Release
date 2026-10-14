# 4. Prioritisation engine and metric definitions

Code: `backend/src/BatchReleaseIQ.Domain/Engine/ReadinessEngine.cs` and `Metrics.cs`. Unit tests: `EngineTests.cs`.

## Readiness
```
readiness = (Σ weight of mandatory items Completed or N/A) / (Σ weight of mandatory items) × 100  − penalty
            (the final release item itself is excluded: 100 % = "everything needed is done – release now")
penalty   = blocked items × BlockedItemPenalty + Σ open quality events (OpenEventPenalty, or CriticalEventPenalty
            for critical ones), capped at MaxPenalty
```
- **Weights**: each item's weight comes from its template; if blank it is derived from the step position,
  rising linearly from `MinStageWeight` (first step, default 1) to `MaxStageWeight` (last step, default 3), so
  items nearer release count more. Change control can adjust an item's weight for a single batch.
- Released batches are 100 %.

## Deadline
`deadline = earliest of (Target release date, Shelf-life dispatch limit, Expiry date)`

**Shelf-life dispatch limit** = the last day the batch can leave the warehouse with at least the required
share of shelf life remaining (default **75 %**, configurable per market; the strictest of the batch's markets
applies): `expiry − ceil(total shelf-life days × 75 %)`.

## Ranking
1. Batches that can be released (not on hold / quarantine / investigation) first.
2. **Readiness band**, highest first (band = ⌊readiness / band size⌋, default 10 %).
3. Within a band, **earliest deadline** first.
4. Then higher readiness, then batch number.

## Flags
| Flag | Rule |
|---|---|
| Overdue | Not released and today > target release date |
| At risk | Not overdue and (estimated release date > target, or target within `AtRiskBufferDays` with items pending) |
| Shelf-life limit near | Dispatch limit within `ExpiryWarningDays` (default 30) |
| Shelf-life limit passed | Today > dispatch limit (release then needs a justification) |
| Expired | Today > expiry (release blocked) |
| Stuck | No checklist item completed for more than `StuckDays` (default 7) |
| Blocked | Any item Blocked |
| Held | Disposition On hold / Quarantine / Under investigation |
| Open QE | Any open quality event |
| At vendor | Any movement Sent out / At vendor |
| Change pending | Any checklist change request pending |

**Priority label**: Critical (overdue, expired, shelf-life limit passed or deadline ≤ 3 days), High (at risk,
shelf-life near or deadline ≤ 7 days), Medium (≤ 21 days), Low.

## Time to release
Remaining mandatory items are grouped by step. Items in a step run in parallel, so a step takes as long as its
slowest item; steps run one after another. Each item's duration is the **historical average for the same item code
over the last 12 months** (start → completion), falling back to `DefaultItemDurationDays`; an item already in
progress counts only its remaining time.

## Explanation ("why it is here")
Built from the same numbers, for example:
`52.5% ready · 2 items pending: Regulatory approval for target market (IN), QA batch documentation review ·
blocked: Regulatory approval – Awaiting CDSCO variation approval · target release 18-10-2026 in 4 days`

## Metric dictionary (single source of truth)
| Metric | Definition |
|---|---|
| Released | Final QA release completed (full or conditional) – `ReleasedAt` set |
| Partially released | Some quantity released, final QA release not yet completed |
| Ready for Release | All mandatory items except final QA release are Completed or N/A (active batches) |
| In pipeline (active) | Not released, not rejected, not recalled |
| Overdue | Not released on or before the target release date |
| On-time release | Final QA release date ≤ target release date; **On-time %** = on-time ÷ released |
| Stuck | Active and no checklist item completed for more than N days |
| Open quality events | Deviation, OOS, OOT, CAPA, Change Control with status Open |
| Warehouse age | Days since first release decision while released quantity is not fully dispatched |
| Stage duration | Item start (or batch creation) → completion, completed items only |
| Planned (period) | Target release date in the period |
| Released (period) | Final QA release timestamp in the period |

All admin parameters are on **Administration → Prioritisation**; saving recalculates every batch and is audited.
