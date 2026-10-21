// Deterministic script/CSP regressions. No network or third-party packages.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '..');
const javascript = name => fs.readFileSync(path.join(root, 'EnterpriseERP.Web/wwwroot/js', name), 'utf8');
let checks = 0;
function test(name, fn) { fn(); checks++; console.log('PASS ' + name); }
function run(name, document) { vm.runInNewContext(javascript(name), { document }); }
test('navigation opens and closes with accurate aria-expanded state', () => {
 const attributes = {}, handlers = {}, linkHandlers = {}; let open = false;
 const toggle = { addEventListener(name, action) { handlers[name] = action; }, setAttribute(name, value) { attributes[name] = value; } };
 const shell = { classList: { toggle() { return open = !open; }, remove() { open = false; } } };
 const link = { addEventListener(name, action) { linkHandlers[name] = action; } };
 run('navigation-menu.js', { getElementById: id => id === 'nav-toggle' ? toggle : shell, querySelectorAll: () => [link] });
 handlers.click(); assert.equal(open, true); assert.equal(attributes['aria-expanded'], 'true');
 linkHandlers.click(); assert.equal(open, false); assert.equal(attributes['aria-expanded'], 'false');
});
test('context selector preserves valid choice and disables other-company branches', () => {
 const handlers = {}; const company = { value: '1', addEventListener(name, action) { handlers[name] = action; } };
 const options = [{ value: '11', dataset: { company: '1' } }, { value: '12', dataset: { company: '1' } }, { value: '21', dataset: { company: '2' } }];
 const branch = { value: '12', options, selectedIndex: 1 };
 run('context-selection.js', { getElementById: id => id === 'companyId' ? company : branch });
 assert.equal(branch.value, '12'); assert.equal(options[2].disabled, true); assert.equal(options[2].hidden, true);
 company.value = '2'; handlers.change(); assert.equal(branch.value, '21'); assert.equal(options[0].disabled, true); assert.equal(options[2].disabled, false);
 company.value = '3'; handlers.change(); assert.equal(branch.selectedIndex, -1); assert.equal(options.every(option => option.disabled), true);
});
test('both scripts tolerate absent optional UI', () => {
 for (const name of ['navigation-menu.js', 'context-selection.js']) run(name, { getElementById: () => null });
});
test('all Razor scripts are external self-hosted assets and have no inline handlers', () => {
 let scripts = 0;
 function walk(directory) {
  for (const item of fs.readdirSync(directory, { withFileTypes: true })) {
   const file = path.join(directory, item.name);
   if (item.isDirectory()) walk(file);
   else if (file.endsWith('.cshtml')) {
    const markup = fs.readFileSync(file, 'utf8');
    for (const match of markup.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)) {
     scripts++; assert.match(match[1], /\bsrc="~\/(?:js\/[^"<>]+|vendor\/(?:signalr\/signalr\.min\.js|datatables\/dataTables\.min\.js))"/, file); assert.equal(match[2].trim(), '', file);
    }
    assert.doesNotMatch(markup, /\son[a-z]+\s*=|javascript\s*:/i, file);
   }
  }
 }
 walk(path.join(root, 'EnterpriseERP.Web/Views')); assert.ok(scripts >= 6);
});
test('CSP denies inline scripts and eval while keeping styles separately scoped', () => {
 const source = fs.readFileSync(path.join(root, 'EnterpriseERP.Web/SecurityHeadersMiddleware.cs'), 'utf8');
 const scriptPolicy = source.match(/script-src[^;]+/)[0];
 assert.equal(scriptPolicy, "script-src 'self'"); assert.doesNotMatch(scriptPolicy, /unsafe-inline|unsafe-eval/);
 assert.match(source, /style-src 'self' 'unsafe-inline'/);
});
console.log(`PASS ${checks} browser security checks`);
