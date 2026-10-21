// Deterministic support pager and localization regression checks. No browser/network/packages.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const source = fs.readFileSync(path.join(root, 'EnterpriseERP.Web/wwwroot/js/support-tickets.js'), 'utf8');
const view = fs.readFileSync(path.join(root, 'EnterpriseERP.Web/Views/Support/Tickets.cshtml'), 'utf8');
class Element {
 constructor(tag = 'div') { this.tagName = tag; this.children = []; this.dataset = {}; this.attributes = {}; this.handlers = {}; this.disabled = false; this.value = ''; }
 set textContent(value) { this.children = []; this.text = String(value ?? ''); }
 get textContent() { return (this.text || '') + this.children.map(x => x.textContent).join(''); }
 append(...children) { this.children.push(...children); }
 replaceChildren(...children) { this.text = ''; this.children = children; }
 addEventListener(name, fn) { this.handlers[name] = fn; }
 setAttribute(name, value) { this.attributes[name] = value; }
}
async function fixture(payloads, language = 'en', missing = false) {
 const names = ['tickets', 'supportMessage', 'status', 'previousTickets', 'nextTickets', 'ticketPage', 'refresh'];
 const nodes = Object.fromEntries(names.map(x => [x, new Element()]));
 if (missing) delete nodes.previousTickets;
 const handlers = {}, calls = [];
 const document = { querySelector: key => nodes[key.slice(1)] || null, createElement: tag => new Element(tag), createTextNode: value => { const e = new Element('#text'); e.textContent = value; return e; }, addEventListener: (name, fn) => handlers[name] = fn };
 const context = { document, console, AbortController, encodeURIComponent, fetch: async url => { calls.push(url); const data = payloads.shift(); if (data instanceof Error) throw data; return { ok: true, json: async () => data }; } }; context.window = context;
 vm.createContext(context);
 vm.runInContext(fs.readFileSync(path.join(root, 'EnterpriseERP.Web/wwwroot/js/farm-core-translations.js'), 'utf8'), context);
 context.FarmSphereLanguage = { translate: key => language === 'en' ? key : context.FarmSphereTranslations[key]?.[language] || key };
 vm.runInContext(source, context);
 await new Promise(resolve => setImmediate(resolve));
 return { nodes, calls, handlers, context, setLanguage: value => { language = value; handlers['farmsphere:language']?.(); } };
}
let checks = 0;
async function test(name, action) { await action(); checks++; console.log('PASS ' + name); }
(async () => {
 await test('all required pager/script selectors exist exactly once in markup', () => {
  for (const match of source.matchAll(/document\.querySelector\('#([^']+)'\)/g)) assert.equal((view.match(new RegExp(`id="${match[1]}"`, 'g')) || []).length, 1, match[1]);
  assert.match(view, /id="ticketPage"[^>]*aria-live="polite"/);
 });
 await test('first page loads and buttons reflect total count', async () => {
  const f = await fixture([{ rows: [{ id: 1, ticketNumber: 'T-1', subject: 'Farm Expenses <script>alert(1)</script>', priority: 'High', status: 'Open' }], total: 26 }]);
  assert.equal(f.nodes.previousTickets.disabled, true); assert.equal(f.nodes.nextTickets.disabled, false); assert.equal(f.nodes.ticketPage.textContent, 'Page 1 / 2 (26 tickets)');
  assert.match(f.nodes.tickets.textContent, /Farm Expenses <script>alert\(1\)<\/script>/); assert.equal(f.nodes.tickets.attributes['aria-busy'], 'false'); assert.ok(!source.includes('.innerHTML'));
 });
 await test('next and previous navigation preserve numeric page values', async () => {
  const f = await fixture([{ rows: [], total: 26 }, { rows: [], total: 26 }, { rows: [], total: 26 }]);
  await f.nodes.nextTickets.handlers.click(); assert.match(f.calls[1], /page=2$/); assert.equal(f.nodes.nextTickets.disabled, true); assert.equal(f.nodes.previousTickets.disabled, false);
  await f.nodes.previousTickets.handlers.click(); assert.match(f.calls[2], /page=1$/); assert.equal(f.nodes.previousTickets.disabled, true);
 });
 await test('status filter resets to first page and keeps canonical submitted enum', async () => {
  const f = await fixture([{ rows: [], total: 26 }, { rows: [], total: 26 }, { rows: [], total: 1 }]); await f.nodes.nextTickets.handlers.click();
  f.nodes.status.value = 'InProgress'; await f.nodes.status.handlers.change(); assert.match(f.calls[2], /status=InProgress&page=1$/); assert.equal(f.nodes.nextTickets.disabled, true);
 });
 await test('Arabic and Bengali rerender labels without fetching or translating customer text', async () => {
  const f = await fixture([{ rows: [{ id: 1, ticketNumber: 'T-1', subject: 'Farm Expenses', priority: 'High', status: 'Open' }], total: 26 }]);
  f.setLanguage('ar'); assert.equal(f.nodes.ticketPage.textContent, 'الصفحة 1 / 2 (26 تذاكر)'); assert.match(f.nodes.tickets.textContent, /Farm Expenses/); assert.match(f.nodes.tickets.textContent, /مرتفع/);
  f.setLanguage('bn'); assert.match(f.nodes.ticketPage.textContent, /1 \/ 2 \(26/); assert.match(f.nodes.tickets.textContent, /Farm Expenses/); assert.equal(f.calls.length, 1);
 });
 await test('empty list and failed request show localized notices without null access', async () => {
  const empty = await fixture([{ rows: [], total: 0 }], 'ar'); assert.equal(empty.nodes.tickets.textContent, 'لم يتم العثور على تذاكر'); assert.equal(empty.nodes.nextTickets.disabled, true);
  const failure = await fixture([new Error('HTTP 403')], 'ar'); assert.equal(failure.nodes.tickets.textContent, 'تعذر تحميل التذاكر. HTTP 403'); assert.equal(failure.nodes.tickets.attributes['aria-busy'], 'false');
 });
 await test('an absent required pager exits safely rather than throwing', async () => { const f = await fixture([], 'en', true); assert.equal(f.calls.length, 0); });
 await test('all runtime status keys have Bengali, Banglish and Arabic translations', async () => {
  const f = await fixture([{ rows: [], total: 0 }]); const keys = ['Loading...', 'No tickets found', 'Page', 'tickets', 'Unable to load tickets.', 'Ticket created.', 'Open ticket', 'Unable to create ticket.'];
  for (const key of keys) for (const locale of ['bn', 'banglish', 'ar']) assert.ok(f.context.FarmSphereTranslations[key]?.[locale], `${key} ${locale}`);
 });
 console.log(`Support UI checks passed: ${checks}`);
})().catch(error => { console.error(error); process.exitCode = 1; });
