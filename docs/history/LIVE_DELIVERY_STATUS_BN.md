# Smart Farm ERP — Live readiness truth report

This is a development workpack, **NOT a production-approved package**.

## Added in this release
- Animal Health record entry/history: MVC controller and Razor form, tenant scoping, permission guard, antiforgery, input validation.
- Customer live gate: SDK, restore/build/tests, committed migration detection, placeholder detection, staging HTTP verification; blocks deployment on failure.

## Mandatory outstanding tasks
- 22 Enterprise module scaffold views need working business flows and signed customer acceptance.
- EF Core migration files and SQL Server schema application/rollback testing.
- Verify full build and tests with .NET 10 on Windows (not available in authoring environment).
- Verify company isolation, financial reconciliation, workflows, subscription, AI, automation and user permissions using real staging data.
- Install .NET hosting bundle, configure IIS with HTTPS, secrets, backups, logs, least privilege and monitor staging.
- Customer UAT signoff.

Do NOT run database creation or migrations against production without a reviewed backup/rollback plan.
