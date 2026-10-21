# Latest update / actual release status

## Implemented in source this turn
- Security: TenantContext no longer accepts attacker-controlled X-Company-Id/X-Branch-Id headers to select another authorized tenant. ContextController authenticated selection claims remain authoritative.
- Customer ticket Razor MVC screen at `/support/tickets` plus fetch-based list and create workflow.
- Server-side antiforgery on ticket create/update API endpoints; UI sends anti-CSRF header.
- Basic ticket request validation and responsive ticket list/form.
- Windows build/test script at `scripts/SOURCE_SANITY_CHECK.ps1`.

## Still pending / not verified
- No .NET 10 SDK or SQL Server available in the tool runtime: compilation, migrations, accounting reconciliation and IIS integration **NOT TESTED**.
- Several Enterprise modules remain scaffold-only, dynamic roles and customer-specific workflows incomplete.
- Complete agentic AI, background job execution, approval workflows, accounting controls, production localization, and hardening are **NOT COMPLETE**.
- Customer delivery requires authenticated UI tests, database backup, migrations, UAT and rollback plan on a staging server.

This package is a **development update**, NOT a certified live production release. Never run new migrations against customer data without a verified backup and migration review.
