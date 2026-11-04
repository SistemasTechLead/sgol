// Start tools/Sgol.ValidationChecks with --serve. Uses compiled product Razor and synthetic data.
const { chromium, webkit } = require('playwright');
const assert = require('node:assert/strict');
(async () => {
  let checks = 0;
  for (const [name, engine] of [['chromium', chromium], ['webkit', webkit]]) {
    const browser = await engine.launch(name === 'chromium' ? { channel: 'msedge' } : {});
    const page = await browser.newPage();
    for (const width of [320, 768, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto('http://127.0.0.1:7398/');
      for (const theme of ['light', 'dark', 'special']) {
        await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
        assert.equal(await page.locator('.validation-card__conclusion').count(), 6);
        assert.ok(await page.locator('.validation-card__conclusion').first().isVisible());
        assert.equal(await page.locator('.validation-card__conclusion script').count(), 0);
        assert.equal(await page.locator('.validation-card__conclusion .supervision-text').first().evaluate(e => getComputedStyle(e).whiteSpace), 'pre-wrap');
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
        await page.locator('summary').first().focus();
        await page.keyboard.press('Enter');
        assert.ok(await page.locator('details').first().evaluate(e => e.open));
        await page.keyboard.press('Enter');
        await page.screenshot({ path: `artifacts/validation-conclusions/${name}-${width}-${theme}.png` });
        checks++;
      }
    }
    await browser.close();
  }
  console.log(`PASS: ${checks} browser combinations, real Razor component; no authenticated workflow.`);
})().catch(e => { console.error(e); process.exit(1); });
