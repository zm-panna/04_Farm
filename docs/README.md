# Current documentation index

Start with the root `README_BN.md`, then:

- `ENTERPRISE_REVISION_CHANGES.md`: requested enterprise modules, actual workflow boundaries and acceptance gates.
- `ENTERPRISE_POLICY_AND_WINDOWS_GUIDE_BN.md`: reviewed country/currency/timezone/tax configuration and Windows PC/IIS paths.
- `HR_PAYROLL_WORKFLOW.md`, `CRM_WORKFLOW.md`, `FIXED_ASSET_WORKFLOW.md`, `ECOMMERCE_WORKFLOW.md`: end-to-end module workflows.
- `REVISION2_CHANGES.md`: preserved historical core revision changes.
- `REQUIREMENT_MATRIX.md`: current feature coverage and remaining acceptance requirements.
- `CURRENT_VERIFICATION.json`: actual evidence from this revision, distinct from live approval.
- `ENTERPRISE_VERIFICATION_COMMANDS.md`: repeatable source, JavaScript and isolated localhost HTTP checks.
- `LIVE_DEPLOYMENT_RUNBOOK_BN.md`: SQL Server, secrets/keyring, IIS, backup and release procedure.
- `LIVE_UAT_CHECKLIST_BN.md`: customer staging acceptance evidence to collect.
- `DEMO_LOGIN_AND_SQL_BN.md`, `DEMO_SEED_MANIFEST.md`: isolated fixtures, secure local initialization, expected rows.
- `FARM_BUSINESS_WORKFLOWS.md`, `FINANCE_AND_INVENTORY_WORKFLOWS.md`: actual supported workflows and domain limits.
- `OPENING_BALANCE_WORKFLOW.md`, `STOCK_TRANSFER_WORKFLOW.md`: reviewed opening and exact branch transfers.
- `MFA_AND_LOCAL_ACCOUNT_RECOVERY.md`: authenticator enrollment, recovery codes and trusted local recovery.
- `MULTILINGUAL_REPORTS.md`, `CORE_LOCALIZATION_VERIFICATION.md`: four-language/RTL source coverage and acceptance boundaries.
- `PRODUCTION_SECURITY_AND_RESTART.md`: encrypted SQL, cookie/key protection, restart and server requirements.
- `API_EXAMPLES.md`: current cookie/tenant/anti-forgery contract.

`history/` preserves the uploaded package's earlier assessments for traceability. They are not current status, installation instructions or proof of release approval. Some say the SDK/migrations/implemented services do not exist; those statements described earlier source snapshots and have been superseded. Do not run commands copied from history.
