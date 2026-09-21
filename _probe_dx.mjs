import pkg from './web/node_modules/playwright-core/index.js';
const { chromium } = pkg;
const browser = await chromium.launch({ executablePath: process.argv[2], headless: true, args:['--no-sandbox'] });
const page = await browser.newPage({ viewport:{width:1280,height:900} });
page.on('pageerror', e => console.log('PAGEERROR', e.message));
await page.goto(process.argv[3], { waitUntil:'domcontentloaded', timeout:60000 });
for (let i=0;i<80;i++){
  if (await page.evaluate(() => !!window.__FBX_READY__)) break;
  await page.waitForTimeout(200);
}
const info = await page.evaluate(() => {
  const root = window.__FBX_PLACEMENT__ || window.__FBX_ROOT__;
  let opaque=0, cut=0, nScale=null;
  const face=[];
  root?.traverse((o)=>{
    if (!o.isMesh && !o.isSkinnedMesh) return;
    for (const m of (Array.isArray(o.material)?o.material:[o.material])) {
      if (!m) continue;
      if (m.transparent || m.alphaTest>0) cut++; else opaque++;
      if (m.normalScale && nScale==null) nScale={x:m.normalScale.x,y:m.normalScale.y};
      if (/face/i.test(m.name||'')) face.push({mat:m.name,t:m.transparent,a:m.alphaTest,ns:m.normalScale?{x:m.normalScale.x,y:m.normalScale.y}:null});
    }
  });
  return { hud: document.getElementById('hud')?.textContent, opaque, cut, nScale, face };
});
console.log(JSON.stringify(info,null,2));
await page.waitForTimeout(500);
await page.screenshot({ path:'proof/compare/aniplayer_f1_hualuo_closeup.png', type:'png' });
await browser.close();
