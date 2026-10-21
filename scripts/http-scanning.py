#!/usr/bin/env python3
"""Authenticated scanner journeys against an isolated synthetic localhost fixture. Never a remote/customer DB."""
from __future__ import annotations
import importlib.util,json,os,sqlite3,subprocess,uuid
from pathlib import Path
from datetime import date,timedelta
from urllib.parse import urlencode
from enterprise_http_fixture import Fixture,Forms,ROOT
spec=importlib.util.spec_from_file_location('farm_messaging_http',Path(__file__).with_name('http-messaging-smoke.py'));http=importlib.util.module_from_spec(spec);spec.loader.exec_module(http)

def run():
 with Fixture('scanning') as f:
  admin=f.login();reviewer=f.login(reviewer=True);other=f.login(2)
  company=f.scalar("SELECT Id FROM Companies WHERE Slug='demo-farm-1'");branch=f.scalar('SELECT Id FROM Branches WHERE CompanyId=? AND Name=?',(company,'Demo Branch 1'))
  store=f.scalar('SELECT Id FROM Branches WHERE CompanyId=? AND Id<>?',(company,branch));store_user=f.login(branch=store)
  ui=f.page(admin,'/scan');token=http.Tokens(ui.text).token;reviewer_token=http.Tokens(f.page(reviewer,'/scan').text).token;other_token=http.Tokens(f.page(other,'/scan').text).token;store_token=http.Tokens(f.page(store_user,'/scan').text).token
  f.check('Scanner route enables only same-origin camera and narrow WASM capability',ui.headers.get('Permissions-Policy')=='camera=(self), microphone=(), geolocation=()' and "'wasm-unsafe-eval'" in ui.headers.get('Content-Security-Policy','') and "'unsafe-eval'" not in ui.headers.get('Content-Security-Policy',''))
  ordinary=f.page(admin,'/Inventory');f.check('Ordinary routes retain disabled camera and no WASM evaluation',ordinary.headers.get('Permissions-Policy')=='camera=(), microphone=(), geolocation=()' and 'wasm-unsafe-eval' not in ordinary.headers.get('Content-Security-Policy',''))
  f.check('Scanner contains explicit review and no auto camera start',bool(token) and 'scan-confirm-reviewed' in ui.text and 'scanner-camera.js' in ui.text)
  f.check('Anonymous scanner API rejected',http.request(f.anonymous,'/api/scan/drafts').status==401)
  f.check('Resolve without anti-forgery is rejected',http.request(admin,'/api/scan/resolve',{'kind':'Product','value':'FEED'},method='POST').status==400)
  def call(path,payload=None,session=admin,csrf=token,method=None):
   return http.request(session,'/api/scan/'+path,payload,csrf,method or ('POST' if payload is not None else 'GET'))
  def good(name,path,payload=None,session=admin,csrf=token):
   r=call(path,payload,session,csrf);f.check(name,r.status==200,r.text[:350] if r.status!=200 else '');return json.loads(r.text)
  def label(kind,target,format='Internal',value='',session=admin,csrf=token):return good('Create exact '+kind+' label '+format,'labels',{'kind':kind,'targetId':target,'format':format,'value':value},session,csrf)
  def draft(action,primary,**fields):
   value={'requestKey':uuid.uuid4().hex,'action':action,'labelId':primary['id'],'notes':'Synthetic HTTP reviewed scanner entry'};value.update(fields)
   return good('Prepare '+action+' draft','drafts',value)
  def confirm(d):return good('Confirm '+d['review']['input']['action'],'drafts/'+str(d['id'])+'/confirm',{'confirmReviewed':True})
  product=f.query('SELECT Id,SKU,QuantityOnHand FROM StockItems WHERE CompanyId=? AND BranchId=? ORDER BY Id LIMIT 1',(company,branch))[0]
  animal=f.query('SELECT Id,Tag FROM Animals WHERE CompanyId=? AND BranchId=? AND Sold=0 ORDER BY Id LIMIT 1',(company,branch))[0]
  pl=label('Product',product[0],'SKU',product[1]);al=label('Animal',animal[0]);opaque=label('Product',product[0])
  before=(f.scalar('SELECT COUNT(*) FROM Vouchers'),f.scalar('SELECT COUNT(*) FROM StockMovements'),f.scalar('SELECT COUNT(*) FROM AnimalWeightRecords'))
  preview=good('Product scan resolves exact authorized target','resolve',{'kind':'Product','value':pl['value']});f.check('Product preview contains no unit cost or payroll data','unitCost' not in json.dumps(preview) and 'salary' not in json.dumps(preview))
  f.check('Context mismatch fails without exposing animal',call('resolve',{'kind':'Product','value':al['value']}).status==404)
  f.check('Foreign company label cannot resolve',call('resolve',{'kind':'Product','value':pl['value']},other,other_token).status==404)
  f.check('Arbitrary URL is rejected',call('resolve',{'kind':'Product','value':'https://example.invalid/'}).status==400)
  f.check('Cross-company target cannot receive a label',call('labels',{'kind':'Product','targetId':product[0],'format':'Internal'},other,other_token).status==404)
  checksum=label('Product',product[0],'UPCA','012345678905');equivalent=good('UPC-A scanner EAN representation resolves configured barcode','resolve',{'kind':'Product','value':'0012345678905'});f.check('Equivalent barcode resolves same label',equivalent['labelId']==checksum['id'])
  f.check('Equivalent duplicate barcode cannot be assigned',call('labels',{'kind':'Product','targetId':product[0],'format':'EAN13','value':'0012345678905'}).status==409)
  f.check('Invalid checksum is rejected',call('labels',{'kind':'Product','targetId':product[0],'format':'EAN13','value':'4006381333932'}).status==400)
  page=good('Scoped bounded label list','labels?'+urlencode({'kind':'Product','page':1}));f.check('Label page bounded at twenty',len(page['rows'])<=20 and page['total']>=4)
  f.check('Negative pagination rejected',call('options?kind=Product&page=-1').status==400)
  foreign_page=good('Other branch search cannot infer exact source target','options?'+urlencode({'kind':'Animal','q':animal[1]}),session=other,csrf=other_token);f.check('Scope applied before target count',foreign_page['total']==0 and foreign_page['rows']==[])
  print_page=f.page(admin,f"/scan/labels/{opaque['id']}/print");f.check('Printable label omits target name and cost',product[1] not in print_page.text and animal[1] not in print_page.text)
  images=[]
  for fmt in ['qr','code128']:
   r=call(f"labels/{opaque['id']}/image?format={fmt}");f.check('Authorized '+fmt+' image is self-contained SVG',r.status==200 and r.headers.get('Content-Type','').startswith('image/svg+xml') and '<script' not in r.text and 'href=' not in r.text);images.append({'format':fmt,'svg':r.text,'expected':opaque['value']})
  decoded=subprocess.run(['node',str(ROOT/'scripts/decode-scanning-svg.js')],input=json.dumps(images),text=True,capture_output=True,timeout=30)
  (f.out/'printed-label-decode.log').write_text(decoded.stdout+decoded.stderr);f.check('Actual WASM decodes both actual server-generated print labels',decoded.returncode==0,decoded.stdout.strip() if decoded.returncode==0 else decoded.stderr[-500:])
  f.check('Foreign printable label denied',http.request(other,f"/scan/labels/{opaque['id']}/print").status==409)
  f.check('Resolve and label management never post stock money or animal records',before==(f.scalar('SELECT COUNT(*) FROM Vouchers'),f.scalar('SELECT COUNT(*) FROM StockMovements'),f.scalar('SELECT COUNT(*) FROM AnimalWeightRecords')))
  adjusted=draft('AdjustStock',pl,quantity=-1);f.check('Draft itself retains original quantity',float(f.scalar('SELECT QuantityOnHand FROM StockItems WHERE Id=?',(product[0],)))==float(product[2]))
  f.check('False explicit confirmation rejected',call(f"drafts/{adjusted['id']}/confirm",{'confirmReviewed':False}).status==400)
  f.check('Confirmation missing CSRF rejected',http.request(admin,f"/api/scan/drafts/{adjusted['id']}/confirm",{'confirmReviewed':True},method='POST').status==400)
  f.check('Same-company other user cannot inspect draft',call(f"drafts/{adjusted['id']}",session=reviewer,csrf=reviewer_token).status==404)
  f.check('Same-company other user cannot confirm draft',call(f"drafts/{adjusted['id']}/confirm",{'confirmReviewed':True},reviewer,reviewer_token).status==404)
  f.check('Other branch cannot inspect draft',call(f"drafts/{adjusted['id']}",session=store_user,csrf=store_token).status==404)
  committed=confirm(adjusted);replayed=confirm(adjusted);f.check('Confirmation replay is the same persisted outcome',committed==replayed and f.scalar('SELECT COUNT(*) FROM StockMovements WHERE Reference=?',('ADJ-'+committed['businessReference'],))==1)
  cancelled=draft('AdjustStock',pl,quantity=1);f.check('Cancel a pending draft',call(f"drafts/{cancelled['id']}/cancel",method='POST').status==204);f.check('Cancelled draft cannot post',call(f"drafts/{cancelled['id']}/confirm",{'confirmReviewed':True}).status==409)
  stale=draft('AdjustStock',pl,quantity=1);fresh=draft('AdjustStock',pl,quantity=1);confirm(fresh);f.check('Changed source rejects stale draft',call(f"drafts/{stale['id']}/confirm",{'confirmReviewed':True}).status==409)
  revoked=draft('AdjustStock',opaque,quantity=1);f.check('Explicit label revocation succeeds',call(f"labels/{opaque['id']}/revoke",{'reason':'Synthetic replaced label'}).status==204);f.check('Revoked primary label cannot commit',call(f"drafts/{revoked['id']}/confirm",{'confirmReviewed':True}).status==409)
  today=str(date.today());weight=draft('Weight',al,quantity=345.678,date=str(date.today()-timedelta(days=1)));confirm(weight);f.check('Weight recorded by existing service exactly once',f.scalar('SELECT COUNT(*) FROM AnimalWeightRecords WHERE Reference=?',(weight['businessReference'],))==1)
  health=draft('Health',al,quantity=0,date=today,eventType='Observation',nextDueDate=today);confirm(health);f.check('Operator entered health observation persisted without clinical automation',f.scalar('SELECT EventType FROM AnimalHealthRecords WHERE Reference=?',(health['businessReference'],))=='Observation')
  feed=draft('Feed',al,quantity=.25,date=today,secondaryLabelId=pl['id']);confirm(feed);f.check('Feed stock expense uses existing service',f.scalar('SELECT COUNT(*) FROM AnimalFeedRecords WHERE Reference=?',(feed['businessReference'],))==1)
  destination=f.scalar('SELECT Id FROM StockItems WHERE CompanyId=? AND BranchId=? AND SKU=?',(company,store,product[1]));dl=label('Product',destination,session=store_user,csrf=store_token)
  good('Destination label preview requires explicit authorized branch','resolve',{'kind':'Product','value':dl['value'],'destinationBranchId':store})
  transfer=draft('Transfer',pl,quantity=.5,secondaryLabelId=dl['id'],destinationBranchId=store);confirm(transfer);f.check('Transfer persisted once with both branches',f.scalar('SELECT COUNT(*) FROM StockTransfers WHERE Reference=?',(transfer['businessReference'],))==1)
  supplier=f.scalar("SELECT Id FROM TradingParties WHERE CompanyId=? AND BranchId=? AND Kind='Supplier' LIMIT 1",(company,branch))
  form=f.form(admin,'/Purchase','/Purchase/order');r=admin.submit(form,{'supplierId':supplier,'ItemId':product[0],'Quantity':3,'Price':2,'Reference':'HTTP-SCAN-PO'});f.check('Existing purchase workflow creates order',r.status==302)
  po=f.scalar("SELECT Id FROM PurchaseOrders WHERE Reference='HTTP-SCAN-PO'");r=admin.request('/Purchase/order/approve',{'__RequestVerificationToken':token,'orderId':po});f.check('Existing purchase workflow approves order',r.status==302)
  pol=label('PurchaseOrder',po);receiving=draft('ReceiveOrder',pol,quantity=2);confirm(receiving);f.check('Receiving creates exact existing purchase receipt',f.scalar('SELECT COUNT(*) FROM PurchaseReceipts WHERE Reference=?',(receiving['businessReference'],))==1)
  docid=f.scalar('SELECT Id FROM FinanceDocuments WHERE Reference=?',('PUR-'+receiving['businessReference'],));document=label('Document',docid);look=good('Accounting document scan is read-only','resolve',{'kind':'Document','value':document['value']});f.check('Document preview offers no posting action',look['actions']==[])
  fields={'AssetCode':'SCAN-HTTP-ASSET','Name':'Synthetic scanner pump','Category':'Equipment','SerialNumber':'SYNTHETIC','Notes':'Synthetic local test only','SourceReference':'SCAN-HTTP-INV','AcquisitionDate':'2025-01-01','Cost':'100','AcquisitionSettlement':'Cash','DepreciationMethod':'None','UsefulLifeMonths':0,'ResidualValue':'100','DepreciationStart':'2025-01-01','PolicyReason':'Synthetic test estimate only','Custodian':'Synthetic A','Location':'Synthetic shed A'}
  r=admin.submit(f.form(admin,'/fixed-assets/new','/fixed-assets/new'),fields);f.check('Existing acquisition form creates new asset for custody test',r.status==302);assetpath=r.location;asset=int(assetpath.rsplit('/',1)[-1])
  r=reviewer.submit(f.form(reviewer,assetpath,assetpath+'/approve'),{'ConfirmReviewed':'true','ReviewNote':'Synthetic independent review'});f.check('Independent acquisition review succeeds',r.status==302)
  r=admin.submit(f.form(admin,assetpath,assetpath+'/post'));f.check('Existing acquisition service posts asset',r.status==302)
  assetlabel=label('Asset',asset);custody=draft('Custody',assetlabel,quantity=0,date=today,custodian='Synthetic B',location='Synthetic shed B');count=f.scalar('SELECT COUNT(*) FROM Vouchers');confirm(custody);f.check('Custody scan changes custody without new journal',f.scalar('SELECT Custodian FROM FixedAssets WHERE Id=?',(asset,))=='Synthetic B' and f.scalar('SELECT COUNT(*) FROM Vouchers')==count)
  f.check('All resulting journals balance',f.scalar('SELECT COUNT(*) FROM (SELECT VoucherId FROM VoucherLines GROUP BY VoucherId HAVING ABS(SUM(Debit)-SUM(Credit))>0.0001)')==0)
  history=good('Own persisted outcomes available after reconnect','drafts?page=1');f.check('Seven successful operation families are durable',set(x['action'] for x in history['rows'] if x['status']=='Committed')=={'AdjustStock','Weight','Health','Feed','Transfer','ReceiveOrder','Custody'})
  if os.environ.get('FARM_TEST_JSDOM'):
   cookies=[{'name':c.name,'value':c.value} for c in admin.cookies];dom=subprocess.run(['node',str(ROOT/'scripts/dom-scanning.js')],input=json.dumps({'base':f.base,'cookies':cookies,'label':pl['value'],'out':str(f.out)}),capture_output=True,text=True,timeout=60)
   (f.out/'scanner-dom.log').write_text(dom.stdout+dom.stderr);f.check('Actual scanner DOM with authenticated live HTTP',dom.returncode==0,dom.stdout[-500:] if dom.returncode==0 else dom.stderr[-1000:]);f.report['scanner_dom_executed']=True
  # Per-user bounded API flood rejection is tested last, without waiting or weakening runtime policy.
  statuses=[]
  for _ in range(121):
   r=call('drafts?page=1',session=reviewer,csrf=reviewer_token);statuses.append(r.status)
   if r.status==429:break
  f.check('Per-user scanner endpoint limiter rejects floods',429 in statuses)
  f.report['physical_camera_executed']=False;f.report['actual_local_wasm_print_decode']=True
if __name__=='__main__':run()
