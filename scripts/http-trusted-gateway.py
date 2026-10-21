#!/usr/bin/env python3
"""Actual Farm cookie/CSRF/REST/WebSocket through shared PublicGateway; disposable loopback only."""
import importlib.util,json,os,shutil,socket,subprocess,time,urllib.error,urllib.request,uuid
from pathlib import Path
from enterprise_http_fixture import Fixture,Session,ROOT,DLL,_core
spec=importlib.util.spec_from_file_location('messaging_smoke',ROOT/'scripts/http-messaging-smoke.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)

def port():
 with socket.socket() as s:s.bind(('127.0.0.1',0));return s.getsockname()[1]
def stop(p):
 if p and p.poll() is None:
  p.terminate()
  try:p.wait(timeout=10)
  except subprocess.TimeoutExpired:p.kill();p.wait(timeout=5)
class GatewayFixture(Fixture):
 def __init__(self):super().__init__('trusted-gateway');self.gateway=None;self.gateway_log=None
 def __enter__(self):
  try:
   super().__enter__();self.backend=self.base;stop(self.process);self.log.close()
   self.base='http://127.0.0.1:'+str(port())
   gateway=Path(os.environ.get('FARM_TEST_GATEWAY_DLL',ROOT.parent.parent/'trusted_platform/src/PublicGateway/bin/Debug/net10.0/PublicGateway.dll')).resolve()
   self.check('Shared gateway binary exists',gateway.is_file());self.report['gateway_dll_sha256']=_core.digest(gateway)
   env=dict(os.environ,ASPNETCORE_ENVIRONMENT='Development',DOTNET_ENVIRONMENT='Development',Database__Provider='Sqlite',ConnectionStrings__DefaultConnection='Data Source='+str(self.dbpath),Automation__WorkerEnabled='false',Ollama__Enabled='false',TrustedVault__Mode='Disabled',TrustedVault__RequireInProduction='true',TrustedProxy__Enabled='true',TrustedProxy__KnownProxyIp='127.0.0.1',TrustedProxy__PublicOrigin=self.base,AllowedHosts='127.0.0.1',HOME=str(self.path),XDG_DATA_HOME=str(self.path/'.local/share'),DOTNET_CLI_HOME=str(self.path),Logging__LogLevel__Default='Warning')
   self.log=(self.out/'server.log').open('a');self.process=subprocess.Popen([shutil.which('dotnet'),str(DLL),'--urls',self.backend],cwd=ROOT/'EnterpriseERP.Web',env=env,stdout=self.log,stderr=subprocess.STDOUT)
   env=dict(os.environ,ASPNETCORE_ENVIRONMENT='Development',DOTNET_ENVIRONMENT='Development',ASPNETCORE_URLS=self.base,Gateway__AllowDevelopmentLoopbackHttp='true',Gateway__Backends__0__Project='farm',Gateway__Backends__0__PublicOrigin=self.base,Gateway__Backends__0__BackendOrigin=self.backend,HOME=str(self.path),DOTNET_CLI_HOME=str(self.path),Logging__LogLevel__Default='Warning')
   self.gateway_log=(self.out/'gateway.log').open('w');self.gateway=subprocess.Popen([shutil.which('dotnet'),str(gateway)],cwd=gateway.parent,env=env,stdout=self.gateway_log,stderr=subprocess.STDOUT)
   self.anonymous=Session(self.base)
   for _ in range(200):
    try:
     if self.anonymous.request('/Account/Login').status==200:break
    except (urllib.error.URLError,TimeoutError):pass
    if self.process.poll() is not None or self.gateway.poll() is not None:raise RuntimeError('Disposable gateway or backend exited')
    time.sleep(.15)
   else:raise RuntimeError('Disposable gateway did not become ready')
   self.check('Actual gateway and strict proxy backend ready',True);return self
  except BaseException as e:self.__exit__(type(e),e,e.__traceback__);raise
 def __exit__(self,kind,error,tb):
  stop(self.gateway)
  if self.gateway_log:self.gateway_log.close()
  return super().__exit__(kind,error,tb)

def run():
 with GatewayFixture() as f:
  f.check('Direct backend without fixed proxy headers denied',Session(f.backend).request('/Account/Login').status==400)
  for path in ['/_platform','/_vault','/v1/sys/health','/v1/auth/token','/v1/secret/data/apps/farm/runtime']:
   f.check('Reserved management route blocked '+path,f.anonymous.request(path).status==404)
  f.check('Anonymous table API still401 through gateway',f.anonymous.request('/api/tables/products?draw=1&start=0&length=10').status==401)
  f.check('Anonymous hub negotiate still401 through gateway',m.request(f.anonymous,'/hubs/messaging/negotiate?negotiateVersion=1',method='POST').status==401)
  sender=f.login();recipient=f.login(reviewer=True);other=f.login(2)
  f.check('Backend issued HttpOnly authentication cookie',any(c.name=='.AspNetCore.Identity.Application' and any(k.lower()=='httponly' for k in c._rest) for c in sender.cookies))
  for path in ['/','/Messaging','/data/products','/HRPayroll','/CRM','/fixed-assets','/Ecommerce','/scan']:
   f.page(sender,path)
  for path in ['/js/farm-server-tables.js','/vendor/datatables/dataTables.min.js','/vendor/signalr/signalr.min.js','/css/farm-messaging.css','/js/scanner-camera.js','/js/farm-scanning.js','/lib/zxing-wasm-3.1.5/reader.js','/lib/zxing-wasm-3.1.5/zxing_reader.wasm']:
   f.check('Local asset reachable through gateway '+path,sender.request(path).status==200)
  page=f.page(sender,'/Messaging');token=m.Tokens(page.text).token
  recipient_id=f.scalar("SELECT Id FROM AspNetUsers WHERE Email='farmreviewer1@example.invalid'")
  payload={'recipientUserId':recipient_id,'subject':'SYNTHETIC gateway send','body':'Private gateway fixture','idempotencyKey':str(uuid.uuid4())}
  f.check('Gateway preserves CSRF rejection',m.request(sender,'/api/messaging/send',payload,method='POST').status==400)
  ws=m.socket_for(f,recipient)
  try:
   sent=m.request(sender,'/api/messaging/send',payload,token,'POST');f.check('Gateway preserves valid CSRF and durable send',sent.status==200)
   mid=json.loads(sent.text)['messageId'];hint,_=m.receive(ws,target='inboxChanged');f.check('Gateway WebSocket delivers content-free hint',hint=={'type':1,'target':'inboxChanged','arguments':[]})
   f.check('Cross-company message stays hidden through gateway',m.request(other,f'/api/messaging/messages/{mid}').status==404)
   f.check('Owner fetch works through gateway',m.request(recipient,f'/api/messaging/messages/{mid}').status==200)
  finally:ws.close()
  grid=sender.request('/api/tables/products?draw=7&start=0&length=2');data=json.loads(grid.text)
  f.check('Authenticated server-side protocol works through gateway',grid.status==200 and data['draw']==7 and len(data['data'])<=2)
  foreign=other.request('/api/tables/products?draw=8&start=0&length=10&search%5Bvalue%5D=DEMO-1')
  f.check('Other-tenant filtered grid count remains isolated',foreign.status==200 and json.loads(foreign.text)['recordsFiltered']==0)
  scan_token=m.Tokens(f.page(sender,'/scan').text).token
  label=f.query("SELECT Id,Value FROM ScanLabels WHERE CompanyId=(SELECT Id FROM Companies WHERE Slug='demo-farm-1') AND Kind='Product' ORDER BY Id LIMIT 1")[0]
  scan_payload={'kind':'Product','value':label[1]}
  f.check('Gateway scanner preview rejects missing CSRF',m.request(sender,'/api/scan/resolve',scan_payload,method='POST').status==400)
  preview=m.request(sender,'/api/scan/resolve',scan_payload,scan_token,'POST')
  f.check('Gateway scanner preview retains authorized context',preview.status==200 and json.loads(preview.text)['labelId']==label[0])
  other_token=m.Tokens(f.page(other,'/scan').text).token
  f.check('Gateway scanner preview cannot cross tenants',m.request(other,'/api/scan/resolve',scan_payload,other_token,'POST').status==404)
  f.check('Gateway opaque label print remains authenticated',sender.request(f'/scan/labels/{label[0]}/print').status==200)
  req=urllib.request.Request(f.base+'/api/messaging/inbox',headers={'Origin':'https://attacker.invalid'})
  try:response=f.anonymous.opener.open(req);status=response.status
  except urllib.error.HTTPError as e:status=e.code
  f.check('Untrusted browser Origin denied at gateway',status==403)
  req=urllib.request.Request(f.base+'/api/tables/products?draw=9&start=0&length=1',headers={'X-Forwarded-Host':'attacker.invalid','X-Forwarded-Proto':'https','X-User-Id':'someone-else','X-Tenant-Id':'2'})
  with sender.opener.open(req) as response:data=json.load(response);status=response.status
  f.check('Spoofed forwarding and identity headers cannot change authority',status==200 and data['draw']==9)
  f.report.update(actual_gateway=True,actual_signalr_websocket=True,vault_backend_mode='Disabled: isolated Development transport fixture only',production_tls_verified=False)
if __name__=='__main__':run()
