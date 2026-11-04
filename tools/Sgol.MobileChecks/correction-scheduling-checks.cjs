const { chromium, webkit } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async () => {
    const dir = process.argv[2];
    const ready = fs.readFileSync(`${dir}/${process.argv[3] || 'http.log'}`, 'utf8').match(/CORRECTION_UI_READY (\S+) worker=(\S+)/);
    assert(ready, 'Isolated HTTP test host must be ready');
    const url = ready[1], origin = new URL(url).origin;
    const results = [];
    for (const [engine, type] of [['chromium', chromium], ['webkit', webkit]]) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        try {
            const page = await browser.newPage({ ignoreHTTPSErrors: true });
            await page.goto(url);
            const form = page.locator('[data-supervision-review]');
            const taskName = await page.locator('#supervision-task-title').innerText();
            await form.locator('[name="result"][value="no"]').check();
            assert.equal(await form.locator('[data-correction-mode]').inputValue(), 'ASAP', 'Default is Cuanto antes');
            await form.locator('[data-review-comment]').fill('Corregir la evidencia y conservar este comentario largo. '.repeat(12));
            await form.locator('[data-correction-mode]').selectOption('CUSTOM');
            await form.locator('[name="Schedule.From"]').fill('2026-11-04');
            for (const theme of ['light', 'dark', 'special']) {
                await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
                for (const width of [320, 390, 768, 1280]) {
                    await page.setViewportSize({ width, height: 900 });
                    await page.waitForTimeout(120);
                    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'No document overflow');
                    assert(await page.locator('[data-supervision-detail]').evaluate(e => e.scrollWidth <= e.clientWidth + 1), 'No dialog overflow');
                    assert(await form.locator('[name="Schedule.From"]').isVisible());
                    if (width === 390 || width === 1280) await page.screenshot({ path: `${dir}/correction-${engine}-${theme}-${width}.png` });
                    results.push({ engine, theme, width, individual: true });
                }
            }
            await page.setViewportSize({ width: 320, height: 900 });
            await page.evaluate(() => document.documentElement.style.fontSize = '200%');
            assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
            await page.evaluate(() => document.documentElement.style.fontSize = '');
            await form.locator('[data-correction-mode]').selectOption('ASAP');
            assert(await form.locator('[name="Schedule.From"]').isDisabled());
            await form.locator('[data-correction-mode]').selectOption('CUSTOM');
            await form.locator('[name="Schedule.From"]').focus();
            await page.keyboard.press('Tab');
            assert(await page.evaluate(() => document.activeElement.closest('form')?.hasAttribute('data-supervision-review')));
            await page.locator('[name="Schedule.From"]').fill('2026-11-01');
            await Promise.all([page.waitForNavigation(), form.getByRole('button', { name: 'Validar', exact: true }).click()]);
            assert.equal(await page.locator('[data-correction-preview], [data-correction-apply]').count(), 0);
            assert(await page.locator('dialog [data-correction-error]').isVisible());
            assert(await page.locator('dialog [data-correction-error]').evaluate(e => e === document.activeElement));
            assert((await page.locator('[data-review-comment]').inputValue()).startsWith('Corregir la evidencia'));
            assert.equal(await page.locator('[data-correction-mode]').inputValue(), 'CUSTOM');
            await page.keyboard.press('Escape');
            assert.equal(await page.locator('[data-supervision-detail]').evaluate(e => e.open), false);
            await page.goto(`${origin}/validaciones?Validation=true&Section=PISO_VENTAS&PISO_VENTAS.search=${encodeURIComponent(taskName)}`);
            const card = page.locator('[data-validation-card]').filter({ hasText: taskName });
            assert.equal(await card.count(), 1);
            await card.locator('[data-validation-choice][value="no"]').check();
            assert.equal(await card.locator('[data-correction-mode]').inputValue(), 'ASAP', 'Group default is Cuanto antes');
            await card.locator('textarea').fill('Revisión de grupo');
            await card.locator('[data-correction-mode]').selectOption('CUSTOM');
            await card.locator('input[name$=".From"]').fill('2026-11-01');
            for (const width of [320, 390, 768, 1280]) {
                await page.setViewportSize({ width, height: 900 });
                assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Group reflows');
                assert(await card.locator('input[name$=".From"]').isEnabled());
            }
            await Promise.all([page.waitForNavigation(), page.locator('[data-validation-save]').click()]);
            assert.equal(await page.locator('[data-correction-preview]').count(), 0);
            assert.equal(await page.locator('[data-validation-save]').innerText(), 'Validar');
            assert(await page.locator('[data-correction-error]').isVisible());
            assert.equal(await card.locator('textarea').inputValue(), 'Revisión de grupo');
            await page.goto(origin + ready[2]);
            assert(await page.locator('[data-assigned-task-detail]').getByText('Comentario HTTP conservado', { exact: true }).first().isVisible());
            const noJs = await browser.newPage({ ignoreHTTPSErrors: true, javaScriptEnabled: false });
            await noJs.goto(url);
            assert(await noJs.locator('noscript p').first().isVisible());
            await noJs.close();
            results.push({ engine, group: true, errorFocus: true, directReview: true, keyboard: true, myWork: true, errorPreservesMode: true, noJsDiagnosis: true });
        } finally { await browser.close(); }
    }
    fs.writeFileSync(`${dir}/browser-results.json`, JSON.stringify(results, null, 2));
    console.log(`PASS ${results.length} browser cases`);
})().catch(e => { console.error(e); process.exitCode = 1; });
