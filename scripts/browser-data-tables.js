// Test-only Chromium journey; receives disposable localhost cookies on stdin, never saved or logged.
const { chromium } = require('playwright');
const fs = require('fs');
(async()=>{
 const input=JSON.parse(fs.readFileSync(0,'utf8'));
 if(!/^http:\/\/127\.0\.0\.1:\d+$/.test(input.base))throw new Error('Only an isolated localhost fixture is accepted');
 const browser=await chromium.launch({executablePath:process.env.FARM_TEST_CHROMIUM||'/usr/bin/chromium',headless:true,args:['--no-sandbox']});
 try{
  const context=await browser.newContext({viewport:{width:1360,height:900}});await context.addCookies(input.cookies);
  await context.route('**/*',route=>route.request().url().startsWith(input.base+'/')?route.continue():route.abort());
  const page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(input.base+'/data/products');
  await page.locator('.server-data-table tbody tr').first().waitFor();
  await page.waitForFunction(()=>document.querySelector('.server-data-table tbody tr td')?.textContent?.match(/^\d+$/));
  const search=page.locator('.server-grid input[type=search]');await search.fill('Page match');
  await page.waitForFunction(()=>document.querySelector('.dt-info')?.textContent.includes('140'));
  await page.locator('.dt-length select').selectOption('50');
  await page.waitForFunction(()=>document.querySelectorAll('.server-data-table tbody tr').length===50);
  await page.getByRole('button',{name:'Next',exact:true}).click();
  await page.waitForFunction(()=>document.querySelector('.dt-info')?.textContent.includes('51'));
  await page.locator('#erp-language').selectOption('ar');
  await page.waitForFunction(()=>document.documentElement.dir==='rtl'&&document.querySelectorAll('.server-data-table tbody tr').length>0);
  await page.locator('#erp-language').selectOption('bn');
  await page.waitForFunction(()=>document.documentElement.lang==='bn');
  await page.locator('#erp-language').selectOption('en');
  await page.locator('.grid-status').fill('NO-SUCH-UNIT');await page.locator('.grid-filter').click();
  await page.waitForFunction(()=>document.querySelector('.server-data-table tbody tr')?.textContent.includes('No matching records'));
  await page.locator('.grid-status').fill('');await page.locator('.grid-filter').click();
  await page.waitForFunction(()=>document.querySelector('.server-data-table tbody tr td')?.textContent?.match(/^\d+$/));
  if(errors.length)throw new Error('Browser errors: '+errors.join('; '));
  await page.screenshot({path:input.out+'/server-table.png',fullPage:false});
  console.log('PASS Chromium: actual DataTables loaded, search/filter, 50-row paging, next page, Arabic RTL, Bengali, language rebuild, zero JS errors.');
  await context.close();
 }finally{await browser.close();}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
