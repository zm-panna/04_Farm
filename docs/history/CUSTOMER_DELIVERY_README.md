# Smart Farm ERP — customer delivery gate (2026-10-18)

## Actual verification in this environment
- Original customer-support ZIP located and extracted, source patch applied.
- Updated code and scripts packaged; ZIP CRC check performed.
- **.NET 10 build: NOT TESTED** (`dotnet` SDK unavailable).
- **SQL Server migration: NOT TESTED** (SQL Server and CLI unavailable).
- **IIS: NOT TESTED** (no Windows/IIS).
- **UAT: NOT TESTED** (no running target environment / customer test credentials).
- Existing modules include scaffolds; they are NOT all fully implemented.

## Improvements in this source update
1. User-facing company/branch selector (`/Context`) and server-side membership validation.
2. Tenant API context can use authenticated identity's selected company/branch instead of mandatory custom headers; explicitly provided headers are still checked against company/branch memberships.
3. First-tenant/admin one-time bootstrap via `--bootstrap-admin` with secrets from environment, using Identity UserManager hashing.
4. Default authentication required for endpoints unless explicitly marked AllowAnonymous.
5. Windows PowerShell first-admin provisioning and release gate scripts.

## Known blockers and release risks
- The source does not include a reviewed EF Core initial migration. A developer must generate, review, and apply it against a BACKUP/STAGING database using `dotnet ef migrations add InitialCreate --project EnterpriseERP.Web --startup-project EnterpriseERP.Web` and `dotnet ef database update`. DO NOT auto-create a migration against the customer's existing database without schema comparison.
- Not all module controllers/services, finance requirements, AI and scheduler are complete.
- Global user claims hold the selected company/branch across sessions/devices; future revision should use per-session server-side active context and avoid conflicting simultaneous branch selections.
- A missing subscription policy/invoice should not be mistaken for a paid subscription; tenant onboarding must define and verify subscription state.
- UserPermissions grant/revoke screen is only a basic capability; a comprehensive role model still needs implementation.
- Verification is not a substitute for penetration testing, user acceptance, accounting reconciliation and deployment validation.

## Windows setup (new/empty database only)
1. Install .NET 10 SDK, SQL Server and dotnet-ef 10 CLI compatible with installed SDK; run `dotnet --info`.
2. Configure `ConnectionStrings__DefaultConnection` using environment variables or secured deployment settings. Do not commit connection strings with passwords.
3. Run `scripts/CUSTOMER_RELEASE_CHECK.ps1` without migration to verify restore/build/tests.
4. Create and review initial EF migration (if none exists), perform backup and apply only to an EMPTY database. Existing customer database requires separate mapping/migration reconciliation.
5. Run `scripts/PROVISION_FIRST_ADMIN.ps1 -ConnectionString '<connection-string>' -Email '<admin-email>' -Company '<company>' -Branch '<branch>'` (password entered securely). Only valid for empty user/company database.
6. Run site, login and select authorized company and branch at `/Context`.
7. Complete customer UAT checklist, create verified SQL backup, publish with `dotnet publish -c Release`, install ASP.NET Core 10 Hosting Bundle on IIS, configure app pool No Managed Code and secured HTTPS binding; smoke-test actual site.

## Customer acceptance checklist (must PASS before live)
- [ ] Restore / build / automated tests and fix all failures
- [ ] Initial EF migration reviewed / applied in staging
- [ ] Tenant and branch access checks, unauthorized URL/API requests
- [ ] Concurrent stock deduction, duplicate invoice and voucher posting
- [ ] Debit/credit and cost-of-goods-sold reconciliation
- [ ] Farm lifecycle, veterinary, dairy, fattening and expense workflows
- [ ] Subscription partial payment, 14/15/29/30-day suspension scenarios
- [ ] Ollama outage, tool permissions, prompt-injection and approval replay
- [ ] Reliable job restart/retry, duplicate events, audit and notifications
- [ ] Exports (PDF Bengali fonts / XLSX / CSV), backup/restore and IIS HTTPS
- [ ] Customer sign-off and rollback rehearsal

STATUS: Development source package; **NOT ready for customer production handover**.
