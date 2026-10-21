# Scanner implementation and verification boundaries

## Runtime surfaces

- Domain: `EnterpriseERP.Domain/ScanningEntities.cs`.
- Persistence: `AppDbContext.Scanning.cs`, scoped foreign keys, unique reference/value hashes, state constraints, concurrency versions and owner/history indexes.
- Service: `ScanService.cs` and `ScanRetailBarcode.cs`. Resolution and immutable drafting do not change business data. Confirmation revalidates inside the existing service transaction using `ValidatePendingScan`.
- Controller/view: `Controllers/ScanController.cs`, `Views/Scan/*`, `/scan`, `/api/scan/*`.
- Browser adapter: `wwwroot/js/farm-scanning.js`; all record/notes text uses text rendering, and camera decoding only calls preview.
- Camera helper: `wwwroot/js/scanner-camera.js`, SHA256 `8ae68cc3fafd9b779db7192991dbcec5e547d1ab3de4ae390787012ce7fac72b`.
- Decode dependency: self-hosted zxing-wasm 3.1.5. Exact npm/upstream provenance, integrity and license records are in `wwwroot/lib/zxing-wasm-3.1.5/SOURCE.json` and `SHA256SUMS.txt`. Reader WASM SHA256 `aecc1876de036c62c8419f67a5e1a16b1698a325bcd190aa84810d516e263931`.
- Print encoder: official NuGet ZXing.Net 0.16.11. Server creates bounded geometry-only SVG, QR and Code128; identifiers are never embedded as executable markup.

## Reproducing bounded local checks

Use .NET 10 SDK and Node.js. `scripts/verify-source.sh` / `VERIFY_SOURCE.ps1` perform normal source verification. Run `node scripts/TEST_UI_LOCALIZATION.js`, the other `scripts/TEST_*.js` suites, and `node --test Tests/ui/scanner-camera.test.mjs` for deterministic source/DOM and real WASM checks. `scripts/http-scanning.py` uses a newly created private synthetic Development SQLite fixture and real local HTTP, never a supplied remote/customer connection. Set `FARM_TEST_JSDOM` to an installed official jsdom module path to include the actual served scanner DOM checks. It does not invoke a physical camera.

Full release evidence is indexed in `CURRENT_VERIFICATION.json`, including binary hashes, existing suite regressions, actual loopback Redis and gateway acceptance. Historical records elsewhere are not additive current counts. Local test Redis is a disposable process bound to loopback with persistence disabled. Do not run automated business smoke tests against a live database.

## Scanner-specific assertions

`Tests/EnterpriseERP.UnitTests/ScanWorkflowTests.cs` exercises all seven action families: signed stock adjustment, two-branch transfer, animal weight, operator-entered health observation, feed consumption, approved PO receipt and asset custody. Negative assertions include wrong context, user/company/branch, missing permission, expired/cancelled draft, changed payload, label revocation, source/stock/carrying-value change, reserved stock, destination membership, atomic rollback, immutable history, scoped foreign keys, bounded lists, checksum/alias collision, policy not configured, flood protection and two independent relational contexts racing one draft.

`http-scanning.py` exercises real Identity cookies/CSRF, labels, all seven actions, journal balance, current permissions and owner history. It decodes both actual server-generated print formats with actual offline WASM. `dom-scanning.js` loads the actual served HTML/scripts with live authenticated APIs, checks unreviewed confirmation rejection, cancellation, text-only notes, no initial camera prompt, synthetic decoded frame -> preview only, no frame upload, and all four active languages including Arabic RTL. These ten DOM assertions are nested under one HTTP acceptance check; do not add them again to the published HTTP/Node totals.

Seventeen shared-helper checks include actual synthetic QR/Code128/EAN13/EAN8/UPC-A/UPC-E decoding, cancel while permission is pending, close/reopen, stale detector result suppression, no audio, track shutdown and same-origin pinned fallback. These tests do not establish real-device camera behavior.

## Operational boundaries

Production remains unapproved until operator SQL Server/IIS/TLS/OpenBao/network/backup checks and user workflow acceptance. Root-host gateway transport is supported; nested IIS application paths are not claimed. An actual malicious trusted backend administrator can access process secrets; scanner labels do not change the vault trust model. Physical scanners/printers, real mobile/desktop browsers, native-speaker translations, production load and statutory policy suitability require separate acceptance. SQL Server scripts are review artifacts, not execution evidence.
