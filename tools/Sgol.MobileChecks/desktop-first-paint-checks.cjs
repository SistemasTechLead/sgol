// Real Razor pages; intercept only the candidate CSS and hold the enhancement
// script until the initial header has been checked. No operational writes.
const { chromium, webkit } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const dir = path.resolve(process.argv[2]);
const origin = fs.readFileSync(path.join(dir, 'origin.txt'), 'utf8').trim();
assert.equal(origin, 'https://localhost:7284');
const css = path.resolve(__dirname, '../../src/Sgol.Web/wwwroot/css/desktop-workspace.css');
(async () => {
    for (const [engine, type] of Object.entries({ chromium, webkit })) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        try {
            const page = await browser.newPage({ ignoreHTTPSErrors: true,
                storageState: path.join(dir, 'state-alex.json'), viewport: { width: 1440, height: 900 } });
            await page.route('**/css/desktop-workspace.css*', r => r.fulfill({ path: css, contentType: 'text/css' }));
            for (const route of ['/mi-trabajo', '/tareas', '/validaciones', '/tablero']) {
                let release, intercepted;
                const held = new Promise(resolve => release = resolve);
                const requested = new Promise(resolve => intercepted = resolve);
                await page.route('**/js/desktop-workspace.js*', async r => { intercepted(); await held; await r.continue(); });
                const navigation = page.goto(origin + route);
                navigation.catch(() => {});
                await Promise.race([requested, navigation.then(() => { throw new Error(`Enhancement not requested: ${page.url()}`); })]);
                try {
                    await page.locator('body[data-desktop-workspace="true"]').waitFor();
                    await page.locator('.desktop-screen-title').waitFor({ state: 'visible' });
                    assert.equal(await page.locator('body.desktop-workspace').count(), 0);
                    assert(await page.locator('.desktop-screen-title').isVisible(), `${engine} initial title ${route}`);
                    assert.equal(await page.locator('.encabezado-aplicacion__marca').isVisible(), false);
                    assert.equal(await page.locator('h1:visible').count(), 1);
                    for (const theme of ['light', 'dark', 'special']) {
                        await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
                        assert(await page.locator('.desktop-screen-title').isVisible());
                    }
                    // WebKit waits for document load to settle fonts; this test
                    // intentionally holds a script, so capture this phase in Edge.
                    if (route === '/tareas' && engine === 'chromium') await page.screenshot({ path: path.join(dir, `first-paint-${engine}.png`) });
                } finally { release(); }
                await navigation;
                await page.unroute('**/js/desktop-workspace.js*');
                assert.equal(await page.locator('h1:visible').count(), 1);
                for (const width of [320, 768, 1024, 1440]) {
                    await page.setViewportSize({ width, height: 900 });
                    await page.evaluate(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))));
                    assert.equal(await page.locator('h1:visible').count(), 1, `${route} width ${width}`);
                }
                await page.emulateMedia({ media: 'print' });
                await page.evaluate(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))));
                assert.equal(await page.locator('.desktop-screen-title').isVisible(), false);
                await page.emulateMedia({ media: 'screen' });
            }
            console.log(`PASS ${engine}: initial header with enhancement held, 4 real routes, 3 themes, 4 widths, print.`);
        } finally { await browser.close(); }
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
