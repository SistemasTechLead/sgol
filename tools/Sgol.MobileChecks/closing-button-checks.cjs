// Authenticated real page; opens and dismisses the dialog without saving coverage.
const { chromium, webkit } = require('playwright');
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const fixture = path.resolve(process.argv[2]), output = path.resolve(process.argv[3]);
fs.mkdirSync(output, { recursive: true });
(async () => {
  for (const [engine, type] of Object.entries({ chromium, webkit })) {
    const browser = await type.launch();
    try {
      const page = await browser.newPage({ ignoreHTTPSErrors: true, storageState: path.join(fixture, 'state-alex.json') });
      await page.goto('https://localhost:7285/planificacion');
      const button = page.getByRole('button', { name: 'Cobertura de cierre', exact: true });
      for (const width of [1440, 1024, 768, 320]) {
        await page.setViewportSize({ width, height: 900 });
        for (const theme of ['light', 'dark', 'special']) {
          await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
          await button.waitFor({ state: 'visible' });
          assert.equal(await button.locator('svg[aria-hidden="true"]').count(), 1);
          assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
          if (width === 1440) {
            const nav = await page.locator('.planning-navigation-actions > nav').boundingBox(), box = await button.boundingBox();
            assert(box.x >= nav.x + nav.width, 'Action to right of navigation');
            assert(Math.abs(box.y + box.height / 2 - nav.y - nav.height / 2) < 2, 'Same row');
          }
          await button.focus();
          await page.keyboard.press('Enter');
          await page.locator('#closing-planning-dialog').waitFor({ state: 'visible' });
          await page.keyboard.press('Escape');
          assert(await button.evaluate(e => document.activeElement === e), 'Focus returns to action');
          await page.screenshot({ path: path.join(output, `${engine}-${width}-${theme}.png`) });
        }
      }
      console.log(`PASS ${engine}: four widths, three themes, right alignment, icon, dialog and keyboard focus`);
    } finally { await browser.close(); }
  }
})().catch(e => { console.error(e); process.exitCode = 1; });
