const { chromium, webkit } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const dir = path.resolve(process.argv[2]);
const origin = fs.readFileSync(path.join(dir, 'origin.txt'), 'utf8').trim();
assert.equal(origin, 'https://localhost:7284');
(async () => {
    for (const [engine, type] of Object.entries({ chromium, webkit })) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        try {
            const page = await browser.newPage({ ignoreHTTPSErrors: true, storageState: path.join(dir, 'state-alex.json'), viewport: { width: 1280, height: 900 } });
            await page.goto(origin + '/tareas');
            const repetitions = page.locator('[name="Filter.Repetitions"]');
            await repetitions.locator('xpath=ancestor::details[1]/summary').click();
            await repetitions.fill('0');
            await repetitions.locator('xpath=ancestor::details[1]/summary').click();
            await page.getByRole('button', { name: 'Aplicar filtros', exact: true }).click();
            assert(await repetitions.isVisible(), 'Invalid collapsed field revealed');
            assert(await repetitions.evaluate(e => e === document.activeElement && !e.validity.valid));
            await repetitions.fill('');
            const search = page.locator('input[name="Search"]');
            await search.locator('xpath=ancestor::details[1]/summary').click();
            await search.fill('TextoLargo'.repeat(45));
            await search.locator('xpath=ancestor::details[1]/summary').click();
            assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Long summaries wrap');
            await page.evaluate(() => document.documentElement.style.fontSize = '32px');
            await page.waitForFunction(() => !document.body.classList.contains('desktop-workspace'));
            assert.equal(await search.inputValue(), 'TextoLargo'.repeat(45), '200% text preserves input');
            await page.evaluate(() => document.documentElement.style.fontSize = '');
            console.log(`${engine}: invalid field and long/200% text passed`);

            await page.goto(origin + '/reportes');
            const range = page.locator('[name="F.Range"]');
            await range.locator('xpath=ancestor::details[1]/summary').click();
            await range.selectOption('custom');
            for (const [name, value] of [['From', '2026-11-01'], ['To', '2026-11-03']]) {
                const date = page.locator(`[name="F.${name}"]`);
                await date.locator('xpath=ancestor::details[1]/summary').click(); await date.fill(value);
            }
            await page.getByRole('button', { name: 'Generar reporte', exact: true }).click();
            await page.waitForURL(url => url.searchParams.get('F.Range') === 'custom');
            assert.equal(await page.locator('#report-filters [name="F.From"]').inputValue(), '2026-11-01');
            assert.equal(await page.locator('#report-filters [name="F.To"]').inputValue(), '2026-11-03');
            assert(await page.locator('#report-result').isVisible());
            console.log(`${engine}: custom report dates passed`);

            const initialRefresh = page.waitForResponse(r => r.url().includes('handler=Refresh'));
            await page.goto(origin + '/tablero');
            await initialRefresh;
            await page.waitForFunction(() => document.querySelector('#live-board').getAttribute('aria-busy') === 'false');
            const select = page.locator('#board-task');
            await select.locator('xpath=ancestor::details[1]/summary').click();
            await select.focus();
            const option = await select.locator('option').evaluateAll(options => options.find(o => o.value.startsWith('state:')).value);
            const response = page.waitForResponse(r => r.url().includes('handler=Refresh'));
            await select.selectOption(option); assert.equal((await response).status(), 200);
            await page.waitForFunction(value => document.querySelector('#board-task').value === value && document.querySelector('#live-board').getAttribute('aria-busy') === 'false', option);
            assert(await select.isVisible(), 'Live refresh keeps disclosure open');
            assert(await select.evaluate(e => document.activeElement === e), 'Live refresh keeps focus');
            assert.equal(await page.locator('h1:visible').count(), 1);
            await page.keyboard.press('Escape');
            assert(await select.locator('xpath=ancestor::details[1]/summary').evaluate(e => !e.parentElement.open && document.activeElement === e));
            await page.screenshot({ path: path.join(dir, `board-refresh-${engine}.png`) });
            console.log(`PASS ${engine}: native invalid-field focus, long text, 200% text, report custom dates GET, live-board refresh/open/focus.`);
        } finally { await browser.close(); }
    }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
