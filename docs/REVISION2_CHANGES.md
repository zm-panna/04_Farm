# Follow-up core revision — 19 October 2026

This revision follows the previously delivered core checkpoint. The earlier archive remains unchanged. Deployment approval and the inherited requirements still awaiting business scope or real environments remain open in `REQUIREMENT_MATRIX.md`.

## Corrected workflows

- Older operational records remain reachable through bounded pages, reference search and open/unmatched filters: purchase orders, returnable documents, bank lines, cash vouchers, overdue subscription invoices and pending independent approvals. Action selectors and rendered controls use the selected results; unrelated filter/report state is retained.
- Product edits submit their original concurrency token. Missing/stale tokens return HTTP 409 and preserve metadata, stock values and audit history. Reload the latest form before retrying.
- Animal acquisition cost now accepts the service's exact three-decimal range. Existing acquisition costs and weights cannot be changed through master metadata edits.
- Missing or malformed active/inactive inputs cannot silently deactivate a trading party or automation rule. Explicit `false` remains a supported intentional action.
- Manual journal and bank-statement creation record actor, company, action, reference and time atomically with the business record. Duplicate or failed persistence cannot leave misleading audit entries. Stock creation also records product creation and explicit normalized unit assignment.
- The stock API now requires `unitOfMeasure`. Product/animal API and master text validation reject unsupported lengths and embedded control characters consistently.
- Four-language display now includes structured document/payment/invoice options, lifecycle statuses, support transitions, dairy sessions, system notifications and operational pagination. Names, notes, references, IDs and numeric values remain unchanged. Generated “Unassigned” labels are distinguished from real parties with that name. Unknown infrastructure/provider messages are not machine-translated.

## Verification boundaries

The final dated counts and exact hashes are in `CURRENT_VERIFICATION.json`. Tests include actual compiled Razor rendering, relational SQLite rollback/concurrency checks, deterministic JavaScript DOM adapters, and disposable localhost HTTP forms with more than the old 50/100/200-record limits. The HTTP fixture is synthetic and temporary; its large history is not the five-company demo seed or customer data.

SQL Server execution, Windows/IIS, real browser/mobile interaction, native-language/customer acceptance, external providers, backup restoration and production concurrency/load remain separate acceptance gates. No live deployment or customer database change was performed.
