// Decode actual server-generated QR/Code128 SVG geometry with the actual pinned local WASM. No network.
const fs=require('node:fs'),vm=require('node:vm'),path=require('node:path'),assert=require('node:assert/strict');
(async()=>{
 const samples=JSON.parse(fs.readFileSync(0,'utf8'));const dir=path.join(__dirname,'../EnterpriseERP.Web/wwwroot/lib/zxing-wasm-3.1.5');
 const context={WebAssembly,TextDecoder,TextEncoder,URL,Blob,console,performance,setTimeout,clearTimeout,fetch:()=>{throw new Error('Network is forbidden in offline decoder verification');}};
 vm.runInNewContext(fs.readFileSync(path.join(dir,'reader.js'),'utf8'),context);const lib=context.ZXingWASM;
 await lib.prepareZXingModule({overrides:{wasmBinary:fs.readFileSync(path.join(dir,'zxing_reader.wasm'))},fireImmediately:true});
 for(const sample of samples){const size=sample.svg.match(/viewBox="0 0 (\d+) (\d+)"/);assert.ok(size);const width=Number(size[1]),height=Number(size[2]),scale=3,pixels=new Uint8ClampedArray(width*height*scale*scale*4).fill(255);
  for(const match of sample.svg.matchAll(/M(\d+) (\d+)h(\d+)v(\d+)h-\d+z/g)){const[x,y,w,h]=match.slice(1).map(Number);for(let yy=y*scale;yy<(y+h)*scale;yy++)for(let xx=x*scale;xx<(x+w)*scale;xx++){const offset=(yy*width*scale+xx)*4;pixels[offset]=pixels[offset+1]=pixels[offset+2]=0;}}
  const found=await lib.readBarcodes({width:width*scale,height:height*scale,data:pixels},{formats:['QRCode','Code128'],textMode:'Plain',maxNumberOfSymbols:1});assert.equal(found[0]?.text,sample.expected,sample.format);pixels.fill(0);console.log('PASS actual generated '+sample.format+' label round-trip');
 }lib.purgeZXingModule();
})().catch(ex=>{console.error(ex);process.exitCode=1;});
