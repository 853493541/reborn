import pkg from './web/node_modules/playwright-core/index.js';
const { chromium } = pkg;
const browser = await chromium.launch({ executablePath: process.argv[2], headless: true, args:['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const fails=[];
page.on('response', r => { if (r.status()>=400 && !r.url().includes('pose.json')) fails.push(r.status()+' '+r.url()); });
await page.goto(process.argv[3], { waitUntil:'domcontentloaded', timeout:60000 });
let state=null;
for (let i=0;i<50;i++){
  state = await page.evaluate(() => ({
    ready: window.__FBX_READY__,
    binds: window.__FBX_MAT_BINDINGS__,
    hud: document.getElementById('hud')?.textContent
  }));
  if (state.ready) break;
  await page.waitForTimeout(200);
}
console.log('HUD', state.hud);
console.log('READY', JSON.stringify(state.ready));
console.log('BINDS', JSON.stringify(state.binds, null, 2));
console.log('FAILS', fails.join('\n'));
await page.waitForTimeout(500);
await page.screenshot({ path: 'proof/compare/aniplayer_f1_hualuo_closeup.png', type:'png' });
await browser.close();
