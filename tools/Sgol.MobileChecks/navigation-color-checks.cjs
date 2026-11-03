// Real authenticated pages; read-only navigation regression for all three themes.
const { chromium, webkit } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const dir = process.argv[2];
(async () => {
  let cases = 0;
  for (const [engine, type] of Object.entries({ chromium, webkit })) {
    const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
    try {
      const page = await browser.newPage({ ignoreHTTPSErrors: true, storageState: dir + '/state-alex.json' });
      for (const route of ['/validaciones', '/tareas', '/planificacion']) {
        await page.goto('https://localhost:7286' + route);
        assert(!page.url().includes('/acceso'), 'Authenticated fixture required');
        for (const width of [375, 1440]) for (const theme of ['dark', 'light', 'special']) {
          await page.setViewportSize({ width, height: 960 });
          await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
          await page.mouse.move(0, 0);
          await page.evaluate(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))));
          const active = page.locator('main .section-navigation [aria-current="page"]');
          assert.equal(await active.count(), 1);
          const verify = async hover => {
            const colors = await active.evaluate((e, hover) => {
              const s = getComputedStyle(e), probe = document.createElement('span');
              document.body.append(probe);
              const token = name => { probe.style.color = `var(${name})`; return getComputedStyle(probe).color; };
              const result = { bg: s.backgroundColor, fg: s.color, expectedBg: token(hover ? '--color-acento-hover' : '--color-acento'), expectedFg: token('--color-acento-texto') };
              probe.remove(); return result;
            }, hover);
            assert.equal(colors.bg, colors.expectedBg, `${engine} ${route} ${width} ${theme} active background`);
            assert.equal(colors.fg, colors.expectedFg, `${engine} ${route} ${width} ${theme} active text`);
          };
          await verify(false);
          await active.hover(); await verify(true);
          await page.mouse.move(0, 0);
          await page.keyboard.press('Tab'); await active.focus();
          assert(await active.evaluate(e => document.activeElement === e && getComputedStyle(e).outlineStyle !== 'none'), [engine, route, width, theme, 'keyboard focus'].join(' '));
          await verify(false);
          assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
          if (route === '/validaciones' && theme === 'dark') await page.screenshot({ path: `${dir}/navigation-${engine}-${width}-dark.png` });
          cases++;
        }
      }
    } finally { await browser.close(); }
  }
  fs.writeFileSync(dir + '/navigation-results.json', JSON.stringify({ cases, states: ['active', 'hover', 'keyboard-focus'], passed: true }));
  console.log(`PASS ${cases} navigation cases: active/hover colors and keyboard focus`);
})().catch(e => { console.error(e.message); process.exitCode = 1; });


