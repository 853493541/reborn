import { chromium } from 'playwright-core';
import path from 'path';

const outDir = 'C:/Users/Zhibin Ren/jx3-ani-player/proof/compare';
const chrome = 'C:\\\\Program Files\\\\Google\\\\Chrome\\\\Application\\\\chrome.exe';
const browser = await chromium.launch({ executablePath: chrome, headless: true, args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
await page.goto('http://127.0.0.1:3015/actor-viewer.html', { waitUntil: 'domcontentloaded', timeout: 120000 });
await page.waitForTimeout(3000);

// Discover viewer instance + methods
const probe = await page.evaluate(() => {
  const out = { keys: [], methods: [] };
  for (const k of Object.getOwnPropertyNames(window)) {
    try {
      const v = window[k];
      if (!v || typeof v !== 'object') continue;
      if (v.current || v.loadExport || v.exports || v.updateHeadAttachments) {
        out.keys.push(k);
        out.methods.push({
          k,
          keys: Object.keys(v).slice(0, 40),
          proto: Object.getOwnPropertyNames(Object.getPrototypeOf(v) || {}).slice(0, 40),
        });
      }
    } catch {}
  }
  return out;
});
console.log(JSON.stringify(probe, null, 2));

// Click list buttons that are visible
await page.locator('text=花萝').first().click({ force: true }).catch(() => {});
await page.waitForTimeout(5000);
await page.screenshot({ path: path.join(outDir, 'gt_mv_char.png') });

// Clip source: click 走路 in clip source area
const walkBtns = page.locator('text=走路');
console.log('走路 count', await walkBtns.count());
for (let i = 0; i < Math.min(await walkBtns.count(), 5); i++) {
  try {
    await walkBtns.nth(i).click({ force: true, timeout: 1000 });
    console.log('clicked 走路', i);
    await page.waitForTimeout(2000);
  } catch {}
}

// Force via select force
await page.evaluate(() => {
  const sel = document.getElementById('actor-select');
  if (sel) {
    sel.value = '花萝';
    sel.dispatchEvent(new Event('change', { bubbles: true }));
  }
  const clip = document.getElementById('clip-source-select') || document.querySelector('select#clipSourceSelect');
  const selects = [...document.querySelectorAll('select')];
  for (const s of selects) {
    const opt = [...s.options].find((o) => /走路/.test(o.textContent) && /clip|花萝走路|repoclips|走路 \(/.test(o.textContent + o.value));
    if (opt) {
      s.value = opt.value;
      s.dispatchEvent(new Event('change', { bubbles: true }));
    }
  }
});
await page.waitForTimeout(5000);

// Seek
const seek = await page.evaluate(() => {
  for (const k of Object.getOwnPropertyNames(window)) {
    try {
      const v = window[k];
      if (v && v.current && v.current.mixer) {
        v.current.mixer.setTime(0.63);
        v.current.mixer.update(0);
        if (typeof v.updateHeadAttachments === 'function') v.updateHeadAttachments();
        return { ok: true, key: k, time: v.current.mixer.time, bones: v.currentStats?.bones };
      }
    } catch {}
  }
  return { ok: false };
});
console.log('seek', seek);

await page.waitForTimeout(500);
await page.screenshot({ path: path.join(outDir, 'gt_mv_walk_mid.png'), fullPage: true });
const canvas = page.locator('canvas').first();
if (await canvas.count()) await canvas.screenshot({ path: path.join(outDir, 'gt_mv_walk_mid_canvas.png') });
console.log('done');
await browser.close();
