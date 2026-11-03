// Read-only checks against an authenticated isolated Razor host.
const { chromium, webkit } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const dir = process.argv[2], origin = 'https://localhost:7286';
const routes = ['/validaciones', '/validaciones?Validation=true', '/supervision/bono-tareas', '/mi-trabajo', '/tareas', '/planificacion', '/reportes', '/tablero', '/mi-trabajo/arrastre'];
(async () => {
  const results = [];
  for (const [engine, type] of Object.entries({ chromium, webkit })) {
    const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
    try {
      const page = await browser.newPage({ ignoreHTTPSErrors: true, storageState: dir + '/state-alex.json' });
      for (const route of routes) {
        assert.equal((await page.goto(origin + route)).status(), 200);
        assert(!page.url().includes('/acceso'), 'Authenticated page required');
        const forms = () => page.locator('main form').evaluateAll(es => es.map(e => [...new FormData(e)].filter(([k]) => !k.includes('VerificationToken'))).sort((a,b) => JSON.stringify(a).localeCompare(JSON.stringify(b))));
        const before = await forms();
        for (const width of [320, 768, 1440]) for (const theme of ['light', 'dark', 'special']) {
          await page.setViewportSize({ width, height: 960 });
          await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
          await page.evaluate(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))));
          const checks = await page.locator('main details:not(.my-work-help) > summary, main .boton--textual, main .mobile-fold-toggle, main .task-bonus__trigger, main .report-bar-row:not(:disabled)').evaluateAll(es => es.filter(e => e.checkVisibility()).map(e => {
            const s = getComputedStyle(e), a = getComputedStyle(e, '::after');
            return { label: e.textContent.trim().slice(0, 60), compactGauge: e.matches('.task-bonus--compact > .task-bonus__trigger'), bg: s.backgroundColor, height: e.getBoundingClientRect().height, arrow: e.matches('summary,.mobile-fold-toggle') ? a.width : null };
          }));
          for (const c of checks) {
            assert(!['transparent', 'rgba(0, 0, 0, 0)'].includes(c.bg), JSON.stringify(c));
            assert(c.height >= (c.compactGauge ? 28 : 43), JSON.stringify(c));
            if (c.arrow !== null) assert.equal(c.arrow, '9px', JSON.stringify(c));
          }
          assert.deepEqual(await forms(), before, 'Responsive layout preserves form payloads');
          assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `Overflow ${route} ${width} ${theme}`);
          if (route === '/validaciones' || route === '/supervision/bono-tareas') {
            await page.screenshot({ path: `${dir}/${engine}-${route.includes('bono') ? 'bonus' : 'carryovers'}-${width}-${theme}.png`, fullPage: false });
          }
          results.push({ engine, route, width, theme, controls: checks.length });
        }
        const summary = page.locator('main details:not(.my-work-help) > summary:visible').first();
        if (await summary.count()) {
          await summary.evaluate(e => e.parentElement.open = false);
          await summary.focus(); await page.keyboard.press('Enter');
          assert(await summary.evaluate(e => e.parentElement.open));
          await page.keyboard.press('Space');
          assert(await summary.evaluate(e => !e.parentElement.open));
          assert(await summary.evaluate(e => getComputedStyle(e).outlineStyle !== 'none'));
        }
        if (route === '/validaciones') {
          const cards = page.locator('.carryover-request');
          const count = await cards.count();
          assert(count > 1, 'Fixture must contain multiple carryover requests');
          assert.equal(await page.locator('.carryover-guidance').count(), 1);
          for (const card of await cards.all()) {
            assert.equal(await card.locator('label').textContent(), 'Comentario de decisión');
            assert.equal(await card.locator('[name=requestId]').count(), 1);
            assert.equal(await card.locator('[name=requestRevision]').count(), 1);
            assert(await card.locator('textarea').evaluate(e => e.required && e.maxLength === 4000));
          }
          for (const width of [320, 1440]) {
            await page.setViewportSize({ width, height: 960 });
            await cards.first().locator('summary').first().click();
            await cards.first().scrollIntoViewIfNeeded();
            await page.screenshot({ path: `${dir}/${engine}-request-detail-${width}.png` });
            await cards.first().locator('summary').first().click();
          }
          await page.evaluate(() => document.documentElement.style.fontSize = '32px');
          assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), '200% text reflow');
          await page.evaluate(() => document.documentElement.style.fontSize = '');
        }
      }
    } finally { await browser.close(); }
  }
  fs.writeFileSync(dir + '/results.json', JSON.stringify(results, null, 2));
  console.log(`PASS ${results.length} page/width/theme cases, keyboard, multiple requests and form preservation`);
})().catch(e => { console.error(e); process.exitCode = 1; });

