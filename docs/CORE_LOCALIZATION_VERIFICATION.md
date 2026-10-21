# Core and enterprise UI localization verification

The shared application layout loads `farm-core-translations.js` before `farm-i18n.js`. English is the source key. Bengali, Banglish and Arabic display values are explicit dictionary entries. The globally loaded stock-transfer dictionary registers its entries through the same engine. No runtime translation service, CDN, external font or network request is used.

## Covered behavior

- Saved language restores from `farmsphere-language` localStorage, with a same-site cookie fallback when storage is blocked.
- English, Bengali (`lang=bn`), Banglish (`lang=bn-Latn`) and Arabic (`lang=ar`, `dir=rtl`) are supported. Switching away from Arabic restores `dir=ltr`. Invalid stored/selected values fall back to English.
- The dropdown has an accessible translated label. Language changes announce through a polite live region; a keyboard skip link targets the main content.
- Explicit `data-i18n`/`data-business-i18n`/`data-transfer-i18n` elements, field labels, known system enum displays, placeholders and ARIA-label markers are translated. User-entered notes, names, identifiers, amounts and field values are not changed.
- Known operational validation messages and common ASP.NET required/range/minimum-and-maximum-length/decimal messages are translated. Parameterized matches require recognized application field labels and numeric limits; invalid-value echoes, arbitrary provider messages and unknown error meanings are preserved as returned.
- Native required/minimum/maximum/step/type validation uses the selected language and clears obsolete custom messages on input or language change. Server validation is still authoritative.
- Arabic mirrors sidebar placement, mobile drawer, active navigation indicator and table alignment. Number/date/email/password/authenticator entry and code displays retain LTR isolation. Fonts are self-hosted by the release packaging; no font CDN is needed.
- Mixed-value dashboard/report/account labels wrap only the static prefix or suffix. Form-control wrapper labels are never replaced; translated select options retain explicit original values. Empty conditional markers preserve numeric values.
- Shared success/error notices and dynamically inserted marked elements are refreshed without replacing unmarked user record contents. When a validation node is reused, its new server message replaces the old translation source rather than restoring a stale error.

## Automated checks

Run from the repository root:

`node scripts/TEST_UI_LOCALIZATION.js`

The enterprise verification pass on 19 October 2026 passed 31 tests, including stored language, dropdown and announcement, English restoration, required/range/precision messages, preservation of user text and numeric values, blocked storage, native validation, Arabic/RTL persistence and reset, extension merging, safe form-control/option handling, structured option templates, enum allowlists, notification wording, reused error nodes, malformed metadata, placeholder preservation and explicit missing-party provenance. Source coverage checks 1873 explicit static elements and 2034 combined dictionary keys with Bengali, Banglish and Arabic values. See `evidence/localization-tests.log` for the exact run.

This is deterministic DOM-adapter and source verification, not a substitute for browser, assistive-technology or native-speaker acceptance. New pages or server errors must add their display keys to the dictionary and rerun the check. Existing unused `dairy-language.js` and `farm-business-language.js` are not loaded by the shared layout or new forms; the shared engine owns the current language choice.


The source count measures marked display strings, not every possible runtime message. Explicit templates cover trade/feed stock availability, original documents, settlement options (with a separate missing-party path), invoice balances, breeding cycles, automation rules/proposals and support status transitions. Finite lifecycle markers cover order/document/payment/subscription/job/approval states, support status/priority/activity and milk sessions. Missing-party labels translate only when the scoped server lookup confirms no party; a real party named “Unassigned” remains unchanged. Template values are JSON encoded by Razor and rendered with textContent in one replacement pass. User names, notes, references, numeric strings and long IDs are never reinterpreted as translation keys or HTML. Brand names, technical permission/account/reference codes, user-entered content and arbitrary provider/infrastructure errors remain unchanged. The source audit additionally reviewed bare static text and mixed dashboard/report labels rather than treating dictionary coverage alone as full UI acceptance. Arabic visual layout, screen-reader interaction, and financial/clinical terminology still require customer/native-speaker review.


Support pagination is verified separately with `node scripts/TEST_SUPPORT_TICKETS_UI.js` (8 passing checks; `evidence/support-ui-tests.log`). The view contains all required pager IDs. Previous/next boundaries, page reset on filter change, canonical filter values, localized empty/error notices, Arabic/Bengali live re-rendering without refetching, and unchanged numeric/customer content are covered. Ticket rendering uses text nodes rather than interpolated HTML. These are deterministic DOM-adapter checks, not a claimed browser acceptance pass.


## Compiled-view regression coverage

`UiLocalizationRenderingTests` executes the actual compiled Razor views and tag helpers with an isolated MVC service provider, an ephemeral data-protection provider, and no web listener, application startup, database or external provider. It covers encoding and raw-value preservation in Trade, Accounts, Feed, Breeding, Subscription, Automation, Support, Dairy, Accounting Period and master-editor markup. It distinguishes real parties named `Unassigned` from server-generated fallbacks and verifies that 64-bit IDs remain strings in display metadata. These focused cases are part of the aggregate .NET run, whose result is recorded separately in the current verification evidence. They do not claim browser or native-speaker acceptance.

## Enterprise additions

The four enterprise modules, company/tax settings and AI review checklists use the same renderer. Finite payroll basis/proration, asset methods/states and tax-treatment values are allowlisted. Ecommerce tax-option templates preserve the original code, version, rate and treatment values. Exact known application validation messages have reviewed dictionary entries; composed unknown provider/framework messages remain literal. A bounded audit of 45 enterprise/settings/AI Razor views found 698 literal/allowlisted keys with no missing dictionary entry or unsafe user-content wrapper. See `evidence/enterprise-untranslated-ui-review.json`. This is source coverage, not a claim that every possible dynamic error or native-language nuance has been accepted.
