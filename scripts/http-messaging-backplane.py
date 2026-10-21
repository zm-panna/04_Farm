#!/usr/bin/env python3
"""Two local app nodes + owned official Redis process; never contacts customer infrastructure.

Run in ONE shell with FARM_TEST_REDIS=127.0.0.1:16380. The script starts/stops its
own disposable Redis at that unused loopback endpoint, and cleans up all children.
"""
from __future__ import annotations
import importlib.util, json, os, re, shutil, signal, socket, subprocess, tempfile, time, urllib.error, uuid
from pathlib import Path
from enterprise_http_fixture import Fixture, ROOT, DLL, Session

spec = importlib.util.spec_from_file_location("messaging_http", Path(__file__).with_name("http-messaging-smoke.py"))
http = importlib.util.module_from_spec(spec); spec.loader.exec_module(http)


def until(predicate, seconds=30):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        value = predicate()
        if value: return value
        time.sleep(.2)
    raise AssertionError("Expected local process/database outcome timed out")


class Redis:
    def __init__(self):
        endpoint = os.environ.get("FARM_TEST_REDIS", "")
        if endpoint != "127.0.0.1:16380": raise ValueError("Explicit isolated FARM_TEST_REDIS=127.0.0.1:16380 required.")
        self.binary = Path(os.environ.get("FARM_TEST_REDIS_SERVER", str(ROOT.parent / "tools/redis-7.2.16/src/redis-server"))).resolve()
        self.cli = self.binary.with_name("redis-cli")
        if not self.binary.is_file() or not self.cli.is_file(): raise ValueError("Verified official local Redis binaries are required.")
        with socket.socket() as test:
            if test.connect_ex(("127.0.0.1", 16380)) == 0: raise ValueError("The test Redis endpoint is already occupied; no existing process was changed.")
        self.tmp = tempfile.TemporaryDirectory(prefix="farm-owned-redis-demo-")
        self.log = (Path(self.tmp.name) / "redis.log").open("w")
        self.process = None
    def command(self, *args):
        return subprocess.check_output([str(self.cli), "-h", "127.0.0.1", "-p", "16380", "--raw", *args], text=True, timeout=5).strip()
    def start(self):
        self.process = subprocess.Popen([str(self.binary), "--bind", "127.0.0.1", "--port", "16380", "--protected-mode", "yes",
            "--save", "", "--appendonly", "no", "--dir", self.tmp.name], stdout=self.log, stderr=subprocess.STDOUT)
        def ready():
            if self.process.poll() is not None: raise RuntimeError("Owned Redis test process exited.")
            try:
                with socket.create_connection(("127.0.0.1", 16380), timeout=.2) as client:
                    client.sendall(b"*1\r\n$4\r\nPING\r\n"); return b"PONG" in client.recv(100)
            except OSError: return False
        until(ready, 10)
    def stop(self):
        if self.process and self.process.poll() is None:
            self.process.terminate()
            try: self.process.wait(timeout=10)
            except subprocess.TimeoutExpired: self.process.kill(); self.process.wait(timeout=5)
    def __enter__(self): self.start(); return self
    def __exit__(self, *args):
        self.stop(); self.log.close(); self.tmp.cleanup()


def second_host(f):
    with socket.socket() as sock: sock.bind(("127.0.0.1", 0)); port = sock.getsockname()[1]
    base = f"http://127.0.0.1:{port}"
    env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development", DOTNET_ENVIRONMENT="Development", Database__Provider="Sqlite",
        ConnectionStrings__DefaultConnection="Data Source=" + str(f.dbpath), Automation__WorkerEnabled="false", Ollama__Enabled="false",
        HOME=str(f.path), XDG_DATA_HOME=str(f.path / ".local/share"), DOTNET_CLI_HOME=str(f.path), Logging__LogLevel__Default="Warning")
    log = (f.out / "node-b.log").open("w")
    process = subprocess.Popen([shutil.which("dotnet"), str(DLL), "--urls", base], cwd=ROOT / "EnterpriseERP.Web", env=env, stdout=log, stderr=subprocess.STDOUT)
    session = Session(base)
    def ready():
        if process.poll() is not None: raise RuntimeError("Second localhost test host exited.")
        try: return session.request("/Account/Login").status == 200
        except (urllib.error.URLError, TimeoutError): return False
    try: until(ready)
    except:
        process.terminate(); process.wait(timeout=10); log.close(); raise
    return process, log, base


def run():
    old = {key: os.environ.get(key) for key in ["Scaling__SignalR__RedisEnabled", "Scaling__SignalR__Connection", "Scaling__SignalR__ChannelPrefix", "Scaling__Cache__Enabled"]}
    prefix = "farm:test:signalr:" + uuid.uuid4().hex
    os.environ.update(Scaling__SignalR__RedisEnabled="true", Scaling__SignalR__Connection="127.0.0.1:16380,connectTimeout=2000,syncTimeout=2000,asyncTimeout=2000",
        Scaling__SignalR__ChannelPrefix=prefix, Scaling__Cache__Enabled="false")
    try:
        with Redis() as redis, Fixture("messaging-backplane") as f:
            f.check("Owned official Redis responds", redis.command("PING") == "PONG")
            node_b, log_b, base_b = second_host(f)
            paused = False; ws = None
            try:
                f.check("Two real localhost app nodes share the disposable database", node_b.poll() is None and f.process.poll() is None)
                sender = f.login()
                original_base = f.base
                try: f.base = base_b; recipient = f.login(reviewer=True)
                finally: f.base = original_base
                sender_token = http.Tokens(f.page(sender, "/Messaging").text).token
                recipient_id = f.scalar("SELECT Id FROM AspNetUsers WHERE Email='farmreviewer1@example.invalid'")
                # A cookie established on A must authenticate on B under the shared test Data Protection directory.
                sender.base = base_b
                try: f.check("Identity cookie works across both nodes", http.request(sender, "/api/messaging/inbox").status == 200)
                finally: sender.base = original_base
                until(lambda: f.scalar("SELECT COUNT(*) FROM MessagingOutbox WHERE HintProcessedUtcTicks IS NULL") == 0)
                ws = http.socket_for(f, recipient)
                until(lambda: "inbox:" in redis.command("PUBSUB", "CHANNELS", prefix + "*"))
                f.check("Redis has an actual subscribed server-derived inbox channel", "inbox:" in redis.command("PUBSUB", "CHANNELS", prefix + "*"))
                def publishes():
                    result = re.search(r"cmdstat_publish:calls=(\d+)", redis.command("INFO", "commandstats"))
                    return int(result.group(1)) if result else 0
                before = publishes()
                # B cannot dispatch its own outbox while stopped. A must publish; Redis buffers B's subscription socket.
                os.kill(node_b.pid, signal.SIGSTOP); paused = True; time.sleep(.1)
                payload = {"recipientUserId": recipient_id, "subject": "Synthetic cross-node handover", "body": "CROSS-NODE-PRIVATE-CONTENT", "idempotencyKey": str(uuid.uuid4())}
                sent = http.request(sender, "/api/messaging/send", payload, sender_token, "POST")
                f.check("Node A commits while receiving node B is paused", sent.status == 200)
                message_id = json.loads(sent.text)["messageId"]
                until(lambda: f.scalar("SELECT COUNT(*) FROM MessagingOutbox o JOIN UserInboxNotifications n ON n.Id=o.NotificationId WHERE n.MessageId=? AND o.HintProcessedUtcTicks IS NOT NULL", (message_id,)) == 1)
                f.check("Node A actually publishes through Redis while B cannot run a dispatcher", publishes() > before)
                os.kill(node_b.pid, signal.SIGCONT); paused = False
                hint, frames = http.receive(ws, target="inboxChanged")
                f.check("Node B receives cross-node Redis SignalR hint with empty arguments", hint == {"type": 1, "target": "inboxChanged", "arguments": []})
                f.check("Backplane hint has no message content or identifiers", "PRIVATE" not in json.dumps(frames) and recipient_id not in json.dumps(frames))
                inbox = http.request(recipient, "/api/messaging/inbox")
                f.check("Node B authorized REST sees node A durable message", inbox.status == 200 and any(x["messageId"] == message_id for x in json.loads(inbox.text)["items"]))
                redis.stop()
                f.check("Owned Redis process is fully stopped for outage test", redis.process.poll() is not None)
                outage = dict(payload, subject="Synthetic Redis outage handover", idempotencyKey=str(uuid.uuid4()))
                sent = http.request(sender, "/api/messaging/send", outage, sender_token, "POST")
                f.check("Redis outage does not prevent durable message commit", sent.status == 200)
                outage_id = json.loads(sent.text)["messageId"]
                inbox = http.request(recipient, "/api/messaging/inbox")
                f.check("Other-node authorized REST works during Redis outage", inbox.status == 200 and any(x["messageId"] == outage_id for x in json.loads(inbox.text)["items"]))
                until(lambda: f.scalar("SELECT COUNT(*) FROM MessagingOutbox o JOIN UserInboxNotifications n ON n.Id=o.NotificationId WHERE n.MessageId=? AND o.Attempts>0 AND o.HintProcessedUtcTicks IS NULL", (outage_id,)) == 1, 20)
                f.check("Unsuccessful transport attempt remains pending in SQL outbox", f.scalar("SELECT COUNT(*) FROM MessagingOutbox o JOIN UserInboxNotifications n ON n.Id=o.NotificationId WHERE n.MessageId=? AND o.HintProcessedUtcTicks IS NULL", (outage_id,)) == 1)
                redis.start()
                until(lambda: "inbox:" in redis.command("PUBSUB", "CHANNELS", prefix + "*"), 30)
                until(lambda: f.scalar("SELECT COUNT(*) FROM MessagingOutbox o JOIN UserInboxNotifications n ON n.Id=o.NotificationId WHERE n.MessageId=? AND o.HintProcessedUtcTicks IS NOT NULL", (outage_id,)) == 1, 40)
                f.check("Backplane reconnects and dispatcher drains pending SQL outbox", True)
                inbox = http.request(recipient, "/api/messaging/inbox?unreadOnly=true")
                f.check("Outage recovery preserves unread state and does not duplicate message", inbox.status == 200 and any(x["messageId"] == outage_id for x in json.loads(inbox.text)["items"])
                    and f.scalar("SELECT COUNT(*) FROM InternalMessages WHERE IdempotencyKey=?", (uuid.UUID(outage["idempotencyKey"]).hex,)) == 1)
                f.report.update(signalr_websocket_executed=True, redis_backplane_executed=True, application_nodes=2,
                    redis_version=redis.command("INFO", "server").split("redis_version:")[1].splitlines()[0],
                    limitations=["Both nodes use disposable SQLite for bounded mechanics. Production SQL Server, TLS and multi-IIS deployment were not exercised.",
                        "A content-free hint may duplicate or be missed; authorized durable REST inbox is authoritative."])
                f.write()
            finally:
                if paused: os.kill(node_b.pid, signal.SIGCONT)
                if ws:
                    try: ws.close()
                    except Exception: pass
                if node_b.poll() is None:
                    node_b.terminate()
                    try: node_b.wait(timeout=10)
                    except subprocess.TimeoutExpired: node_b.kill(); node_b.wait(timeout=5)
                log_b.close()
    finally:
        for key, value in old.items():
            if value is None: os.environ.pop(key, None)
            else: os.environ[key] = value


if __name__ == "__main__": run()
