// Actual DataTables vendor library + jsdom + the real disposable HTTP endpoint. This is not a browser execution claim.
const fs=require('fs');const {JSDOM,ResourceLoader,VirtualConsole}=require(process.env.FARM_TEST_JSDOM);
(async()=>{
 const input=JSON.parse(fs.readFileSync(0,'utf8'));if(!/^http:\/\/127\.0\.0\.1:\d+$/.test(input.base))throw new Error('Only isolated localhost is accepted');
 const cookie=input.cookies.map(x=>x.name+'='+x.value).join('; ');const failures=[];let draws=0;
 class LocalResources extends ResourceLoader{fetch(url,options){if(!url.startsWith(input.base+'/'))return null;return super.fetch(url,options);}}
 const response=await fetch(input.base+'/data/products',{headers:{Cookie:cookie}});if(response.status!==200)throw new Error('Authenticated HTML unavailable');
 const virtualConsole=new VirtualConsole();virtualConsole.on('jsdomError',e=>{if(e.type!=='css-parsing')failures.push(e.message);});
 const dom=new JSDOM(await response.text(),{url:input.base+'/data/products',runScripts:'dangerously',resources:new LocalResources(),pretendToBeVisual:true,virtualConsole,
  beforeParse(window){window.AbortController=global.AbortController;window.matchMedia=()=>({matches:false,addEventListener(){},removeEventListener(){}});window.fetch=async(path,options={})=>{const url=new URL(path,input.base);if(url.origin!==input.base)throw new Error('Nonlocal fetch denied');if(url.pathname.startsWith('/api/tables/'))draws++;return fetch(url,{...options,headers:{...options.headers,Cookie:cookie}});};}});
 try{
  const window=dom.window,doc=window.document;const wait=async(fn)=>{const end=Date.now()+15000;while(!fn()){if(Date.now()>end)throw new Error('DOM timeout; '+failures.join('; '));await new Promise(r=>setTimeout(r,30));}};
  await wait(()=>doc.querySelector('.server-data-table tbody tr td')?.textContent.match(/^\d+$/));
  const table=window.DataTable.tables({api:true});if(!table)throw new Error('Actual DataTables instance missing');
  table.search('Page match').draw();await wait(()=>doc.querySelector('.dt-info')?.textContent.includes('140'));
  table.page.len(50).draw();await wait(()=>doc.querySelectorAll('.server-data-table tbody tr').length===50);
  table.page('next').draw('page');await wait(()=>doc.querySelector('.dt-info')?.textContent.includes('51'));
  const chooser=doc.getElementById('erp-language');chooser.value='ar';chooser.dispatchEvent(new window.Event('change',{bubbles:true}));await wait(()=>doc.documentElement.dir==='rtl'&&doc.querySelector('.server-data-table tbody tr td')?.textContent.match(/^\d+$/));
  chooser.value='bn';chooser.dispatchEvent(new window.Event('change',{bubbles:true}));await wait(()=>doc.documentElement.lang==='bn');
  chooser.value='en';chooser.dispatchEvent(new window.Event('change',{bubbles:true}));await wait(()=>doc.documentElement.lang==='en'&&doc.querySelector('.server-data-table tbody tr td')?.textContent.match(/^\d+$/));
  doc.querySelector('.grid-status').value='NO-SUCH-UNIT';doc.querySelector('.grid-filter').click();await wait(()=>doc.querySelector('.server-data-table tbody')?.textContent.includes('No matching records'));
  if(failures.length)throw new Error(failures.join('; '));if(draws<7)throw new Error('Expected server-side draws did not occur');
  console.log('PASS actual DataTables3.1.4 + jsdom with live HTTP: search,50-row page,next,Arabic RTL,Bengali,language rebuild,status filter; '+draws+' AJAX draws. Real browser remains separate.');
 }finally{dom.window.close();}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
