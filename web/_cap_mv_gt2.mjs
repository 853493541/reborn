import { chromium } from 'playwright-core';
import path from 'path';

const outDir = 'C:/Users/Zhibin Ren/jx3-ani-player/proof/compare';
const chrome = 'C:\\\\Program Files\\\\Google\\\\Chrome\\\\Application\\\\chrome.exe';
const browser = await chromium.launch({ executablePath: chrome, headless: true, args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });

await page.goto('http://127.0.0.1:3015/actor-viewer.html', { waitUntil: 'domcontentloaded', timeout: 120000 });
await page.waitForTimeout(2500);

// Character select: first select with 花萝 option
const selects = page.locator('select');
const n = await selects.count();
console.log('selects', n);
for (let i = 0; i < n; i++) {
  const options = await selects.nth(i).locator('option').allTextContents();
  console.log(i, options.slice(0, 6));
  if (options.some((t) => t.includes('花萝') && t.includes('无动作'))) {
    await selects.nth(i).selectOption({ label: options.find((t) => t.includes('花萝') && t.includes('无动作')) });
    console.log('selected character 花萝');
    await page.waitForTimeout(4000);
  }
}

// Clip source select — look for 走路
for (let i = 0; i < n; i++) {
  const options = await selects.nth(i).locator('option').allTextContents();
  const walk = options.find((t) => /走路/.test(t) && /花萝走路|1 clip|走路\//.test(t));
  if (walk) {
    const vals = await selects.nth(i).locator('option').evaluateAll((opts) =>
      opts.map((o) => ({ value: o.value, text: o.textContent.trim() }))
    );
    const hit = vals.find((o) => o.text === walk || /走路\/花萝走路|走路 \(花萝走路/.test(o.text));
    if (hit) {
      await selects.nth(i).selectOption(hit.value);
      console.log('selected clip source', hit);
      await page.waitForTimeout(3000);
    }
  }
}

// Animation select — seasun animation
for (let i = 0; i < (await selects.count()); i++) {
  const vals = await selects.nth(i).locator('option').evaluateAll((opts) =>
    opts.map((o) => ({ value: o.value, text: o.textContent.trim() }))
  );
  const anim = vals.find((o) => /seasun|animation/i.test(o.text));
  if (anim && anim.value !== '-1') {
    await selects.nth(i).selectOption(anim.value);
    console.log('selected anim', anim);
    await page.waitForTimeout(1000);
  }
}

// Play
try {
  await page.getByRole('button', { name: /Play/i }).click({ timeout: 2000 });
  console.log('play');
} catch {}
await page.waitForTimeout(500);

// Seek mixer to 0.63 via page evaluate if viewer exposes API
const seeked = await page.evaluate(() => {
  const v = window.viewer || window.actorViewer || window.app;
  // try common paths
  const cur = window.__actorViewer || null;
  // scan for mixer
  let mixer = null;
  let found = null;
  for (const k of Object.keys(window)) {
    try {
      const o = window[k];
      if (o && o.current && o.current.mixer) { found = k; mixer = o.current.mixer; break; }
      if (o && o.mixer && o.current) { found = k; mixer = o.mixer; break; }
    } catch {}
  }
  if (!mixer && window.ActorViewer) {}
  // brute: look at canvas parent __vue or similar
  return { found, hasMixer: !!mixer, keys: Object.keys(window).filter((k) => /actor|viewer|app/i.test(k)).slice(0, 20) };
});
console.log('seek probe', seeked);

// Try to find ActorViewer instance on DOM
const seek2 = await page.evaluate(() => {
  // actor-viewer.js typically: window.actorViewer or new ActorViewer assigned
  const candidates = [];
  for (const k of Object.getOwnPropertyNames(window)) {
    try {
      const v = window[k];
      if (v && typeof v === 'object' && v.current && v.current.mixer && v.updateHeadAttachments) {
        candidates.push(k);
        v.current.mixer.setTime(0.63);
        v.current.mixer.update(0);
        if (typeof v.updateHeadAttachments === 'function') v.updateHeadAttachments();
        return { ok: true, key: k, time: v.current.mixer.time };
      }
    } catch (e) {
      candidates.push('err:' + k);
    }
  }
  return { ok: false, candidates: candidates.slice(0, 30) };
});
console.log('seek2', seek2);

await page.waitForTimeout(800);
await page.screenshot({ path: path.join(outDir, 'gt_mv_walk_mid.png') });
const canvas = page.locator('canvas').first();
if (await canvas.count()) {
  await canvas.screenshot({ path: path.join(outDir, 'gt_mv_walk_mid_canvas.png') });
}
console.log('wrote gt screenshots');
await browser.close();
