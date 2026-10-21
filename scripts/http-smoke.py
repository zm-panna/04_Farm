#!/usr/bin/env python3
"""Disposable localhost HTTP integration acceptance, using only the Python stdlib.

PDF evidence extraction additionally requires Poppler's pdftotext, pdffonts and pdfinfo.

Run from the repository root with the pinned .NET SDK on PATH:
  dotnet build EnterpriseERP.slnx --configuration Release
  python3 scripts/http-smoke.py

No browser or JavaScript execution is involved. This does not verify UI appearance,
Windows/IIS, SQL Server, production configuration, or a live AI provider. The script
accepts neither an external URL nor a database path. Its SQLite fixture, generated
password, MFA enrollment and recovery values, Identity hashes, cookie jars, and Data
Protection keys are temporary. Only synthetic exports and non-secret logs are saved.
--hold-server keeps that same synthetic localhost host alive after acceptance for
separate inspection; create evidence/http/stop-server for graceful cleanup. Ctrl+C
or SIGTERM also stops it when the process host forwards signals to Python.
"""
from __future__ import annotations

import argparse
import base64
import csv
from datetime import datetime, timedelta, timezone
from decimal import Decimal
import hashlib
import hmac
from html.parser import HTMLParser
import http.cookiejar
import io
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import signal
import socket
import sqlite3
import struct
import subprocess
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / 'EnterpriseERP.Web/bin/Release/net10.0/EnterpriseERP.Web.dll'
OUT = ROOT / 'evidence/http'
CORE_PAGES = ['/animals', '/products', '/milk-entry', '/animal-health',
              '/FeedManagement', '/Fattening', '/breeding', '/Expenses', '/Purchase',
              '/Sales', '/Inventory', '/Parties', '/Accounts', '/Accounts/periods',
              '/Reports', '/Subscription', '/support/tickets', '/Automation',
              '/Administration', '/Account/ChangePassword', '/Accounts/opening',
              '/stock-transfers', '/Account/Security']


class Forms(HTMLParser):
    """Extract real rendered MVC form controls, including anti-forgery tokens."""
    def __init__(self, document: str):
        super().__init__(convert_charrefs=True)
        self.forms = []
        self.ids = []
        self.script_sources = []
        self.inline_script_count = 0
        self.text_parts = []
        self.current = self.select = self.option = self.textarea = None
        self.feed(document)

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if a.get('id'):
            self.ids.append(a['id'])
        if tag == 'script' and a.get('src'):
            self.script_sources.append(a['src'])
        elif tag == 'script':
            self.inline_script_count += 1
        if tag == 'form':
            self.current = {'action': a.get('action', ''), 'method': a.get('method', 'get').lower(),
                            'fields': {}, 'options': {}}
            self.forms.append(self.current)
        elif self.current is not None:
            if tag == 'input' and a.get('name') and 'disabled' not in a:
                if a.get('type', '').lower() in ('submit', 'button', 'file'):
                    return
                if a.get('type', '').lower() in ('checkbox', 'radio') and 'checked' not in a:
                    return
                self.current['fields'][a['name']] = a.get('value', '')
            elif tag == 'select' and a.get('name'):
                self.select = a['name']
                self.current['options'][self.select] = []
            elif tag == 'option' and self.select:
                self.option = a
                options = self.current['options'][self.select]
                options.append({'value': a.get('value', ''), 'text': ''})
                if len(options) == 1 or 'selected' in a:
                    self.current['fields'][self.select] = a.get('value', '')
            elif tag == 'textarea' and a.get('name'):
                self.textarea = a['name']
                self.current['fields'][self.textarea] = ''

    def handle_data(self, data):
        self.text_parts.append(data)
        if self.current is not None:
            if self.option is not None and self.select:
                self.current['options'][self.select][-1]['text'] += data
            if self.textarea:
                self.current['fields'][self.textarea] += data

    def handle_endtag(self, tag):
        if tag == 'form':
            self.current = self.select = self.option = self.textarea = None
        elif tag == 'select':
            self.select = self.option = None
        elif tag == 'option':
            self.option = None
        elif tag == 'textarea':
            self.textarea = None

    def get(self, action):
        matches = [form for form in self.forms if form['action'] == action]
        if not matches:
            raise AssertionError('Rendered form not found: ' + action)
        return matches[0]

    def text_content(self):
        return ' '.join(' '.join(self.text_parts).split())


class CodeBlocks(HTMLParser):
    """Read synthetic enrollment/recovery values into memory only; never persist HTML."""
    def __init__(self, document):
        super().__init__(convert_charrefs=True)
        self.values, self.active = [], False
        self.feed(document)

    def handle_starttag(self, tag, attrs):
        if tag == 'code':
            self.active = True
            self.values.append('')

    def handle_data(self, data):
        if self.active:
            self.values[-1] += data

    def handle_endtag(self, tag):
        if tag == 'code':
            self.active = False


def totp(shared_key, at=None):
    key = ''.join(shared_key.split()).upper()
    decoded = base64.b32decode(key + '=' * (-len(key) % 8))
    counter = int(time.time() if at is None else at) // 30
    value = hmac.new(decoded, struct.pack('>Q', counter), hashlib.sha1).digest()
    offset = value[-1] & 15
    number = struct.unpack('>I', value[offset:offset + 4])[0] & 0x7fffffff
    return f'{number % 1000000:06d}'


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Response:
    def __init__(self, response):
        self.status = response.status
        self.headers = response.headers
        self.body = response.read()
        self.text = self.body.decode('utf-8', errors='replace')
        self.location = self.headers.get('Location', '')


class Session:
    def __init__(self, base):
        self.base = base
        self.cookies = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}),
            urllib.request.HTTPCookieProcessor(self.cookies), NoRedirect())

    def request(self, path, fields=None, json_body=None):
        # Never follow a server redirect to an external location or contact another host.
        if not path.startswith('/') or path.startswith('//'):
            raise ValueError('Only relative localhost paths are accepted')
        data, headers = None, {}
        if fields is not None:
            data = urllib.parse.urlencode(fields).encode()
            headers['Content-Type'] = 'application/x-www-form-urlencoded'
        if json_body is not None:
            data = json.dumps(json_body).encode()
            headers['Content-Type'] = 'application/json'
        request = urllib.request.Request(self.base + path, data=data, headers=headers)
        try:
            with self.opener.open(request, timeout=45) as response:
                return Response(response)
        except urllib.error.HTTPError as response:
            return Response(response)

    def submit(self, form, overrides=None):
        if form['method'] != 'post':
            raise AssertionError('Expected a POST form')
        fields = dict(form['fields'])
        fields.update(overrides or {})
        return self.request(form['action'], fields=fields)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def database_snapshot(dbpath):
    # Used only in process to compare complete seeded rows, including Identity hashes.
    # Never serialize or print these values.
    with sqlite3.connect(dbpath) as db:
        tables = [row[0] for row in db.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")]
        return {table: sorted(db.execute('SELECT * FROM "' + table.replace('"', '""') + '"').fetchall(), key=repr)
                for table in tables}


def run(hold_server=False):
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'live-server.json').unlink(missing_ok=True)
    (OUT / 'stop-server').unlink(missing_ok=True)
    checks = []
    started = datetime.now(timezone.utc).isoformat()
    build_hash = digest(DLL) if DLL.exists() else None
    trusted_dll = DLL.with_name('TrustedInfrastructure.dll')
    trusted_hash = digest(trusted_dll)
    report = {'verification_kind': 'HTTP integration',
              'environment': 'Development SQLite, disposable synthetic fixture, localhost Kestrel',
              'browser_executed': False, 'production_verified': False,
              'not_verified': ['Browser rendering and JavaScript', 'Mobile interaction and localization persistence',
                               'SQL Server migration execution', 'Windows/IIS deployment', 'Live AI provider'],
              'started_utc': started, 'release_dll_sha256': build_hash, 'trusted_runtime_dll_sha256': trusted_hash, 'checks': checks}

    def write_report():
        report['updated_utc'] = datetime.now(timezone.utc).isoformat()
        report['passed'] = sum(c['passed'] for c in checks)
        report['failed'] = sum(not c['passed'] for c in checks)
        (OUT / 'results.json').write_text(json.dumps(report, indent=2) + '\n')

    def check(name, ok, detail=''):
        checks.append({'check': name, 'passed': bool(ok), 'detail': str(detail)})
        write_report()
        print(('PASS ' if ok else 'FAIL ') + name + (': ' + str(detail) if detail else ''), flush=True)
        if not ok:
            raise AssertionError(name + (': ' + str(detail) if detail else ''))

    process = log = None
    try:
        dotnet = shutil.which('dotnet')
        check('Built Release DLL and .NET runtime available', bool(dotnet and DLL.is_file()))
        with tempfile.TemporaryDirectory(prefix='farm-http-demo-') as tmp:
            tmp = Path(tmp)
            dbpath = tmp / 'farm-demo.db'
            password = 'T9!' + secrets.token_urlsafe(32)
            ephemeral_secrets = [password]
            env = dict(os.environ, ASPNETCORE_ENVIRONMENT='Development', DOTNET_ENVIRONMENT='Development',
                       Database__Provider='Sqlite', ConnectionStrings__DefaultConnection='Data Source=' + str(dbpath),
                       FARM_ALLOW_DEMO_SEED='YES', FARM_DEMO_PASSWORD=password,
                       Automation__WorkerEnabled='false', Ollama__Enabled='false',
                       HOME=str(tmp), XDG_DATA_HOME=str(tmp / '.local/share'),
                       DOTNET_CLI_HOME=str(tmp), ASPNETCORE_LOGGING__CONSOLE__DISABLECOLORS='true',
                       Logging__LogLevel__Default='Warning')
            snapshot = None
            for iteration in (1, 2):
                seed = subprocess.run([dotnet, str(DLL), '--init-demo'], cwd=ROOT / 'EnterpriseERP.Web',
                                      env=env, capture_output=True, text=True, timeout=180)
                (OUT / f'seed-{iteration}.log').write_text((seed.stdout + seed.stderr).replace(password, '[REDACTED]'))
                check('Atomic demo seed' if iteration == 1 else 'Idempotent demo seed rerun',
                      seed.returncode == 0, 'CLI exit ' + str(seed.returncode))
                if iteration == 1:
                    snapshot = database_snapshot(dbpath)
                else:
                    check('Seed rerun preserves every persisted row, password hash and grant',
                          snapshot == database_snapshot(dbpath))
            with sqlite3.connect(dbpath) as db:
                scopes = {}
                for number in range(1, 6):
                    tenant = db.execute('SELECT Id FROM Companies WHERE Slug=?', (f'demo-farm-{number}',)).fetchone()[0]
                    main = db.execute('SELECT Id FROM Branches WHERE CompanyId=? AND Name=?', (tenant, f'Demo Branch {number}')).fetchone()[0]
                    store = db.execute('SELECT Id FROM Branches WHERE CompanyId=? AND Name=?', (tenant, f'Demo Store Branch {number}')).fetchone()[0]
                    permitted = {row[0] for row in db.execute('SELECT a.BranchId FROM UserBranchAccess a JOIN AspNetUsers u ON u.Id=a.UserId WHERE u.Email=?', (f'farmdemo{number}@example.invalid',))}
                    scopes[number] = {'company':tenant, 'main':main, 'store':store, 'permitted':permitted}
                company, branch, store_branch = (scopes[1][key] for key in ('company','main','store'))
                other_company, other_branch = scopes[2]['company'], scopes[2]['main']
                stock = db.execute('SELECT Id FROM StockItems WHERE CompanyId=? AND BranchId=?', (company, branch)).fetchone()[0]
                store_stock = db.execute('SELECT Id FROM StockItems WHERE CompanyId=? AND BranchId=?', (company, store_branch)).fetchone()[0]
                other_stock = db.execute('SELECT Id FROM StockItems WHERE CompanyId=? AND BranchId=?', (other_company, other_branch)).fetchone()[0]
                other_ticket = db.execute('SELECT Id FROM SupportTickets WHERE CompanyId=?', (other_company,)).fetchone()[0]
                expected_counts = {'Companies':5,'AspNetUsers':5,'Branches':10,'StockItems':10,'Animals':15,
                                   'Vouchers':70,'VoucherLines':170,'StockMovements':45,'OpeningBalances':5,'StockTransfers':10}
                actual_counts = {table:db.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0] for table in expected_counts}
                check('V3 synthetic fixture has expected companies, branches, opening and transfer records', actual_counts == expected_counts,
                      json.dumps(actual_counts, sort_keys=True))
                check('V3 main and store initial stock quantities and units',
                      all(Decimal(str(db.execute('SELECT QuantityOnHand FROM StockItems WHERE Id=?',(item,)).fetchone()[0])) == expected
                                        and db.execute('SELECT UnitOfMeasure FROM StockItems WHERE Id=?',(item,)).fetchone()[0] == 'KG'
                                        for item,expected in [(stock,Decimal('96')),(store_stock,Decimal('10'))]))
                platform_users = db.execute("SELECT COUNT(*) FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE r.Name='PlatformAdmin'").fetchone()[0]
                check('Demo accounts have no platform administrator role', platform_users == 0)
            del snapshot
            with socket.socket() as sock:
                sock.bind(('127.0.0.1', 0))
                port = sock.getsockname()[1]
            base = f'http://127.0.0.1:{port}'
            env.pop('FARM_DEMO_PASSWORD')
            env.pop('FARM_ALLOW_DEMO_SEED')
            log = (OUT / 'server.log').open('w')
            process = subprocess.Popen([dotnet, str(DLL), '--urls', base], cwd=ROOT / 'EnterpriseERP.Web',
                                       env=env, stdout=log, stderr=subprocess.STDOUT)
            anonymous = Session(base)
            for _ in range(150):
                try:
                    if anonymous.request('/Account/Login').status == 200:
                        break
                except (urllib.error.URLError, TimeoutError):
                    pass
                if process.poll() is not None:
                    raise RuntimeError('Kestrel stopped; inspect evidence/http/server.log')
                time.sleep(.2)
            else:
                raise RuntimeError('Kestrel did not become ready')
            check('Disposable localhost Kestrel started', process.poll() is None)

            seen_script_sources = set()
            inline_script_pages = []

            def page(session, path, name=None):
                response = session.request(path)
                check(name or ('GET ' + path), response.status == 200 and '<html' in response.text.lower(),
                      'HTTP ' + str(response.status))
                markup = Forms(response.text)
                seen_script_sources.update(markup.script_sources)
                if markup.inline_script_count:
                    inline_script_pages.append(path)
                return response

            for path in ['/api/operations/summary', '/api/farm/animals', '/api/advanced/farm-summary.pdf', '/ops/readiness']:
                response = anonymous.request(path)
                check('Anonymous route denied ' + path, response.status == 401, 'HTTP ' + str(response.status))
            response = anonymous.request('/Purchase')
            check('Anonymous MVC redirects to login', response.status == 302 and '/Account/Login' in response.location)
            response = anonymous.request('/Account/Login', fields={'email': 'farmdemo1@example.invalid', 'password': password})
            check('Login requires antiforgery token', response.status == 400, 'HTTP ' + str(response.status))

            def login(number, selected_branch=None):
                session = Session(base)
                login_page = page(session, '/Account/Login', f'User {number} login form')
                form = Forms(login_page.text).get('/Account/Login')
                check(f'User {number} login form has antiforgery token', bool(form['fields'].get('__RequestVerificationToken')))
                response = session.submit(form, {'email': f'farmdemo{number}@example.invalid', 'password': password})
                check(f'User {number} real Identity login', response.status == 302 and response.location == '/Context',
                      'HTTP ' + str(response.status))
                context = page(session, '/Context', f'User {number} authorized context form')
                form = Forms(context.text).get('/Context/Select')
                check(f'User {number} sees exactly their assigned company and branches',
                      {int(o['value']) for o in form['options']['companyId']} == {scopes[number]['company']}
                      and {int(o['value']) for o in form['options']['branchId']} == scopes[number]['permitted'])
                response = session.submit(form, {'companyId':scopes[number]['company'], 'branchId':selected_branch or scopes[number]['main']})
                check(f'User {number} selects authorized tenant context', response.status == 302 and response.location == '/')
                return session

            admin = login(1)
            dashboard = page(admin, '/', 'Tenant administrator dashboard')
            directives = [part.strip() for part in dashboard.headers.get('Content-Security-Policy','').split(';')]
            check('HTTP Content-Security-Policy allows self-hosted scripts only', "script-src 'self'" in directives and
                  not any(part.startswith('script-src ') and part != "script-src 'self'" for part in directives))
            check('Administrator receives authorized dashboard totals', 'class="kpi-grid"' in dashboard.text and 'Restricted' not in dashboard.text)
            admin_sidebar = dashboard.text.partition('</aside>')[0]
            check('Administrator receives staff and permission sidebar navigation links',
                  'href="/Administration"' in admin_sidebar and 'href="/admin/permissions"' in admin_sidebar)
            for path in CORE_PAGES:
                core_page = page(admin, path)
                if path == '/support/tickets':
                    support_markup = Forms(core_page.text)
                    support_ids = ['newTicket','tickets','supportMessage','status','previousTickets','nextTickets','ticketPage','refresh']
                    check('Support pager HTTP markup has every required ID exactly once',
                          all(support_markup.ids.count(element_id) == 1 for element_id in support_ids))
                    check('Support page includes its pager script and antiforgery create form',
                          any(src.startswith('/js/support-tickets.js') for src in support_markup.script_sources)
                          and any('TicketNumber' in form['fields'] and bool(form['fields'].get('__RequestVerificationToken'))
                                  for form in support_markup.forms))
            check('All 23 core MVC pages returned HTML over authenticated HTTP', len(CORE_PAGES) == 23)
            response = admin.request('/HR/Home')
            check('Unimplemented HR shell disabled', response.status == 404, 'HTTP ' + str(response.status))
            response = admin.request('/ops/readiness')
            check('Tenant administrator cannot inspect platform readiness', response.status == 403, 'HTTP ' + str(response.status))
            response = admin.request('/api/advanced/payment', json_body={'reference':'HTTP-BAD-CSRF','partyType':'Customer','amount':1,'documentId':1})
            check('Finance JSON POST rejects missing antiforgery token', response.status == 400, 'HTTP ' + str(response.status))
            context = Forms(admin.request('/Context').text).get('/Context/Select')
            response = admin.request('/Context/Select', fields={'companyId': company, 'branchId': branch})
            check('Context mutation rejects missing antiforgery token', response.status == 400)
            response = admin.submit(context, {'companyId': other_company, 'branchId': other_branch})
            check('Cross-tenant context selection denied', response.status == 403, 'HTTP ' + str(response.status))
            response = admin.submit(context, {'companyId': company, 'branchId': other_branch})
            check('Mismatched company and branch denied', response.status == 403)
            inventory = page(admin, '/Inventory', 'Context remains usable after denied switches')
            check('Inventory HTML excludes other tenant stock', 'DEMO-FEED-1' in inventory.text and 'DEMO-FEED-2' not in inventory.text)
            response = admin.request('/api/farm/animals')
            animals = json.loads(response.text)
            check('Animal API is tenant scoped', response.status == 200 and animals and all(a['companyId'] == company and a['branchId'] == branch for a in animals))
            response = admin.request(f'/api/support/tickets/{other_ticket}/history')
            check('Cross-tenant support object ID cannot be read', response.status == 404, 'HTTP ' + str(response.status))

            def scalar(sql, parameters=()):
                with sqlite3.connect(dbpath) as db:
                    return db.execute(sql, parameters).fetchone()[0]

            def stock_quantity(item=stock):
                return Decimal(str(scalar('SELECT QuantityOnHand FROM StockItems WHERE Id=?', (item,))))

            def stock_value(item=stock):
                return Decimal(str(scalar('SELECT Value FROM InventoryCostBalances WHERE StockItemId=?', (item,))))

            def assert_scope(session, expected_branch, name):
                response = session.request('/api/farm/animals')
                rows = json.loads(response.text) if response.status == 200 else []
                check(name, response.status == 200 and rows and all(row['companyId'] == company and row['branchId'] == expected_branch for row in rows),
                      'HTTP ' + str(response.status) + '; branch ' + str(expected_branch))

            def select_scope(session, selected_branch, name):
                response = session.request('/Context')
                check(name + ' context form', response.status == 200)
                form = Forms(response.text).get('/Context/Select')
                response = session.submit(form, {'companyId':company, 'branchId':selected_branch})
                check(name + ' context selected', response.status == 302 and response.location == '/')

            second_admin = login(1, store_branch)
            assert_scope(admin, branch, 'First independent session keeps main branch after second session selects store')
            assert_scope(second_admin, store_branch, 'Second independent session keeps store branch after Identity principal refresh')
            select_scope(admin, store_branch, 'First session switches to store')
            select_scope(second_admin, branch, 'Second session switches to main')
            assert_scope(admin, store_branch, 'Context selection in second session cannot switch first session')
            assert_scope(second_admin, branch, 'Independent main selection survives repeated principal refresh')
            select_scope(admin, branch, 'Restore first session main branch')
            select_scope(second_admin, store_branch, 'Restore second session store branch')
            check('Tenant choices are not global Identity user claims', scalar("SELECT COUNT(*) FROM AspNetUserClaims WHERE ClaimType IN ('ActiveCompanyId','ActiveBranchId')") == 0)
            opening = page(second_admin, '/Accounts/opening', 'Store branch shows immutable seeded opening')
            check('Existing opening hides create form', 'Posted opening balance' in opening.text and
                  not any(f['action'] == '/Accounts/opening/post' for f in Forms(opening.text).forms))
            csrf = Forms(opening.text).get('/Account/Logout')['fields']['__RequestVerificationToken']
            before_opening = database_snapshot(dbpath)
            response = second_admin.request('/Accounts/opening/post', fields={
                '__RequestVerificationToken':csrf, 'Opening.Reference':'HTTP-OPEN-DUP',
                'Opening.OpeningDate':datetime.now(timezone.utc).date().isoformat(),
                'Opening.ReviewNote':'Synthetic duplicate guard test', 'Opening.ConfirmReviewed':'true',
                'Opening.Ledger[0].AccountCode':'1000','Opening.Ledger[0].Debit':'10','Opening.Ledger[0].Credit':'0',
                'Opening.Ledger[1].AccountCode':'3000','Opening.Ledger[1].Debit':'0','Opening.Ledger[1].Credit':'10'})
            check('Second opening is rejected with explicit validation', response.status == 200 and 'opening balance already exists' in response.text.lower())
            check('Duplicate opening changes no persisted record', before_opening == database_snapshot(dbpath))
            del before_opening
            assert_scope(admin, branch, 'Main session remains unchanged after store opening attempt')

            before_quantity = stock_quantity()
            purchase_page = page(admin, '/Purchase', 'Purchase posting form')
            form = Forms(purchase_page.text).get('/Purchase/post')
            check('Purchase form exposes only active tenant stock',
                  [int(o['value']) for o in form['options']['ItemId'] if o['value']] == [stock])
            before_cross_tenant = database_snapshot(dbpath)
            response = admin.submit(form, {'ItemId': other_stock, 'Quantity':'3','Price':'7.125','Reference':'HTTP-CROSS-PUR'})
            # MVC business validation uses Post/Redirect/Get with a visible error.
            error_page = admin.request('/Purchase') if response.status == 302 else response
            check('Cross-tenant purchase object denied',
                  (response.status in (400, 403, 404) or (response.status == 302 and response.location == '/Purchase'
                   and 'role="alert"' in error_page.text)) and before_cross_tenant == database_snapshot(dbpath),
                  'HTTP ' + str(response.status) + '; no persisted changes')
            del before_cross_tenant
            check('Cross-tenant purchase creates no receipt', scalar("SELECT COUNT(*) FROM PurchaseReceipts WHERE Reference='HTTP-CROSS-PUR'") == 0)
            response = admin.submit(form, {'ItemId': stock, 'Quantity':'3','Price':'7.125','Reference':'HTTP-PUR-1'})
            check('Purchase MVC post succeeds', response.status == 302 and response.location == '/Purchase')
            purchase_page = page(admin, '/Purchase', 'Purchase document shown after post')
            check('Posted purchase reference shown', 'PUR-HTTP-PUR-1' in purchase_page.text)
            check('Purchase stock movement is exact', stock_quantity() == before_quantity + Decimal('3'))
            amount = Decimal(str(scalar("SELECT Amount FROM FinanceDocuments WHERE Reference='PUR-HTTP-PUR-1'")))
            check('Purchase uses exact three-decimal amount', amount == Decimal('21.375'))
            before_duplicate = database_snapshot(dbpath)
            form = Forms(purchase_page.text).get('/Purchase/post')
            response = admin.submit(form, {'ItemId': stock, 'Quantity':'3','Price':'7.125','Reference':'HTTP-PUR-1'})
            check('Duplicate purchase returns handled MVC response', response.status == 302 and response.location == '/Purchase')
            check('Duplicate purchase makes no database change', before_duplicate == database_snapshot(dbpath))
            check('Duplicate purchase leaves exactly one receipt', scalar("SELECT COUNT(*) FROM PurchaseReceipts WHERE Reference='HTTP-PUR-1'") == 1)
            response = admin.request('/Purchase')
            check('Duplicate purchase gives visible error', 'role="alert"' in response.text)

            sales_page = page(admin, '/Sales', 'Sale posting form')
            form = Forms(sales_page.text).get('/Sales/post')
            response = admin.submit(form, {'ItemId': stock, 'Quantity':'1','Price':'9','Reference':'HTTP-SALE-1'})
            check('Sale MVC post succeeds', response.status == 302 and response.location == '/Sales')
            sales_page = page(admin, '/Sales', 'Sale document shown after post')
            check('Posted sale reference shown', 'SAL-HTTP-SALE-1' in sales_page.text)
            check('Purchase and sale net stock movement is exact', stock_quantity() == before_quantity + Decimal('2'))
            before_duplicate = database_snapshot(dbpath)
            form = Forms(sales_page.text).get('/Sales/post')
            response = admin.submit(form, {'ItemId': stock, 'Quantity':'1','Price':'9','Reference':'HTTP-SALE-1'})
            check('Duplicate sale returns handled MVC response', response.status == 302 and response.location == '/Sales')
            check('Duplicate sale makes no database change', before_duplicate == database_snapshot(dbpath))
            check('Duplicate sale leaves exactly one sale', scalar("SELECT COUNT(*) FROM ProductSales WHERE Reference='HTTP-SALE-1'") == 1)
            check('Duplicate sale gives visible error', 'role="alert"' in admin.request('/Sales').text)
            before_invalid = database_snapshot(dbpath)
            response = admin.submit(form, {'ItemId': stock, 'Quantity':'-1','Price':'9','Reference':'HTTP-INVALID-SALE'})
            check('Invalid negative quantity is handled without posting', response.status == 302 and before_invalid == database_snapshot(dbpath))
            del before_duplicate, before_invalid

            transfer_page = page(admin, f'/stock-transfers/new?sourceItemId={stock}&destinationBranchId={store_branch}',
                                 'Real stock-transfer posting form')
            transfer_form = Forms(transfer_page.text).get('/stock-transfers/post')
            check('Transfer form exposes matching SKU in authorized store branch',
                  {int(o['value']) for o in transfer_form['options']['Input.DestinationItemId'] if o['value']} == {store_stock})
            quantities_before = (stock_quantity(), stock_quantity(store_stock))
            carrying_value_before = stock_value() + stock_value(store_stock)
            transfer_fields = {'Input.SourceItemId':stock,'Input.DestinationBranchId':store_branch,
                               'Input.DestinationItemId':store_stock,'Input.Quantity':'3.5','Input.Reference':'HTTP-XFER-1',
                               'Input.Note':'Disposable synthetic acceptance'}
            response = admin.submit(transfer_form, transfer_fields)
            check('Stock transfer posts through MVC and redirects to its detail', response.status == 302 and response.location.startswith('/stock-transfers/'))
            detail_path = response.location
            transfer_detail = page(admin, detail_path, 'Posted transfer detail is readable')
            check('Transfer detail contains reference and stock unit', 'HTTP-XFER-1' in transfer_detail.text and 'KG' in transfer_detail.text)
            check('Transfer changes both stock quantities exactly', stock_quantity() == quantities_before[0] - Decimal('3.5')
                  and stock_quantity(store_stock) == quantities_before[1] + Decimal('3.5'))
            check('Transfer preserves total company carrying value', stock_value() + stock_value(store_stock) == carrying_value_before)
            check('Transfer has two persisted movements and two balanced voucher links',
                  scalar("SELECT COUNT(*) FROM StockMovements WHERE Reference IN ('XFER-OUT-HTTP-XFER-1','XFER-IN-HTTP-XFER-1')") == 2
                  and scalar("SELECT COUNT(*) FROM StockTransfers WHERE Reference='HTTP-XFER-1' AND SourceVoucherId IS NOT NULL AND DestinationVoucherId IS NOT NULL") == 1)
            before_transfer_error = database_snapshot(dbpath)
            response = admin.submit(transfer_form, transfer_fields)
            check('Duplicate stock transfer returns explicit error', response.status == 200 and 'already been posted' in response.text)
            check('Duplicate transfer changes no persisted record', before_transfer_error == database_snapshot(dbpath))
            cross_fields = dict(transfer_fields, **{'Input.DestinationBranchId':other_branch,'Input.DestinationItemId':other_stock,'Input.Reference':'HTTP-XFER-CROSS'})
            response = admin.submit(transfer_form, cross_fields)
            check('Transfer to unauthorized other-company branch denied', response.status == 403)
            check('Denied cross-branch transfer changes no persisted record', before_transfer_error == database_snapshot(dbpath))
            cross_fields = dict(transfer_fields, **{'Input.SourceItemId':store_stock,'Input.Reference':'HTTP-XFER-WRONG-SOURCE'})
            response = admin.submit(transfer_form, cross_fields)
            check('Transfer cannot take source stock from another active branch', response.status == 200 and 'source product in the active branch' in response.text)
            check('Invalid source-branch transfer changes no persisted record', before_transfer_error == database_snapshot(dbpath))
            page(second_admin, detail_path, 'Authorized destination session can read transfer detail')
            del before_transfer_error

            automation = page(admin, '/Automation', 'AI fallback request form')
            response = admin.submit(Forms(automation.text).get('/Automation/ask'), {'question':'Summarize the synthetic demo farm'})
            check('Disabled AI returns explicit unavailable response', response.status == 503 and 'AI chat is disabled' in response.text,
                  'HTTP ' + str(response.status))
            page(admin, '/Reports', 'ERP reports remain usable after AI failure')
            summary_response = admin.request('/api/operations/summary')
            summary = json.loads(summary_response.text, parse_float=Decimal)
            check('Authoritative summary API remains available', summary_response.status == 200 and summary['animalCount'] == 1)
            csv_headers = {'en':['Metric','Value'], 'bn':['সূচক','মান'], 'banglish':['Suchok','Man'], 'ar':['المؤشر','القيمة']}
            excel_headers = {'en':['Account','Debit','Credit'], 'bn':['হিসাব','ডেবিট','ক্রেডিট'],
                             'banglish':['Hisab','Debit','Credit'], 'ar':['الحساب','مدين','دائن']}
            metric_order = ['animalCount','stockValue','sales','purchases','milkLitres']
            for language in ('en','bn','banglish','ar'):
                csv_response = admin.request('/api/operations/summary.csv?lang=' + language)
                rows = list(csv.reader(io.StringIO(csv_response.body.decode('utf-8-sig'))))
                check(f'{language} CSV has localized heading and correct MIME', csv_response.status == 200 and
                      'text/csv' in csv_response.headers.get('Content-Type','') and rows[0] == csv_headers[language])
                check(f'{language} CSV values equal authoritative summary', len(rows) == 6 and
                      all(Decimal(row[1]) == Decimal(str(summary[key])) for row,key in zip(rows[1:],metric_order)))
                if language == 'en':
                    check('English report labels state gross product totals before returns',
                          rows[3][0] == 'Product sales (gross before returns)' and rows[4][0] == 'Product purchases (gross before returns)')
                (OUT / f'farm-summary-{language}.csv').write_bytes(csv_response.body)
                xlsx = admin.request('/api/advanced/trial-balance.xlsx?through=2099-01-01T00:00:00Z&lang=' + language)
                check(f'{language} Excel export has expected MIME and ZIP signature', xlsx.status == 200 and xlsx.body.startswith(b'PK') and
                      'spreadsheetml.sheet' in xlsx.headers.get('Content-Type',''))
                with zipfile.ZipFile(io.BytesIO(xlsx.body)) as workbook:
                    names = workbook.namelist()
                    check(f'{language} Excel is a valid OpenXML workbook', workbook.testzip() is None and
                          '[Content_Types].xml' in names and 'xl/workbook.xml' in names)
                    ns = {'x':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
                    strings = ET.fromstring(workbook.read('xl/sharedStrings.xml'))
                    shared = [''.join(item.itertext()) for item in strings.findall('x:si',ns)]
                    check(f'{language} Excel contains localized account headers', all(label in shared for label in excel_headers[language]))
                    sheet = ET.fromstring(workbook.read('xl/worksheets/sheet1.xml'))
                    right_to_left = any(view.get('rightToLeft') in ('1','true') for view in sheet.findall('x:sheetViews/x:sheetView',ns))
                    check(f'{language} Excel right-to-left marker is appropriate', right_to_left == (language == 'ar'))
                (OUT / f'trial-balance-{language}.xlsx').write_bytes(xlsx.body)
                pdf = admin.request('/api/advanced/farm-summary.pdf?lang=' + language)
                check(f'{language} PDF has valid MIME, header and EOF', pdf.status == 200 and pdf.body.startswith(b'%PDF') and
                      'application/pdf' in pdf.headers.get('Content-Type','') and pdf.body.rstrip().endswith(b'%%EOF') and len(pdf.body)>1000)
                pdf_path = OUT / f'farm-summary-{language}.pdf'
                pdf_path.write_bytes(pdf.body)
                for tool in ('pdftotext','pdffonts','pdfinfo'):
                    executable = shutil.which(tool)
                    check(f'{language} PDF {tool} validator is available', bool(executable))
                    command = [executable,str(pdf_path)] + (['-'] if tool == 'pdftotext' else [])
                    validation = subprocess.run(command,capture_output=True,text=True,timeout=30)
                    (OUT / f'farm-summary-{language}.{tool}.txt').write_text(validation.stdout + validation.stderr)
                    check(f'{language} PDF passes {tool} structural extraction', validation.returncode == 0 and bool(validation.stdout.strip()))
                    if tool == 'pdffonts' and language in ('bn','ar'):
                        check(f'{language} PDF uses packaged script-specific Noto font',
                              ('NotoSansBengali' if language == 'bn' else 'NotoSansArabic') in validation.stdout.replace(' ',''))
                if language == 'en':
                    (OUT / 'farm-summary.csv').write_bytes(csv_response.body)
                    (OUT / 'trial-balance.xlsx').write_bytes(xlsx.body)
                    (OUT / 'farm-summary.pdf').write_bytes(pdf.body)
            for export_path in ('/api/operations/summary.csv?lang=invalid',
                                '/api/advanced/trial-balance.xlsx?through=2099-01-01T00:00:00Z&lang=invalid',
                                '/api/advanced/farm-summary.pdf?lang=invalid'):
                check('Unknown report language rejected ' + export_path, admin.request(export_path).status == 400)
            trial_response = admin.request('/api/advanced/trial-balance?through=2099-01-01T00:00:00Z')
            trial = json.loads(trial_response.text, parse_float=Decimal)
            check('Trial balance API debit equals credit', trial_response.status == 200 and trial and
                  sum(Decimal(str(row['debit'])) for row in trial) == sum(Decimal(str(row['credit'])) for row in trial))

            # Follow-up regressions exercise real MVC model binding and original edit tokens.
            # These changes are confined to this disposable synthetic database and occur
            # after exports so rendered comparison fixtures keep their original values.
            actor = scalar("SELECT Id FROM AspNetUsers WHERE Email='farmdemo1@example.invalid'")
            def audit_rows(action, reference):
                with sqlite3.connect(dbpath) as db:
                    return db.execute('SELECT CompanyId, ActorId, Action, Reference, CreatedUtc FROM Audits WHERE Action=? AND Reference=?',
                                      (action, reference)).fetchall()

            accounts = page(admin, '/Accounts', 'Audited journal and bank forms')
            journal_form = Forms(accounts.text).get('/Accounts/journal')
            journal_started = datetime.now(timezone.utc)
            response = admin.submit(journal_form, {'reference':'HTTP-AUDIT-JOURNAL','debitAccount':'1000','creditAccount':'3000','amount':'12.345'})
            journal_audits = audit_rows('Finance.Journal.Post', 'JRN-HTTP-AUDIT-JOURNAL')
            check('Manual journal records exact tenant actor action reference and current timestamp', response.status == 302 and
                  len(journal_audits) == 1 and journal_audits[0][:4] == (company, actor, 'Finance.Journal.Post', 'JRN-HTTP-AUDIT-JOURNAL') and
                  journal_started <= (datetime(1,1,1,tzinfo=timezone.utc) + timedelta(microseconds=int(journal_audits[0][4]) // 10)) <= datetime.now(timezone.utc))
            before_duplicate = database_snapshot(dbpath)
            response = admin.submit(journal_form, {'reference':'HTTP-AUDIT-JOURNAL','debitAccount':'1000','creditAccount':'3000','amount':'12.345'})
            check('Duplicate manual journal reports validation without another audit or posting', response.status == 302 and
                  'Nothing was posted' in admin.request('/Accounts').text and before_duplicate == database_snapshot(dbpath))
            bank_form = Forms(accounts.text).get('/Accounts/bank-line')
            bank_started = datetime.now(timezone.utc)
            response = admin.submit(bank_form, {'reference':'HTTP-AUDIT-BANK','amount':'12.345','date':datetime.now(timezone.utc).date().isoformat()})
            bank_ref = f'branch:{branch}; HTTP-AUDIT-BANK'
            bank_audits = audit_rows('Bank.StatementLineCreated', bank_ref)
            check('Bank entry records exact tenant actor branch reference and current timestamp', response.status == 302 and
                  len(bank_audits) == 1 and bank_audits[0][:4] == (company, actor, 'Bank.StatementLineCreated', bank_ref) and
                  bank_started <= (datetime(1,1,1,tzinfo=timezone.utc) + timedelta(microseconds=int(bank_audits[0][4]) // 10)) <= datetime.now(timezone.utc))
            before_duplicate = database_snapshot(dbpath)
            response = admin.submit(bank_form, {'reference':'HTTP-AUDIT-BANK','amount':'12.345','date':datetime.now(timezone.utc).date().isoformat()})
            check('Duplicate bank entry reports validation without another audit or line', response.status == 302 and
                  'Nothing was posted' in admin.request('/Accounts').text and before_duplicate == database_snapshot(dbpath))
            del before_duplicate

            csrf = journal_form['fields']['__RequestVerificationToken']
            stock_fields = {'__RequestVerificationToken':csrf,'sku':'HTTP-AUDIT-STOCK','name':'HTTP synthetic product','cost':'1.001'}
            before_invalid = database_snapshot(dbpath)
            response = admin.request('/api/farm/stock', fields=stock_fields)
            check('Stock API requires an explicit unit without guessing or persisting data', response.status == 400 and before_invalid == database_snapshot(dbpath))
            response = admin.request('/api/farm/stock', fields=dict(stock_fields, unitOfMeasure='kg/l'))
            check('Stock API rejects ambiguous unit without data or audits', response.status == 400 and before_invalid == database_snapshot(dbpath))
            response = admin.request('/api/farm/stock', fields=dict(stock_fields, unitOfMeasure=' kg '))
            created_stock = json.loads(response.text) if response.status == 200 else {}
            new_stock = created_stock.get('id')
            check('Stock API creates normalized explicit unit and zero quantity', response.status == 200 and new_stock and
                  created_stock.get('unitOfMeasure') == 'KG' and Decimal(str(created_stock.get('quantityOnHand'))) == 0)
            check('Stock creation and unit assignment both identify tenant and actor',
                  len(audit_rows('Product.Create', 'HTTP-AUDIT-STOCK')) == 1 and
                  audit_rows('Product.Create', 'HTTP-AUDIT-STOCK')[0][:2] == (company, actor) and
                  len(audit_rows('Product.UnitAssigned', 'HTTP-AUDIT-STOCK; unit KG')) == 1 and
                  audit_rows('Product.UnitAssigned', 'HTTP-AUDIT-STOCK; unit KG')[0][:2] == (company, actor))
            before_duplicate = database_snapshot(dbpath)
            response = admin.request('/api/farm/stock', fields=dict(stock_fields, unitOfMeasure='KG'))
            check('Duplicate stock API creates neither a second product nor misleading audit', response.status == 409 and before_duplicate == database_snapshot(dbpath))
            del before_duplicate, before_invalid

            product_form_a = Forms(page(admin, f'/products/{new_stock}/edit', 'First product edit snapshot').text).get('/products/save')
            product_form_b = Forms(page(admin, f'/products/{new_stock}/edit', 'Second product edit snapshot').text).get('/products/save')
            check('Existing product edit includes original nonempty concurrency token', bool(product_form_a['fields'].get('RowVersion')))
            response = admin.submit(product_form_b, {'Name':'HTTP current saved product'})
            check('Current product edit succeeds with original token', response.status == 302 and
                  scalar('SELECT Name FROM StockItems WHERE Id=?',(new_stock,)) == 'HTTP current saved product')
            before_stale = database_snapshot(dbpath)
            response = admin.submit(product_form_a, {'Name':'HTTP stale overwrite'})
            check('Stale product form returns conflict and preserves all records and audits', response.status == 409 and
                  'Reload before saving' in response.text and before_stale == database_snapshot(dbpath))
            missing_token = dict(product_form_b['fields']); missing_token.pop('RowVersion', None)
            response = admin.request('/products/save', fields=missing_token)
            check('Missing product edit token cannot overwrite metadata', response.status == 409 and before_stale == database_snapshot(dbpath))
            del before_stale

            animal_page = page(admin, '/animals/new', 'Exact-scale animal acquisition form')
            check('Animal cost form permits the documented three decimal places',
                  bool(re.search(r'<input[^>]*step="0\.001"[^>]*name="PurchaseCost"', animal_page.text)))
            animal_form = Forms(animal_page.text).get('/animals/save')
            response = admin.submit(animal_form, {'Tag':'HTTP-COST-ANIMAL','Breed':'Synthetic breed','PurchaseCost':'10.001','CurrentWeightKg':'100.001'})
            new_animal = scalar("SELECT COALESCE(MAX(Id),0) FROM Animals WHERE CompanyId=? AND Tag='HTTP-COST-ANIMAL'",(company,))
            check('Animal acquisition accepts exact three-decimal cost through real MVC binding', response.status == 302 and new_animal and
                  Decimal(str(scalar('SELECT PurchaseCost FROM Animals WHERE Id=?',(new_animal,)))) == Decimal('10.001') and
                  scalar("SELECT COUNT(*) FROM FinanceDocuments WHERE Kind='AnimalPurchase' AND SourceId=?",(new_animal,)) == 1)
            edit_animal = Forms(page(admin, f'/animals/{new_animal}/edit', 'Existing animal exact-cost edit').text).get('/animals/save')
            response = admin.submit(edit_animal, {'Breed':'Synthetic reviewed breed'})
            check('Animal metadata edit preserves immutable exact acquisition cost', response.status == 302 and
                  scalar('SELECT Breed FROM Animals WHERE Id=?',(new_animal,)) == 'Synthetic reviewed breed' and
                  Decimal(str(scalar('SELECT PurchaseCost FROM Animals WHERE Id=?',(new_animal,)))) == Decimal('10.001'))

            parties_page = page(admin, '/Parties', 'Explicit party status binding form')
            party_form = Forms(parties_page.text).get('/Parties/status')
            party_id = int(party_form['fields']['partyId'])
            original_active = scalar('SELECT Active FROM TradingParties WHERE Id=?',(party_id,))
            for submitted, label in [('not-a-boolean','Malformed'), (None,'Missing')]:
                fields = dict(party_form['fields'])
                if submitted is None: fields.pop('active',None)
                else: fields['active'] = submitted
                before_invalid = database_snapshot(dbpath)
                response = admin.request('/Parties/status',fields=fields)
                detail = admin.request('/Parties') if response.status == 302 else response
                check(label + ' party active state fails visibly without mutation or audit', response.status in (302,400) and
                      'Choose a valid active status.' in detail.text and before_invalid == database_snapshot(dbpath))
            response = admin.submit(party_form, {'active':'false'})
            check('Explicit false party state remains an intentional supported action', response.status == 302 and
                  scalar('SELECT Active FROM TradingParties WHERE Id=?',(party_id,)) == 0)
            if original_active: admin.submit(party_form, {'active':'true'})

            automation_page = page(admin, '/Automation', 'Explicit rule status binding form')
            rule_form = Forms(automation_page.text).get('/Automation/toggle')
            rule_id = int(rule_form['fields']['ruleId'])
            response = admin.submit(rule_form, {'active':'true'})
            check('Explicit true rule status succeeds', response.status == 302 and scalar('SELECT Active FROM AutomationRules WHERE Id=?',(rule_id,)) == 1)
            for submitted, label in [('not-a-boolean','Malformed'), (None,'Missing')]:
                fields = dict(rule_form['fields'])
                if submitted is None: fields.pop('active',None)
                else: fields['active'] = submitted
                before_invalid = database_snapshot(dbpath)
                response = admin.request('/Automation/toggle',fields=fields)
                check(label + ' rule active state fails visibly without mutation or audit', response.status == 400 and
                      'Select an explicit active or inactive state.' in response.text and before_invalid == database_snapshot(dbpath))
            response = admin.submit(rule_form, {'active':'false'})
            check('Explicit false rule status remains an intentional supported action', response.status == 302 and
                  scalar('SELECT Active FROM AutomationRules WHERE Id=?',(rule_id,)) == 0)
            del before_invalid

            # Boundary fixtures use the same disposable database and authenticated HTTP
            # session, independently from display-history limits and without live access.
            import runpy
            runpy.run_path(str(ROOT / 'scripts/http-paging-smoke.py'))['run_paging_checks'](
                dbpath, admin, Forms, check, page, company=company, branch=branch,
                store_branch=store_branch, other_company=other_company, other_branch=other_branch)

            staff = login(4)
            restricted_dashboard = page(staff, '/', 'Low-privilege staff dashboard remains usable')
            check('Low-privilege dashboard suppresses financial totals', 'Restricted' in restricted_dashboard.text)
            staff_sidebar = restricted_dashboard.text.partition('</aside>')[0]
            check('Inventory staff cannot see staff or permission sidebar navigation links',
                  'href="/Administration"' not in staff_sidebar and 'href="/admin/permissions"' not in staff_sidebar)
            for path in ['/Accounts', '/Accounts/opening', '/Reports', '/Administration', '/Automation', '/api/operations/summary',
                         '/api/operations/summary.csv', '/api/advanced/trial-balance.xlsx?through=2099-01-01T00:00:00Z',
                         '/api/advanced/farm-summary.pdf', '/api/farm/animals', '/ops/readiness']:
                response = staff.request(path)
                check('Low-privilege route denied ' + path, response.status == 403, 'HTTP ' + str(response.status))
            for path in ['/Inventory', '/Purchase', '/Sales', '/stock-transfers', '/Account/Security']:
                page(staff, path, 'Low-privilege permitted route ' + path)
            viewer = login(5)
            viewer_support = page(viewer, '/support/tickets', 'Read-only support page remains usable')
            viewer_support_markup = Forms(viewer_support.text)
            check('Read-only support retains pager elements without an unauthorized create form',
                  'newTicket' not in viewer_support_markup.ids and
                  all(viewer_support_markup.ids.count(element_id) == 1 for element_id in support_ids if element_id != 'newTicket'))
            check('Read-only viewer cannot open transfer posting form', viewer.request('/stock-transfers/new').status == 403)
            viewer_purchase = page(viewer, '/Purchase', 'Read-only viewer can inspect purchases')
            viewer_form = Forms(viewer_purchase.text).get('/Purchase/post')
            viewer_stock = next(o['value'] for o in viewer_form['options']['ItemId'] if o['value'])
            before_forbidden = database_snapshot(dbpath)
            response = viewer.submit(viewer_form, {'ItemId': viewer_stock,'Quantity':'1','Price':'1','Reference':'HTTP-READONLY-PUR'})
            check('Read-only user cannot post purchase with valid antiforgery token', response.status == 403, 'HTTP ' + str(response.status))
            check('Forbidden read-only purchase makes no database change', before_forbidden == database_snapshot(dbpath))
            del before_forbidden
            # Enrollment is exercised only on the synthetic ephemeral account. No enrollment
            # HTML, shared key, proof, TOTP, password or recovery code is written to evidence.
            security_page = page(admin, '/Account/Security', 'Synthetic account security enrollment form')
            begin_form = Forms(security_page.text).get('/Account/BeginAuthenticator')
            setup = admin.submit(begin_form, {'CurrentPassword':password})
            check('Authenticator enrollment requires password and returns setup form', setup.status == 200 and
                  any(f['action'] == '/Account/CompleteAuthenticator' for f in Forms(setup.text).forms))
            shared_keys = CodeBlocks(setup.text).values
            check('Enrollment provides exactly one synthetic TOTP key', len(shared_keys) == 1 and len(shared_keys[0].strip()) >= 16)
            shared_key = shared_keys[0].strip()
            complete_form = Forms(setup.text).get('/Account/CompleteAuthenticator')
            ephemeral_secrets += [shared_key, complete_form['fields']['Proof']]
            assert_scope(admin, branch, 'Selected branch survives enrollment-start security-stamp refresh')
            complete = admin.submit(complete_form, {'CurrentPassword':password,'Code':totp(shared_key)})
            recovery_codes = [value.strip() for value in CodeBlocks(complete.text).values]
            check('TOTP enrollment succeeds and displays ten one-time recovery codes', complete.status == 200 and
                  len(recovery_codes) == 10 and len(set(recovery_codes)) == 10 and all(len(code) >= 8 for code in recovery_codes))
            ephemeral_secrets += recovery_codes
            assert_scope(admin, branch, 'Selected branch survives MFA enable security-stamp refresh')
            check('MFA enabled state appears without redisplaying secret or recovery values',
                  'Authenticator MFA is enabled' in admin.request('/Account/Security').text)
            check('MFA security change invalidates previous independent login cookie', second_admin.request('/api/farm/animals').status == 401)

            def password_challenge(name):
                session = Session(base)
                form_page = page(session,'/Account/Login',name + ' password form')
                response = session.submit(Forms(form_page.text).get('/Account/Login'),
                                          {'email':'farmdemo1@example.invalid','password':password})
                check(name + ' requires second factor after correct password', response.status == 302 and
                      urllib.parse.urlsplit(response.location).path == '/Account/TwoFactor')
                check(name + ' password-only challenge cannot read protected records', session.request('/api/farm/animals').status == 401)
                return session

            authenticator_session = password_challenge('Authenticator login')
            challenge = page(authenticator_session,'/Account/TwoFactor','Authenticator challenge form')
            challenge_form = Forms(challenge.text).get('/Account/TwoFactor')
            valid_window = {totp(shared_key,time.time() + offset*30) for offset in range(-3,4)}
            invalid_code = next(f'{value:06d}' for value in range(10) if f'{value:06d}' not in valid_window)
            response = authenticator_session.submit(challenge_form, {'Code':invalid_code})
            check('Incorrect TOTP is rejected with explicit challenge error', response.status == 200 and
                  'Second-factor verification failed' in response.text)
            check('Incorrect TOTP creates no authenticated application session', authenticator_session.request('/api/farm/animals').status == 401)
            challenge_form = Forms(response.text).get('/Account/TwoFactor')
            response = authenticator_session.submit(challenge_form, {'Code':totp(shared_key)})
            check('Correct TOTP finishes real Identity sign-in', response.status == 302 and response.location == '/Context')
            select_scope(authenticator_session,branch,'TOTP-authenticated session selects main')
            assert_scope(authenticator_session,branch,'TOTP-authenticated context survives principal refresh')

            recovery_session = password_challenge('One-time recovery login')
            recovery_page = page(recovery_session,'/Account/RecoveryLogin','Recovery-code challenge form')
            response = recovery_session.submit(Forms(recovery_page.text).get('/Account/RecoveryLogin'), {'Code':recovery_codes[0]})
            check('Unused recovery code finishes real Identity sign-in', response.status == 302 and response.location == '/Context')
            select_scope(recovery_session,store_branch,'Recovery-authenticated session selects store')
            assert_scope(recovery_session,store_branch,'Recovery-authenticated store context survives principal refresh')
            assert_scope(authenticator_session,branch,'Recovery context selection cannot switch independent TOTP main session')
            check('One successful recovery consumes exactly one code',
                  bool(re.search(r'Unused recovery codes remaining\s*:\s*9\b',
                                 Forms(recovery_session.request('/Account/Security').text).text_content())))
            replay_session = password_challenge('Recovery replay login')
            replay_page = page(replay_session,'/Account/RecoveryLogin','Recovery replay challenge form')
            response = replay_session.submit(Forms(replay_page.text).get('/Account/RecoveryLogin'), {'Code':recovery_codes[0]})
            check('Used recovery code cannot be replayed', response.status == 200 and 'Recovery-code verification failed' in response.text)
            check('Recovery replay cannot read protected records', replay_session.request('/api/farm/animals').status == 401)
            check('Failed recovery replay leaves remaining code count unchanged',
                  bool(re.search(r'Unused recovery codes remaining\s*:\s*9\b',
                                 Forms(recovery_session.request('/Account/Security').text).text_content())))
            # Refreshing a successful security change must retain this browser's store
            # selection while immediately revoking earlier sessions with the old stamp.
            rotate_page = page(recovery_session,'/Account/Security','Recovery regeneration form')
            rotated = recovery_session.submit(Forms(rotate_page.text).get('/Account/RegenerateRecoveryCodes'),
                                              {'CurrentPassword':password,'Code':totp(shared_key)})
            replacement_codes = [value.strip() for value in CodeBlocks(rotated.text).values]
            check('Recovery regeneration produces ten replacement codes', rotated.status == 200 and len(replacement_codes) == 10
                  and not set(replacement_codes).intersection(recovery_codes))
            ephemeral_secrets += replacement_codes
            assert_scope(recovery_session,store_branch,'Store context survives recovery-regeneration security-stamp refresh')
            check('Recovery regeneration revokes older TOTP session', authenticator_session.request('/api/farm/animals').status == 401)
            check('Security flows do not persist global tenant-selection claims', scalar("SELECT COUNT(*) FROM AspNetUserClaims WHERE ClaimType IN ('ActiveCompanyId','ActiveBranchId')") == 0)
            check('Visited HTML pages have no inline script elements', not inline_script_pages,
                  'No JavaScript execution or interaction is claimed')
            check('Navigation, context selector and support pager scripts are linked externally',
                  all(any(urllib.parse.urlsplit(src).path == required for src in seen_script_sources)
                      for required in ('/js/navigation-menu.js','/js/context-selection.js','/js/support-tickets.js')))
            for script_url in sorted(seen_script_sources):
                check('Script URL is self-hosted ' + urllib.parse.urlsplit(script_url).path,
                      script_url.startswith('/') and not script_url.startswith('//'))
                script_response = anonymous.request(script_url)
                check('HTTP script resource loads ' + urllib.parse.urlsplit(script_url).path,
                      script_response.status == 200 and 'javascript' in script_response.headers.get('Content-Type','')
                      and len(script_response.body) > 20)
            check('Synthetic credentials, enrollment secrets and recovery codes are absent from evidence',
                  not any(secret.encode() in artifact.read_bytes() for secret in ephemeral_secrets
                          for artifact in OUT.iterdir() if artifact.is_file()))
            del ephemeral_secrets, shared_key, shared_keys, recovery_codes, replacement_codes, complete_form, setup, complete, rotated

            with sqlite3.connect(dbpath) as db:
                lines = db.execute('SELECT VoucherId, Debit, Credit FROM VoucherLines').fetchall()
                totals = {}
                for voucher_id, debit, credit in lines:
                    totals[voucher_id] = totals.get(voucher_id, Decimal(0)) + Decimal(str(debit)) - Decimal(str(credit))
                check('Every persisted journal remains exactly balanced after HTTP postings', bool(totals) and all(value == 0 for value in totals.values()),
                      str(len(totals)) + ' balanced vouchers')
                stocks = db.execute('SELECT Id, QuantityOnHand FROM StockItems').fetchall()
                movements = db.execute('SELECT StockItemId, QuantityDelta FROM StockMovements').fetchall()
                check('All stock balances equal persisted movements', all(
                    Decimal(str(qty)) == sum((Decimal(str(delta)) for item, delta in movements if item == stock_id), Decimal(0))
                    for stock_id, qty in stocks), str(len(stocks)) + ' reconciled stock items')
            check('Release DLL remained unchanged during HTTP acceptance', build_hash == digest(DLL))
            check('Trusted runtime dependency remained unchanged during HTTP acceptance', trusted_hash == digest(trusted_dll))
            report['completed_utc'] = datetime.now(timezone.utc).isoformat()
            report['status'] = 'passed'
            write_report()
            print(json.dumps({'passed':report['passed'], 'failed':report['failed'], 'report':str(OUT / 'results.json')}), flush=True)
            if hold_server:
                live = {'url':base, 'anonymous_login_url':base+'/Account/Login', 'harness_pid':os.getpid(),
                        'server_pid':process.pid, 'fixture':'temporary synthetic data only', 'credentials_shared':False}
                (OUT / 'live-server.json').write_text(json.dumps(live, indent=2) + '\n')
                print('HOLD_SERVER ' + base + '/Account/Login', flush=True)
                try:
                    while process.poll() is None and not (OUT / 'stop-server').exists():
                        time.sleep(2)
                except KeyboardInterrupt:
                    pass
            # Terminate before TemporaryDirectory removes the SQLite DB and key ring.
            process.terminate()
            try:
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
            process = None
    except (Exception, KeyboardInterrupt) as exc:
        report['status'] = 'failed'
        report['error'] = str(exc) or type(exc).__name__
        if not checks or checks[-1]['passed']:
            checks.append({'check':'Harness execution', 'passed':False, 'detail':report['error']})
        raise
    finally:
        if process is not None and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
        if log is not None:
            log.close()
        (OUT / 'live-server.json').unlink(missing_ok=True)
        (OUT / 'stop-server').unlink(missing_ok=True)
        write_report()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--hold-server', action='store_true', help='Keep the disposable localhost host alive after acceptance until Ctrl+C/SIGTERM')
    args = parser.parse_args()
    signal.signal(signal.SIGTERM, lambda signum, frame: (_ for _ in ()).throw(KeyboardInterrupt()))
    run(hold_server=args.hold_server)
