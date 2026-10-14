# 5. Real-world scenarios

Each scenario uses the normal model (templates, items, movements, child batches, releases) – no special cases.

## Contract manufacturing (CMO)
*Medisynth Contract Pharma manufactures the batch at its site; we release it.*
- The CMO site is a `Site` with `SiteType = ContractManufacturer`, linked to its `ExternalParty`.
- The template **"Contract manufactured tablets (CMO)"** matches on site type and applies: CMO approved status &
  quality agreement valid · CMO executed batch record review · CMO CoA received & reviewed · Goods receipt &
  transport temperature log (WMS) · Identity testing on receipt (LIMS, depends on receipt) · QMS check including
  CMO deviations · Regulatory approval per market (CMO site registered) · QA review · Final release.
- An `ExternalMovement` (purpose *Contract manufacturing*) records API/excipients supplied, at vendor, finished
  goods returned. Recording the return completes the linked goods-receipt item automatically.
- Unapproved parties cannot be selected; the quality agreement expiry date is held on the party.

## Contract testing (third-party lab)
*The microbial assay for Azithromycin is done by Spectra Analytical Labs.*
- Template **"Azithromycin – outsourced microbial assay"** (matches product + own site) adds: Samples sent to
  contract lab · Contract lab CoA received & reviewed (depends on samples sent); QA review depends on the CoA.
- A movement (purpose *Contract testing*) tracks samples sent, at lab, expected back and CoA returned; days out
  and overdue returns are visible in Batch 360 and the Batch map. Returning the movement completes the CoA item.
- Any product can use the same pattern by adding the two items to its template, or to one batch via change control.

## Relabelling / repacking at another party
*We manufacture and pack; PrimePack Labelling Services relabels part of the batch for South Africa.*
1. The parent batch is released **for further processing** (release kind *FurtherProcessing*, signed). It is not
   counted as a market release.
2. A **child batch** of type *Relabel* (or *Repack*) is created from the parent's Genealogy tab
   (`MET26N123-RL1`). It inherits product and expiry and can never expire after the parent.
3. Template **"Relabelling at external vendor"** applies: Parent released (completed automatically when the
   parent is released) · Market-specific artwork approved · Material sent to labelling vendor (depends on parent
   release) · Labelling record & label reconciliation at vendor · Returned & receipt inspection · Regulatory
   approval for the market · QA review · Final release.
4. A movement (purpose *Relabelling* / *Packaging*) tracks the vendor leg.
5. The child is released for its market; genealogy links both ways for recall traceability.

## Other scenarios covered
| Scenario | How |
|---|---|
| Rework / reprocessing | Child batch of type *Rework* with "Investigation closed & rework approved", rework protocol, re-testing; parent under investigation |
| Partial release / split | *Partial* release with quantity; balance stays held; final release completes when the remaining quantity is released |
| Multiple markets | One `BatchMarket` per market, each with its own regulatory item and shelf-life rule; release can name a market |
| Quarantine, QA hold, investigation | Disposition with reason; held batches drop to the bottom of the queue |
| Rejection, recall | Disposition with e-signature; recall linked to complaint deviation/CAPA |
| Re-test, OOS investigation | Item Blocked with reason + OOS quality event; penalty lowers readiness |
| Conditional release | *Conditional* release with justification; pending items listed on the signed record and stay open |
| Expiry near release date | Deadline uses the 75 % dispatch limit; flags "near"/"passed"; expired batches cannot be released |
| Shelf-life extension | Expiry override requires a reason (e.g. CC number), recorded on the batch and in the audit trail |
| Released but in warehouse | Warehouse aging from first release to dispatch; partial dispatches supported |
| Post-release issues | Recall + quality events linked to the batch; genealogy shows affected children |

## Suggested additional scenarios (for your review)
1. **Stability commitment batches** – first three commercial batches with a stability pull item that does not block release.
2. **Market-specific release by QP** – EU/UK QP certification as the final item (templates provided), including
   import testing for products made outside the EU.
3. **Temperature excursion in transit** – movement exception that opens a deviation and blocks receipt.
4. **Quality agreement expired** for a CMO/lab – block new movements and flag affected batches.
5. **Serialization / track & trace** – DSCSA (US) or FMD (EU) data verification (US template provided).
6. **Reduced testing / skip-lot** – optional QC items made N/A with justification under an approved skip-lot programme.
7. **Returned goods / reprocessing of returns** – child batch from returned stock.
8. **Retest date for API-like products** – deadline based on retest instead of expiry.
9. **Batch record corrections after release** – change control against a released batch with QA approval.
10. **Customer-specific CoA and release documents** – customer templates matched by customer.
