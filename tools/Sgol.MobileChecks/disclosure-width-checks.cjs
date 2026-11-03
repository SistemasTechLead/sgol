// Read-only real Razor pages. Optional candidate CSS is served only to this browser.
const { chromium, webkit } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const dir = process.argv[2], candidate = process.argv[3];
const routes = ['/tareas', '/tareas/actualizar', '/validaciones', '/validaciones?Validation=true', '/supervision/bono-tareas', '/supervision/carga', '/mi-trabajo', '/mi-trabajo/arrastre', '/planificacion', '/planificacion/procesos', '/tareas/procesos', '/personas-y-accesos', '/configuracion', '/reportes', '/tablero'];
(async () => {
  const results = [], failures = [];
  for (const [engine, type] of Object.entries({ chromium, webkit })) {
    const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
    try {
      const page = await browser.newPage({ ignoreHTTPSErrors: true, storageState: dir + '/state-alex.json' });
      if (candidate) await page.route('**/css/interactive-controls.css*', r => r.fulfill({ contentType: 'text/css', body: fs.readFileSync(candidate, 'utf8') }));
      for (const route of routes) {
        assert.equal((await page.goto('https://localhost:7286' + route)).status(), 200, route);
        assert(!page.url().includes('/acceso'), 'Authenticated fixture required');
        for (const width of [320, 768, 1440]) {
          await page.setViewportSize({ width, height: 960 });
          await page.evaluate(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))));
          const checks = await page.evaluate(() => {
            const rows = [];
            for (const s of document.querySelectorAll('main details:not(.my-work-help) > summary')) {
              const d = s.parentElement, parents = [];
              for (let p = d.parentElement; p; p = p.parentElement) if (p.matches('details')) { parents.push([p,p.open]); p.open = true; }
              const original = d.open;
              d.open = false;
              const before = s.getBoundingClientRect().width;
              d.open = true;
              const after = s.getBoundingClientRect().width;
              const available = d.clientWidth;
              const label = s.textContent.trim();
              d.open = original;
              for (const [p,open] of parents) p.open = open;
              if (before > 0) rows.push({ label: label.slice(0,80), before, after, available, stable: Math.abs(before-after) < 1, compact: !(label.length < 55 && available > 450 && after >= available-25) });
            }
            return rows;
          });
          failures.push(...checks.filter(c => !c.stable || !c.compact).map(c => ({ engine, route, width, ...c })));
          const visible = page.locator('main details:not(.my-work-help) > summary:visible').first();
          if (await visible.count()) {
            const before = (await visible.boundingBox()).width;
            await visible.click();
            assert(Math.abs((await visible.boundingBox()).width - before) < 1, `Click width ${engine} ${route} ${width}: ${await visible.textContent()} ${before} -> ${(await visible.boundingBox()).width}`);
            await visible.click();
          }
          for (const button of await page.locator('main .mobile-fold-toggle:visible').all()) {
            const before = (await button.boundingBox()).width;
            await button.click();
            assert(Math.abs((await button.boundingBox()).width - before) < 1, `Mobile click width ${route}`);
            await button.click();
          }
          if (await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)) { await page.screenshot({path: dir + '/width-overflow.png'}); console.log(await page.locator('main *').evaluateAll(es => es.filter(e => e.getBoundingClientRect().right > innerWidth + 1 && e.checkVisibility()).slice(0,10).map(e => ({tag:e.tagName,cls:e.className,width:e.getBoundingClientRect().width,text:e.textContent.slice(0,65)})))); }
          assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `Overflow ${route} ${width}`);
          if (['/tareas','/tareas/actualizar','/validaciones'].includes(route) && width !== 768) await page.screenshot({ path: `${dir}/width-${engine}-${route.split('/').pop()}-${width}.png` });
          results.push({engine, route, width, summaries: checks.length});
        }
      }
    } finally { await browser.close(); }
  }
  fs.writeFileSync(dir + '/width-results.json', JSON.stringify({ results, failures }, null, 2));
  assert.equal(failures.length, 0, JSON.stringify(failures.slice(0, 8), null, 2));
  for (const route of ['/tareas','/tareas/actualizar','/validaciones']) assert(results.some(r => r.route === route && r.summaries > 0), `Missing real disclosure fixture ${route}`);
  console.log(`PASS ${results.length} page/width cases, ${results.reduce((n,r)=>n+r.summaries,0)} disclosure measurements, real clicks and no overflow`);
})().catch(e => { console.error(e.message); process.exitCode = 1; });


