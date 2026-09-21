import pkg from './web/node_modules/playwright-core/index.js';
const { chromium } = pkg;
const browser = await chromium.launch({ executablePath: process.argv[2], headless: true, args:['--no-sandbox'] });
const page = await browser.newPage({ viewport:{width:1400,height:900} });
const url = 'http://127.0.0.1:3015/actor-animation-player.html';
await page.goto(url, { waitUntil:'domcontentloaded', timeout:60000 });
await page.waitForTimeout(2500);
// Try select F1 / search 花萝 or load preset
const dump = await page.evaluate(async () => {
  const out = { title: document.title, bodyButtons: [], texts: [] };
  document.querySelectorAll('button,[role=button],.chip,.body-btn').forEach((el,i)=>{
    if (i<40) out.bodyButtons.push(el.textContent.trim().slice(0,40));
  });
  // canvas present?
  out.canvas = !!document.querySelector('canvas');
  out.url = location.href;
  return out;
});
console.log(JSON.stringify(dump,null,2));
await page.screenshot({ path: 'proof/compare/mapviewer_actor_page.png', type:'png' });
await browser.close();
