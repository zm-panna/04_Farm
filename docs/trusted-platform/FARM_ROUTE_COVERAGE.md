# Farm gateway coverage and boundaries

Supported topology is one fixed public hostname per Farm instance at root `/`. Gateway is transport; trusted Farm backend remains the authority for authentication, company/branch selection, permissions, CSRF and workflow approvals.

- Account/Context, Dashboard and all enabled MVC module routes: forwarded unchanged. No blanket gateway permission bypass.
- `/api/tables/{key}`: real DataTables protocol, SQL-scoped counts and pages. All 47 catalog definitions retain permission checks.
- `/scan`, `/api/scan/*` and authorized print/image routes: scoped lookup, label lifecycle, owner-only reviewed drafts, CSRF and fresh business permissions on the trusted Farm backend. Same-origin decoder assets under `/lib/zxing-wasm-3.1.5` are forwarded; no frame uploads or gateway authorization bypass.
- `/api/messaging/*` and `/Messaging`: durable internal inbox/message operations, ownership checks, CSRF for changes.
- `/hubs/messaging` and negotiate/WebSocket: backend cookie authentication, exact public origin, server-assigned groups and content-free hints.
- `/api/*`, `/ops/*`, report/export and business admin routes: delegated to existing backend authentication and module-specific authorization; no gateway-created identity.
- `/js`, `/css`, `/vendor`, `/fonts`, other existing static roots: forwarded.
- `/_platform`, `/_vault`, `/v1/sys`, `/v1/auth`, `/v1/secret`, `/v1/kv`, `/v1/identity`: blocked publicly by platform gateway.
- No general base-path rewrite, new customer public signup, provider payment webhook, SMTP service, full business REST rewrite or offline untrusted executable data store is claimed.

The current acceptance script `scripts/http-trusted-gateway.py` exercises representative authenticated routes, static assets, CSRF rejection and success, table protocol and tenant separation, backend-issued cookies, actual SignalR WebSocket transport, reserved-route denial and spoofed-header/origin rejection. Existing direct-backend regression suites still exercise all previously tested business workflows. Production TLS/certificate/IIS/load acceptance must be separate.
