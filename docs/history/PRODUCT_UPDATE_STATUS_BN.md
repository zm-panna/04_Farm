# Product Master Update — 2026-10-19

**Source-level feature, NOT production certified.**

- /products: tenant/branch-scoped product list, search, pagination, current stock and unit cost display.
- /products/new: create product SKU and name; inventory balance/cost always zero initially.
- /products/{id}/edit: edit SKU and name only, not stock or cost.
- Server-side permission demands Inventory.View and Inventory.Edit, validated tenant context, anti-forgery on writes, audit entry and duplicate SKU handling.
- English, বাংলা and Banglish UI labels added (not full app localization).
- Role management allowlist extended with Inventory.Edit.

## Important gaps

.NET 10 SDK and SQL Server unavailable in authoring runtime: restore/build/migrations/IIS integration **NOT TESTED**.  Existing other Enterprise modules remain incomplete. Product master is not a full SKU variations, barcode, pricing, tax and supplier catalog module.

On Windows, run dotnet restore, dotnet build, dotnet test, database migrations and manual browser UAT. Run acceptance tests for company isolation, duplicate SKU, unauthorized POST, stock invariance and concurrent edits. Do not deploy until passed.
