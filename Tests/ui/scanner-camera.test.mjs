import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import fs from 'node:fs';
import crypto from 'node:crypto';
const require = createRequire(import.meta.url);
const {CameraSession,WASM_SHA256,SCRIPT_SRI,NATIVE_FORMATS,FALLBACK_FORMATS} = require('../../EnterpriseERP.Web/wwwroot/js/scanner-camera.js');
const tick = () => new Promise(resolve => setImmediate(resolve));
function deferred(){let resolve,reject;const promise=new Promise((a,b)=>{resolve=a;reject=b});return {promise,resolve,reject};}
function fixture(options={}){
 const tracks=[{stopped:0,stop(){this.stopped++}}];const stream={getTracks:()=>tracks};const calls=[],statuses=[],values=[];
 const video={readyState:4,videoWidth:640,videoHeight:480,srcObject:null,async play(){},pause(){}};
 let timerId=0;const timers=new Map();
 const env={isSecureContext:true,AbortController,document:{createElement:()=>({width:0,height:0})},navigator:{mediaDevices:{getUserMedia:async constraints=>{calls.push(constraints);return stream},enumerateDevices:async()=>[]}},setTimeout(fn){timers.set(++timerId,fn);return timerId},clearTimeout(id){timers.delete(id)},BarcodeDetector:class{static async getSupportedFormats(){return NATIVE_FORMATS}async detect(){return []}},...options.env};
 let fallbackCalls=0;
 const session=new CameraSession({video,onValue:value=>values.push(value),onStatus:value=>statuses.push(value),env,createFallback:async()=>{fallbackCalls++;return {detect:async()=>[],dispose(){}}},...options.session});
 return {session,env,video,stream,tracks,calls,statuses,values,timers,get fallbackCalls(){return fallbackCalls}};
}
test('construction and stop never request camera; explicit start requests video only',async()=>{const f=fixture();assert.equal(f.calls.length,0);f.session.stop();assert.equal(f.calls.length,0);await f.session.start();assert.equal(f.calls.length,1);assert.equal(f.calls[0].audio,false);assert.deepEqual(f.calls[0].video,{facingMode:{ideal:'environment'}});f.session.stop();assert.equal(f.video.srcObject,null);assert.equal(f.tracks[0].stopped,1);assert.equal(f.timers.size,0)});
test('camera selection requests exact selected device',async()=>{const f=fixture();await f.session.start('camera2');assert.deepEqual(f.calls[0].video,{deviceId:{exact:'camera2'}});f.session.stop()});
test('pending permission result is stopped after close and never attached',async()=>{const d=deferred(),f=fixture();f.env.navigator.mediaDevices.getUserMedia=()=>d.promise;const task=f.session.start();assert.equal(f.session.pending,true);f.session.stop();d.resolve(f.stream);await task;assert.equal(f.tracks[0].stopped,1);assert.equal(f.video.srcObject,null);assert.equal(f.session.active,false)});
test('rapid start stop start discards stale first camera grant',async()=>{const a=deferred(),b=deferred(),f=fixture();let n=0;f.env.navigator.mediaDevices.getUserMedia=()=>++n===1?a.promise:b.promise;const first=f.session.start();const second=f.session.start('next');a.resolve(f.stream);await first;assert.equal(f.tracks[0].stopped,1);const t={stopped:0,stop(){this.stopped++}};b.resolve({getTracks:()=>[t]});await second;assert.equal(f.session.active,true);f.session.stop();assert.equal(t.stopped,1)});
test('permission denied returns actionable manual fallback without retained stream',async()=>{const f=fixture();f.env.navigator.mediaDevices.getUserMedia=async()=>{throw Object.assign(new Error(),{name:'NotAllowedError'})};await f.session.start();assert.equal(f.statuses.at(-1),'denied');assert.equal(f.session.stream,null);assert.equal(f.session.pending,false)});
test('insecure context and unavailable browser never ask for permission',async()=>{const f=fixture({env:{isSecureContext:false}});await f.session.start();assert.equal(f.statuses.at(-1),'https');assert.equal(f.calls.length,0);f.env.isSecureContext=true;f.env.navigator={};await f.session.start();assert.equal(f.statuses.at(-1),'unsupported')});
test('missing native detector and missing QR supported format use fallback',async()=>{for(const detector of [undefined,class{static async getSupportedFormats(){return ['ean_13']}}]){const f=fixture({env:{BarcodeDetector:detector}});await f.session.start();assert.equal(f.fallbackCalls,1);f.session.stop()}});
test('native detector runtime failure switches to fallback',async()=>{const f=fixture({env:{BarcodeDetector:class{static async getSupportedFormats(){return NATIVE_FORMATS}async detect(){throw new Error('not supported')}}}});await f.session.start();await tick();assert.equal(f.fallbackCalls,1);f.session.stop()});
test('repeated frames generate one read-only callback and explicit reset permits same SKU',async()=>{const f=fixture({env:{BarcodeDetector:class{static async getSupportedFormats(){return NATIVE_FORMATS}async detect(){return [{rawValue:'SKU-1'}]}}}});await f.session.start();await tick();assert.deepEqual(f.values,['SKU-1']);assert.equal(f.tracks[0].stopped,1);assert.equal(f.session.latched,true);await f.session.start();assert.equal(f.calls.length,1);assert.equal(f.statuses.at(-1),'scanNextFirst');f.session.reset();await f.session.start();await tick();assert.deepEqual(f.values,['SKU-1','SKU-1']);assert.equal(f.calls.length,2)});
test('async decode resolving after deactivate cannot dispatch preview',async()=>{const d=deferred(),f=fixture({env:{BarcodeDetector:class{static async getSupportedFormats(){return NATIVE_FORMATS}detect(){return d.promise}}}});await f.session.start();f.session.stop();d.resolve([{rawValue:'stale'}]);await tick();assert.deepEqual(f.values,[]);assert.equal(f.video.srcObject,null)});
test('fallback loading failure cleans camera resources',async()=>{const f=fixture({env:{BarcodeDetector:undefined},session:{createFallback:async()=>{throw new Error('failed')}}});await f.session.start();assert.equal(f.tracks[0].stopped,1);assert.equal(f.statuses.at(-1),'decoder');assert.equal(f.video.srcObject,null)});
test('vendored reader JS SRI and WASM upstream hash are exact',()=>{const dir=new URL('../../EnterpriseERP.Web/wwwroot/lib/zxing-wasm-3.1.5/',import.meta.url);assert.equal('sha256-'+crypto.createHash('sha256').update(fs.readFileSync(new URL('reader.js',dir))).digest('base64'),SCRIPT_SRI);assert.equal(crypto.createHash('sha256').update(fs.readFileSync(new URL('zxing_reader.wasm',dir))).digest('hex'),WASM_SHA256)});
test('actual pinned WASM decodes a known QR from RGBA without any network access',async()=>{
 const vm=require('node:vm'),dir=new URL('../../EnterpriseERP.Web/wwwroot/lib/zxing-wasm-3.1.5/',import.meta.url);
 const fixture=JSON.parse(fs.readFileSync(new URL('qr-decode-fixture.json',import.meta.url),'utf8'));const scale=6,quiet=4,size=(fixture.modules.length+quiet*2)*scale;
 const pixels=new Uint8ClampedArray(size*size*4).fill(255);
 fixture.modules.forEach((row,y)=>Array.from(row).forEach((bit,x)=>{if(bit==='1')for(let dy=0;dy<scale;dy++)for(let dx=0;dx<scale;dx++){const i=(((y+quiet)*scale+dy)*size+(x+quiet)*scale+dx)*4;pixels[i]=pixels[i+1]=pixels[i+2]=0}}));
 const context={WebAssembly,TextDecoder,TextEncoder,URL,Blob,console,performance,setTimeout,clearTimeout,fetch:()=>{throw new Error('Network forbidden during fixture decode')}};
 vm.runInNewContext(fs.readFileSync(new URL('reader.js',dir),'utf8'),context);const lib=context.ZXingWASM;
 await lib.prepareZXingModule({overrides:{wasmBinary:fs.readFileSync(new URL('zxing_reader.wasm',dir))},fireImmediately:true});
 const found=await lib.readBarcodes({width:size,height:size,data:pixels},{formats:FALLBACK_FORMATS,textMode:'Plain',maxNumberOfSymbols:1});
 assert.equal(found[0]?.text,fixture.expected);assert.equal(found.length,1);pixels.fill(0);lib.purgeZXingModule();
});
test('native camera requests every supported QR and product barcode format',async()=>{
 let requested;const f=fixture({env:{BarcodeDetector:class{static async getSupportedFormats(){return NATIVE_FORMATS}constructor(options){requested=options.formats}async detect(){return [{rawValue:'5901234123457',format:'ean_13'}]}}}});await f.session.start();await tick();assert.deepEqual(requested,NATIVE_FORMATS);assert.deepEqual(f.values,['5901234123457']);assert.equal(f.fallbackCalls,0);
});
test('native QR-only implementation falls back rather than losing barcode support',async()=>{const f=fixture({env:{BarcodeDetector:class{static async getSupportedFormats(){return ['qr_code']}}}});await f.session.start();assert.equal(f.fallbackCalls,1);f.session.stop()});
test('actual pinned WASM decodes Code128, EAN13, EAN8, UPCA and UPCE fixtures offline',async()=>{
 const vm=require('node:vm'),dir=new URL('../../EnterpriseERP.Web/wwwroot/lib/zxing-wasm-3.1.5/',import.meta.url);
 const fixture=JSON.parse(fs.readFileSync(new URL('barcode-decode-fixture.json',import.meta.url),'utf8'));
 const context={WebAssembly,TextDecoder,TextEncoder,URL,Blob,console,performance,setTimeout,clearTimeout,fetch:()=>{throw new Error('No network permitted')}};
 vm.runInNewContext(fs.readFileSync(new URL('reader.js',dir),'utf8'),context);const lib=context.ZXingWASM;
 await lib.prepareZXingModule({overrides:{wasmBinary:fs.readFileSync(new URL('zxing_reader.wasm',dir))},fireImmediately:true});
 for(const sample of fixture.samples){const scale=3,width=sample.stripe.length*scale,height=150,pixels=new Uint8ClampedArray(width*height*4).fill(255);for(let x=0;x<sample.stripe.length;x++)if(sample.stripe[x]==='1')for(let dx=0;dx<scale;dx++)for(let y=15;y<height-15;y++){const offset=(y*width+x*scale+dx)*4;pixels[offset]=pixels[offset+1]=pixels[offset+2]=0}const found=await lib.readBarcodes({width,height,data:pixels},{formats:FALLBACK_FORMATS,textMode:'Plain',maxNumberOfSymbols:1});assert.equal(found[0]?.text,sample.expected,sample.format);pixels.fill(0)}lib.purgeZXingModule();
});
test('generic camera adapter bounds decoded callbacks before any application lookup',async()=>{for(const value of ['X'.repeat(129),'bad\u0000reference']){const f=fixture({env:{BarcodeDetector:class{static async getSupportedFormats(){return NATIVE_FORMATS}async detect(){return [{rawValue:value}]}}}});await f.session.start();await tick();assert.deepEqual(f.values,[]);assert.equal(f.statuses.at(-1),'invalid');assert.equal(f.tracks[0].stopped,1)}});
