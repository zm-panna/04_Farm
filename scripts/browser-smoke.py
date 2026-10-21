#!/usr/bin/env python3
"""Disposable local Chromium acceptance. Requires built Release, Python Playwright and Chromium.
Never accepts a production URL or reuses a customer database. Generated secret is process-only.
"""
import hashlib, json, os, pathlib, secrets, shutil, socket, sqlite3, subprocess, tempfile, time, urllib.request
from datetime import datetime, timezone
ROOT = pathlib.Path(__file__).resolve().parents[1]
DLL = ROOT / 'EnterpriseERP.Web/bin/Release/net10.0/EnterpriseERP.Web.dll'
OUT = ROOT / 'evidence/browser'
OUT.mkdir(parents=True, exist_ok=True)
checks = []
browser_executed = False
phase = 'fixture'
status = 'running'
failure = None
started_utc = datetime.now(timezone.utc).isoformat()
release_sha256 = hashlib.sha256(DLL.read_bytes()).hexdigest() if DLL.is_file() else None

def safe_failure(exc):
    # Never copy Playwright's verbose launch log, machine paths or cookie/profile data.
    if not browser_executed:
        if 'socket() failed: Operation not permitted' in str(exc):
            return 'Chromium could not create a required local process socket (Operation not permitted).'
        if isinstance(exc, (ImportError, ModuleNotFoundError)):
            return 'Python Playwright is unavailable; no browser checks executed.'
        return 'Browser prerequisites or startup failed (' + type(exc).__name__ + '); no browser checks executed.'
    if checks and not checks[-1]['passed']:
        return 'Browser assertion failed: ' + checks[-1]['check']
    return 'Browser acceptance stopped unexpectedly (' + type(exc).__name__ + ').'
def record(name, ok, detail=''):
    checks.append({'check': name, 'phase': phase, 'passed': bool(ok), 'detail': str(detail)})
    if not ok: raise AssertionError(name + ': ' + str(detail))
def check_response(page, path, expected=200):
    response = page.goto(BASE + path, wait_until='networkidle')
    record('GET ' + path, response.status == expected, response.status)
    return response
try:
    with tempfile.TemporaryDirectory(prefix='farm-browser-demo-') as tmp:
        dbpath = pathlib.Path(tmp) / 'farm-demo.db'
        password = 'T9!' + secrets.token_urlsafe(25)
        env = dict(os.environ, ASPNETCORE_ENVIRONMENT='Development', DOTNET_ENVIRONMENT='Development', Database__Provider='Sqlite',
                   ConnectionStrings__DefaultConnection='Data Source=' + str(dbpath), FARM_ALLOW_DEMO_SEED='YES', FARM_DEMO_PASSWORD=password,
                   Automation__WorkerEnabled='false', Ollama__Enabled='false', HOME=str(tmp),
                   XDG_DATA_HOME=str(pathlib.Path(tmp)/'.local/share'), DOTNET_CLI_HOME=str(tmp),
                   Logging__LogLevel__Default='Warning')
        dotnet = shutil.which('dotnet')
        if not dotnet: raise RuntimeError('dotnet must be on PATH')
        for run in range(2):
            seed = subprocess.run([dotnet, str(DLL), '--init-demo'], cwd=ROOT/'EnterpriseERP.Web', env=env, capture_output=True, text=True)
            (OUT/f'seed-{run+1}.log').write_text(seed.stdout + seed.stderr)
            record('Atomic demo seed' if run==0 else 'Idempotent demo rerun', seed.returncode==0, 'CLI exit '+str(seed.returncode))
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', 0)); port=sock.getsockname()[1]
        BASE = f'http://127.0.0.1:{port}'
        env.pop('FARM_DEMO_PASSWORD'); env.pop('FARM_ALLOW_DEMO_SEED')
        log = (OUT/'server.log').open('w')
        process = subprocess.Popen([dotnet, str(DLL), '--urls', BASE], cwd=ROOT/'EnterpriseERP.Web', env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            for _ in range(100):
                try:
                    if urllib.request.urlopen(BASE+'/Account/Login', timeout=1).status==200: break
                except Exception:
                    if process.poll() is not None: raise RuntimeError('Web host stopped; see evidence/browser/server.log')
                    time.sleep(.2)
            from playwright.sync_api import sync_playwright
            with sync_playwright() as p:
                browser=p.chromium.launch(executable_path=shutil.which('chromium') or None, headless=True, chromium_sandbox=True)
                browser_executed = True
                phase = 'browser'
                context=browser.new_context(viewport={'width':1440,'height':1000})
                page=context.new_page()
                errors=[]; page.on('pageerror', lambda e:errors.append(str(e)))
                record('Anonymous API rejected', context.request.get(BASE+'/api/operations/summary').status==401)
                check_response(page,'/Account/Login')
                page.locator('input[name=email]').fill('farmdemo1@example.invalid')
                page.locator('input[name=password]').fill(password)
                page.locator('form[action*="Login"] button[type=submit]').click()
                page.wait_for_url('**/Context')
                record('Login succeeds', page.locator('#companyId option').count()==1)
                page.locator('form[action*="Select"] button').click(); page.wait_for_url(BASE+'/')
                record('Tenant dashboard', page.locator('h1').count()>0 and 'Restricted' not in page.locator('.kpi-grid').inner_text())
                page.screenshot(path=str(OUT/'dashboard-desktop.png'),full_page=True)
                paths=['/animals','/products','/milk-entry','/animal-health','/FeedManagement','/Fattening','/breeding','/Expenses','/Purchase','/Sales','/Inventory','/Parties','/Accounts','/Accounts/periods','/Reports','/Subscription','/support/tickets','/Automation','/Administration','/Account/ChangePassword']
                for path in paths: check_response(page,path)
                record('Unimplemented HR shell disabled', context.request.get(BASE+'/HR/Home').status==404)
                record('Demo admin cannot inspect platform readiness', context.request.get(BASE+'/ops/readiness').status==403)
                record('CSRF rejects finance POST', context.request.post(BASE+'/api/advanced/payment',data={'reference':'BAD','partyType':'Customer','amount':1,'documentId':1}).status==400)
                check_response(page,'/Context')
                token=page.locator('form[action*="Select"] input[name=__RequestVerificationToken]').input_value()
                record('Cross-tenant context denied', context.request.post(BASE+'/Context/Select',form={'companyId':'2','branchId':'2','__RequestVerificationToken':token}).status==403)
                check_response(page,'/Purchase')
                form=page.locator('form[action="/Purchase/post"]')
                first=form.locator('select[name=ItemId] option').nth(1).get_attribute('value')
                form.locator('select[name=ItemId]').select_option(first)
                form.locator('input[name=Quantity]').fill('3')
                form.locator('input[name=Price]').fill('7.125')
                form.locator('input[name=Reference]').fill('BROWSER-PUR-1')
                form.locator('button[type=submit]').click(); page.wait_for_load_state('networkidle')
                record('Purchase browser posting', 'PUR-BROWSER-PUR-1' in page.locator('main').inner_text())
                form=page.locator('form[action="/Purchase/post"]')
                token=form.locator('input[name=__RequestVerificationToken]').input_value()
                duplicate=context.request.post(BASE+'/Purchase/post',form={'ItemId':first,'Quantity':'3','Price':'7.125','Reference':'BROWSER-PUR-1','__RequestVerificationToken':token})
                with sqlite3.connect(dbpath) as db:
                    n=db.execute("select count(*) from PurchaseReceipts where Reference='BROWSER-PUR-1'").fetchone()[0]
                    record('Duplicate post does not duplicate receipt',n==1)
                check_response(page,'/Sales')
                form=page.locator('form[action="/Sales/post"]')
                form.locator('select[name=ItemId]').select_option(first)
                form.locator('input[name=Quantity]').fill('1')
                form.locator('input[name=Price]').fill('9')
                form.locator('input[name=Reference]').fill('BROWSER-SALE-1')
                form.locator('button[type=submit]').click(); page.wait_for_load_state('networkidle')
                record('Sale browser posting', 'SAL-BROWSER-SALE-1' in page.locator('main').inner_text())
                check_response(page,'/Automation')
                page.locator('form[action="/Automation/ask"] textarea').fill('Summarize my farm')
                page.locator('form[action="/Automation/ask"] button').click(); page.wait_for_load_state('networkidle')
                record('Disabled AI is explicit and ERP remains reachable', 'disabled' in page.locator('main').inner_text().lower() or 'unavailable' in page.locator('body').inner_text().lower())
                check_response(page,'/Reports')
                record('CSV export',context.request.get(BASE+'/api/operations/summary.csv').status==200)
                xlsx=context.request.get(BASE+'/api/advanced/trial-balance.xlsx?through=2099-01-01T00:00:00Z')
                record('Excel export',xlsx.status==200 and xlsx.body().startswith(b'PK'))
                pdf=context.request.get(BASE+'/api/advanced/farm-summary.pdf')
                record('PDF export',pdf.status==200 and pdf.body().startswith(b'%PDF'))
                (OUT/'farm-summary.pdf').write_bytes(pdf.body())
                page.locator('#erp-language').select_option('bn'); page.wait_for_timeout(250)
                record('Bengali language state',page.locator('html').get_attribute('lang')=='bn')
                page.reload(); record('Language persists on reload',page.locator('#erp-language').input_value()=='bn')
                page.locator('#erp-language').select_option('banglish');page.wait_for_timeout(100)
                record('Banglish language state',page.locator('html').get_attribute('lang') in ['bn-Latn','en'])
                page.locator('#erp-language').select_option('en')
                page.set_viewport_size({'width':390,'height':844});check_response(page,'/')
                page.locator('#nav-toggle').click()
                record('Mobile navigation opens',page.locator('#nav-toggle').get_attribute('aria-expanded')=='true')
                page.locator('.side a[href="/animals"]').click();page.wait_for_load_state('networkidle')
                record('Mobile navigation closes after selection',page.locator('#nav-toggle').get_attribute('aria-expanded')=='false')
                page.screenshot(path=str(OUT/'animals-mobile.png'),full_page=True)
                record('No browser JavaScript exceptions',not errors,errors)
                context.close()
                # Valid low-privilege user must land in a usable workspace without receiving financial totals.
                context=browser.new_context();page=context.new_page();check_response(page,'/Account/Login')
                page.locator('input[name=email]').fill('farmdemo4@example.invalid');page.locator('input[name=password]').fill(password)
                page.locator('form[action*="Login"] button[type=submit]').click();page.wait_for_url('**/Context')
                page.locator('form[action*="Select"] button').click();page.wait_for_url(BASE+'/')
                record('Restricted staff dashboard remains usable', 'Restricted' in page.locator('.kpi-grid').inner_text())
                record('Restricted staff cannot read accounts',context.request.get(BASE+'/Accounts').status==403)
                browser.close()
            with sqlite3.connect(dbpath) as db:
                mismatches=db.execute('SELECT COUNT(*) FROM (SELECT VoucherId FROM VoucherLines GROUP BY VoucherId HAVING ABS(SUM(CAST(Debit AS REAL))-SUM(CAST(Credit AS REAL))) > 0.00001)').fetchone()[0]
                record('All persisted journals balanced after browser postings',mismatches==0)
        finally:
            process.terminate()
            try: process.wait(timeout=15)
            except subprocess.TimeoutExpired: process.kill()
            log.close()
    status = 'passed'
except Exception as exc:
    status = 'failed' if browser_executed else 'blocked'
    failure = {'type':type(exc).__name__, 'detail':safe_failure(exc)}
    if not checks or checks[-1]['passed']:
        checks.append({'check':'Browser execution' if browser_executed else 'Browser startup',
                       'phase':'browser', 'passed':False, 'detail':failure['detail']})
finally:
    ended_sha256 = hashlib.sha256(DLL.read_bytes()).hexdigest() if DLL.is_file() else None
    report = {'verification_kind':'Browser acceptance',
              'environment':'Development SQLite; sandboxed local Chromium requested',
              'status':status, 'browser_executed':browser_executed, 'production_verified':False,
              'started_utc':started_utc, 'completed_utc':datetime.now(timezone.utc).isoformat(),
              'release_dll_sha256':release_sha256, 'release_dll_sha256_after':ended_sha256,
              'release_dll_unchanged':release_sha256 is not None and release_sha256 == ended_sha256,
              'fixture_checks_passed':sum(x['passed'] and x['phase']=='fixture' for x in checks),
              'browser_checks_passed':sum(x['passed'] and x['phase']=='browser' for x in checks) if browser_executed else 0,
              'failed':sum(not x['passed'] for x in checks), 'failure':failure, 'checks':checks}
    (OUT/'results.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'status':status,'browser_executed':browser_executed,
                  'browser_checks_passed':report['browser_checks_passed'],
                  'fixture_checks_passed':report['fixture_checks_passed'],
                  'failed':report['failed'],'report':'evidence/browser/results.json'}))
if status != 'passed':
    raise SystemExit(1)
