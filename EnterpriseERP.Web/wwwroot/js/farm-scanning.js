/* Scan => authorized preview => immutable review => explicit confirmation. No decoded value posts a business action. */
(() => {
    "use strict";
    const root = document.getElementById("farm-scan"); if (!root) return;
    const $ = id => document.getElementById(id), tr = key => window.FarmSphereLanguage?.translate(key) || key;
    const csrf = root.querySelector('input[name="__RequestVerificationToken"]').value;
    const labels = { AdjustStock: "Stock adjustment", Transfer: "Branch stock transfer", Weight: "Animal weight", Health: "Animal health event", Feed: "Feed consumption", ReceiveOrder: "Purchase order receiving", Custody: "Asset custody change" };
    const cameraMessages = {
        stopped: "Camera stopped.", https: "Camera access requires HTTPS. Use manual entry instead.", unsupported: "Camera access is unavailable. Use manual entry or a keyboard scanner.",
        permission: "Waiting for camera permission. You can cancel with Stop camera.", active: "Point the camera at one label.", denied: "Camera permission was denied. Use manual entry or change the browser permission yourself.",
        unavailable: "The selected camera is unavailable. Choose another camera or use manual entry.", decoder: "The local decoder could not start. Use manual entry or a keyboard scanner.",
        detected: "Label detected. Loading an authorized preview.", invalid: "The detected value is not a supported label.", scanNextFirst: "Choose Scan next label before starting another scan."
    };
    let primary = null, secondary = null, draft = null, stopped = false, busy = false, cameraTarget = "primary";
    let resolveGeneration = 0, secondaryGeneration = 0, reviewGeneration = 0, targetGeneration = 0, labelGeneration = 0;
    let historyPage = 1, targetPage = 1, labelPage = 1, submitted = null;
    const node = (tag, value = "") => { const el = document.createElement(tag); el.textContent = value; return el; };
    const sys = (tag, key) => { const el = node(tag, tr(key)); el.dataset.i18n = key; return el; };
    function message(id, key) { $(id).dataset.i18n = key; $(id).textContent = tr(key); }
    function error(key) { message("scan-error", key || ""); $("scan-error").hidden = !key; }
    function status(key) { message("scan-status", key); }
    function pair(dl, key, value, controlled = false) { dl.append(sys("dt", key), controlled ? sys("dd", value) : node("dd", value == null ? "" : String(value))); }
    function setBusy(value) {
        busy = value; root.dataset.busy = String(value); root.setAttribute("aria-busy", String(value));
        if (!stopped) root.querySelectorAll('button[type="submit"],#scan-confirm,#scan-cancel,#scan-camera-start,#scan-secondary-camera').forEach(el => el.disabled = value);
    }
    function clearReview() { ++reviewGeneration; draft = null; submitted = null; $("scan-review-section").hidden = true; $("scan-confirm-reviewed").checked = false; }
    function resetPrimary() { ++resolveGeneration; ++secondaryGeneration; primary = secondary = null; clearReview(); $("scan-preview").hidden = true; $("scan-entry").hidden = true; $("scan-secondary-preview").replaceChildren(); }
    function lock() {
        stopped = true; session.stop(null); resetPrimary(); $("scan-history").replaceChildren(); $("scan-label-list")?.replaceChildren();
        root.querySelectorAll("input,textarea,select,button").forEach(el => el.disabled = true);
        status("Your session or access changed. Reload and sign in again.");
    }
    async function api(path, method = "GET", payload) {
        if (stopped) throw new Error("Your session or access changed. Reload and sign in again.");
        const options = { method, credentials: "same-origin", cache: "no-store", headers: { Accept: "application/json" } };
        if (method !== "GET") { options.headers["X-CSRF-TOKEN"] = csrf; if (payload !== undefined) { options.headers["Content-Type"] = "application/json"; options.body = JSON.stringify(payload); } }
        const response = await fetch("/api/scan/" + path, options);
        if (response.status === 401 || response.status === 403 || response.redirected) lock();
        if (!response.ok || response.redirected) { const body = await response.json().catch(() => ({})); throw new Error(body.detail || "The request could not be completed. Refresh and try again."); }
        return response.status === 204 ? null : response.json();
    }
    const session = new window.ParuScannerCamera.CameraSession({ video: $("scan-video"), onValue(value) {
        if (stopped) return;
        if (value.length > 80) { error("The detected value is not a supported label."); return; }
        if (cameraTarget === "secondary") { $("scan-secondary-value").value = value; resolveSecondary(); }
        else { $("scan-value").value = value; resolvePrimary(); }
    }, onStatus(code) { if (!stopped && cameraMessages[code]) status(cameraMessages[code]); }, onDevices(devices) {
        const old = $("scan-device").value; const choices = [sys("option", "Default rear camera")]; choices[0].value = "";
        devices.forEach((device, index) => { const option = node("option", device.label || tr("Camera") + " " + (index + 1)); option.value = device.deviceId; choices.push(option); });
        $("scan-device").replaceChildren(...choices); $("scan-device").value = old;
    }});
    function preview(value) {
        const dl = node("dl"); pair(dl, "Record", value.target.title); pair(dl, "State", value.target.state, true); pair(dl, "Branch", value.branchId);
        pair(dl, "Label value", value.value); if (value.target.unit) pair(dl, "Unit", value.target.unit);
        if (value.target.available != null) pair(dl, "Available or remaining quantity", value.target.available);
        if (value.document) {
            pair(dl, "Document kind", value.document.kind, true);
            pair(dl, "Recorded document amount", Number(value.document.amount).toFixed(value.document.displayDecimals) + " " + (value.document.currencyCode || tr("Currency not configured")));
        }
        if (/^\/(Inventory|animals|fixed-assets|Purchase|Sales|Accounts)(?:[/?]|$)/.test(value.target.link)) {
            const link = sys("a", "Open record"); link.href = value.target.link; const dd = node("dd"); dd.append(link); dl.append(sys("dt", "Record"), dd);
        }
        return dl;
    }
    async function resolvePrimary() {
        if (busy) return; session.stop(null); resetPrimary(); const seq = ++resolveGeneration;
        try {
            const value = await api("resolve", "POST", { kind: $("scan-context").value, value: $("scan-value").value });
            if (seq !== resolveGeneration || stopped) return;
            primary = value; $("scan-preview").replaceChildren(preview(value)); $("scan-preview").hidden = false;
            const options = value.actions.map(action => { const opt = sys("option", labels[action]); opt.value = action; return opt; });
            $("scan-action").replaceChildren(...options); $("scan-entry").hidden = !options.length; actionChanged();
            status(options.length ? "Preview ready. Enter an operation and prepare a separate review draft." : "Read-only preview. No operation is available for this context or permission."); error("");
        } catch (ex) { if (seq === resolveGeneration && !stopped) error(ex.message); }
    }
    async function resolveSecondary() {
        if (busy || !primary) return; session.stop(null); secondary = null; clearReview(); const seq = ++secondaryGeneration;
        $("scan-secondary-preview").replaceChildren();
        try {
            const destination = $("scan-action").value === "Transfer" ? Number($("scan-destination").value) : null;
            if (destination === 0) throw new Error("Choose a destination branch first.");
            const value = await api("resolve", "POST", { kind: "Product", value: $("scan-secondary-value").value, destinationBranchId: destination });
            if (seq !== secondaryGeneration || stopped) return;
            secondary = value; $("scan-secondary-preview").replaceChildren(preview(value)); error("");
        } catch (ex) { if (seq === secondaryGeneration && !stopped) error(ex.message); }
    }
    async function actionChanged() {
        clearReview(); secondary = null; ++secondaryGeneration; $("scan-secondary-preview").replaceChildren(); $("scan-secondary-value").value = "";
        const action = $("scan-action").value;
        const visible = { quantity: ["AdjustStock","Transfer","Weight","Feed","ReceiveOrder"].includes(action), date: ["Weight","Health","Feed","Custody"].includes(action), health: action === "Health", destination: action === "Transfer", secondary: ["Transfer","Feed"].includes(action), custody: action === "Custody" };
        root.querySelectorAll("[data-scan-field]").forEach(el => { el.hidden = !visible[el.dataset.scanField]; el.querySelectorAll("input,select,button").forEach(input => input.disabled = el.hidden); });
        $("scan-date").value = root.dataset.businessDate; $("scan-quantity").value = ""; $("scan-quantity").required = visible.quantity; $("scan-date").required = visible.date;
        $("scan-event").required = visible.health; $("scan-custodian").required = $("scan-location").required = visible.custody;
        $("scan-notes").required = ["AdjustStock","Custody"].includes(action); $("scan-notes").maxLength = action === "Transfer" ? 500 : 1000;
        if (action === "Transfer") try {
            const branches = await api("branches"); if ($("scan-action").value !== "Transfer" || stopped) return;
            const empty = sys("option", "Choose a destination branch"); empty.value = "";
            $("scan-destination").replaceChildren(empty, ...branches.map(b => { const option = node("option", b.name); option.value = b.id; return option; }));
        } catch (ex) { error(ex.message); }
    }
    function key() { const bytes = new Uint8Array(16); window.crypto.getRandomValues(bytes); return Array.from(bytes, b => b.toString(16).padStart(2,"0")).join(""); }
    function input() {
        const action = $("scan-action").value;
        if (!primary) throw new Error("Preview a primary label first.");
        if (["Feed","Transfer"].includes(action) && !secondary) throw new Error("Preview the secondary product first.");
        return { requestKey: key(), action, labelId: primary.labelId, secondaryLabelId: ["Feed","Transfer"].includes(action) ? secondary.labelId : null,
            destinationBranchId: action === "Transfer" ? Number($("scan-destination").value) : null,
            quantity: ["AdjustStock","Transfer","Weight","Feed","ReceiveOrder"].includes(action) ? Number($("scan-quantity").value) : 0,
            date: ["Weight","Health","Feed","Custody"].includes(action) ? $("scan-date").value : null,
            nextDueDate: action === "Health" ? $("scan-next-due").value || null : null, eventType: action === "Health" ? $("scan-event").value : "",
            notes: $("scan-notes").value, custodian: action === "Custody" ? $("scan-custodian").value : "", location: action === "Custody" ? $("scan-location").value : "" };
    }
    function renderReview(value) {
        draft = value; $("scan-review-section").hidden = false; $("scan-confirm-reviewed").checked = false;
        const r = value.review, dl = node("dl");
        pair(dl,"Operation", labels[r.input.action],true); pair(dl,"Status",value.status,true); pair(dl,"Business reference",value.businessReference);
        pair(dl,"Record",r.primary.target.title); pair(dl,"Branch",r.primary.branchId); pair(dl,"Label value",r.primary.value);
        if (r.secondary) { pair(dl,"Secondary product",r.secondary.target.title); pair(dl,"Destination or feed branch",r.secondary.branchId); pair(dl,"Secondary label",r.secondary.value); }
        if (!["Health","Custody"].includes(r.input.action)) pair(dl,"Quantity or weight",r.input.quantity);
        if (r.input.date) pair(dl,"Event date",r.input.date);
        if (r.input.eventType) pair(dl,"Health event type",r.input.eventType,true);
        if (r.input.nextDueDate) pair(dl,"Next due date",r.input.nextDueDate);
        pair(dl,"Notes or reason",r.input.notes);
        if (r.input.action === "Custody") { pair(dl,"Previous custodian",r.impact.previousCustodian); pair(dl,"Previous location",r.impact.previousLocation); pair(dl,"New custodian",r.input.custodian); pair(dl,"New location",r.input.location); }
        if (r.impact.previousWeightKg != null) pair(dl,"Previous recorded weight",r.impact.previousWeightKg);
        if (r.impact.journalAmount != null) { pair(dl,"Expected journal amount",Number(r.impact.journalAmount).toFixed(r.impact.displayDecimals) + " " + r.impact.currencyCode); pair(dl,"Amount precision",r.impact.displayDecimals); }
        pair(dl,"Company business date",r.businessDate); pair(dl,"Expires",new Date(value.expiresUtc).toLocaleString());
        $("scan-review").replaceChildren(dl, sys("p",r.consequence));
        const pending = value.status === "Pending" && new Date(value.expiresUtc) > new Date();
        $("scan-confirm-label").hidden = $("scan-confirm").hidden = !pending; $("scan-cancel").hidden = value.status !== "Pending";
    }
    async function prepare(event) {
        event.preventDefault(); if (busy) return; session.stop(null); const seq = ++reviewGeneration;
        try { submitted ??= input(); setBusy(true); const value = await api("drafts","POST",submitted); if (seq !== reviewGeneration || stopped) return; renderReview(value); status("Draft saved. Review every field before confirmation."); error(""); await history(); }
        catch (ex) { if (!stopped) error(ex.message); }
        finally { setBusy(false); }
    }
    async function confirm() {
        if (!draft || busy || !$("scan-confirm-reviewed").checked) { if (!busy) error("Review the draft and explicitly confirm the operation."); return; }
        setBusy(true); session.stop(null); const id = draft.id;
        try { const value = await api("drafts/"+id+"/confirm","POST",{confirmReviewed:true}); renderReview(value); primary = secondary = null; $("scan-entry").hidden = true; $("scan-preview").hidden = true; status("Operation committed. Repeated confirmation returns the same outcome."); error(""); await history(); }
        catch(ex) { if(!stopped) error(ex.message); }
        finally { setBusy(false); }
    }
    async function cancel() {
        if (!draft || busy) return; setBusy(true); session.stop(null);
        try { const id = draft.id; await api("drafts/"+id+"/cancel","POST"); renderReview(await api("drafts/"+id)); status("Draft cancelled. No business operation was posted."); error(""); await history(); }
        catch(ex) { if(!stopped) error(ex.message); } finally { setBusy(false); }
    }
    async function history() {
        try { const page = historyPage, value = await api("drafts?"+new URLSearchParams({page:String(page),q:$("scan-history-search").value,status:$("scan-history-status").value})); if (page !== historyPage || stopped) return;
            $("scan-history").replaceChildren(...value.rows.map(row => { const div=node("div"); div.className="scan-list-row"; const open=node("button",row.businessReference); open.type="button";
                open.addEventListener("click",async()=>{ if(busy)return; session.stop(null); const seq=++reviewGeneration; try{const value=await api("drafts/"+row.id);if(seq===reviewGeneration&&!stopped){renderReview(value);error("");}}catch(ex){error(ex.message);} });
                div.append(open,sys("span",labels[row.action]||row.action),node("span"," · "),sys("span",row.status)); return div; }));
            $("scan-history-page").textContent=String(page); $("scan-history-prev").disabled=page<=1; $("scan-history-next").disabled=page*20>=value.total;
        } catch(ex){ if(!stopped)error(ex.message); }
    }
    async function targets() {
        const seq=++targetGeneration;
        try { const value=await api("options?"+new URLSearchParams({kind:$("scan-label-context").value,q:$("scan-target-search").value,page:String(targetPage)})); if(seq!==targetGeneration||stopped)return;
            const empty=sys("option","Choose a target record");empty.value="";
            $("scan-target").replaceChildren(empty,...value.rows.map(row=>{const option=node("option",row.title);option.value=row.id;return option;}));
            $("scan-target-page").textContent=String(targetPage);$("scan-target-prev").disabled=targetPage<=1;$("scan-target-next").disabled=targetPage*20>=value.total;
        }catch(ex){if(seq===targetGeneration&&!stopped)error(ex.message);}
    }
    async function labelList() {
        const seq=++labelGeneration;
        try { const value=await api("labels?"+new URLSearchParams({kind:$("scan-label-context").value,q:$("scan-label-search").value,page:String(labelPage)}));if(seq!==labelGeneration||stopped)return;
            $("scan-label-list").replaceChildren(...value.rows.map(row=>{const div=node("div");div.className="scan-list-row";div.append(node("p",row.value),sys("span",row.revoked?"Revoked":"Active"));
                if(!row.revoked){const print=sys("a","Print QR and Code128 label");print.href="/scan/labels/"+row.id+"/print";div.append(print);
                    const reason=node("input");reason.maxLength=300;reason.setAttribute("aria-label",tr("Revocation reason"));reason.dataset.i18nAriaLabel="Revocation reason";
                    const revoke=sys("button","Revoke this label");revoke.type="button";revoke.addEventListener("click",async()=>{if(busy)return;if(!reason.value.trim()){error("Enter a bounded revocation reason.");return;}setBusy(true);
                        try{await api("labels/"+row.id+"/revoke","POST",{reason:reason.value});error("");await labelList();status("Label revoked. Unconfirmed drafts using it can no longer commit.");}catch(ex){error(ex.message);}finally{setBusy(false);}});div.append(reason,revoke);
                }else div.append(node("p",row.revocationReason));return div;}));
            $("scan-label-page").textContent=String(labelPage);$("scan-label-prev").disabled=labelPage<=1;$("scan-label-next").disabled=labelPage*20>=value.total;
        }catch(ex){if(seq===labelGeneration&&!stopped)error(ex.message);}
    }
    function mappingFormat(){const internal=$("scan-label-format").value==="Internal";$("scan-label-value").disabled=internal;$("scan-label-value").required=!internal;if(internal)$("scan-label-value").value="";}
    $("scan-resolve-form").addEventListener("submit",e=>{e.preventDefault();resolvePrimary();});
    $("scan-context").addEventListener("change",()=>{session.stop(null);resetPrimary();$("scan-value").value="";});
    $("scan-value").addEventListener("input",()=>{resetPrimary();});
    $("scan-camera-start").addEventListener("click",()=>{if(busy)return;cameraTarget="primary";session.start($("scan-device").value);});
    $("scan-camera-stop").addEventListener("click",()=>session.stop());
    $("scan-camera-next").addEventListener("click",()=>{session.stop(null);session.reset();status("Ready for a label.");$("scan-value").focus();});
    $("scan-secondary-camera").addEventListener("click",()=>{if(busy)return;cameraTarget="secondary";session.reset();session.start($("scan-device").value);});
    $("scan-device").addEventListener("change",()=>session.stop());
    $("scan-secondary-preview-button").addEventListener("click",resolveSecondary);
    $("scan-secondary-value").addEventListener("keydown",e=>{if(e.key==="Enter"){e.preventDefault();resolveSecondary();}});
    $("scan-secondary-value").addEventListener("input",()=>{secondary=null;++secondaryGeneration;clearReview();$("scan-secondary-preview").replaceChildren();});
    $("scan-destination").addEventListener("change",()=>{secondary=null;++secondaryGeneration;clearReview();$("scan-secondary-preview").replaceChildren();});
    $("scan-action").addEventListener("change",actionChanged);
    $("scan-entry-form").addEventListener("input",clearReview);$("scan-entry-form").addEventListener("submit",prepare);
    $("scan-confirm").addEventListener("click",confirm);$("scan-cancel").addEventListener("click",cancel);
    $("scan-history-refresh").addEventListener("click",()=>{historyPage=1;history();});$("scan-history-status").addEventListener("change",()=>{historyPage=1;history();});$("scan-history-prev").addEventListener("click",()=>{if(historyPage>1){historyPage--;history();}});$("scan-history-next").addEventListener("click",()=>{historyPage++;history();});
    if(root.dataset.canManage==="true"){
        $("scan-label-context").addEventListener("change",()=>{targetPage=labelPage=1;const product=$("scan-label-context").value==="Product";$("scan-label-format").value="Internal";$("scan-label-format").disabled=!product;mappingFormat();targets();labelList();});
        $("scan-label-format").addEventListener("change",mappingFormat);
        $("scan-target-search-button").addEventListener("click",()=>{targetPage=1;targets();});$("scan-label-search-button").addEventListener("click",()=>{labelPage=1;labelList();});
        for(const [name,fn] of [["target",targets],["label",labelList]])for(const delta of [-1,1])$("scan-"+name+(delta<0?"-prev":"-next")).addEventListener("click",()=>{if(name==="target")targetPage=Math.max(1,targetPage+delta);else labelPage=Math.max(1,labelPage+delta);fn();});
        $("scan-label-form").addEventListener("submit",async e=>{e.preventDefault();if(busy)return;setBusy(true);try{await api("labels","POST",{kind:$("scan-label-context").value,targetId:Number($("scan-target").value),format:$("scan-label-format").value,value:$("scan-label-value").value});labelPage=1;await labelList();status("Label mapping created. Print and test the label before use.");error("");}catch(ex){error(ex.message);}finally{setBusy(false);}});
        targets();labelList();
    }
    document.addEventListener("visibilitychange",()=>{if(document.hidden)session.stop(null);});
    window.addEventListener("pagehide",()=>session.stop(null));window.addEventListener("beforeunload",()=>session.stop(null));
    document.addEventListener("submit",e=>{if(e.target.action&&new URL(e.target.action,location.href).pathname.toLowerCase().includes("logout"))session.stop(null);},true);
    history();
})();
