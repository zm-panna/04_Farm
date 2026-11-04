# PC-independent development

Repository: https://github.com/zm-panna/04_Farm

Development branch: `codex/cloud-development`.

## Source provenance

The cloud workspace began at `7a3cd50af3358f6396456269b10c6a5dadcb58f2`,
which was GitHub `main` on 2026-11-04. The other remote branch,
`codex/local-ai-windows` at `ba6c59f31004456a1008727d5de4d0011486eb66`,
is one commit ahead and adds only 312 files under `publish/`. Its application
source is identical to main. Cloud development therefore starts from main's
source without importing generated Windows binaries. Both existing remote
branches are preserved; no force push or merge is required.

This verifies GitHub source parity only. Uncommitted or unpublished changes on
the user's PC cannot be verified from this environment.

## Fresh cloud workspace

```bash
git clone --branch codex/cloud-development https://github.com/zm-panna/04_Farm.git
cd 04_Farm
# Install the .NET SDK specified by global.json using the official installer
# or the cloud environment's supported SDK setup; retain proxy and CA trust.
bash scripts/cloud-verify.sh
```

`global.json` selects .NET 10 (minimum SDK 10.0.100). The verification script
restores packages, builds the complete solution, runs the existing unit tests,
and creates a publish package. Logs, test results and published files are kept
in ignored `.cloud/`. It does not start the application, run migrations, seed
data, or deploy anything. Private package/SDK caches can live outside the repo.
Keep credentials, database exports, backups and customer data out of commits.

In the saved cloud environment, the checkout is `/workspace/04_Farm` and the
SDK is `/workspace/.dotnet`. Before using the script there:

```bash
export PATH="/workspace/.dotnet:$PATH"
export DOTNET_ROOT=/workspace/.dotnet
export DOTNET_CLI_HOME=/workspace/.dotnet-home
export NUGET_PACKAGES=/workspace/.nuget/packages
bash scripts/cloud-verify.sh
```

Commit reviewed source changes to this branch and push normally. A fresh cloud
session can fetch the branch without the user's PC being online. Never reset or
force push over divergent work; fetch and compare it first.

## Live hosting remains a separate step

A development workspace and GitHub branch are not a continuously running
production service. Production needs an approved host and budget, SQL Server,
reviewed migrations and data migration, TLS/domain setup, secure secret delivery,
persistent Data Protection keys, backup/restore verification, monitoring, and
the repository's production security and customer acceptance gates. SQLite is
supported only for isolated Development demos/tests. Ollama is disabled by
default; enabling AI requires a reachable approved service independent of the PC.

Do not weaken the existing production startup checks to make a preview run.
No production deployment, live database operation, paid resource provisioning,
or security/credential change is part of this source-development setup.
