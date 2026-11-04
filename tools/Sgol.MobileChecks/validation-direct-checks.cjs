const { chromium, webkit } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async () => {
    const dir = process.argv[2];
    const results = [];
    for (const [engine, type] of [['chromium', chromium], ['webkit', webkit]]) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        try {
            const page = await browser.newPage({ ignoreHTTPSErrors: true });
            let dialogs = 0;
            page.on('dialog', async dialog => { dialogs++; await dialog.dismiss(); });
            await page.goto('https://localhost:7289/validaciones?Validation=true&Section=PISO_VENTAS');
            const save = page.locator('[data-validation-save]');
            assert.equal(await save.innerText(), 'Validar');
            assert(await save.isDisabled());
            const card = page.locator('[data-validation-card]').filter({ has: page.locator('[data-validation-choice]') }).last();
            await card.locator('[data-validation-choice][value="no"]').check();
            await card.locator('select[name$=".Action"]').selectOption('NO_SATISFECHA');
            await save.click();
            assert(await card.locator('textarea').evaluate(e => e.validity.valueMissing));
            await card.locator('textarea').fill('Comentario largo de validación directa. '.repeat(30));
            for (const theme of ['light', 'dark', 'special']) {
                await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
                for (const width of [320, 390, 768, 1280]) {
                    await page.setViewportSize({ width, height: 900 });
                    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
                    assert(await save.isEnabled());
                    results.push({ engine, theme, width });
                }
            }
            await save.scrollIntoViewIfNeeded();
            await page.screenshot({ path: `${dir}/validation-${engine}.png` });
            let posts = 0;
            page.on('request', request => { if (request.method() === 'POST') posts++; });
            await save.focus();
            await Promise.all([page.waitForNavigation(), page.keyboard.press('Enter')]);
            assert.equal(posts, 1);
            assert.equal(dialogs, 0);
            assert.equal(await page.locator('[data-correction-preview], [data-correction-apply]').count(), 0);
            assert(await page.getByText('Se guardó la revisión de 1 tareas.', { exact: true }).isVisible());
            assert.equal(await save.innerText(), 'Validar');
            assert(await save.isDisabled());
        } finally { await browser.close(); }
    }
    fs.writeFileSync(`${dir}/direct-browser-results.json`, JSON.stringify(results, null, 2));
    console.log(`PASS ${results.length} viewport/theme cases; two keyboard single-POST saves without confirmation`);
})().catch(error => { console.error(error); process.exitCode = 1; });
