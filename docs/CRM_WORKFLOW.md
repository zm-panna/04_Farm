# CRM workflow and operational boundaries

## What is implemented

- Company-and-active-branch-scoped leads, contacts, lead source and notes. Reference codes are normalized and unique within that scope. Archive/restore retains history; open opportunities or activities prevent archiving.
- Marketing-consent states Unknown, Granted and Withdrawn. A consent change requires an explicit review and evidence for Granted/Withdrawn. Granted consent needs a contact method. Changing the identity of a consented contact requires fresh evidence or Unknown. Append-only events retain actor, time, prior state and evidence. No consent state causes outbound contact.
- Reviewed qualification creates an opportunity. Reviewed stages follow Qualified → Proposal → Negotiation, with Negotiation → Proposal for rework, or an open stage → Lost. Lost cancels other open quotes. Won occurs only through quote acceptance. Closed opportunities cannot silently reopen.
- Internal Task, Call and Meeting activities have due dates, notes, open/done/cancelled state, and completion actor/time/evidence. They do not place calls, send invitations or messages. Existing open activities retain a closed opportunity link when their due date or notes are edited; new/reassigned links require an open opportunity. Lists include due-date ordering and an overdue filter using approved company-local business date; if a policy is missing, a visible warning identifies a temporary UTC highlighting fallback.
- Multi-line draft quote snapshots (1–50 lines) reuse active-branch StockItems. SKU, description, reviewed unit, quantity, unit price, explicit line discount and net amount are retained. Recipient contact/address snapshots are refreshed only on draft save. Product renames and lead contact changes do not rewrite a saved snapshot.
- A quote snapshots the explicitly approved company business-policy ID/version, currency code and currency precision. There is no FX conversion. Quantity supports up to three decimal places; price/discount use approved currency precision. Each gross line is rounded once, away from zero; net totals sum those rounded lines after explicit discounts. Nonpositive nets, excess precision, over-range arithmetic and discounts equal to/exceeding gross are rejected.
- Quote states: Draft → Reviewed → Accepted; Reviewed → Draft explicitly invalidates review; Draft/Reviewed → Cancelled. Expired/future-dated quotes cannot be reviewed or accepted. Policy/currency validity is checked at quote date and current company-local date.
- Acceptance requires fresh quote, opportunity and lead concurrency tokens, customer acceptance evidence and an explicit confirmation. Staff choose an active scoped Customer or a unique new code. Existing Customer details are never overwritten; a new Customer uses the quote's recipient snapshot. Already-linked leads must explicitly reuse that customer. One immutable internal sales-ready record is created per opportunity and quote; other open quotes for the opportunity are cancelled.
- MVC screens cover lists with search/status filters and pagination, record details, create/edit forms, reviewed action forms, CSRF protection, encoded user data, and validation/stale-action feedback. Related summaries show the latest 10 records with full-list links; consent history is paginated.

## What accepting a quote means

Acceptance is an internal record that staff reviewed evidence of the customer's acceptance. It creates or links a Customer in TradingParty, marks the opportunity Won and creates an immutable sales-ready handoff referencing the accepted quote's immutable lines.

It does **not** send a quote/email/message, reserve or consume stock, create a ProductSale, invoice, FinanceDocument, tax posting, payment or ledger voucher. The core Sales workflow must be reviewed and posted separately. The CRM handoff is not proof that an order was fulfilled or financially posted. There is no automatic reconciliation between a handoff and a later manually posted sale.

All CRM monetary values are conspicuously **net-only estimates**. Tax is neither calculated nor assumed to be zero, even when approved tax policies exist elsewhere. Configure and review the appropriate currency, tax and stock treatment before posting Sales. CRM does not claim jurisdictional compliance.

## Access, integrity and failure safety

Permissions: CRM.View, CRM.Manage, CRM.Review. The reviewer permission controls stage transitions and quote review/acceptance. Reviewer and maker can be the same authorized person for CRM; this module does not claim dual-person approval.

Every lookup/action validates authenticated company/branch access. Related lead/opportunity/customer/product lookups are scoped again, and composite foreign keys enforce company/branch consistency. Unique scoped references plus unique quote/opportunity handoff keys block duplicate conversion. Mutable records use rowversion on SQL Server and application-generated concurrency tokens on SQLite. Hidden IDs alone never grant access.

Mutations use serializable transactions; failure rolls back and clears EF's change tracker before any retry. Audit records carry company, branch in the reference, actor and operation. Quote acceptance, accepted lines, handoffs and consent history cannot be rewritten or deleted through ordinary context saves. Reviewed snapshots must first return to Draft before edits. Archive/cancel operations preserve records.

## Demonstration data

EnterpriseDemoSeeder.SeedCRM is called only by the explicit enterprise demonstration opt-in. It contributes one synthetic lead, consent event, opportunity, activity, accepted quote, quote line, internal sales-ready record and audit event per company. Across five companies this means five records in each CRM table. Contact addresses use example.invalid. Consent/acceptance evidence is explicitly fictional and must never be treated as permission or a customer commitment. No demo tax rate is inferred by CRM.

## Verification

CRMWorkflowTests exercise isolation, permissions, reviewed stages, consent evidence and identity changes, snapshot arithmetic, invalid precision/overflow, stale/repeated actions, draft replacement, invalid customer conversion, expired dates, approved currency requirements, historical immutability, pagination, and rollback after a customer has been saved inside an uncommitted conversion. CRMRenderingTests render compiled views and check forms, tokens, disclosures, field names, encoding and paginated filter links.

Deployment still needs normal environment/database backups, migrations, real company policy approval, real permissions, user acceptance and business review. Synthetic fixtures and passing automated tests are not a claim that a live deployment is complete.
