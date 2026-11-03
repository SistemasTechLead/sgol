const { chromium, webkit } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async () => {
    const dir = process.argv[2], fixture = JSON.parse(fs.readFileSync(`${dir}/fixture.json`, 'utf8'));
    const results = [];
    for (const [index, [engine, type]] of [['chromium', chromium], ['webkit', webkit]].entries()) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 375, height: 900 } });
        const page = await context.newPage(), task = fixture.tasks[2 + index];
        await page.request.post(`${fixture.origin}/__test/clock/2/11`);
        await page.goto(`${fixture.origin}/validaciones?task=${task}`);
        await page.locator('[data-supervision-review] [name="result"][value="no"]').check();
        await page.locator('[data-supervision-review] [name="comment"]').fill('Corregir la entrega para comprobar el resultado real');
        await page.locator('[data-supervision-review] [name="action"]').selectOption('MISMA_PERSONA');
        await Promise.all([page.waitForNavigation(), page.locator('[data-supervision-review] [value="preview"]').click()]);
        await Promise.all([page.waitForNavigation(), page.locator('[data-correction-apply]').click()]);
        await page.goto(`${fixture.origin}/mi-trabajo`);
        const before = Number(await page.locator('[data-task-bonus]').getAttribute('data-score'));
        assert(before < 100, 'real return deducts full points');
        const correction = page.locator('[data-work-task][data-delivered="false"]').filter({ hasText: `Tarea de bono ${2 + index}` }).filter({ hasText: 'Corrección de la entrega' });
        await correction.locator('[data-assigned-task]').click();
        await page.request.post(`${fixture.origin}/__test/clock/2/12`);
        await page.locator('[data-assigned-task-submit] [name="conclusion"]').fill('Corrección enviada dentro del día evaluado');
        await Promise.all([page.waitForNavigation(), page.locator('[data-assigned-task-submit] button[type="submit"]').click()]);
        const after = Number(await page.locator('[data-task-bonus]').getAttribute('data-score'));
        assert(after > before && after === 100, 'real correction restores the actual score');
        assert((await page.locator('#plan-tasks').innerText()).includes('Porcentaje actualizado:'));
        // The operational return remains at the task; animation waits for the gauge to enter view.
        const dialog = page.locator('dialog[open]');
        if (await dialog.count()) await page.keyboard.press('Escape');
        await page.locator('[data-task-bonus]').scrollIntoViewIfNeeded();
        await page.waitForFunction(() => document.querySelector('[data-task-bonus]').classList.contains('task-bonus--animate'));
        await page.screenshot({ path: `${dir}/${engine}-correction-return.png` });
        results.push({ engine, before, after, scenario: 'real review preview/apply and corrected delivery animate only real change', status: 'approved' });
        await context.close(); await browser.close();
    }
    fs.writeFileSync(`${dir}/browser-correction-results.json`, JSON.stringify(results, null, 2));
    console.log(`PASS ${results.length} correction browser workflows`);
})().catch(e => { console.error(e); process.exit(1); });
