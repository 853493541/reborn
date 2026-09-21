import { chromium } from 'playwright-core';
import fs from 'fs';
import path from 'path';

const outDir = 'C:/Users/Zhibin Ren/jx3-ani-player/proof/compare';
const chrome = 'C:\\\\Program Files\\\\Google\\\\Chrome\\\\Application\\\\chrome.exe';
const browser = await chromium.launch({ executablePath: chrome, headless: true, args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
page.on('console', (m) => {
  const t = m.text();
  if (/error|fail|clip|FBX|花萝|走路/i.test(t)) console.log('PAGE:', t.slice(0, 200));
});

await page.goto('http://127.0.0.1:3015/actor-viewer.html', { waitUntil: 'domcontentloaded', timeout: 120000 });
await page.waitForTimeout(3000);
await page.screenshot({ path: path.join(outDir, 'gt_mv_01_initial.png') });

// Dump UI hints
const ui = await page.evaluate(() => {
  const texts = [...document.querySelectorAll('button, option, label, a, select')]
    .map((e) => (e.innerText || e.textContent || '').trim())
    .filter((t) => t && t.length < 80)
    .slice(0, 80);
  return { title: document.title, texts };
});
console.log('UI', JSON.stringify(ui, null, 2));

// Try click 花萝 / exports
for (const label of ['花萝', '走路', 'Exports', 'Repo', 'Load']) {
  try {
    const el = page.getByText(label, { exact: false }).first();
    if (await el.count()) {
      await el.click({ timeout: 2000 });
      console.log('clicked', label);
      await page.waitForTimeout(1500);
    }
  } catch (e) {
    console.log('skip', label, e.message?.slice(0, 80));
  }
}
await page.screenshot({ path: path.join(outDir, 'gt_mv_02_after_click.png') });

// Look for select options containing 花萝 or 走路
const opts = await page.evaluate(() => {
  return [...document.querySelectorAll('select option')].map((o) => ({
    value: o.value,
    text: (o.textContent || '').trim(),
  })).filter((o) => /花萝|走路|跳跃|hualuo|walk|F1/i.test(o.text + o.value));
});
console.log('OPTS', JSON.stringify(opts.slice(0, 40), null, 2));

await browser.close();
