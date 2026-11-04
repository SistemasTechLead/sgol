const { chromium, webkit } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async () => {
    const dir = process.argv[2], results = [];
    for (const [engine, type] of [['chromium', chromium], ['webkit', webkit]]) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        try {
            const page = await browser.newPage({ ignoreHTTPSErrors: true });
            let dialogs = 0, posts = 0;
            page.on('dialog', async d => { dialogs++; await d.dismiss(); });
            await page.goto('https://localhost:7289/validaciones?Validation=true&Section=PISO_VENTAS');
            const card = page.locator('[data-validation-card]').filter({ has: page.locator('[data-validation-choice]') }).last();
            const id = await card.locator('input[name$=".Id"]').inputValue();
            await page.goto(`https://localhost:7289/validaciones?task=${id}`);
            const form = page.locator('[data-supervision-review]'), save = form.getByRole('button', { name: 'Validar', exact: true });
            await form.locator('[name="result"][value="no"]').check();
            await save.click();
            assert(await form.locator('textarea').evaluate(e => e.validity.valueMissing));
            await form.locator('[name="action"]').selectOption('NO_SATISFECHA');
            await form.locator('textarea').fill('Comentario largo de revisión individual. '.repeat(30));
            for (const theme of ['light', 'dark', 'special']) {
                await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
                for (const width of [320, 390, 768, 1280]) {
                    await page.setViewportSize({ width, height: 900 });
                    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
                    assert(await page.locator('[data-supervision-detail]').evaluate(e => e.scrollWidth <= e.clientWidth + 1));
                    assert(await save.isEnabled());
                    results.push({ engine, theme, width });
                }
            }
            await save.scrollIntoViewIfNeeded();
            await page.screenshot({ path: `${dir}/individual-${engine}.png` });
            page.on('request', r => { if (r.method() === 'POST') posts++; });
            await save.focus();
            await Promise.all([page.waitForNavigation(), page.keyboard.press('Enter')]);
            assert.equal(posts, 1); assert.equal(dialogs, 0);
            assert.equal(await page.locator('[data-correction-preview], [data-correction-apply], [data-supervision-review]').count(), 0);
            assert(await page.getByText('Se guardó la revisión de la tarea.', { exact: true }).count());
            assert((await page.locator('[data-supervision-detail]').innerText()).includes('Comentario largo de revisión individual.'));
        } finally { await browser.close(); }
    }
    fs.writeFileSync(`${dir}/individual-browser-results.json`, JSON.stringify(results, null, 2));
    console.log(`PASS ${results.length} individual viewport/theme cases; single POST and persisted review in both engines`);
})().catch(e => { console.error(e); process.exitCode = 1; });
