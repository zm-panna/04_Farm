/*
  RETIRED: the earlier direct-SQL fixture did not preserve accounting/stock invariants.
  Use scripts/SEED_DEMO.ps1, or the explicit --seed-demo EF command, instead.
  The typed EF seed creates business data and 5 Identity users atomically and idempotently.
  Password hashes are created only through ASP.NET Core Identity UserManager.
  No application table is modified by this compatibility guard.
*/
THROW 51000, 'Direct SQL demo insertion is disabled. Use scripts/SEED_DEMO.ps1 or --seed-demo; see docs/DEMO_LOGIN_AND_SQL_BN.md.', 1;
