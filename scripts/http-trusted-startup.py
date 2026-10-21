#!/usr/bin/env python3
"""Fail-closed process startup probes; no SQL connection, server deployment or credential provisioning."""
import hashlib,json,os,subprocess,tempfile
from datetime import datetime,timezone
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];DLL=ROOT/'EnterpriseERP.Web/bin/Release/net10.0/EnterpriseERP.Web.dll'
checks=[]
cases=[
 ('Production defaults reject missing vault','Production',{},'TrustedVault secure production mode requires OpenBao'),
 ('Staging refuses disabled vault even explicit requirement opt-out','Staging',{'TrustedVault__Mode':'Disabled','TrustedVault__RequireInProduction':'false'},'Disabled vault mode is Development-only'),
 ('Production refuses synthetic vault','Production',{'TrustedVault__Mode':'Synthetic'},'Synthetic vault fixtures are Development-only'),
 ('Legacy mode cannot silently opt out','Production',{'TrustedVault__Mode':'LegacyLocal'},'TrustedVault secure production mode requires OpenBao'),
 ('Automatic trust-all forwarding is rejected','Development',{'TrustedVault__Mode':'Disabled','ASPNETCORE_FORWARDEDHEADERS_ENABLED':'true'},'Automatic forwarded-header trust is prohibited'),
 ('Wildcard gateway trust rejected','Development',{'TrustedVault__Mode':'Disabled','TrustedProxy__Enabled':'true','TrustedProxy__KnownProxyIp':'0.0.0.0','TrustedProxy__PublicOrigin':'https://farm.example.invalid','AllowedHosts':'farm.example.invalid'},'TrustedProxy requires one explicit gateway IP address')]
with tempfile.TemporaryDirectory(prefix='farm-startup-negative-') as folder:
 for label,environment,extra,expected in cases:
  env=dict(os.environ,ASPNETCORE_ENVIRONMENT=environment,DOTNET_ENVIRONMENT=environment,TrustedVault__Mode='Disabled',TrustedVault__RequireInProduction='true',TrustedProxy__Enabled='false',ConnectionStrings__DefaultConnection='',Database__Provider='SqlServer',HOME=folder,DOTNET_CLI_HOME=folder,Logging__LogLevel__Default='Warning');env.update(extra)
  result=subprocess.run(['dotnet',str(DLL),'--urls','http://127.0.0.1:0'],cwd=ROOT/'EnterpriseERP.Web',env=env,capture_output=True,text=True,timeout=30)
  ok=result.returncode!=0 and expected in result.stdout+result.stderr
  checks.append({'check':label,'passed':ok,'exit_nonzero':result.returncode!=0});print(('PASS ' if ok else 'FAIL ')+label,flush=True)
report={'kind':'Actual negative startup process checks; no database access','dll_sha256':hashlib.sha256(DLL.read_bytes()).hexdigest(),'trusted_runtime_dll_sha256':hashlib.sha256(DLL.with_name('TrustedInfrastructure.dll').read_bytes()).hexdigest(),'completed_utc':datetime.now(timezone.utc).isoformat(),'checks':checks,'passed':sum(x['passed'] for x in checks),'failed':sum(not x['passed'] for x in checks),'production_approved':False}
p=ROOT/'evidence/trusted/final/startup-results.json';p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(report,indent=2)+'\n')
raise SystemExit(0 if report['failed']==0 else 1)
