// Deterministic inbox, interrupted-send and reconnect tests. No browser packages or external network.
const fs = require("node:fs"), path = require("node:path"), vm = require("node:vm"), assert = require("node:assert/strict");
const source = fs.readFileSync(path.join(__dirname, "../EnterpriseERP.Web/wwwroot/js/farm-messaging.js"), "utf8");
class Element {
    constructor(tag = "div") { this.tagName = tag; this.children = []; this.handlers = {}; this.attributes = {}; this.dataset = {}; this.value = ""; this.hidden = false; this.disabled = false; this.checked = false; }
    set textContent(value) { this.children = []; this.text = String(value ?? ""); }
    get textContent() { return (this.text || "") + this.children.map(x => x.textContent).join(""); }
    append(...items) { for (const item of items) item.tagName === "#fragment" ? this.children.push(...item.children) : this.children.push(item); }
    replaceChildren(...items) { this.children = []; this.text = ""; this.append(...items); }
    addEventListener(name, handler) { this.handlers[name] = handler; }
    setAttribute(name, value) { this.attributes[name] = value; }
    reportValidity() { return true; }
    scrollIntoView() {}
}
const page = (items = [], nextBeforeId = null, unreadCount = items.length) => ({ items, nextBeforeId, unreadCount });
const row = (id = 1) => ({ id, messageId: id, subject: "<img src=x onerror=alert(1)>", sender: "Colleague", recipient: "Recipient", createdUtc: "2026-01-01T00:00:00Z", readUtc: null });
const tick = () => new Promise(resolve => setImmediate(resolve));
function deferred() { let resolve; const promise = new Promise(r => resolve = r); return { promise, resolve }; }
async function fixture(responses = [], live = false) {
    const markup = fs.readFileSync(path.join(__dirname, "../EnterpriseERP.Web/Views/Messaging/Index.cshtml"), "utf8");
    const ids = [...markup.matchAll(/id="([^"]+)"/g)].map(x => x[1]);
    const nodes = Object.fromEntries([...new Set(ids)].map(x => [x, new Element()]));
    const root = new Element(); root.dataset = { canSend: "true", canReview: "true" };
    const tabs = ["inbox", "sent", "automation"].map(folder => { const e = new Element("button"); e.dataset.folder = folder; return e; });
    root.querySelector = () => ({ value: "csrf-fixture" });
    const descendants = node => [node, ...node.children.flatMap(descendants)];
    root.querySelectorAll = selector => selector === "[data-folder]" ? tabs : selector === "[data-i18n]"
        ? Object.values(nodes).flatMap(descendants).filter(x => typeof x.dataset.i18n === "string") : Object.values(nodes);
    const calls = [], timers = [], documentHandlers = {}, windowHandlers = {};
    const document = {
        hidden: false, getElementById: id => id === "farm-messaging" ? root : nodes[id],
        createElement: tag => new Element(tag), createDocumentFragment: () => new Element("#fragment"),
        addEventListener: (name, handler) => documentHandlers[name] = handler
    };
    const hub = { events: {}, stopped: 0, starts: 0, async start() { this.starts++; }, async stop() { this.stopped++; },
        on(name, fn) { this.events[name] = fn; }, onreconnecting(fn) { this.reconnecting = fn; }, onreconnected(fn) { this.reconnected = fn; }, onclose(fn) { this.closed = fn; } };
    let keys = 0;
    const context = {
        document, console, URLSearchParams, Date, encodeURIComponent,
        crypto: { randomUUID: () => "test-uuid-" + (++keys) },
        setTimeout: fn => { timers.push(fn); return timers.length; }, clearTimeout: () => {},
        setInterval: fn => { timers.push(fn); return timers.length; }, clearInterval: () => {},
        fetch: async (url, options) => {
            calls.push({ url, options });
            let data = responses.shift();
            if (data?.promise) data = await data.promise;
            if (data instanceof Error) throw data;
            const status = data?.status || 200, value = data?.value ?? data ?? page();
            return { status, ok: status >= 200 && status < 300, json: async () => value };
        }
    };
    context.window = { addEventListener: (name, handler) => windowHandlers[name] = handler };
    if (live) context.signalR = { HubConnectionBuilder: class { withUrl(url) { assert.equal(url, "/hubs/messaging"); return this; } withAutomaticReconnect() { return this; } build() { return hub; } } };
    vm.createContext(context); vm.runInContext(source, context); await tick();
    return { nodes, tabs, root, calls, responses, timers, documentHandlers, windowHandlers, hub, context };
}
function fill(f) {
    f.nodes["messaging-recipient"].value = "colleague";
    f.nodes["messaging-subject"].value = "Subject";
    f.nodes["messaging-body"].value = "Body";
}
let count = 0;
async function test(name, run) { await run(); count++; console.log("PASS " + name); }
(async () => {
    await test("all selectors exist in Razor and content is rendered as text", async () => {
        const view = fs.readFileSync(path.join(__dirname, "../EnterpriseERP.Web/Views/Messaging/Index.cshtml"), "utf8");
        for (const id of [...source.matchAll(/\$\("([^"]+)"\)/g)].map(x => x[1])) assert.equal((view.match(new RegExp('id="' + id + '"', "g")) || []).length, 1, id);
        const f = await fixture([page([row()])]);
        assert.match(f.nodes["messaging-list"].textContent, /<img src=x onerror=alert\(1\)>/);
        assert.ok(!source.includes("innerHTML")); assert.ok(!source.includes("localStorage"));
        assert.equal(f.calls[0].options.credentials, "same-origin"); assert.equal(f.calls[0].options.cache, "no-store");
    });
    await test("keyset pagination and folder changes reset safely", async () => {
        const f = await fixture([page([row(40)], 40, 40), page([row(39)]), page()]);
        await f.nodes["messaging-older"].handlers.click(); assert.match(f.calls[1].url, /beforeId=40/);
        await f.tabs[1].handlers.click(); await tick(); assert.match(f.calls[2].url, /sent\?$/);
        assert.equal(f.nodes["messaging-filter-label"].hidden, true);
    });
    await test("message detail uses fresh REST and explicit CSRF read acknowledgement", async () => {
        const f = await fixture([page([row()]), { messageId: 1, subject: "Subject", body: "<script>private</script>", sender: "A", recipient: "B", createdUtc: row().createdUtc }, { status: 204 }, page([], null, 0)]);
        await f.nodes["messaging-list"].children[0].children[0].handlers.click(); await tick();
        assert.match(f.calls[1].url, /messages\/1$/); assert.equal(f.nodes["messaging-detail-body"].textContent, "<script>private</script>");
        await f.nodes["messaging-read"].handlers.click();
        assert.equal(f.calls[2].options.method, "POST"); assert.equal(f.calls[2].options.headers["X-CSRF-TOKEN"], "csrf-fixture");
        assert.equal(f.nodes["messaging-unread"].textContent, "0");
    });
    await test("network failure preserves idempotency key for safe retry", async () => {
        const f = await fixture([page(), new Error("Offline"), { messageId: 12, replayed: true }, page()]);
        fill(f); await f.nodes["messaging-compose"].handlers.submit({ preventDefault() {} });
        assert.match(f.nodes["messaging-send-status"].textContent, /Retry the unchanged/);
        await f.nodes["messaging-compose"].handlers.submit({ preventDefault() {} });
        const sends = f.calls.filter(x => x.url.endsWith("/send"));
        assert.equal(sends.length, 2); assert.equal(JSON.parse(sends[0].options.body).idempotencyKey, JSON.parse(sends[1].options.body).idempotencyKey);
        assert.equal(sends[0].options.headers["X-CSRF-TOKEN"], "csrf-fixture"); assert.equal(f.nodes["messaging-body"].value, "");
    });
    await test("double click cannot send twice while save is pending", async () => {
        const pending = deferred(); const f = await fixture([page(), pending, page()]);
        fill(f); const first = f.nodes["messaging-compose"].handlers.submit({ preventDefault() {} });
        await f.nodes["messaging-compose"].handlers.submit({ preventDefault() {} });
        assert.equal(f.calls.filter(x => x.url.endsWith("/send")).length, 1);
        pending.resolve({ messageId: 1 }); await first;
    });
    await test("late folder response cannot overwrite a newer selection", async () => {
        const pending = deferred(); const f = await fixture([page(), pending, page([row(99)])]);
        f.nodes["messaging-refresh"].handlers.click();
        f.tabs[1].handlers.click(); await tick();
        pending.resolve(page([{ ...row(1), subject: "STALE" }])); await tick();
        assert.ok(!f.nodes["messaging-list"].textContent.includes("STALE"));
    });
    await test("permission revocation clears content and stops all controls", async () => {
        const f = await fixture([page([row()]), { status: 403, value: { detail: "Access changed" } }]);
        await f.nodes["messaging-refresh"].handlers.click();
        assert.equal(f.nodes["messaging-list"].textContent, "");
        assert.equal(f.nodes["messaging-send"].disabled, true);
        assert.match(f.nodes["messaging-status"].textContent, /session or access changed/);
    });
    await test("recipient discovery starts only after two characters", async () => {
        const f = await fixture([page(), [{ userId: "colleague", displayName: "<b>Colleague</b>" }]]);
        f.nodes["messaging-recipient-search"].value = "a"; f.nodes["messaging-recipient-search"].handlers.input();
        assert.equal(f.calls.length, 1);
        f.nodes["messaging-recipient-search"].value = "ab"; f.nodes["messaging-recipient-search"].handlers.input();
        await f.timers[f.timers.length - 1]();
        assert.match(f.calls[1].url, /recipients\?q=ab/);
        assert.equal(f.nodes["messaging-recipient"].children[1].textContent, "<b>Colleague</b>");
    });
    await test("SignalR hint and reconnect always perform authorized REST catch-up", async () => {
        const f = await fixture([page(), page(), page(), page()], true);
        const before = f.calls.length; f.hub.events.inboxChanged(); await tick();
        f.hub.reconnected(); await tick();
        assert.equal(f.calls.length, before + 2); assert.ok(f.calls.every(x => x.url.startsWith("/api/messaging/inbox")));
        assert.ok(!source.includes('invoke("Join')); assert.ok(!source.includes("accessTokenFactory"));
    });
    await test("page departure stops connection and ignores late results", async () => {
        const pending = deferred(); const f = await fixture([page(), page(), pending], true);
        f.nodes["messaging-refresh"].handlers.click(); f.windowHandlers.pagehide();
        pending.resolve(page([{ ...row(), subject: "LATE" }])); await tick();
        assert.equal(f.hub.stopped, 1); assert.ok(!f.nodes["messaging-list"].textContent.includes("LATE"));
    });
    await test("sent state never claims transport delivery or recipient read", async () => {
        const f = await fixture([page(), page([row()])]);
        f.tabs[1].handlers.click(); await tick();
        assert.match(f.nodes["messaging-list"].textContent, /Saved to the recipient's inbox/);
        assert.ok(!f.nodes["messaging-list"].textContent.includes("Delivered"));
    });
    await test("language change translates system labels without fetching or translating user content", async () => {
        const f = await fixture([page([{ ...row(), subject: "Unread" }])]);
        const calls = f.calls.length;
        f.context.window.FarmSphereLanguage = { translate: key => key === "Unread" ? "অপঠিত" : key === "From:" ? "প্রেরক:" : key };
        f.documentHandlers["farmsphere:language"]();
        assert.equal(f.calls.length, calls);
        assert.equal(f.nodes["messaging-list"].children[0].children[0].textContent, "Unread");
        assert.match(f.nodes["messaging-list"].textContent, /অপঠিত/);
        assert.match(f.nodes["messaging-list"].textContent, /প্রেরক:/);
    });
    console.log(`Messaging UI checks passed: ${count}`);
})().catch(error => { console.error(error); process.exitCode = 1; });
