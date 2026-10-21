// Node-only deterministic locale tests. No browser, network or third-party packages required.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '..');
const source = name => fs.readFileSync(path.join(root, 'EnterpriseERP.Web/wwwroot/js', name), 'utf8');
function element(text, data = {}) {
  return { textContent: text, dataset: data, children: [], attributes: {}, handlers: {}, value: '', validity: {},
    setAttribute(k,v) {this.attributes[k]=v;}, addEventListener(k,fn){this.handlers[k]=fn;},
    setCustomValidity(v){this.customValidity=v;} };
}
function fixture(saved='en', denied=false) {
  const heading=element('Farm Expenses',{i18n:'Farm Expenses'});
  const label=element('Completion date',{i18n:'Completion date'});
  const error=element('The AnimalId field is required.');
  const amount=element('',{});amount.value='123.456';amount.min='0.001';amount.validity={rangeUnderflow:true};
  const blankMarker=element('123.456',{i18n:''});
  const option=element('SKU — Paid (available 12.345)', {i18nTemplate:'stock-availability',i18nValues:JSON.stringify({sku:'SKU',name:'Paid',quantity:'12.345'})});option.value='72';
  const enumCell=element('PartiallyReceived',{i18nEnum:'order',i18nValue:'PartiallyReceived'});
  const notification=element('3 stock items are at or below their reorder point. Review purchasing needs.',{i18nSystemMessage:'notification'});
  const templateElements=[option],enumElements=[enumCell],notificationElements=[notification];
  const userText=element('Farm Expenses'); // User data without a translation marker must remain unchanged.
  const chooser=element('');const announcement=element('');announcement.id='language-announcement';
  const events={};const store=new Map([['farmsphere-language',saved]]);
  const document={documentElement:{lang:'en',dataset:{}},cookie:`farmsphere-language=${saved}`,body:{},
    getElementById(id){return id==='erp-language'?chooser:id==='language-announcement'?announcement:null;},
    querySelectorAll(selector){
      if(selector==='[data-i18n], [data-business-i18n], [data-transfer-i18n]')return [heading,label,blankMarker];
      if(selector.startsWith('.validation-summary-errors'))return [error];
      if(selector==='input, select, textarea')return [amount];
      if(selector==='[data-i18n-template]')return templateElements;
      if(selector==='[data-i18n-enum]')return enumElements;
      if(selector==='[data-i18n-system-message="notification"]')return notificationElements;
      return [];
    },addEventListener(name,fn){events[name]=fn;},dispatchEvent(){}};
  const context={document,console,CustomEvent:class{constructor(name,args){this.type=name;this.detail=args.detail;}},location:{protocol:'https:'},
    localStorage:{getItem(k){if(denied)throw Error('blocked');return store.get(k);},setItem(k,v){if(denied)throw Error('blocked');store.set(k,v);}},
    addEventListener(){},queueMicrotask};context.window=context;
  vm.createContext(context);vm.runInContext(source('farm-core-translations.js'),context);vm.runInContext(source('farm-i18n.js'),context);vm.runInContext(source('stock-transfer-language.js'),context);
  return {context,heading,label,error,amount,userText,chooser,announcement,events,store,document,blankMarker,option,enumCell,notification,templateElements,enumElements,notificationElements};
}
let checks=0;function test(name,fn){fn();checks++;console.log('PASS '+name);}
test('saved Bengali changes labels, dropdown, language and secure persistence',()=>{
 const f=fixture('bn');assert.equal(f.heading.textContent,'খামারের ব্যয়');assert.equal(f.chooser.value,'bn');assert.equal(f.document.documentElement.lang,'bn');assert.match(f.document.cookie,/Secure/);
});
test('Banglish selection announces and translates; English restores original error and labels',()=>{
 const f=fixture();f.chooser.handlers.change({target:{value:'banglish'}});assert.equal(f.heading.textContent,'Khamarer Byay');assert.equal(f.document.documentElement.lang,'bn-Latn');assert.match(f.announcement.textContent,/Banglish/);
 f.context.FarmSphereLanguage.set('en');assert.equal(f.error.textContent,'The AnimalId field is required.');assert.equal(f.label.textContent,'Completion date');
});
test('required, numeric range and decimal validation templates retain meaning',()=>{
 const f=fixture('bn');assert.equal(f.error.textContent,'পশু পূরণ করুন।');assert.match(f.context.FarmSphereLanguage.validation('The field Amount must be between 0.001 and 1000.'),/0\.001.*1000/);
 assert.match(f.context.FarmSphereLanguage.validation('Amount must be positive and have at most 3 decimal places and be within the allowed range.'),/৩/);
});
test('user-entered content and numeric form values are untouched',()=>{const f=fixture('bn');assert.equal(f.userText.textContent,'Farm Expenses');assert.equal(f.amount.value,'123.456');});
test('unsupported locale safely falls back to English',()=>{const f=fixture();f.context.FarmSphereLanguage.set('unknown');assert.equal(f.document.documentElement.lang,'en');assert.equal(f.heading.textContent,'Farm Expenses');});
test('blocked localStorage uses cookie without breaking forms',()=>{const f=fixture('bn',true);assert.equal(f.heading.textContent,'খামারের ব্যয়');f.context.FarmSphereLanguage.set('banglish');assert.match(f.document.cookie,/banglish/);});
test('native invalid message is translated and cleared on input',()=>{const f=fixture('bn');f.events.invalid({target:f.amount});assert.match(f.amount.customValidity,/0\.001/);f.events.input({target:f.amount});assert.equal(f.amount.customValidity,'');});
test('all explicit static translation keys have Bengali, Banglish and Arabic values',()=>{
 const f=fixture();const dictionary=f.context.FarmSphereTranslations;const files=[];
 function walk(dir){for(const row of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,row.name);if(row.isDirectory())walk(p);else if(p.endsWith('.cshtml'))files.push(p);}}
 walk(path.join(root,'EnterpriseERP.Web/Views'));let count=0;
 const decode=x=>x.replaceAll('&amp;','&').replaceAll('&quot;','"').replaceAll('&#x27;',"'").replaceAll('&#39;',"'").replaceAll('&lt;','<').replaceAll('&gt;','>');
 for(const file of files)for(const match of fs.readFileSync(file,'utf8').matchAll(/data-(?:(?:business|transfer)-)?i18n(?:-aria-label)?="([^"]+)"/g)){
  const key=decode(match[1]);if(key.includes('@'))continue;assert.ok(dictionary[key]?.bn,`${file}: missing Bengali ${key}`);assert.ok(dictionary[key]?.banglish,`${file}: missing Banglish ${key}`);assert.ok(dictionary[key]?.ar,`${file}: missing Arabic ${key}`);count++;
 }
 for (const [key,values] of Object.entries(dictionary)) for (const locale of ['bn','banglish','ar']) assert.ok(values[locale],`dictionary key missing ${locale}: ${key}`);
 assert.ok(count>300);console.log(`  verified ${count} explicit localized elements and ${Object.keys(dictionary).length} dictionary keys`);
});
test('Arabic preference applies RTL, labels, validation and secure persistence',()=>{
 const f=fixture('ar');assert.equal(f.document.documentElement.lang,'ar');assert.equal(f.document.documentElement.dir,'rtl');assert.equal(f.heading.textContent,'مصروفات المزرعة');assert.equal(f.chooser.value,'ar');assert.equal(f.error.textContent,'حقل الحيوان مطلوب.');assert.match(f.document.cookie,/farmsphere-language=ar/);
 f.events.invalid({target:f.amount});assert.match(f.amount.customValidity,/أدخل.*0\.001/);assert.equal(f.amount.value,'123.456');assert.equal(f.userText.textContent,'Farm Expenses');
 assert.match(f.context.FarmSphereLanguage.validation('The field Amount must be between 0.001 and 1000.'),/بين 0\.001 و1000/);
 assert.match(f.context.FarmSphereLanguage.validation('The field Notes must be a string with a maximum length of \'100\'.'),/100 حرف/);
 assert.match(f.context.FarmSphereLanguage.validation('Amount must be positive and have at most 3 decimal places and be within the allowed range.'),/موجبة.*3/);
 f.context.FarmSphereLanguage.set('ar');assert.match(f.announcement.textContent,/العربية/);
});
test('leaving Arabic restores LTR and missing translations fall back without changing values',()=>{
 const f=fixture('ar');assert.equal(f.context.FarmSphereLanguage.translate('Unrecognized source text'),'Unrecognized source text');f.context.FarmSphereLanguage.set('bn');assert.equal(f.document.documentElement.dir,'ltr');f.context.FarmSphereLanguage.set('invalid');assert.equal(f.document.documentElement.lang,'en');assert.equal(f.document.documentElement.dir,'ltr');assert.equal(f.amount.customValidity,'');
});
test('registered extension dictionaries merge locales and cover stock transfer and MFA',()=>{
 const f=fixture('ar');assert.notEqual(f.context.FarmSphereLanguage.translate('Stock transfers'),'Stock transfers');assert.notEqual(f.context.FarmSphereLanguage.translate('Authenticator code'),'Authenticator code');f.context.FarmSphereLanguage.register({'Farm Expenses':{bn:'বাংলা খরচ'}});assert.equal(f.context.FarmSphereLanguage.translate('Farm Expenses'),'مصروفات المزرعة');
});
test('layout offers Arabic and styles mirror navigation while preserving numeric entry',()=>{
 const layout=fs.readFileSync(path.join(root,'EnterpriseERP.Web/Views/Shared/_Layout.cshtml'),'utf8');const css=fs.readFileSync(path.join(root,'EnterpriseERP.Web/wwwroot/css/site.css'),'utf8');assert.match(layout,/<option value="ar" lang="ar">العربية<\/option>/);assert.match(css,/html\[dir="rtl"\] \.side\{left:auto;right:0;transform:translateX\(100%\)/);assert.match(css,/direction:ltr;text-align:start;unicode-bidi:isolate/);
});
test('empty conditional markers preserve numeric data',()=>{const f=fixture('ar');assert.equal(f.blankMarker.textContent,'123.456');f.context.FarmSphereLanguage.set('bn');assert.equal(f.blankMarker.textContent,'123.456');});
test('translated labels never own form controls and enum options retain submitted values',()=>{
 const files=[];function walk(dir){for(const row of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,row.name);if(row.isDirectory())walk(p);else if(p.endsWith('.cshtml'))files.push(p);}}walk(path.join(root,'EnterpriseERP.Web/Views'));
 for(const file of files){const source=fs.readFileSync(file,'utf8');for(const match of source.matchAll(/<label[^>]*data-(?:(?:business|transfer)-)?i18n[^>]*>[\s\S]*?<\/label>/g))assert.ok(!/<(?:input|select|textarea)\b/.test(match[0]),`translation replaces control: ${file}`);for(const match of source.matchAll(/<option([^>]*data-(?:(?:business|transfer)-)?i18n[^>]*)>/g))assert.ok(/\bvalue\s*=/.test(match[1]),`translated option changes submitted value: ${file} ${match[0]}`);}
});
test('structured option display changes languages while preserving submitted IDs and user values',()=>{
 const f=fixture();
 for(const language of ['bn','banglish','ar','en']){
  f.context.FarmSphereLanguage.set(language);
  assert.equal(f.option.value,'72');assert.ok(f.option.textContent.includes('SKU'));assert.ok(f.option.textContent.includes('Paid'));assert.ok(f.option.textContent.includes('12.345'));
  assert.equal(f.enumCell.textContent,f.context.FarmSphereLanguage.translate('PartiallyReceived'));
 }
 assert.equal(f.option.textContent,'SKU — Paid (available 12.345)');assert.equal(f.enumCell.textContent,'PartiallyReceived');
});
test('all reachable composite template paths preserve raw arguments and translate only finite enum fields',()=>{
 const f=fixture();const language=f.context.FarmSphereLanguage;
 const cases=[
  ['stock-availability',{sku:'S-001',name:'Paid',quantity:'1,234.567'},['S-001','Paid','1,234.567']],
  ['feed-availability',{sku:'S-001',name:'Purchase',quantity:'1.25'},['S-001','Purchase','1.25']],
  ['original-document',{reference:'D-001',amount:'123.456'},['D-001','123.456']],
  ['document-settlement',{reference:'D-001',kind:'AnimalSale',party:'Purchase',amount:'-123.456',balance:'refund'},['D-001','Purchase','-123.456']],
  ['invoice-balance',{reference:'INV-001',amount:'123.456'},['INV-001','123.456']],
  ['breeding-cycle',{tag:'AN-001',reference:'B-001',status:'Bred'},['AN-001','B-001']],
  ['automation-rule',{name:'Manual',trigger:'LowStock'},['Manual']],
  ['approval-request',{id:'9007199254740993',rule:'42',requester:'staff@example.test'},['9007199254740993','42','staff@example.test']],
  ['support-transition',{previous:'Open',next:'InProgress',note:'Paid <img> {next}'},['Paid <img> {next}']]
 ];
 for(const locale of ['en','bn','banglish','ar']){
  language.set(locale);
  for(const [template,values,raw] of cases){const text=language.formatTemplate(template,values);assert.ok(text,template);for(const value of raw)assert.ok(text.includes(value),`${template}: changed ${value}`);}
  const payment=language.formatTemplate('document-settlement',cases[3][1]);assert.ok(payment.includes(language.translate('AnimalSale')));assert.ok(payment.includes(language.translate('refund')));
  assert.ok(language.formatTemplate('breeding-cycle',cases[5][1]).includes(language.translate('Bred')));
 }
});
test('composite templates do not interpret HTML, placeholders, replacement tokens or user labels',()=>{
 const f=fixture('ar');const raw='<img src=x onerror="alert(1)"> Farm Expenses {quantity} $& $`';
 f.option.dataset.i18nValues=JSON.stringify({sku:'000123',name:raw,quantity:'0.001'});f.context.FarmSphereLanguage.refresh();
 assert.ok(f.option.textContent.includes(raw));assert.equal(f.option.value,'72');assert.equal(f.option.children.length,0);
 assert.equal(f.context.FarmSphereLanguage.enumText('document','Farm Expenses'),'Farm Expenses');assert.equal(f.context.FarmSphereLanguage.enumText('constructor','Purchase'),'Purchase');
});
test('invalid template metadata and wrappers cannot remove controls or fallback text',()=>{
 const f=fixture('bn');const wrapper=element('keep wrapper',{i18nTemplate:'stock-availability',i18nValues:JSON.stringify({sku:'S',name:'N',quantity:'2'})});wrapper.children=[f.amount];
 const invalid=[element('bad JSON',{i18nTemplate:'stock-availability',i18nValues:'{'}),element('unknown',{i18nTemplate:'unknown',i18nValues:'{}'}),element('missing',{i18nTemplate:'stock-availability',i18nValues:'{}'}),wrapper];
 f.templateElements.push(...invalid);f.context.FarmSphereLanguage.refresh();assert.deepEqual(invalid.map(x=>x.textContent),['bad JSON','unknown','missing','keep wrapper']);assert.equal(wrapper.children[0],f.amount);
 for(const values of [null,[],{sku:'S',name:'N',quantity:2},{sku:'S',name:'N',quantity:'2',extra:'unsafe'}])assert.equal(f.context.FarmSphereLanguage.formatTemplate('stock-availability',values),null);
 assert.equal(f.context.FarmSphereLanguage.formatTemplate('__proto__',{}),null);
});
test('scoped notification templates translate fixed wording without changing counts or rule names',()=>{
 const f=fixture('bn');assert.ok(f.notification.textContent.startsWith('3'));assert.notEqual(f.notification.textContent,'3 stock items are at or below their reorder point. Review purchasing needs.');
 const rawName='Farm Expenses {name} <script> $&';f.notification.textContent='Scheduled review: '+rawName;f.context.FarmSphereLanguage.refresh();assert.ok(f.notification.textContent.endsWith(rawName));
 f.context.FarmSphereLanguage.set('en');assert.equal(f.notification.textContent,'Scheduled review: '+rawName);
 f.notification.textContent='5 recorded health follow-ups are due. Staff review is required.';f.context.FarmSphereLanguage.set('ar');assert.ok(f.notification.textContent.includes('5'));assert.notEqual(f.notification.textContent,'5 recorded health follow-ups are due. Staff review is required.');
 f.notification.textContent='Provider result: Farm Expenses';f.context.FarmSphereLanguage.refresh();assert.equal(f.notification.textContent,'Provider result: Farm Expenses');
});
test('reused validation nodes display their newest server error and restore it in English',()=>{
 const f=fixture('bn');f.error.textContent='Insufficient source stock.';f.context.FarmSphereLanguage.refresh();assert.equal(f.error.textContent,f.context.FarmSphereLanguage.translate('Insufficient source stock.'));
 f.context.FarmSphereLanguage.set('ar');assert.equal(f.error.textContent,f.context.FarmSphereLanguage.translate('Insufficient source stock.'));assert.match(f.error.textContent,/[\u0600-\u06ff]/);f.context.FarmSphereLanguage.set('en');assert.equal(f.error.textContent,'Insufficient source stock.');
});
test('application parameterized errors localize known labels and preserve numeric limits',()=>{
 const f=fixture();for(const locale of ['bn','banglish','ar']){f.context.FarmSphereLanguage.set(locale);for(const text of ['Transfer quantity must be positive and have at most 3 decimal places and be within the allowed range.','Opening stock value must have at most 3 decimal places and be within the allowed range.','Order total exceeds the supported range.','Company name must contain 1–120 characters.','The Opening.Ledger[0].Debit field is required.'])assert.notEqual(f.context.FarmSphereLanguage.validation(text),text,text);
 const message=f.context.FarmSphereLanguage.validation('Transfer note is required and must be at most 500 characters without control characters.');assert.ok(message.includes('500'));assert.notEqual(message,'Transfer note is required and must be at most 500 characters without control characters.');}
});
test('unknown errors and interpolated sensitive or provider text are never treated as known validation fields',()=>{
 const f=fixture('ar');const errors=["The value 'secret-account-value' is not valid for Amount.",'The Customer Ada private record field is required.','The field Amount must be between account-abc and private-token.','Patient diagnosis is required and must be at most 40 characters.','Provider connection string exceeds the supported range.','The constructor field is required.','The Provider.Amount field is required.','Passwords must match private-value.'];
 for(const text of errors)assert.equal(f.context.FarmSphereLanguage.validation(text),text);
});
test('template and enum metadata is explicit on active views and never owns form controls',()=>{
 const files=[];function walk(dir){for(const entry of fs.readdirSync(dir,{withFileTypes:true})){const file=path.join(dir,entry.name);if(entry.isDirectory())walk(file);else if(file.endsWith('.cshtml'))files.push(file);}}walk(path.join(root,'EnterpriseERP.Web/Views'));
 const counts={};for(const file of files){const text=fs.readFileSync(file,'utf8');for(const match of text.matchAll(/data-i18n-template="([^"]+)"/g))counts[match[1]]=(counts[match[1]]||0)+1;
  for(const match of text.matchAll(/<option([^>]*data-i18n-(?:template|enum)[^>]*)>/g))assert.match(match[1],/\bvalue="/);
  assert.doesNotMatch(text,/<label[^>]*data-i18n-(?:template|enum)[^>]*>[\s\S]*?<\/(?:select|textarea)>/);
 }
 for(const name of ['stock-availability','feed-availability','original-document','invoice-balance','breeding-cycle','automation-rule','approval-request','support-transition'])assert.ok(counts[name]>0,`unreachable template: ${name}`);
 const accounts=fs.readFileSync(path.join(root,'EnterpriseERP.Web/Views/OperationsWorkspace/Accounts.cshtml'),'utf8');assert.ok(accounts.includes('d.PartyAssigned ? \"document-settlement\" : \"unassigned-settlement\"'));
 const engine=source('farm-i18n.js');for(const match of engine.matchAll(/text: '([^']+)'/g))for(const locale of ['bn','banglish','ar'])assert.ok(fixture().context.FarmSphereTranslations[match[1]]?.[locale],`missing template ${match[1]}`);
});
test('all financial document kinds include animal acquisitions in selectors and tables',()=>{
 const f=fixture();for(const locale of ['bn','banglish','ar']){f.context.FarmSphereLanguage.set(locale);const translated=f.context.FarmSphereLanguage.enumText('document','AnimalPurchase');assert.notEqual(translated,'AnimalPurchase');const text=f.context.FarmSphereLanguage.formatTemplate('document-settlement',{reference:'ACQ-12',kind:'AnimalPurchase',party:'Paid',amount:'123.456',balance:'due'});assert.ok(text.includes(translated));assert.ok(text.includes('Paid'));assert.ok(text.includes('123.456'));}
});
test('template placeholder coverage and malformed-extension fallback preserve every record value',()=>{
 const f=fixture('ar');for(const [key,values] of Object.entries(f.context.FarmSphereTranslations)){const expected=[...key.matchAll(/\{([a-z]+)\}/g)].map(x=>x[1]).sort();if(!expected.length)continue;for(const locale of ['bn','banglish','ar'])assert.deepEqual([...values[locale].matchAll(/\{([a-z]+)\}/g)].map(x=>x[1]).sort(),expected,`${key}: ${locale}`);}
 f.context.FarmSphereLanguage.register({'{sku} — {name} (available {quantity})':{ar:'{sku} {name} {name}'}});assert.equal(f.option.textContent,'SKU — Paid (available 12.345)');
});
test('minimum/maximum string and password length errors keep their actual configured limits',()=>{
 const f=fixture();for(const locale of ['bn','banglish','ar']){f.context.FarmSphereLanguage.set(locale);const range='The field SKU must be a string with a minimum length of 2 and a maximum length of 60.';const translated=f.context.FarmSphereLanguage.validation(range);assert.notEqual(translated,range);assert.match(translated,/2.*60/);const password=f.context.FarmSphereLanguage.validation('Passwords must be at least 12 characters.');assert.ok(password.includes('12'));assert.notEqual(password,'Passwords must be at least 12 characters.');}
});
test('unassigned fallback is explicit while actual parties named Unassigned remain literal',()=>{
 const f=fixture();const values={reference:'P-12',kind:'AnimalPurchase',party:'Unassigned',amount:'123.456',balance:'due'};
 for(const locale of ['bn','banglish','ar']){f.context.FarmSphereLanguage.set(locale);const raw=f.context.FarmSphereLanguage.formatTemplate('document-settlement',values);assert.ok(raw.includes('Unassigned'));const fallback=f.context.FarmSphereLanguage.formatTemplate('unassigned-settlement',values);assert.ok(fallback.includes(f.context.FarmSphereLanguage.translate('Unassigned')));assert.ok(!fallback.includes('Unassigned'));assert.ok(raw.includes('123.456'));assert.ok(fallback.includes('123.456'));}
 f.notification.textContent='Scheduled review:   literal  ';f.context.FarmSphereLanguage.refresh();assert.ok(f.notification.textContent.endsWith('  literal  '));f.context.FarmSphereLanguage.set('en');assert.equal(f.notification.textContent,'Scheduled review:   literal  ');
});
test('enterprise tax options preserve codes versions and rates while translating controlled treatment only',()=>{
 const f=fixture();const values={code:'RAW <img> Standard',version:'7',rate:'7.125',treatment:'Exempt'};
 for(const locale of ['bn','banglish','ar','en']){f.context.FarmSphereLanguage.set(locale);const result=f.context.FarmSphereLanguage.formatTemplate('ecommerce-tax-policy',values);assert.ok(result.includes(values.code));assert.ok(result.includes('7.125'));assert.ok(result.includes('7'));assert.ok(result.includes(f.context.FarmSphereLanguage.translate('Exempt')));}
 assert.equal(f.context.FarmSphereLanguage.formatTemplate('ecommerce-tax-policy',{...values,rate:7.125}),null);
});
test('enterprise asset enum accepts only finite system states',()=>{
 const f=fixture('ar');assert.equal(f.context.FarmSphereLanguage.enumText('asset','Active'),f.context.FarmSphereLanguage.translate('Active'));assert.equal(f.context.FarmSphereLanguage.enumText('asset','Customer supplied name'),'Customer supplied name');assert.equal(f.context.FarmSphereLanguage.enumText('tax','OutOfScope'),f.context.FarmSphereLanguage.translate('OutOfScope'));
});
test('dynamic enterprise navigation and confidential payroll formula enums have all three translated locales',()=>{
 const f=fixture();const keys=['HR and payroll','My employee portal','Customer orders','Payroll register','CRM','Fixed assets','E-commerce','Business policy','AI Manager','Daily','Monthly','Mixed','ActualCalendarDays','FixedPaidUnits','Yes','No'];
 for(const key of keys)for(const locale of ['bn','banglish','ar'])assert.ok(f.context.FarmSphereTranslations[key]?.[locale],`dynamic enterprise key ${key}/${locale}`);
 const view=fs.readFileSync(path.join(root,'EnterpriseERP.Web/Views/AIManager/Index.cshtml'),'utf8');assert.ok(!view.includes('@Model.Draft.Text'));assert.ok(view.includes('@fact.Count'));assert.ok(view.includes('@Model.Draft.Checklist'));
});
test('exact enterprise validation feedback localizes while unknown composed provider messages remain literal',()=>{
 const f=fixture();const keys=['A cart supports at most 50 products.','A different authorized user must approve payroll.','Compensation cannot rewrite an approved or posted payroll period. Choose a later effective month.'];
 for(const locale of ['bn','banglish','ar']){f.context.FarmSphereLanguage.set(locale);for(const key of keys){assert.notEqual(f.context.FarmSphereLanguage.validation(key),key);assert.ok(f.context.FarmSphereTranslations[key]?.[locale]);}const unknown='Provider-private <img src=x> customer 123: policy error';assert.equal(f.context.FarmSphereLanguage.validation(unknown),unknown);}
});
test('scanner labels, controlled states and exact validation keys have three display dictionaries',()=>{
 const f=fixture();const keys=new Set(['Expired','Pending','Committed','Cancelled','Revoked']);
 for(const file of ['NEW_TRANSLATIONS.json','FINAL_TRANSLATIONS.json'])for(const key of Object.keys(JSON.parse(fs.readFileSync(path.join(root,'evidence/scanning',file),'utf8'))))keys.add(key);
 for(const key of keys)for(const locale of ['bn','banglish','ar'])assert.ok(f.context.FarmSphereTranslations[key]?.[locale],`scanner translation ${key}/${locale}`);
 for(const locale of ['en','bn','banglish','ar']){f.context.FarmSphereLanguage.set(locale);assert.equal(f.context.FarmSphereLanguage.validation('Literal supplied tag <img> 000123'),'Literal supplied tag <img> 000123');}
});
console.log(`Localization checks passed: ${checks}`);
