const { chromium, webkit } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
(async () => {
    const out = process.argv[2]; fs.mkdirSync(out, { recursive: true });
    const results = [];
    for (const [name, engine] of [['chromium', chromium], ['webkit', webkit]]) {
        const browser = await engine.launch(name === 'chromium' ? { channel: 'msedge' } : {});
        try {
            const page = await browser.newPage({ ignoreHTTPSErrors: true });
            for (const width of [320, 390, 768, 1440]) for (const theme of ['light', 'dark', 'special']) {
                await page.setViewportSize({ width, height: 900 });
                await page.goto('https://localhost:7257/planificacion');
                await page.evaluate(t => { localStorage.setItem('sgol-theme', t); document.documentElement.dataset.theme = t; }, theme);
                const nav = page.locator('nav[data-section="planning"]');
                await nav.getByRole('link', { name: 'Operaciones semanales', exact: true }).focus();
                await Promise.all([page.waitForURL('**/planificacion?mode=operations'), page.keyboard.press('Enter')]);
                await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
                assert.equal(await nav.locator('[aria-current="page"]').innerText(), 'Operaciones semanales');
                assert.equal(await page.locator('#direction-create-form').count(), 0);
                await page.reload();
                assert.equal(await nav.locator('[aria-current="page"]').innerText(), 'Operaciones semanales');
                await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
                if (width === 390 || width === 1440) await page.screenshot({ path: `${out}/${name}-${width}-${theme}.png` });
                await nav.getByRole('link', { name: 'Planes', exact: true }).click();
                assert.equal(await nav.locator('[aria-current="page"]').innerText(), 'Planes');
                await page.getByRole('link', { name: 'Consultar', exact: true }).first().click();
                assert.equal(await nav.locator('[aria-current="page"]').innerText(), 'Planes');
                results.push({ engine: name, width, theme });
            }
        } finally { await browser.close(); }
    }
    fs.writeFileSync(`${out}/results.json`, JSON.stringify(results, null, 2));
    console.log(`PASS ${results.length} real-page navigation cases: keyboard, reload, list and detail`);
})().catch(e => { console.error(e); process.exit(1); });
