# Revision 5: server tables, bounded Redis cache and internal messaging

## What is active

`/data` is a permission-filtered register catalog. `/data/{key}` opens a real server-side DataTables screen; `/api/tables/{key}` implements the draw/count/page protocol. The same widgets are wired into the applicable existing operational pages under **Interactive server tables**. They load when expanded, so merely visiting a multi-register workflow does not request every register. Existing authenticated forms and server-rendered actions remain available. This is not a client-side download-and-filter table.

`evidence/realtime/GRID_INVENTORY.json` lists all 47 registrations, permissions, active workflow routes and cache status. `/Inventory` also exposes products and stock movements. Inventory and party fallback registers now have SQL search/paging, including the inventory action selectors for that page. Inventory reorder suggestions and exact carrying balances refer to that same selected page. `/data/products` searches the complete authorized product register.

### Inventory and explicit boundaries

- Stock/catalog/livestock: products, animals, movements, milk, health, feed, weights, fattening, breeding and stock transfers.
- Trading/accounts: purchase orders, receipts, product sales, finance documents, returns, parties, payments, bank lines, vouchers, expenses and opening balances.
- HR/payroll: staff, attendance, leave, policies, runs and the authenticated employee's own posted payslips. HR-only rows never contain compensation.
- CRM: leads, opportunities, activities, quotes and reviewed sales-ready handoffs.
- Assets: assets, depreciation runs and disposals.
- E-commerce: catalog administration, fulfillment orders and current customer's own orders. A wildcard administrator does not impersonate a customer or employee in an own-data grid.
- Service/settings: support tickets, automation notifications/rules/jobs/approvals, subscription invoices/payments and company/tax policy history. Company-wide records are explicitly marked and keep their existing company-wide permission boundary.
- Internal inbox/sent/automation acknowledgements use bounded keyset paging rather than offset DataTables. Administrative identity/grant screens retain their existing bounded server paging and protections; no generic Identity/session/security table is exposed by this API.
- Detail child collections, immutable payroll lines, quote lines, cart/order lines, custody/history timelines, document allocation lines, report totals and action form choices are not general-purpose DataTables endpoints. Existing bounded/domain-specific workflows remain. Some legacy action dropdowns still load their authorized choice lists; for very large catalogs, the direct data browser avoids those legacy forms until a narrower item is selected. This release does not claim every legacy dropdown is an asynchronous search widget.
- Hotel, LC, advanced warehouse and cross-currency conversion are not enabled by this change.

## Query contract and indexing

Every query starts with current SQL-verified company/branch/user access and an explicit module permission. Tenant filters are applied before both total and filtered counts. Own portals add user ownership; stock transfers require access to both branches. Company-wide histories intentionally use company scope and are labeled as such.

`draw` is parsed as a nonnegative integer and never cached. `start` is 0–2,000,000; `length` is 1–100, with no “all rows” mode. Search is literal, at most 120 characters; exact status is at most 40. Regex, per-column search, multiple order clauses, invalid directions and non-allowlisted columns fail with HTTP 400. Sorting is by ID, reference, description or status, with ID as a stable tiebreaker. Date/quantity/action headers deliberately do not sort. Global search uses parameterized EF string predicates, not interpolated SQL. `CountAsync`, ordering, `Skip` and `Take` run before materialization. SQL Server `OFFSET/FETCH` translation is tested independently of SQLite execution.

The migration adds scope+ID indexes where a key/index does not already cover the default order, and specific inbox/outbox/idempotency indexes. Arbitrary substring searches and exact filtered counts still cost SQL work. These indexes are not a claim that every substring query is index-seeking. A user-keyed 120 requests/minute/node HTTP limiter, 350 ms UI debounce, canceling obsolete requests and bounded page sizes reduce accidental flood. Multi-node aggregate ingress rate limits belong at the deployment gateway; this local read limiter is not a distributed quota. Message writes have separate durable database rate buckets.

## Redis cache

SQL remains the source of truth. Redis cache is optional and disabled in the shipped configuration. Enable it only after the operator configures a private trusted service. The current allowlist is **products, animals and HR-safe staff summaries**; financial, payroll, customer orders, permission/session/security decisions and message contents are not cached.

Keys include application prefix, company, branch, hashed user ID, language, hashed SQL generation and hashed normalized query. No raw user ID/search is put in a key. Values can include authorized catalog/staff names, so Redis access, transport and backup policy must protect these business records. No authorization result is stored in Redis. Permissions and membership are read fresh from SQL before a cache lookup.

`ScaleCacheEpochs` is SQL metadata. Every application `SaveChanges` updates the applicable generation in the same transaction as the mutation. A rollback rolls back both. A committed change makes previous Redis keys unreachable; this remains correct after a Redis outage. A fill rechecks generation before storing. Old unreachable entries expire normally, with no wildcard flush. Scoped raw update paths used by business services also save their audit in the same transaction, causing conservative company invalidation. Other writers, manual DBA edits, bulk imports or external services must participate in this generation contract or the operator must disable/rotate the cache namespace after that reviewed maintenance. No system can infer unannounced external database edits.

TTL is 1–120 seconds (default20), payloads at most64 KiB, and cache operation timeout50–1000 ms (default400). A fixed128 local semaphore stripe set prevents unbounded local lock growth. Redis SET NX owner leases provide cross-node fill coordination, expire after5 seconds, and release by compare-owner script. Waiting is bounded; long fills or Redis outages use SQL. Corrupt cache JSON/shape also falls back. The cache cannot convert an unsuccessful database transaction into a successful write.

Secure configuration names (supply locally through the deployment secret store; do not paste secrets into logs/chat):

- `Scaling__Cache__Enabled=true`
- `Scaling__Cache__Connection=<trusted private Redis configuration>`
- `Scaling__Cache__KeyPrefix=<unique application/environment prefix>`
- `Scaling__Cache__TtlSeconds=20`
- `Scaling__Cache__TimeoutMilliseconds=400`

Non-Development startup requires TLS in the configured Redis connection. Certificate validation must remain enabled. The test service is loopback plaintext only and does not verify deployed TLS/ACL/capacity.

## SignalR, durable notifications and messages

See [INTERNAL_MESSAGING.md](INTERNAL_MESSAGING.md). `/Messaging` is the real inbox/compose/read UI. `Messaging.View` and `Messaging.Send` are distinct grants. Requests use existing Identity and selected-company/branch controls. Sending requires explicit confirmation, bounded text, current sender and recipient membership/permissions, an idempotency key and antiforgery protection. Content is rendered as text. The durable inbox, unread state and transactional outbox survive disconnects. Real-time events contain only an empty `inboxChanged` hint; authorized REST performs catch-up and obtains content. Delivery of a hint is not proof that the person read a message. No external email, SMS, WhatsApp or provider send is implied.

Optional Redis SignalR backplane uses **separate configuration** from read caching:

- `Scaling__SignalR__RedisEnabled=true`
- `Scaling__SignalR__Connection=<trusted private Redis configuration>`
- `Scaling__SignalR__ChannelPrefix=<unique app/environment prefix>`

Setting the cache connection does not silently enable the backplane. Production backplane also requires TLS. Redis pub/sub is transient; the durable SQL inbox/outbox and reconnect/poll catch-up are what prevent business message loss. If a subscriber misses a published hint, its next authorized fetch still sees the persisted inbox. Disabling Redis caching or the backplane does not delete inbox records.

## Windows PC and IIS scale-out deployment

No migration, Redis installation, secret provisioning, permission grant, ACL change, IIS restart or deployment is run automatically by these files. Existing Windows/SQL/IIS preflight instructions remain in force.

1. Review the new SQL migration and indexes against a backup-restored staging database with the designated migration operator. Estimate index-build duration and locking with representative data. Keep runtime DB credentials separate from migration privileges. Never apply demo SQL to the user database.
2. Configure private Redis endpoint(s), TLS/ACLs, isolation/prefixes and memory/eviction policy locally. Do not expose Redis to the public internet. Redis is optional on a single Windows PC; Windows PC and one IIS node can run with both Redis options disabled.
3. Multi-IIS nodes must use the same application build, SQL database, Identity cookie configuration, Data Protection application name and securely shared key material. Machine-specific DPAPI keys are not a portable multi-node key ring; use an operator-reviewed shared certificate-protected setup. The source does not provision that access.
4. Follow Microsoft SignalR guidance for WebSockets and load-balancer session affinity. With Redis backplane, sticky sessions are normally required, except clients configured for WebSockets-only with skip-negotiation; this UI negotiates normally, so plan affinity. Test connection drain/reconnect during rolling restarts. Preserve existing authentication/security headers and HTTPS.
5. Run authenticated, tenant-scoped staging acceptance with representative authorized users. Revoke permissions while connections are open, interrupt Redis, reconnect, confirm own-data isolation and verify database state. The source harnesses use temporary SQLite fixtures and refuse remote target URLs; do not point them at a live database.
6. Choose a reviewed retention/archive policy for internal correspondence, audits, outbox rows and rate buckets. This release does not automatically purge messages or silently delete legal/business history. Monitor pending outbox age, failure counts, database size and Redis connection/fill rates.

## Evidence and performance measurement

`evidence/realtime` and the HTTP result folders contain actual checks with build hashes and explicit environment flags. The local Redis service is official7.2.16, verified by its source archive hash before compilation; the client libraries and vendored JS have package integrity evidence. Real Redis roundtrip/TTL/lease/invalidation/fallback, SignalR WebSocket, and a two-node backplane test are separately reported when run. Source-only SQL translation and generated migration SQL are **not** a live SQL Server execution test. Windows PC/IIS/SQL Server/TLS/load/UAT remain external acceptance gates.

For a staging benchmark, record data counts by branch, SQL/Redis versions, CPU/RAM/IOPS, network latency, index plans and cache settings; use synthetic representative distribution. Measure cold and warm search/count/page p50/p95/p99, maximum page payload, concurrent users, SQL logical reads/CPU, write latency, cache hit/miss/fill/fallback, outbox age and reconnect catch-up. Include deep pages, substring filters, a noisy tenant, Redis downtime and permission revocation. Compare the same workload with caching disabled. Establish the owner's acceptance thresholds before running a resource-heavy load test. No throughput or latency improvement percentage is asserted from unit tests.

Primary references: [DataTables server-side protocol](https://datatables.net/manual/server-side), [ASP.NET Core Redis backplane](https://learn.microsoft.com/en-us/aspnet/core/signalr/redis-backplane?view=aspnetcore-10.0), [SignalR authentication](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0), [distributed caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-10.0).
