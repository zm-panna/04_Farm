/* Durable REST inbox is authoritative. SignalR carries no message, user or tenant data. */
(() => {
    "use strict";
    const root = document.getElementById("farm-messaging");
    if (!root) return;
    const $ = id => document.getElementById(id);
    const csrf = root.querySelector('input[name="__RequestVerificationToken"]').value;
    let folder = "inbox", beforeId = null, nextBeforeId = null, generation = 0, recipientGeneration = 0;
    let detailGeneration = 0, pendingSend = false, stopped = false, connection = null, retryTimer = null;
    let selectedRead = null, submission = null, searchTimer = null, pollTimer = null;
    const text = (tag, content) => { const node = document.createElement(tag); node.textContent = content; return node; };
    const translate = key => window.FarmSphereLanguage?.translate(key) || key;
    const system = (tag, key) => { const node = text(tag, translate(key)); node.dataset.i18n = key; return node; };
    function setSystem(id, key) { $(id).dataset.i18n = key; $(id).textContent = translate(key); }
    function error(message) { setSystem("messaging-error", message || ""); $("messaging-error").hidden = !message; }
    function status(message) { setSystem("messaging-status", message); }
    function lock() {
        stopped = true; generation++; detailGeneration++; recipientGeneration++;
        $("messaging-list").replaceChildren(); $("messaging-detail").hidden = true;
        $("messaging-detail-body").textContent = ""; $("messaging-detail-subject").textContent = "";
        $("messaging-detail-meta").textContent = ""; $("messaging-unread").textContent = "";
        root.querySelectorAll("button, input, textarea, select").forEach(x => { x.disabled = true; });
        clearTimeout(retryTimer); clearInterval(pollTimer);
        if (connection) connection.stop().catch(() => {});
        status("Your session or access changed. Reload and sign in again.");
    }
    async function api(path, method = "GET", payload) {
        const options = { method, credentials: "same-origin", cache: "no-store", headers: { Accept: "application/json" } };
        if (method !== "GET") {
            options.headers["X-CSRF-TOKEN"] = csrf;
            if (payload !== undefined) { options.headers["Content-Type"] = "application/json"; options.body = JSON.stringify(payload); }
        }
        const response = await fetch("/api/messaging/" + path, options);
        if (response.status === 401 || (response.status === 403 && !path.startsWith("automation"))) lock();
        if (!response.ok) {
            const value = await response.json().catch(() => ({}));
            throw new Error(value.detail || "The request could not be completed. Refresh and try again.");
        }
        return response.status === 204 ? null : response.json();
    }
    const date = value => new Date(value).toLocaleString();
    async function refresh(reset = false) {
        if (stopped) return;
        if (reset) beforeId = null;
        const sequence = ++generation, currentFolder = folder;
        const params = new URLSearchParams();
        if (beforeId !== null) params.set("beforeId", String(beforeId));
        if ($("messaging-unread-only").checked && folder !== "sent") params.set("unreadOnly", "true");
        try {
            const page = await api(currentFolder + "?" + params.toString());
            if (sequence !== generation || stopped) return;
            nextBeforeId = page.nextBeforeId;
            $("messaging-older").hidden = nextBeforeId === null;
            if (currentFolder === "inbox") $("messaging-unread").textContent = String(page.unreadCount);
            const fragment = document.createDocumentFragment();
            for (const row of page.items) {
                const item = text("article", "");
                item.className = "messaging-row" + ((currentFolder === "inbox" ? row.readUtc === null : currentFolder === "automation" && !row.isRead) ? " messaging-unread" : "");
                const open = text("button", currentFolder === "automation" ? row.category : row.subject);
                open.type = "button";
                open.addEventListener("click", () => openDetail(row, currentFolder));
                const meta = text("small", "");
                if (currentFolder !== "automation") meta.append(system("span", currentFolder === "sent" ? "To:" : "From:"),
                    text("span", " " + (currentFolder === "sent" ? row.recipient : row.sender) + " · "));
                meta.append(text("span", date(row.createdUtc)));
                item.append(open, meta);
                item.append(system("small", currentFolder === "sent" ? "Saved to the recipient's inbox" :
                    (currentFolder === "automation" ? row.isRead : row.readUtc !== null) ? "Read" : "Unread"));
                fragment.append(item);
            }
            if (!page.items.length) fragment.append(system("p", "No messages in this view."));
            $("messaging-list").replaceChildren(fragment);
            error("");
        } catch (ex) {
            if (sequence === generation && !stopped) {
                // Do not leave previously authorized automation content visible after review permission revocation.
                if (currentFolder === "automation") { $("messaging-list").replaceChildren(); $("messaging-detail").hidden = true; }
                error(ex.message);
            }
        }
    }
    async function openDetail(row, rowFolder) {
        const sequence = ++detailGeneration;
        selectedRead = null; $("messaging-read").hidden = true; $("messaging-detail").hidden = true;
        try {
            const value = await api(rowFolder === "automation" ? "automation/" + encodeURIComponent(row.id) : "messages/" + encodeURIComponent(row.messageId));
            if (sequence !== detailGeneration || stopped) return;
            $("messaging-detail-subject").textContent = rowFolder === "automation" ? value.category : value.subject;
            $("messaging-detail-body").textContent = rowFolder === "automation" ? value.message : value.body;
            $("messaging-detail-meta").replaceChildren();
            if (rowFolder !== "automation") $("messaging-detail-meta").append(system("span", "From:"), text("span", " " + value.sender + " · "),
                system("span", "To:"), text("span", " " + value.recipient + " · "));
            $("messaging-detail-meta").append(text("span", date(value.createdUtc)));
            if (rowFolder === "inbox" && row.readUtc === null) selectedRead = "notifications/" + row.id + "/read";
            if (rowFolder === "automation" && !row.isRead) selectedRead = "automation/" + row.id + "/read";
            $("messaging-read").hidden = selectedRead === null; $("messaging-read").disabled = false;
            $("messaging-detail").hidden = false;
            $("messaging-detail").scrollIntoView({ behavior: "smooth", block: "nearest" });
        } catch (ex) { if (!stopped && sequence === detailGeneration) error(ex.message); }
    }
    $("messaging-read").addEventListener("click", async () => {
        if (!selectedRead || stopped) return;
        const path = selectedRead; $("messaging-read").disabled = true;
        try { await api(path, "POST"); selectedRead = null; $("messaging-read").hidden = true; await refresh(); }
        catch (ex) { error(ex.message); if (!stopped) $("messaging-read").disabled = false; }
    });
    $("messaging-close").addEventListener("click", () => { detailGeneration++; selectedRead = null; $("messaging-detail").hidden = true; });
    root.querySelectorAll("[data-folder]").forEach(button => button.addEventListener("click", () => {
        folder = button.dataset.folder; beforeId = null; detailGeneration++; selectedRead = null; $("messaging-detail").hidden = true;
        root.querySelectorAll("[data-folder]").forEach(x => x.setAttribute("aria-pressed", String(x === button)));
        $("messaging-filter-label").hidden = folder === "sent"; refresh(true);
    }));
    $("messaging-refresh").addEventListener("click", () => refresh(true));
    $("messaging-older").addEventListener("click", () => { beforeId = nextBeforeId; refresh(); });
    $("messaging-unread-only").addEventListener("change", () => refresh(true));
    if (root.dataset.canSend === "true") {
        $("messaging-recipient-search").addEventListener("input", () => {
            clearTimeout(searchTimer);
            const sequence = ++recipientGeneration, query = $("messaging-recipient-search").value.trim();
            const empty = system("option", "Search for a recipient"); empty.value = "";
            $("messaging-recipient").replaceChildren(empty);
            if (query.length < 2) return;
            searchTimer = setTimeout(async () => {
                try {
                    const rows = await api("recipients?q=" + encodeURIComponent(query));
                    if (sequence !== recipientGeneration || stopped) return;
                    for (const row of rows) { const option = text("option", row.displayName); option.value = row.userId; $("messaging-recipient").append(option); }
                    if (!rows.length) { empty.dataset.i18n = "No available branch colleagues found."; empty.textContent = translate(empty.dataset.i18n); }
                } catch (ex) { if (!stopped && sequence === recipientGeneration) error(ex.message); }
            }, 250);
        });
        $("messaging-compose").addEventListener("submit", async event => {
            event.preventDefault();
            if (pendingSend || stopped || !$("messaging-compose").reportValidity()) return;
            const payload = { recipientUserId: $("messaging-recipient").value, subject: $("messaging-subject").value.trim(), body: $("messaging-body").value.trim() };
            const signature = JSON.stringify(payload);
            if (!submission || submission.signature !== signature) {
                if (!globalThis.crypto || !globalThis.crypto.randomUUID) { error("A secure browser connection is required to send messages."); return; }
                submission = { signature, idempotencyKey: crypto.randomUUID() };
            }
            pendingSend = true; $("messaging-send").disabled = true;
            setSystem("messaging-send-status", "Saving your message…");
            try {
                await api("send", "POST", { ...payload, idempotencyKey: submission.idempotencyKey });
                if (stopped) return;
                setSystem("messaging-send-status", "Message saved to the recipient's inbox.");
                $("messaging-subject").value = ""; $("messaging-body").value = ""; submission = null;
                await refresh(true);
            } catch (ex) {
                if (!stopped) {
                    delete $("messaging-send-status").dataset.i18n;
                    $("messaging-send-status").replaceChildren(system("span", ex.message), text("span", " "), system("span", "Retry the unchanged message safely."));
                }
            } finally { pendingSend = false; if (!stopped) $("messaging-send").disabled = false; }
        });
    }
    function catchUp() { if (!stopped) refresh(true); }
    async function connect() {
        if (stopped || !connection) return;
        try {
            await connection.start();
            if (stopped) { await connection.stop(); return; }
            status("Live inbox connected. Messages are stored securely in your inbox.");
            catchUp();
        } catch {
            if (!stopped) { status("Live hints paused. Your inbox still refreshes automatically."); retryTimer = setTimeout(connect, 15000); }
        }
    }
    if (globalThis.signalR) {
        connection = new signalR.HubConnectionBuilder().withUrl("/hubs/messaging").withAutomaticReconnect([0, 2000, 10000, 30000]).build();
        connection.on("inboxChanged", catchUp);
        connection.onreconnecting(() => status("Reconnecting live hints. Your inbox still refreshes automatically."));
        connection.onreconnected(() => { status("Live inbox reconnected."); catchUp(); });
        connection.onclose(() => { if (!stopped) { status("Live hints paused. Your inbox still refreshes automatically."); clearTimeout(retryTimer); retryTimer = setTimeout(connect, 15000); } });
        connect();
    } else status("Your inbox refreshes automatically. Live hints are unavailable.");
    pollTimer = setInterval(() => { if (!document.hidden && !stopped) refresh(); }, 20000);
    document.addEventListener("visibilitychange", () => { if (!document.hidden) catchUp(); });
    // Only explicitly marked system labels are translation keys; user subjects, body and names never are.
    document.addEventListener("farmsphere:language", () => {
        root.querySelectorAll("[data-i18n]").forEach(node => {
            if (typeof node.dataset.i18n === "string") node.textContent = translate(node.dataset.i18n);
        });
    });
    window.addEventListener("pagehide", () => { stopped = true; clearTimeout(retryTimer); clearTimeout(searchTimer); clearInterval(pollTimer); if (connection) connection.stop().catch(() => {}); });
    refresh(true);
})();
