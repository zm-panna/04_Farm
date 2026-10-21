// Deterministic browser-boundary tests; the separate Chromium journey exercises the real vendor widget.
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const source=fs.readFileSync(path.join(__dirname,'../EnterpriseERP.Web/wwwroot/js/farm-server-tables.js'),'utf8');
const tick=()=>new Promise(r=>setImmediate(r));
const payload={draw:1,recordsTotal:1,recordsFiltered:1,data:[{id:1,reference:'REF',description:'Safe',status:'KG',date:'',value:'0 KG',url:'/products/1/edit'}]};
function fixture(responses=[],collapsed=false){
 const nodes={'.grid-feedback':{textContent:''},'.grid-status':{value:''},'.grid-filter':{},'table':{},'input[type="search"]':{}};
 const details={open:!collapsed,addEventListener:(n,fn)=>details[n]=fn,querySelectorAll:()=>[panel]};
 const panel={dataset:{gridUrl:'/api/tables/products'},querySelector:s=>nodes[s],closest:()=>collapsed?details:null};
 const handlers={},calls=[],tables=[];
 const document={documentElement:{dataset:{language:'en'}},querySelectorAll:s=>s==='.server-grid'?[panel]:collapsed?[details]:[],addEventListener:(n,fn)=>handlers[n]=fn,createElement:tag=>({tagName:tag,textContent:''})};
 class DataTable{constructor(element,options){this.options=options;this.destroyed=false;this.ajax={reload:()=>this.load()};tables.push(this);this.load();}load(){return this.options.ajax({draw:tables.length,start:0,length:25,search:{value:'<tag> %_'},order:[{column:0,dir:'desc'}]},r=>this.response=r);}destroy(){this.destroyed=true;}}
 const context={document,URLSearchParams,AbortController,console,DataTable,window:{DataTable,FarmSphereTranslations:{Open:{bn:'Khulun'}}},fetch:async(url,options)=>{calls.push({url,options});const item=responses.shift()??payload;const result=item.promise?await item.promise:item;if(result instanceof Error)throw result;return {ok:result.status?result.status===200:true,status:result.status??200,json:async()=>result.value??result};}};
 vm.runInNewContext(source,context);return{nodes,details,panel,handlers,calls,tables,document};
}
let passed=0;async function test(name,fn){await fn();passed++;console.log('PASS '+name);}
(async()=>{
 await test('real server-side mode, bounded pages and literal encoded search',async()=>{const f=fixture();await tick();assert.equal(f.tables[0].options.serverSide,true);assert.deepEqual([...f.tables[0].options.lengthMenu],[10,25,50,100]);const query=new URL(f.calls[0].url,'http://localhost').searchParams;assert.equal(query.get('search[value]'),'<tag> %_');assert.equal(query.get('length'),'25');assert.equal(f.calls[0].options.credentials,'same-origin');assert.equal(f.calls[0].options.cache,'no-store');assert.equal(f.nodes['input[type="search"]'].maxLength,120);});
 await test('record values are text nodes and action links reject external scripts',async()=>{const f=fixture();const cols=f.tables[0].options.columns;assert.equal(cols[2].render('<img onerror=x>','display').textContent,'<img onerror=x>');for(const url of ['javascript:x','//evil.test','https://evil.test','/x?escape'])assert.equal(cols[6].render(url,'display').href,undefined);assert.equal(cols[6].render('/products/1/edit','display').href,'/products/1/edit');assert.ok(!source.includes('innerHTML'));});
 await test('date quantity and action columns cannot request unsupported sorts',async()=>{const f=fixture();for(const column of f.tables[0].options.columns.slice(4))assert.equal(column.orderable,false);assert.equal(f.tables[0].options.orderMulti,false);});
 await test('collapsed active-page tables load only on explicit expansion',async()=>{const f=fixture([],true);assert.equal(f.calls.length,0);f.details.open=true;f.details.toggle();await tick();assert.equal(f.calls.length,1);});
 await test('error responses clear stale data with a visible safe error',async()=>{const f=fixture([{status:403,value:{}}]);await tick();assert.equal(f.tables[0].response.data.length,0);assert.match(f.nodes['.grid-feedback'].textContent,/session or access changed/);});
 await test('rate limiting reports a retryable wait without false data',async()=>{const f=fixture([{status:429,value:{}}]);await tick();assert.match(f.nodes['.grid-feedback'].textContent,/Wait a minute/);assert.equal(f.tables[0].response.recordsTotal,0);});
 await test('damaged oversized response is rejected',async()=>{const f=fixture([{...payload,data:Array(101).fill(payload.data[0])}]);await tick();assert.equal(f.tables[0].response.data.length,0);assert.match(f.nodes['.grid-feedback'].textContent,/could not be loaded/);});
 await test('status changes use server AJAX refresh',async()=>{const f=fixture();await tick();f.nodes['.grid-status'].value='KG';await f.nodes['.grid-filter'].onclick();assert.equal(new URL(f.calls[1].url,'http://localhost').searchParams.get('status'),'KG');});
 await test('language rebuild disposes old requests once without looping the Map',async()=>{const f=fixture();await tick();f.document.documentElement.dataset.language='bn';f.handlers['farmsphere:language']();await tick();assert.equal(f.tables.length,2);assert.equal(f.tables[0].destroyed,true);assert.equal(new URL(f.calls[1].url,'http://localhost').searchParams.get('language'),'bn');assert.equal(f.tables[1].options.columns[6].render('/products','display').textContent,'Khulun');});
 await test('late stale responses cannot overwrite newer page data',async()=>{let resolve;const promise=new Promise(r=>resolve=r);const f=fixture([{promise},{...payload,draw:2,data:[]}]);await f.tables[0].load();resolve(payload);await tick();assert.equal(f.tables[0].response.draw,2);assert.equal(f.tables[0].response.data.length,0);});
 console.log('Server table UI checks passed: '+passed);
})().catch(e=>{console.error(e);process.exitCode=1;});
