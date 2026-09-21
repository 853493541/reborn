import pkg from './web/node_modules/playwright-core/index.js';
const { chromium } = pkg;
const browser = await chromium.launch({ executablePath: process.argv[2], headless: true, args:['--no-sandbox'] });
const page = await browser.newPage({ viewport:{width:1280,height:900} });
await page.goto(process.argv[3], { waitUntil:'domcontentloaded', timeout:60000 });
for (let i=0;i<60;i++){
  if (await page.evaluate(() => !!window.__FBX_READY__)) break;
  await page.waitForTimeout(200);
}
const info = await page.evaluate(() => {
  const root = window.__FBX_PLACEMENT__ || window.__FBX_ROOT__;
  let withNormal=0, withMap=0, samples=[];
  root?.traverse((o)=>{
    if (!o.isMesh && !o.isSkinnedMesh) return;
    for (const m of (Array.isArray(o.material)?o.material:[o.material])) {
      if (!m) continue;
      if (m.map) withMap++;
      if (m.normalMap) withNormal++;
      if (samples.length<3) samples.push({mat:m.name, map:m.map?.image?.currentSrc||m.map?.image?.src, nrm:m.normalMap?.image?.currentSrc||m.normalMap?.image?.src||null});
    }
  });
  return { hud: document.getElementById('hud')?.textContent, withMap, withNormal, samples };
});
console.log(JSON.stringify(info,null,2));
await page.waitForTimeout(500);
await page.screenshot({ path:'proof/compare/aniplayer_f1_hualuo_closeup.png', type:'png' });
await browser.close();
