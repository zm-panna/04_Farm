param([Parameter(ValueFromRemainingArguments=$true)][object[]]$LegacyArguments)
throw 'This historical script is retired. Run scripts/VERIFY_SOURCE.ps1 for source verification, scripts/FINAL_RELEASE_VERIFY.ps1 for fail-closed live evidence, and follow docs/LIVE_DEPLOYMENT_RUNBOOK_BN.md. No database was modified.'
