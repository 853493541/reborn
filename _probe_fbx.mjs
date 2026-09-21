import pkg from './web/node_modules/playwright-core/index.js';
const { chromium } = pkg;
const browser = await chromium.launch({ executablePath: process.argv[2], headless: true, args:['--no-sandbox'] });
const page = await browser.newPage();
const fails = [];
page.on('response', (r) => { if (r.status()>=400) fails.push(r.status()+' '+r.url()); });
page.on('console', (m) => console.log('CON:', m.type(), m.text()));
await page.goto(process.argv[3], { waitUntil: 'domcontentloaded', timeout: 60000 });
for (let i=0;i<30;i++) {
  const st = await page.evaluate(() => ({ ready: window.__FBX_READY__, err: window.__FBX_ERROR__, hud: document.getElementById('hud')?.textContent || document.body.innerText.slice(0,200) }));
  if (st.ready || st.err) { console.log('STATE', JSON.stringify(st)); break; }
  await page.waitForTimeout(500);
}
console.log('FAILS\n'+fails.slice(0,50).join('\n'));
// list a few tex files via http
await browser.close();
