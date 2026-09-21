import pkg from './web/node_modules/playwright-core/index.js';
const { chromium } = pkg;
const browser = await chromium.launch({ executablePath: process.argv[2], headless: true, args:['--no-sandbox'] });
const page = await browser.newPage({ viewport:{width:1400,height:900} });
await page.goto('http://127.0.0.1:3015/actor-animation-player.html', { waitUntil:'domcontentloaded', timeout:60000 });
// Click F1 body chip
await page.evaluate(() => {
  const btns = [...document.querySelectorAll('button')];
  const f1 = btns.find(b => /小女孩|F1/.test(b.textContent||''));
  if (f1) f1.click();
});
await page.waitForTimeout(8000);
const info = await page.evaluate(() => {
  // Try find global scene / root
  const keys = Object.keys(window).filter(k => /THREE|player|scene|anchor|rig/i.test(k)).slice(0,40);
  let faceMats = [];
  // walk common holders
  const roots = [];
  if (window.player?.anchorRig) roots.push(window.player.anchorRig);
  if (window.__anchorRig) roots.push(window.__anchorRig);
  if (window.animationPlayer?.anchorRoot) roots.push(window.animationPlayer.anchorRoot);
  // brute: look for THREE objects with traverse
  function walk(obj, depth=0) {
    if (!obj || depth>3) return;
    if (obj.isObject3D || obj.traverse) roots.push(obj);
    if (typeof obj === 'object') {
      for (const k of Object.keys(obj).slice(0,30)) {
        try { walk(obj[k], depth+1); } catch {}
      }
    }
  }
  // Find canvas-related app state
  return { keys, rootCount: roots.length, title: document.title };
});
console.log(JSON.stringify(info));
await page.screenshot({ path: 'proof/compare/mapviewer_f1_loaded.png', type:'png' });
await browser.close();
