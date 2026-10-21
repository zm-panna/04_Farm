# Current evidence: Revision 7

923 .NET tests, 1333 real localhost HTTP checks, 84 Node/JavaScript checks and 6 fail-closed startup process probes pass with zero failures. The full current index and compiled/source hashes are in `docs/CURRENT_VERIFICATION.json`; release logs are `evidence/scanning/final/`. Twelve HTTP suites record matching Farm and TrustedInfrastructure hashes.

Scanner coverage includes 43 domain cases, 121 authenticated HTTP checks and actual server label decoding. Seventeen camera-helper checks decode synthetic QR/Code128/EAN/UPC using real pinned WASM. Ten actual served-UI jsdom/live-API assertions are nested under the scanner HTTP suite, not counted twice. Physical camera and real browser rendering were not exercised.

Current full regression includes real loopback Redis cache/outage and two-node SignalR WebSockets/backplane, actual vendor DataTables with jsdom, and 66 separate gateway transport checks. Disposable Development SQLite and disabled vault transport fixtures do not establish production SQL Server/OpenBao/TLS/IIS approval.

Demo export freshly verified 95 tables / 1,345 rows / 100 balanced vouchers; random sample password discarded. New migration and idempotent/additive SQL generated without contacting SQL Server. Prior evidence is historical unless explicitly indexed. Production approval remains false.
