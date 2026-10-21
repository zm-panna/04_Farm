# Farm requirements and release evidence

Current engineering revision: 2026-10-19. This is the authoritative feature assessment; `history/` is preserved historical context, not current approval. A source implementation and its local tests do not substitute for SQL Server, IIS, provider or customer acceptance.

**Release decision: NOT PRODUCTION APPROVED.** The implemented core has passed the local source verification cycle: 453 .NET tests, an independent 453-test rerun, 407 real localhost HTTP checks and 40 deterministic JavaScript checks. Real browser execution was blocked. See `CURRENT_VERIFICATION.json` for dated execution results and exact artifact hashes. A clean extraction also restored, built and passed all 453 .NET tests and 40 JavaScript checks without reusing authoring binaries; all 291 runtime source input hashes matched. No completion percentage is assigned to unspecified/inherited requirements.

## Core workflows implemented

| Requirement | Actual scope and route | Verification / remaining acceptance |
|---|---|---|
| .NET 10 modular source | Solution `.slnx`, pinned SDK/tool contract, production SQL Server provider, isolated SQLite development provider | Release build/tests/publish; actual Windows deployment still required |
| Company/farm/branch | Authenticated selection, membership checked on every operation; platform-only company/branch creation | Relational tests, HTTP tenant isolation; actual SQL Server concurrency still required |
| Identity/admin | Secure local bootstrap, staff creation, protected existing-account attach, scoped revoke/offboarding, change password | No shipped passwords, no fake Identity hashes; local tests and real HTTP login |
| Permission management | Canonical operational catalog, protected wildcard/platform grants, server-side checks | Tested role, tenant and cross-branch denials; business least-privilege review required |
| Authenticator MFA/recovery | Password-confirmed enrollment, TOTP login, one-time recovery codes, privileged local audited recovery | Local regression tests and HTTP verification cycle; no email/SMS provider dependency |
| Livestock | Registry, immutable acquisition value, current weight from dated records, acquisition and sale journals | Cross-tenant, sale/duplicate/accounting tests |
| Dairy | Positive bounded milk records, animal/day/session uniqueness, date guards and audit | MVC/service regression tests; farmer acceptance required |
| Animal health | Observations/treatment records and follow-up due/completed lifecycle | Records/reminders only; no automated veterinary decisions |
| Feed | Atomic feed record, stock movement, exact inventory cost and expense journal | Precision, insufficient stock, closed-date and tenant tests |
| Fattening | Plans, chronological weights, feed/direct expense costs, realized/expected margins | Tested accounting inputs; no biological/market-price guarantee |
| Breeding | Open cycles and guarded pregnancy/birth/failure lifecycle, dated records | No diagnostic predictions or veterinary orders |
| Farm expenses | Reviewed expense entry and balanced dated journal | Permission, precision, closed periods and audit tests |
| Products and stock | Original-token master edits, explicit API unit and audited creation, append-only ledger, exact carrying values, adjustments | Legacy unknown UOM/valuation must be explicitly reviewed |
| Reorder policies | Branch item threshold/target and suggestions including open PO quantity | Suggestions do not send an order or spend money |
| Stock transfers | Same-company interbranch transfer, matching SKU/UOM, access to both branches, exact paired stock/value journals and immutable snapshots | Branch-level locations; no unit conversion, per-bin warehouse hierarchy, or lot/expiry allocation |
| Purchase orders | Single-line Draft/Approved/PartiallyReceived/Received/Cancelled, atomic receipt, supplier link | Approval is optional workflow; direct receipt remains separately permissioned; no external supplier send |
| Purchase and sales | Atomic product receipt/sale with stock, document and GL; linked supplier/customer | Single currency/unit model; tax/discount/multi-line invoice policy still requires specification |
| Parties | Scoped customer/supplier master, document balances | Document-based balances; not a complete statutory aging suite |
| Returns/refunds | Quantity/value-limited linked returns and payment/refund allocations | Exact residual cost, duplicate and overpayment checks |
| Accounts | Immutable balanced postings with atomic actor audits, ledger, trial balance, prefix-based financial summaries | Operational accounting; accountant must approve chart and classifications |
| Opening balances | One reviewed opening per financially empty branch; cash/other balance-sheet lines plus itemized zero-baseline stock/livestock and exact generated asset entries | Excludes synthetic AR/AP and existing live-book conversion; import of an established customer ledger needs reconciled source data and approved mapping |
| Accounting close | Monotonic closed-through date, no back-posting into closed periods | No reopening workflow; an authorized correction is a new permitted-period journal/document |
| Bank reconciliation | Atomically audited statement-line entry, exact one-to-one voucher matching and older unmatched-record search/pages | No bank connection or automated statement import |
| Reporting | Scoped real totals, CSV, XLSX, PDF, precise product gross-sales labels, missing-valuation fail-closed | English/বাংলা/العربية/Banglish exports, Arabic RTL and bundled Bengali/Arabic fonts; final rendered/native-language acceptance required |
| Support | Ticket create/detail/history, guarded statuses, assignment and explicit reopen | Tenant-scoped transactional audit and concurrency tests |
| Subscription | Notice/alert/grace/suspension policy, guarded MVC/API routes, trusted platform-only payment recording | Manual trusted bookkeeping; gateway verification and commercial contract/onboarding policy not supplied |
| AI Manager | Configurable disabled-by-default Ollama, bounded read-only facts, permission isolation and unavailable fallback | No connected provider tested; remote transmission requires approved endpoint/data policy |
| Automation | Scoped rules, dry-run, hash-bound separate-person approval, durable jobs, idempotent in-app notifications | No arbitrary model-driven financial write, email/SMS/WhatsApp dispatch, or provider outbox |
| UI/languages | Responsive source styles; four-language static and structured dynamic labels, known validation, lifecycle states/preferences and Arabic RTL; raw user content preserved | 40 Node checks, compiled Razor cases and HTTP forms; browser/mobile/native-speaker UAT not yet verified |
| Older actionable records | Bounded pages/search/open filters for orders, returnable documents, bank lines, vouchers, invoices and approvals; actual selected results drive controls | Real HTTP actions beyond 50/100/200-record boundaries, scoped counts and preserved query state |
| Production safety | HTTPS/secure cookies, CSRF, lockout, protected persistent keyring, security headers, immutable financial history | Source/security tests; actual IIS app-pool/TLS/key restart/backup restore evidence required |
| Schema | Real committed SQL Server migration/snapshot, model check, idempotent SQL | SQL generated and reviewed in source; not applied to an actual SQL Server here |
| SQL with synthetic data | Guarded pristine-fixture exporter and packaged schema/data SQL; actual synthetic Identity hashes, no plaintext or known default password | 55 mapped tables exported; protected target/path/overwrite checks tested; actual SQL Server import remains unexecuted |
| Demo fixture | Five distinct scoped Identity users, coherent linked business records, atomic/idempotent seed, explicit target guard | Counts and security-table exceptions in `DEMO_SEED_MANIFEST.md`; no seed on customer DB |

## Inherited requirements still pending scope or real inputs

| Area | Current status | Required next input/work |
|---|---|---|
| HR / Payroll | Inherited project scaffolds disabled; incomplete | First-live inclusion, personnel/payroll rules and statutory jurisdiction; then business implementation/tests |
| CRM | Inherited scaffold incomplete; party master is not a CRM | Accepted lead/opportunity/activity workflow and first-live decision |
| Fixed assets | Inherited scaffold incomplete; opening GL is not an asset register | Asset classes/depreciation/disposal policy and inclusion |
| LC / Import-Export | Inherited scaffolds incomplete | Currency, trade documents, duties, settlement rules and inclusion |
| Hotel / E-commerce | Inherited scaffolds incomplete | Whether licensed/included in this Farm release; separate business workflows are not implied by a project name |
| Store / warehouse bins / lots | Branch inventory and interbranch transfer implemented; bins/batches/expiry allocation incomplete | Concrete location/lot/unit-conversion requirements; no claim these are done |
| Statutory tax/currency statements | Not implemented | Approved jurisdiction, rates, currency/rounding, invoice law and accountant-approved chart; no invented defaults |
| Existing customer data conversion | Not performed | Authorized source data, mapping, reconciled control totals, staging restore and approval |
| External payment/messaging/provider | No real transaction/dispatch/integration acceptance | Named provider, credentials through secure configuration, sandbox contracts and authorization |
| Advanced autonomous AI actions | Deliberately not exposed as unrestricted execution | Explicit safe action schema, approval policy, threat review and action-specific authorization |
| MFA organization policy | MFA source available; organization rollout policy pending | Required users, break-glass custody, enrollment/offboarding and recovery approval ownership |
| Windows SQL/IIS rollout | Not executed | Accessible approved staging target, valid certificates/identities and deployment authorization |
| Backups/monitoring/load/UAT | Not verified on production topology | Actual restore rehearsal, alerts, expected capacity and customer sign-off |

Disabling an unfinished module prevents misleading or unsafe routes; it does not complete that requirement. No unresolved row above is silently excluded from the user's request. Owner confirmation is still required for first-live scope, and external gates remain open until their actual evidence exists.
