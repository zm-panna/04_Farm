# Farm requirements and release evidence

Current engineering revision: QR/barcode scanner Revision 7, 2026-10-20. See `SCANNING_WORKFLOWS_BN.md` for implemented actions and boundaries. Revision 6 trusted hosting and the `REALTIME_AND_LARGE_DATA.md` query/cache/notification contract are retained. This is the authoritative feature assessment; `history/` is preserved historical context, not current approval. A source implementation and its local tests do not substitute for SQL Server, IIS, provider or customer acceptance.

**Release decision: NOT PRODUCTION APPROVED.** This enterprise revision implements the four confirmed modules plus reviewed company currency/timezone/tax policies. First live is confirmed as one company in Bangladesh, retaining all four modules. See `FIRST_LIVE_BANGLADESH_REVIEW.md` for the corrective changes and `CURRENT_VERIFICATION.json` for this revision’s actual verification status and hashes, and `ENTERPRISE_REVISION_CHANGES.md` for precise workflow boundaries. SQL Server, Windows/IIS, real-browser and jurisdiction-specific acceptance remain separate gates. Historical Core Revision 2 counts are preserved under `history/` and must not be mistaken for this revision’s verification.

## Core workflows implemented

| Requirement | Actual scope and route | Verification / remaining acceptance |
|---|---|---|
| .NET 10 modular source | Solution `.slnx`, pinned SDK/tool contract, production SQL Server provider, isolated SQLite development provider | Release build/tests/publish; actual Windows deployment still required |
| Company/farm/branch | Authenticated selection, membership checked on every operation; platform-only company/branch creation | Relational tests, HTTP tenant isolation; actual SQL Server concurrency still required |
| Identity/admin | Secure local bootstrap, staff creation, explicit password-confirmed audited offline mailbox-ownership review, protected existing-account attach, scoped revoke/offboarding, change password | No shipped passwords, no fake Identity hashes; local tests and real HTTP login |
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
| Large-data registers | 47 real DataTables server-side grids on active workflows and `/data`, SQL-filtered counts/paging/allowlisted order; own-portal scope preserved | Deep-page/search capacity requires representative SQL Server load measurement; legacy action dropdowns are not all async search widgets |
| Redis read cache | Optional safe-page cache with explicit tenant/branch/user/language keys, transactional SQL generation invalidation, bounded TTL/size and outage fallback | Real loopback Redis tested; deployed TLS/ACL/size/latency remain staging gates; no financial or auth decisions cached |
| Internal messages/notifications | Durable inbox/read records, idempotent explicit send, SQL flood limits/audit/outbox, authenticated content-free SignalR and reconnect fetch | Real two-node Redis mechanics tested with disposable SQLite; external communications and production multi-IIS acceptance are separate |
| UI/languages | Responsive source styles; four-language static and structured dynamic labels, known validation, lifecycle states/preferences and Arabic RTL; raw user content preserved | 84 Node checks, compiled Razor cases and HTTP forms; browser/mobile/native-speaker UAT not yet verified |
| Older actionable records | Bounded pages/search/open filters for orders, returnable documents, bank lines, vouchers, invoices and approvals; actual selected results drive controls | Real HTTP actions beyond 50/100/200-record boundaries, scoped counts and preserved query state |
| QR/barcode scanner | Product/feed, animal, asset, approved PO and document label resolution; seven confirmed service workflows, printable QR/Code128, owned immutable review/cancel/history | See `SCANNING_WORKFLOWS_BN.md`; signed-delta adjustment, branch model only. Physical devices, printer, real browser and SQL Server concurrency remain unverified |
| Production safety | HTTPS/secure cookies, CSRF, lockout, protected persistent keyring, security headers, immutable financial history | Source/security tests; actual IIS app-pool/TLS/key restart/backup restore evidence required |
| Schema | Real committed SQL Server migration/snapshot, model check, idempotent SQL | SQL generated and reviewed in source; not applied to an actual SQL Server here |
| SQL with synthetic data | Guarded pristine-fixture exporter and packaged schema/data SQL; actual synthetic Identity hashes, no plaintext or known default password | 95 mapped tables supported, including optional fictional enterprise fixture; protected target/path/overwrite checks tested; actual SQL Server import remains unexecuted |
| Demo fixture | Five distinct scoped Identity users plus five optional synthetic enterprise reviewers, coherent linked business records, atomic/idempotent seed, explicit target guard | Counts and security-table exceptions in `DEMO_SEED_MANIFEST.md`; no seed on customer DB |

## Confirmed enterprise workflows and remaining scope / real inputs

| Area | Current status | Required next input/work |
|---|---|---|
| HR / Payroll | Implemented staff, attendance, leave, explicit reviewed Daily/Monthly/Mixed calculation with immutable effective-dated compensation, independent approval/posting, register and own slips | See `HR_PAYROLL_WORKFLOW.md`; jurisdiction rules, real policy values and payroll UAT remain required |
| CRM | Implemented leads, opportunities, activities, consent, reviewed quotes and conversion | See `CRM_WORKFLOW.md`; external outreach not dispatched |
| Fixed assets | Implemented acquisition, custody, straight-line/None book depreciation and disposal, independent review and ledger | See `FIXED_ASSET_WORKFLOW.md`; statutory tax depreciation/revaluation/impairment not supplied |
| LC / Import-Export | Inherited scaffolds incomplete | Currency, trade documents, duties, settlement rules and inclusion |
| E-commerce | Implemented catalog/cart/checkout/reservation/fulfillment/returns and own customer access | See `ECOMMERCE_WORKFLOW.md`; branch collection and manual pending payment, no payment provider |
| Hotel | Not confirmed or implemented | Separate inclusion and business requirements needed |
| Store / warehouse bins / lots | Branch inventory and interbranch transfer implemented; bins/batches/expiry allocation incomplete | Concrete location/lot/unit-conversion requirements; no claim these are done |
| Configurable currency/timezone/payroll/tax | Explicit independently reviewed effective-dated settings and immutable snapshots implemented | Bangladesh first live requires its actual independently reviewed policies and accountant-reviewed chart. Country selection does not establish statutory compliance/filing. Exact Arab countries and US states/localities are inputs for later expansion, not blockers for the confirmed Bangladesh-only launch |
| Existing customer data conversion | Not performed | Authorized source data, mapping, reconciled control totals, staging restore and approval |
| External payment/messaging/provider | No real transaction/dispatch/integration acceptance | Named provider, credentials through secure configuration, sandbox contracts and authorization |
| Advanced autonomous AI actions | Deliberately not exposed as unrestricted execution | Explicit safe action schema, approval policy, threat review and action-specific authorization |
| MFA organization policy | MFA source available; organization rollout policy pending | Required users, break-glass custody, enrollment/offboarding and recovery approval ownership |
| Windows SQL/IIS rollout | Not executed | Accessible approved staging target, valid certificates/identities and deployment authorization |
| Backups/monitoring/load/UAT | Not verified on production topology | Actual restore rehearsal, alerts, expected capacity and customer sign-off |

Disabling an unfinished module prevents misleading or unsafe routes; it does not complete that requirement. No unresolved row above is silently excluded from the user's request. The owner confirmed HR/Payroll, CRM, Fixed Assets and E-commerce for first-live scope; they are implemented within the documented boundaries. Hotel, LC/import-export and advanced warehouse inclusion remain unconfirmed. First live is one Bangladesh company. Its real reviewed business policies and external acceptance evidence remain required. Exact Arab countries and US state/locality are deferred expansion inputs.

## Revision 6 trusted backend / vault extension

Shared v1.1 OpenBao adapter, explicit SQL/cache/backplane/Data Protection bindings, default fail-closed Production/Staging, hosted EF command/commit gates, background/AI/hub gates, exact single-proxy/public-origin profile and separate secret-free PublicGateway transport are implemented. Current evidence: 880 .NET, 1,201 localhost HTTP, 66 JS and 6 negative startup probes. Full MVC-to-REST rewrite, real OpenBao/SQL Server/IIS/TLS deployment, actual browser acceptance and customer approval are not claimed. See `TRUSTED_BACKEND_AND_VAULT_BN.md`.

## Revision 7 scanner acceptance

Confirmed current totals: 923 .NET, 1,333 real localhost HTTP, 84 Node/JavaScript and 6 fail-closed startup probes, all passing. Seven business action families, source/label/access revalidation inside existing transactions, own review history, printable QR/Code128 and actual offline WASM decoding are verified locally. The sample SQL contains 95 tables / 1,345 synthetic rows. Exact source and binary hashes are in `CURRENT_VERIFICATION.json`. Physical devices, real browser, SQL Server/IIS and customer approval remain separate.
