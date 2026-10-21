#!/usr/bin/env python3
"""Real localhost DataTables protocol and Chromium UI checks. Uses only disposable synthetic data."""
import json,os,sqlite3,subprocess
from pathlib import Path
from urllib.parse import urlencode
from enterprise_http_fixture import Fixture,Forms,ROOT

def run():
 with Fixture('data-tables') as f:
  admin=f.login();reviewer=f.login(reviewer=True);other=f.login(2)
  keys=json.loads((ROOT/'evidence/realtime/GRID_INVENTORY.json').read_text())
  keys=[x['key'] for x in keys]
  def grid(session,key,**args):
   query={'draw':42,'start':0,'length':25,'order[0][column]':0,'order[0][dir]':'desc'};query.update(args)
   return session.request('/api/tables/'+key+'?'+urlencode(query))
  f.check('Anonymous table API returns401',grid(f.anonymous,'products').status==401)
  for key in keys:
   response=grid(admin,key);f.check('Bounded authorized real SQL table '+key,response.status==200,response.text[:300] if response.status!=200 else '')
   page=json.loads(response.text);f.check('Draw/count contract '+key,page['draw']==42 and 0<=len(page['data'])<=25 and page['recordsFiltered']<=page['recordsTotal'])
  company=f.scalar("SELECT Id FROM Companies WHERE Slug='demo-farm-1'");branch=f.scalar('SELECT Id FROM Branches WHERE CompanyId=? AND Name=?',(company,'Demo Branch 1'))
  # Direct inserts are synthetic fixture generation, not a bypass in any application endpoint.
  with sqlite3.connect(f.dbpath) as db:
   for i in range(140):db.execute('INSERT INTO StockItems (CompanyId,BranchId,SKU,Name,UnitOfMeasure,QuantityOnHand,UnitCost,ReservedQuantity,RowVersion) VALUES (?,?,?,?,?,0,0,0,randomblob(16))',(company,branch,'GRID-'+str(i).zfill(3),'Page match '+str(i).zfill(3),'KG'))
  response=grid(admin,'products',**{'search[value]':'Page match','start':100,'length':25,'order[0][dir]':'asc'})
  page=json.loads(response.text);f.check('Server pages beyond100 with scoped filtered count',response.status==200 and page['recordsFiltered']==140 and len(page['data'])==25 and page['data'][0]['reference']=='GRID-100')
  foreign=json.loads(grid(other,'products',**{'search[value]':'Page match'}).text);f.check('Other company cannot observe count or rows',foreign['recordsFiltered']==0 and foreign['data']==[])
  for args in [{'draw':'<script>'},{'length':-1},{'length':101},{'start':-1},{'order[0][column]':99},{'search[regex]':'true'},{'search[value]':'x'*121}]:f.check('Reject unsupported protocol '+str(args),grid(admin,'products',**args).status==400)
  f.check('Unknown table is404',grid(admin,'unknown').status==404)
  own=json.loads(grid(reviewer,'own-orders').text);f.check('Wildcard reviewer cannot see another customer orders in own portal',own['recordsTotal']==0)
  own=json.loads(grid(reviewer,'own-payslips').text);f.check('Wildcard reviewer cannot see another employee payslips in own portal',own['recordsTotal']==0)
  row=f.query('SELECT Id,SKU,Name FROM StockItems WHERE CompanyId=? AND BranchId=? ORDER BY Id LIMIT1'.replace('LIMIT1','LIMIT 1'),(company,branch))[0]
  # Fill cached safe grid, then mutate via an authenticated anti-forgery protected real edit form.
  before=json.loads(grid(admin,'products',**{'search[value]':row[1]}).text)
  form=Forms(f.page(admin,f'/products/{row[0]}/edit').text).get('/products/save')
  result=admin.submit(form,{'Name':'Cache changed synthetic item','UnitOfMeasure':'KG'});f.check('Real product mutation commits',result.status==302)
  after=json.loads(grid(admin,'products',**{'search[value]':row[1]}).text);f.check('Committed product mutation invalidates cache immediately',after['data'][0]['description']=='Cache changed synthetic item')
  staff=json.loads(grid(admin,'staff').text);f.check('Staff grid never exposes compensation fields',all(set(x)=={'id','reference','description','status','date','value','url'} and x['value']=='' for x in staff['data']))
  f.page(admin,'/Inventory?page=2');f.page(admin,'/Parties?page=2');f.page(admin,'/data');f.page(admin,'/data/products')
  cookies=[{'name':c.name,'value':c.value,'url':f.base,'httpOnly':c.name.startswith('.AspNetCore.'),'sameSite':'Lax'} for c in admin.cookies]
  payload={'base':f.base,'cookies':cookies,'out':str(f.out)}
  if os.environ.get('FARM_TEST_BROWSER')=='1':
   browser=subprocess.run(['node',str(ROOT/'scripts/browser-data-tables.js')],input=json.dumps(payload),capture_output=True,text=True,timeout=120)
   (f.out/'browser.log').write_text(browser.stdout+browser.stderr)
   f.check('Actual Chromium DataTables UI checks',browser.returncode==0,browser.stdout[-300:] if browser.returncode==0 else browser.stderr[-1000:])
   f.report['browser_executed']=True
  else:
   f.report['browser_executed']=False
   f.report['browser_gate']='Real Chromium requires a supported browser executor; this environment denied its local Unix socket. HTTP and DOM checks are separate evidence.'
  if os.environ.get('FARM_TEST_JSDOM'):
   dom=subprocess.run(['node',str(ROOT/'scripts/dom-data-tables.js')],input=json.dumps(payload),capture_output=True,text=True,timeout=60)
   (f.out/'vendor-dom.log').write_text(dom.stdout+dom.stderr)
   f.check('Actual vendored DataTables DOM and live HTTP integration',dom.returncode==0,dom.stdout[-300:] if dom.returncode==0 else dom.stderr[-1000:])
   f.report['real_vendor_dom_executed']=True
  f.report['redis_cache_configured']=os.environ.get('Scaling__Cache__Enabled','').lower()=='true'
if __name__=='__main__':run()
