#!/usr/bin/env python3
"""Shared disposable localhost fixture. Never accepts a remote URL, credentials or customer database."""
from __future__ import annotations
import importlib.util, json, os, secrets, shutil, socket, sqlite3, subprocess, tempfile, time, urllib.error
from pathlib import Path
from datetime import datetime, timezone
from contextlib import closing
_spec=importlib.util.spec_from_file_location('farm_core_http',Path(__file__).with_name('http-smoke.py'))
_core=importlib.util.module_from_spec(_spec); _spec.loader.exec_module(_core)
Forms,Session=_core.Forms,_core.Session
ROOT=Path(__file__).resolve().parents[1]
DLL=ROOT/'EnterpriseERP.Web/bin/Release/net10.0/EnterpriseERP.Web.dll'
TRUSTED_DLL=DLL.with_name('TrustedInfrastructure.dll')

class Fixture:
    def __init__(self,name):
        if not name.replace('-','').isalnum(): raise ValueError('Safe evidence name required')
        # Preserve historical release evidence and separate concurrent runs.
        runs=ROOT/'artifacts'/'http-verification';runs.mkdir(parents=True,exist_ok=True)
        self.out=Path(tempfile.mkdtemp(prefix=name+'-',dir=runs))
        print('HTTP evidence: '+str(self.out),flush=True)
        self.checks=[];self.process=None;self.log=None;self.tmp=None;self.password=None
        self.report={'kind':'Real localhost HTTP, isolated synthetic enterprise fixture','browser_executed':False,'sql_server_executed':False,'production_approved':False,'started_utc':datetime.now(timezone.utc).isoformat(),'checks':self.checks,'dll_sha256':_core.digest(DLL),'trusted_runtime_dll_sha256':_core.digest(TRUSTED_DLL)}
    def write(self):
        self.report.update(passed=sum(x['passed'] for x in self.checks),failed=sum(not x['passed'] for x in self.checks))
        (self.out/'results.json').write_text(json.dumps(self.report,indent=2)+'\n')
    def check(self,name,ok,detail=''):
        self.checks.append({'check':name,'passed':bool(ok),'detail':str(detail)});self.write()
        print(('PASS ' if ok else 'FAIL ')+name+(': '+str(detail) if detail else ''),flush=True)
        if not ok: raise AssertionError(name+': '+str(detail))
    def __enter__(self):
        try:
            self.tmp=tempfile.TemporaryDirectory(prefix='farm-enterprise-http-demo-');self.path=Path(self.tmp.name);self.dbpath=self.path/'enterprise-demo.db'
            self.password='T9!'+secrets.token_urlsafe(32);dotnet=shutil.which('dotnet')
            self.check('Built runtime available',bool(dotnet and DLL.is_file()))
            env=dict(os.environ,ASPNETCORE_ENVIRONMENT='Development',DOTNET_ENVIRONMENT='Development',Database__Provider='Sqlite',ConnectionStrings__DefaultConnection='Data Source='+str(self.dbpath),FARM_ALLOW_DEMO_SEED='YES',FARM_DEMO_PASSWORD=self.password,Automation__WorkerEnabled='false',Ollama__Enabled='false',HOME=str(self.path),XDG_DATA_HOME=str(self.path/'.local/share'),DOTNET_CLI_HOME=str(self.path),Logging__LogLevel__Default='Warning')
            seed=subprocess.run([dotnet,str(DLL),'--init-enterprise-demo'],cwd=ROOT/'EnterpriseERP.Web',env=env,capture_output=True,text=True,timeout=180)
            (self.out/'seed.log').write_text((seed.stdout+seed.stderr).replace(self.password,'[REDACTED]'))
            self.check('Guarded enterprise fixture seeded',seed.returncode==0,'CLI exit '+str(seed.returncode))
            with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
            self.base=f'http://127.0.0.1:{port}';env.pop('FARM_DEMO_PASSWORD');env.pop('FARM_ALLOW_DEMO_SEED')
            self.log=(self.out/'server.log').open('w');self.process=subprocess.Popen([dotnet,str(DLL),'--urls',self.base],cwd=ROOT/'EnterpriseERP.Web',env=env,stdout=self.log,stderr=subprocess.STDOUT)
            self.anonymous=Session(self.base)
            for _ in range(150):
                try:
                    if self.anonymous.request('/Account/Login').status==200:break
                except (urllib.error.URLError,TimeoutError):pass
                if self.process.poll() is not None:raise RuntimeError('Local test host exited')
                time.sleep(.2)
            else:raise RuntimeError('Local test host did not become ready')
            self.check('Temporary localhost host ready',self.process.poll() is None)
            return self
        except BaseException as e:self.__exit__(type(e),e,e.__traceback__);raise
    def query(self,sql,args=()):
        # sqlite3's transaction context does not close the connection. Windows
        # otherwise retains a file handle and cannot clean the disposable DB.
        with closing(sqlite3.connect(self.dbpath)) as db, db:return db.execute(sql,args).fetchall()
    def scalar(self,sql,args=()):return self.query(sql,args)[0][0]
    def login(self,number=1,reviewer=False,branch=None):
        email=f'farm{"reviewer" if reviewer else "demo"}{number}@example.invalid';session=Session(self.base)
        form=Forms(self.page(session,'/Account/Login').text).get('/Account/Login')
        response=session.submit(form,{'email':email,'password':self.password});self.check('Identity login '+email,response.status==302 and response.location=='/Context','HTTP '+str(response.status))
        context=Forms(self.page(session,'/Context').text).get('/Context/Select')
        company=self.scalar('SELECT Id FROM Companies WHERE Slug=?',(f'demo-farm-{number}',));branch=branch or self.scalar('SELECT Id FROM Branches WHERE CompanyId=? AND Name=?',(company,f'Demo Branch {number}'))
        response=session.submit(context,{'companyId':company,'branchId':branch});self.check('Select verified context '+email,response.status==302 and response.location=='/')
        return session
    def page(self,session,path):
        response=session.request(path);self.check('GET '+path,response.status==200 and '<html' in response.text.lower(),'HTTP '+str(response.status));return response
    def form(self,session,path,action):return Forms(self.page(session,path).text).get(action)
    def __exit__(self,kind,error,tb):
        if self.process is not None and self.process.poll() is None:
            self.process.terminate()
            try:self.process.wait(timeout=15)
            except subprocess.TimeoutExpired:self.process.kill();self.process.wait(timeout=5)
        if self.log:self.log.close()
        if self.password:
            for file in self.out.iterdir():
                if file.is_file():
                    content=file.read_bytes();self.checks.append({'check':'No initialization password in '+file.name,'passed':self.password.encode() not in content,'detail':''})
        self.checks.append({'check':'Runtime binary unchanged during test','passed':self.report['dll_sha256']==_core.digest(DLL),'detail':''})
        self.checks.append({'check':'Trusted runtime dependency unchanged during test','passed':self.report['trusted_runtime_dll_sha256']==_core.digest(TRUSTED_DLL),'detail':''})
        self.password=None
        self.report['cleanup']='not_created'
        if self.tmp:
            try:
                self.tmp.cleanup()
                self.report['cleanup']='passed'
            except Exception as cleanup_error:
                self.report['cleanup']='failed'
                self.checks.append({'check':'Disposable fixture cleanup','passed':False,'detail':type(cleanup_error).__name__})
        self.report['completed_utc']=datetime.now(timezone.utc).isoformat();self.report['status']='passed' if error is None and all(x['passed'] for x in self.checks) else 'failed'
        if error:self.report['error']=str(error)
        self.write()
        # A failing teardown security/integrity check must fail the command too.
        # Preserve an existing exception rather than masking its original cause.
        if error is None and self.report['status']=='failed':
            raise AssertionError('HTTP fixture verification failed; see '+str(self.out/'results.json'))
