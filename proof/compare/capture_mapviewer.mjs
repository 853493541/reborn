import { chromium } from "playwright";
import path from "path";
import fs from "fs";

const outDir = "C:/Users/Zhibin Ren/jx3-ani-player/proof/compare";
fs.mkdirSync(outDir, { recursive: true });

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
page.setDefaultTimeout(90000);

const url = "http://localhost:3015/actor-animation-player.html";
console.log("goto", url);
await page.goto(url, { waitUntil: "domcontentloaded" });
await page.waitForTimeout(3000);
await page.screenshot({ path: path.join(outDir, "mapviewer_01_initial.png") });

// Body F1
try {
  const f1 = page.getByRole("button", { name: /^F1$/i });
  if (await f1.count()) {
    await f1.first().click();
    console.log("clicked F1");
  }
} catch (e) {
  console.log("F1 click skip", e.message);
}
await page.waitForTimeout(800);

async function trySearch(q) {
  const inputs = page.locator("input");
  const n = await inputs.count();
  console.log("inputs", n, "query", q);
  for (let i = 0; i < n; i++) {
    const inp = inputs.nth(i);
    const ph = ((await inp.getAttribute("placeholder")) || "").toLowerCase();
    const type = ((await inp.getAttribute("type")) || "").toLowerCase();
    if (type === "hidden") continue;
    if (ph.includes("search") || ph.includes("搜索") || ph.includes("filter") || i < 3) {
      await inp.fill("");
      await inp.fill(q);
      await inp.press("Enter");
      await page.waitForTimeout(1200);
      return true;
    }
  }
  return false;
}

await trySearch("风来吴山");
await page.screenshot({ path: path.join(outDir, "mapviewer_02_search_flws.png") });

async function clickMatching(substrs) {
  const items = page.locator("li, .anim-row, .list-row, tr, .item, button");
  const n = await items.count();
  console.log("candidates", n);
  for (let i = 0; i < Math.min(n, 120); i++) {
    const t = ((await items.nth(i).innerText().catch(() => "")) || "").trim();
    if (!t || t.length > 200) continue;
    if (substrs.some((s) => t.includes(s))) {
      await items.nth(i).click();
      console.log("clicked", t.slice(0, 100));
      return t;
    }
  }
  return null;
}

let hit = await clickMatching(["风来吴山", "s07cj", "重剑技能15"]);
if (!hit) {
  await trySearch("龙牙");
  await page.screenshot({ path: path.join(outDir, "mapviewer_02b_search_longya.png") });
  hit = await clickMatching(["龙牙", "技能13", ".tani", "s04tc"]);
}

await page.waitForTimeout(5000);
await page.screenshot({ path: path.join(outDir, "mapviewer_03_after_select.png") });

for (const label of [/play/i, /播放/, /▶/]) {
  const el = page.getByRole("button", { name: label });
  if (await el.count()) {
    await el.first().click();
    console.log("pressed play", label);
    break;
  }
}
await page.waitForTimeout(3000);
await page.screenshot({ path: path.join(outDir, "mapviewer_04_playing.png") });

const canvas = page.locator("canvas").first();
if (await canvas.count()) {
  await canvas.screenshot({ path: path.join(outDir, "mapviewer_05_viewport.png") });
}

// dump some page text for diagnosis
const bodyText = (await page.locator("body").innerText()).slice(0, 1500);
fs.writeFileSync(path.join(outDir, "mapviewer_page_text.txt"), bodyText, "utf8");
console.log("files", fs.readdirSync(outDir));
await browser.close();
