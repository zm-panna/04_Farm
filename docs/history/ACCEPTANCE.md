# Acceptance checklist (all NOT TESTED)
- dotnet restore/build succeeds
- Razor area route and view discovery for all 23 modules
- EF SQL Server migration + schema creation
- Seed idempotency and first admin provisioning
- Two-company isolation across direct URLs, APIs, exports and files
- Company/branch selection UX (currently missing)
- Low-stock → PO → receipt → journal (missing)
- Milk yield and animal costing full pipeline (missing)
- Sale → invoice → payment → GL (missing)
- Double posting, concurrency, transaction replay tests
- Security/CSRF and penetration tests
- Localization, responsive UI, PDF/Excel/CSV
- Ollama availability and tool security (missing)
- Durable automation, billing, messaging, approval engine (missing)
- Deployment, backup, restore, rollback and IIS tests

**Important:** No service, interface or package should be considered production-ready until these are implemented and verified.
