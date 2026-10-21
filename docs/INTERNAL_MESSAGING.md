# Internal messaging and durable notifications

## User workflow

Open **Internal messages** after selecting an authorized company and branch. `Messaging.View` permits inbox, sent history and message detail. `Messaging.Send` additionally permits searching current eligible branch colleagues and sending one internal message to one recipient. Sending requires both permissions. The Permissions screen can grant these two named permissions; a company-scoped wildcard also covers them.

- Subject: 1–160 characters; body: 1–8,000 characters. Whitespace-only input and unsupported control characters are rejected.
- The recipient must currently belong to the same company and branch and have `Messaging.View`. Search is bounded to 30 eligible matches and starts at two characters.
- Messages are immutable after sending. There is no edit/delete API and no attachment, email, SMS, third-party send or arbitrary broadcast.
- The recipient's explicit **Mark as read** action is durable and audited. Opening a message does not silently acknowledge it. The sender sees “Saved to the recipient's inbox,” never an invented delivered/read status.
- Inbox, unread inbox, sent history and automation alerts use 30-row descending ID keyset pages. Offline messages remain accessible through older pages.
- Branch automation alerts require fresh `Automation.Review` independently of messaging permissions. Their read receipts are per user. The existing deterministic automation worker is unchanged.

## Security boundary

Every REST operation and every public hub operation rechecks the authenticated user's exact Identity ID, current security stamp, active company/branch claim uniqueness, current branch/company membership and current company permission against the relational store. Locked-out users fail eligibility checks. Recipient discovery and direct sends apply the same fresh scope and permission checks; unknown and ineligible recipient errors are indistinguishable.

Message detail is available only to its sender or recipient within the selected authorized company/branch. Only the exact notification owner can acknowledge it. Composite relational foreign keys prevent a notification from claiming another message recipient or branch. Message content and receipt ownership are immutable at `AppDbContext.SaveChanges`; direct SQL remains an administrator trust boundary.

Mutating REST endpoints require the application's `X-CSRF-TOKEN` antiforgery header. Responses are `no-store`. User-controlled subject, body and names are written with `textContent`; they are never HTML or localization keys. The page uses the checked-in official SignalR client, without a CDN.

The hub exposes only `Sync()`. It does not accept a user ID, tenant ID, group name, message body or permission claim from a client. The server derives an opaque SHA-256 group from the current user, scope and security stamp. A changed stamp derives a different group; expired authentication closes the connection. Browser WebSocket connections with a foreign `Origin` are rejected.

## Persistence, retries and flood protection

`InternalMessages`, `UserInboxNotifications`, `MessagingOutbox`, the audit entry and two rate-bucket updates commit in one serializable relational transaction. An unsuccessful transaction rolls back all of them. The authenticated sender/company/branch plus a UUID idempotency key is unique. An exact replay returns the original message ID without another message, audit or rate debit; a changed payload under the same key returns a conflict.

The browser keeps the current send key while an uncertain request is retried, prevents simultaneous repeated submissions and resets the key after confirmed success. Pending drafts and keys are held only in memory, not in local storage. A page reload loses an unsent draft.

`MessagingRateBuckets` enforces 20 messages per minute and 200 per UTC day for each sender/company/branch. SQL uniqueness, row-version concurrency, check constraints and the send transaction coordinate nodes. A concurrent database conflict is a safe retryable HTTP 409, not a bypass of the rate budget. An exhausted budget returns HTTP 429 with `Retry-After`. Historical buckets are retained for now; no automatic destructive retention job is installed.

## SignalR, outbox and recovery

The hosted dispatcher examines at most 100 pending outbox rows every three seconds. Conditional SQL updates claim rows with a one-minute lease, allowing concurrent nodes and recovery after crashes. It rechecks the recipient's current membership, permission, lockout state and stamp immediately before each hint.

The only notification frame is `inboxChanged` with an empty argument list. It contains no message text, IDs, user identifiers, tenant metadata or unread count. The client then performs a freshly authorized REST fetch. The outbox column `HintProcessedUtcTicks` records processing, not successful delivery or read.

- Failed hint sends retain the row for retry after ten seconds.
- A crash after a send but before completion can produce duplicate hints; refreshing the authoritative inbox is idempotent.
- Revoked recipients receive no hint. The durable inbox is not deleted, so a later authorized session can catch up.
- A disconnected browser may miss every hint and still recover all messages through REST.
- Initial load, automatic reconnect, visibility restoration and manual Refresh fetch the latest page. The visible page also refreshes every 20 seconds when the tab is visible.
- Automation alerts use that same authorized polling/reconnect catch-up. They do not fan out copies to every branch member or emit automation content over SignalR.

Redis is optional transport infrastructure, never the durable message store. Single-node operation works without it. For more than one application node, configure the optional backplane through `Scaling:SignalR:RedisEnabled`, `Scaling:SignalR:Connection` and an environment-specific `Scaling:SignalR:ChannelPrefix`. The connection must be supplied securely; production requires TLS. Nodes also need the same relational database and the application's established shared Data Protection/cookie configuration. Apply the reviewed EF migration before enabling the feature. No user infrastructure was provisioned or deployed by this change.

## Synthetic demo

The existing explicit enterprise-demo seed invokes `MessagingDemoSeeder` inside its guarded transaction. The base demo leaves the new messaging tables empty. The enterprise fixture adds:

- Five clearly labelled synthetic branch handovers from existing fictional reviewers to fictional demo actors
- Five inbox rows, with two synthetic read examples and three unread examples
- Five pending outbox rows, ten historical rate buckets, five synthetic automation alerts and five reviewer acknowledgements
- Twelve related audit rows and seven narrow demo grants; the original Viewer does not gain send permission

The helper refuses an unguarded/non-demo transaction or existing message data. It never runs on ordinary startup, changes credentials, or sends external messages. The normal seed fingerprint/export checks still protect untouched-fixture exports.

## Verification

- `MessagingSecurityTests.cs`: relational SQLite scope/ownership/permission/stamp checks; bounded inputs; immutable content; composite FKs; replay conflict; rate limits; rollback after writes; paging; automation receipts; outbox offline recovery/lease/revocation; direct hub authorization and method surface.
- `scripts/TEST_MESSAGING_UI.js`: deterministic JavaScript checks for literal text rendering, keyset paging, CSRF, uncertain/repeated sends, stale responses, revocation clearing, recipient search, reconnect, departure cleanup and localization without translating user text.
- `scripts/http-messaging-smoke.py`: real localhost Identity cookies, MVC page, REST/CSRF, actual SignalR JSON WebSocket handshake/invocation/content-free hint, offline catch-up, flood protection, permission revocation and security-stamp invalidation. It creates and destroys its own synthetic SQLite database, takes no external destination, and requires the official app build plus Python `websockets`.
- `scripts/http-messaging-backplane.py`: two real localhost application nodes sharing a disposable SQLite database and an owned official Redis 7.2.16 process. The receiving node is paused while the sending node publishes and processes the outbox, then resumed to prove an actual cross-node empty hint. Redis is then stopped completely: sends and other-node REST still work, the SQL outbox remains pending, and restarting Redis allows reconnect and drain. Run with `FARM_TEST_REDIS=127.0.0.1:16380` in one execution shell; the script refuses an already occupied endpoint and cleans up its processes.

Recorded development evidence: `evidence/http-enterprise-messaging/results.json` passed 59 checks; `evidence/http-enterprise-messaging-backplane/results.json` passed 34 checks with two nodes and Redis 7.2.16. Both reports record the exact tested DLL SHA-256. The deterministic JavaScript suite passed 12 checks.

Unit tests and local harnesses do not establish production SQL Server runtime behavior, production Redis/TLS/topology, load-balancer affinity, or browser rendering. Those deployment checks remain explicit acceptance gates.
