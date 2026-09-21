import pkg from './web/node_modules/playwright-core/index.js';
const { chromium } = pkg;
const browser = await chromium.launch({ executablePath: process.argv[2], headless: true, args:['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const pageErrs=[];
page.on('pageerror', e => pageErrs.push(String(e)));
await page.goto(process.argv[3], { waitUntil:'domcontentloaded', timeout:60000 });
for (let i=0;i<80;i++){
  if (await page.evaluate(() => !!window.__FBX_READY__)) break;
  await page.waitForTimeout(200);
}
const info = await page.evaluate(() => {
  const root = window.__FBX_PLACEMENT__ || window.__FBX_ROOT__;
  let opaque=0, cutout=0, withN=0, face=[];
  root?.traverse((o)=>{
    if (!o.isMesh && !o.isSkinnedMesh) return;
    for (const m of (Array.isArray(o.material)?o.material:[o.material])) {
      if (!m) continue;
      if (m.transparent || m.alphaTest>0) cutout++; else opaque++;
      if (m.normalMap) withN++;
      if (/face|head/i.test(m.name||'')) {
        face.push({
          mat:m.name, transparent:m.transparent, alphaTest:m.alphaTest,
          depthWrite:m.depthWrite, side:m.side,
          map:(m.map?.image?.currentSrc||m.map?.image?.src||'').split('/').pop(),
          nrm:!!m.normalMap
        });
      }
    }
  });
  return { hud: document.getElementById('hud')?.textContent, opaque, cutout, withN, face, ready: window.__FBX_READY__ };
});
console.log(JSON.stringify(info, null, 2));
if (pageErrs.length) console.log('PAGEERRS', pageErrs);
await page.waitForTimeout(600);
await page.screenshot({ path: 'proof/compare/aniplayer_f1_hualuo_closeup.png', type:'png' });
// also a tighter face crop via second camera nudge if possible
await page.evaluate(() => {
  // nudge camera toward head if bones exist
  const root = window.__FBX_ROOT__;
  let head=null;
  root?.traverse(o=>{ if(o.isBone && /head/i.test(o.name) && !/end/i.test(o.name)) head=o; });
  if (head && window.__FBX_CAMERA__) {
    const p=new THREE.Vector3(); head.getWorldPosition(p);
    window.__FBX_CAMERA__.position.set(p.x+40, p.y+10, p.z+70);
    window.__FBX_CAMERA__.lookAt(p.x, p.y, p.z);
  }
});
await page.waitForTimeout(200);
await page.screenshot({ path: 'proof/compare/aniplayer_f1_hualuo_face.png', type:'png' });
await browser.close();
