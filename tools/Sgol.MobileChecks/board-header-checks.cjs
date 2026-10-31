const { chromium, webkit } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');

(async () => {
    const dir = process.argv[2];
    const origin = fs.readFileSync(`${dir}/origin.txt`, 'utf8').trim();
    const results = [];
    for (const [engine, type] of [['chromium', chromium], ['webkit', webkit]]) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        try {
            const page = await browser.newPage({ ignoreHTTPSErrors: true, storageState: `${dir}/state-alex.json` });
            assert.equal((await page.goto(`${origin}/tablero`)).status(), 200);
            await page.locator('.board-heading .section-navigation').waitFor();
            const form = page.locator('.board-plan');
            const values = () => form.evaluate(e => [...new FormData(e)]);
            const original = await values();
            for (const theme of ['light', 'dark', 'special']) {
                await page.evaluate(theme => document.documentElement.dataset.theme = theme, theme);
                for (const width of [320, 390, 430, 768]) {
                    await page.setViewportSize({ width, height: 844 });
                    await page.waitForTimeout(150);
                    const tabs = await page.locator('.board-heading .section-navigation a').all();
                    assert.equal(tabs.length, 2);
                    const [a, b] = await Promise.all(tabs.map(t => t.boundingBox()));
                    assert(Math.abs(a.y - b.y) < 1, 'Tabs must share a row');
                    const filters = page.locator('[data-mobile-control="board-filters"]');
                    const f = await filters.boundingBox();
                    assert(f.y >= Math.max(a.y + a.height, b.y + b.height), 'Filters below navigation');
                    const people = page.locator('[data-mobile-control="board-people"]');
                    if (await people.count()) assert(Math.abs((await people.boundingBox()).y - f.y) < 1);
                    await filters.focus();
                    await page.keyboard.press('Enter');
                    await form.waitFor({ state: 'visible' });
                    assert((await form.boundingBox()).y >= f.y + f.height, 'Open form below disclosures');
                    assert.deepEqual(await values(), original);
                    await page.keyboard.press('Enter');
                    assert.equal(await filters.getAttribute('aria-expanded'), 'false');
                    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
                    if (width === 390) await page.screenshot({ path: `${dir}/board-header-${engine}-${theme}.png` });
                    results.push({ engine, theme, width, passed: true });
                }
            }
            await page.setViewportSize({ width: 320, height: 844 });
            await page.evaluate(() => document.documentElement.style.fontSize = '32px');
            await page.waitForTimeout(150);
            const enlarged = await page.locator('.board-heading .section-navigation a').evaluateAll(es => es.map(e => e.getBoundingClientRect().top));
            assert(Math.abs(enlarged[0] - enlarged[1]) < 1);
            assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
            await page.evaluate(() => document.documentElement.style.fontSize = '');
            await page.setViewportSize({ width: 1440, height: 900 });
            await page.waitForTimeout(150);
            assert.equal(await page.locator('[data-mobile-control="board-filters"]').isVisible(), false);
            assert(await form.isVisible());
            assert.deepEqual(await values(), original);
        } finally { await browser.close(); }
    }
    fs.writeFileSync(`${dir}/board-header-results.json`, JSON.stringify(results, null, 2));
    console.log(`PASS ${results.length} mobile cases, keyboard disclosures, form preservation and desktop return`);
})().catch(error => { console.error(error.message); process.exit(1); });
