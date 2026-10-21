#!/usr/bin/env python3
"""Real localhost Identity/CSRF/REST/SignalR regression; disposable synthetic enterprise database only."""
from __future__ import annotations
import json, sqlite3, time, urllib.error, urllib.parse, urllib.request, uuid
from html.parser import HTMLParser
from pathlib import Path
from enterprise_http_fixture import Fixture, _core
from websockets.sync.client import connect


class Tokens(HTMLParser):
    def __init__(self, html):
        super().__init__(); self.token = None; self.feed(html)
    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == "input" and a.get("name") == "__RequestVerificationToken":
            self.token = a.get("value")


def request(session, path, payload=None, token=None, method="GET"):
    if not path.startswith("/") or path.startswith("//"): raise ValueError("Local relative URL required")
    headers = {"Accept": "application/json"}
    data = None
    if payload is not None:
        data = json.dumps(payload).encode(); headers["Content-Type"] = "application/json"
    elif method == "POST": data = b""
    if token: headers["X-CSRF-TOKEN"] = token
    req = urllib.request.Request(session.base + path, data=data, headers=headers, method=method)
    try:
        with session.opener.open(req, timeout=30) as response: return _core.Response(response)
    except urllib.error.HTTPError as response: return _core.Response(response)


def socket_for(f, session):
    negotiation = request(session, "/hubs/messaging/negotiate?negotiateVersion=1", method="POST")
    f.check("Authenticated SignalR negotiation", negotiation.status == 200)
    token = json.loads(negotiation.text)["connectionToken"]
    cookie = "; ".join(f"{c.name}={c.value}" for c in session.cookies)
    ws = connect(session.base.replace("http://", "ws://") + "/hubs/messaging?id=" + urllib.parse.quote(token),
        origin=session.base, additional_headers={"Cookie": cookie}, open_timeout=10, close_timeout=2, proxy=None)
    ws.send('{"protocol":"json","version":1}\x1e')
    handshake = ws.recv(timeout=10)
    f.check("Real WebSocket SignalR JSON handshake", "{}\x1e" in handshake)
    return ws


def receive(ws, target=None, invocation=None, seconds=12):
    deadline = time.monotonic() + seconds
    seen = []
    while time.monotonic() < deadline:
        value = ws.recv(timeout=max(.05, deadline - time.monotonic()))
        for frame in value.split("\x1e"):
            if not frame: continue
            data = json.loads(frame); seen.append(data)
            if target is not None and data.get("target") == target: return data, seen
            if invocation is not None and data.get("invocationId") == invocation: return data, seen
    raise AssertionError("Expected SignalR event did not arrive")


def run():
    with Fixture("messaging") as f:
        sender = f.login(); recipient = f.login(reviewer=True); outsider = f.login(2)
        sender_id = f.scalar("SELECT Id FROM AspNetUsers WHERE Email='farmdemo1@example.invalid'")
        recipient_id = f.scalar("SELECT Id FROM AspNetUsers WHERE Email='farmreviewer1@example.invalid'")
        outsider_id = f.scalar("SELECT Id FROM AspNetUsers WHERE Email='farmdemo2@example.invalid'")
        ui = f.page(sender, "/Messaging"); sender_token = Tokens(ui.text).token
        recipient_token = Tokens(f.page(recipient, "/Messaging").text).token
        f.check("Inbox renders self-hosted official SignalR client", "/vendor/signalr/signalr.min.js" in ui.text and "/js/farm-messaging.js" in ui.text)
        f.check("CSRF token is present", bool(sender_token and recipient_token))
        f.check("Unauthenticated REST rejected", request(f.anonymous, "/api/messaging/inbox").status == 401)
        f.check("Unauthenticated SignalR negotiation rejected", request(f.anonymous, "/hubs/messaging/negotiate?negotiateVersion=1", method="POST").status == 401)
        candidate = request(sender, "/api/messaging/recipients?q=farmreviewer1")
        f.check("Recipient discovery is current branch scoped", candidate.status == 200 and json.loads(candidate.text)[0]["userId"] == recipient_id)
        candidate = request(sender, "/api/messaging/recipients?q=farmdemo2")
        f.check("Foreign recipient is never discoverable", candidate.status == 200 and json.loads(candidate.text) == [])
        first = {"recipientUserId": recipient_id, "subject": "SYNTHETIC <img src=x onerror=alert(1)>",
            "body": "PRIVATE-SYNTHETIC-SIGNALR-CONTENT", "idempotencyKey": str(uuid.uuid4()), "senderUserId": outsider_id}
        f.check("Send requires antiforgery", request(sender, "/api/messaging/send", first, method="POST").status == 400)
        ws = socket_for(f, recipient)
        try:
            sent = request(sender, "/api/messaging/send", first, sender_token, "POST")
            f.check("Authorized internal send saved", sent.status == 200, "HTTP " + str(sent.status))
            message_id = json.loads(sent.text)["messageId"]
            notification = f.scalar("SELECT Id FROM UserInboxNotifications WHERE MessageId=?", (message_id,))
            f.check("Sender comes exclusively from authenticated identity", f.scalar("SELECT SenderUserId FROM InternalMessages WHERE Id=?", (message_id,)) == sender_id)
            hint, frames = receive(ws, target="inboxChanged")
            f.check("Actual SignalR hint contains no message or tenant metadata", hint == {"type": 1, "target": "inboxChanged", "arguments": []} and "PRIVATE" not in json.dumps(frames))
            f.check("Durable inbox is uncached", "no-store" in request(recipient, "/api/messaging/inbox").headers.get("Cache-Control", ""))
            retry = request(sender, "/api/messaging/send", first, sender_token, "POST")
            f.check("HTTP idempotency replay avoids duplicate writes", retry.status == 200 and json.loads(retry.text)["replayed"] and f.scalar("SELECT COUNT(*) FROM InternalMessages WHERE IdempotencyKey=?", (uuid.UUID(first["idempotencyKey"]).hex,)) == 1)
            conflict = dict(first, body="Different body")
            f.check("Changed idempotent payload rejected", request(sender, "/api/messaging/send", conflict, sender_token, "POST").status == 409)
            foreign = dict(first, idempotencyKey=str(uuid.uuid4()), recipientUserId=outsider_id)
            f.check("Cross-company direct send rejected", request(sender, "/api/messaging/send", foreign, sender_token, "POST").status == 400)
            f.check("Cross-company message detail hidden", request(outsider, f"/api/messaging/messages/{message_id}").status == 404)
            f.check("Sender cannot mark recipient inbox read", request(sender, f"/api/messaging/notifications/{notification}/read", token=sender_token, method="POST").status == 404)
            f.check("Read mutation requires antiforgery", request(recipient, f"/api/messaging/notifications/{notification}/read", method="POST").status == 400)
            detail = request(recipient, f"/api/messaging/messages/{message_id}")
            f.check("Authorized REST retains literal user content", detail.status == 200 and json.loads(detail.text)["body"] == first["body"])
            f.check("Owner acknowledgement succeeds", request(recipient, f"/api/messaging/notifications/{notification}/read", token=recipient_token, method="POST").status == 204)
            f.check("Repeated acknowledgement is idempotent", request(recipient, f"/api/messaging/notifications/{notification}/read", token=recipient_token, method="POST").status == 204 and f.scalar("SELECT COUNT(*) FROM Audits WHERE Action='Messaging.Read' AND ActorId=? AND Reference=?", (recipient_id, str(notification))) == 1)
            ws.send('{"type":1,"invocationId":"sync-1","target":"Sync","arguments":[]}\x1e')
            synced, _ = receive(ws, invocation="sync-1")
            f.check("Authorized direct hub Sync succeeds", synced.get("type") == 3 and "error" not in synced)
            ws.send('{"type":1,"invocationId":"join-1","target":"JoinGroup","arguments":["foreign-user"]}\x1e')
            denied, _ = receive(ws, invocation="join-1")
            f.check("No client-selected group join method exists", "error" in denied)
        finally: ws.close()
        second = dict(first, idempotencyKey=str(uuid.uuid4()), subject="Offline recipient handover")
        sent = request(sender, "/api/messaging/send", second, sender_token, "POST")
        f.check("Offline recipient message remains durable", sent.status == 200)
        offline_id = json.loads(sent.text)["messageId"]
        ws = socket_for(f, recipient)
        try:
            inbox = request(recipient, "/api/messaging/inbox?unreadOnly=true")
            f.check("Reconnect REST catches offline unread message", inbox.status == 200 and any(x["messageId"] == offline_id for x in json.loads(inbox.text)["items"]))
            with sqlite3.connect(f.dbpath) as db:
                db.execute("UPDATE MessagingRateBuckets SET Count=200 WHERE UserId=? AND Period='Day'", (sender_id,))
            f.check("Persisted flood budget returns 429", request(sender, "/api/messaging/send", dict(first, idempotencyKey=str(uuid.uuid4())), sender_token, "POST").status == 429)
            f.check("Flood budget still allows same-key replay", request(sender, "/api/messaging/send", first, sender_token, "POST").status == 200)
            with sqlite3.connect(f.dbpath) as db:
                db.execute("DELETE FROM UserPermissions WHERE UserId=?", (recipient_id,))
                db.execute("INSERT INTO UserPermissions (CompanyId,UserId,Permission) SELECT CompanyId,UserId,'Messaging.View' FROM UserCompanyAccess WHERE UserId=?", (recipient_id,))
            f.check("Automation endpoint rechecks its own permission", request(recipient, "/api/messaging/automation").status == 403)
            f.check("Messaging view remains independently permitted", request(recipient, "/api/messaging/inbox").status == 200)
            with sqlite3.connect(f.dbpath) as db: db.execute("DELETE FROM UserPermissions WHERE UserId=?", (recipient_id,))
            f.check("REST immediately rejects revoked messaging grant", request(recipient, "/api/messaging/inbox").status == 403)
            ws.send('{"type":1,"invocationId":"revoked","target":"Sync","arguments":[]}\x1e')
            try:
                denied, _ = receive(ws, invocation="revoked", seconds=8)
                revoked = "error" in denied
            except Exception as ex:
                # Abort may close before its error completion reaches the websocket.
                revoked = type(ex).__name__.startswith("ConnectionClosed")
            f.check("Existing SignalR connection rejects revoked permission", revoked)
        finally: ws.close()
        with sqlite3.connect(f.dbpath) as db:
            db.execute("INSERT INTO UserPermissions (CompanyId,UserId,Permission) SELECT CompanyId,UserId,'Messaging.View' FROM UserCompanyAccess WHERE UserId=?", (recipient_id,))
        ws = socket_for(f, recipient)
        try:
            with sqlite3.connect(f.dbpath) as db: db.execute("UPDATE AspNetUsers SET SecurityStamp=? WHERE Id=?", (str(uuid.uuid4()), recipient_id))
            f.check("Security-stamp change invalidates HTTP cookie", request(recipient, "/api/messaging/inbox").status == 401)
            ws.send('{"type":1,"invocationId":"stale-stamp","target":"Sync","arguments":[]}\x1e')
            try:
                denied, _ = receive(ws, invocation="stale-stamp", seconds=8); revoked = "error" in denied
            except Exception as ex: revoked = type(ex).__name__.startswith("ConnectionClosed")
            f.check("Existing hub rejects stale security stamp", revoked)
        finally: ws.close()
        f.report["signalr_websocket_executed"] = True
        f.report["browser_executed"] = False
        f.report["limitations"] = ["SQLite Development fixture; SQL Server not executed.", "Actual SignalR WebSocket exercised; browser UI covered separately by deterministic JavaScript tests."]
        f.write()


if __name__ == "__main__": run()
