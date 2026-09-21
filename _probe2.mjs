import pkg from './web/node_modules/playwright-core/index.js';
const { chromium } = pkg;
const browser = await chromium.launch({ executablePath: process.argv[2], headless: true, args:['--no-sandbox'] });
const page = await browser.newPage();
const fails=[];
page.on('response', r => { if (r.status()>=400) fails.push(r.status()+' '+r.url()); });
await page.goto(process.argv[3], { waitUntil:'domcontentloaded', timeout:60000 });
let state=null;
for (let i=0;i<40;i++){
  state = await page.evaluate(() => ({
    ready: window.__FBX_READY__ || window.__FBX_READY__,
    err: window.__FBX_ERROR__,
    keys: Object.keys(window).filter(k=>/FBX|READY|fbx/i.test(k)),
    hud: document.getElementById('hud')?.textContent
  }));
  if (state.ready || state.err) break;
  await page.waitForTimeout(250);
}
console.log(JSON.stringify(state,null,2));
const texFails = fails.filter(u=>!u.includes('pose.json'));
console.log('NON-POSE FAILS', texFails.slice(0,30).join('\n'));
console.log('pose fails', fails.filter(u=>u.includes('pose')).length);
await page.screenshot({ path: 'proof/compare/aniplayer_f1_hualuo_closeup_probe.png', type:'png' });
await browser.close();
