const { chromium, webkit } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async () => {
    const dir = process.argv[2], fixture = JSON.parse(fs.readFileSync(`${dir}/fixture.json`, 'utf8'));
    const results = [];
    for (const [index, [engine, type]] of [['chromium', chromium], ['webkit', webkit]].entries()) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        const context = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 375, height: 900 } });
        const worker = await context.newPage(), supervisor = await context.newPage();
        await worker.request.post(`${fixture.origin}/__test/clock/2/10`);
        await supervisor.goto(`${fixture.origin}/supervision/bono-tareas`);
        const person = supervisor.locator(`[data-bonus-person="${fixture.person}"]`);
        const oldVersion = await person.locator('[data-task-bonus]').getAttribute('data-version');
        await person.locator('.bonus-detail > summary').click();
        await person.locator('[name="Reason"]').fill('Borrador que debe conservarse durante el refresco');
        const task = fixture.tasks[2 + index];
        await worker.bringToFront();
        await worker.goto(`${fixture.origin}/mi-trabajo?assignedTask=${task}`);
        const score = await worker.locator('[data-bonus-score]').textContent();
        await worker.locator('[data-assigned-task-submit] [name="conclusion"]').fill('Entrega desde navegador para comprobar bono');
        await Promise.all([worker.waitForNavigation(), worker.locator('[data-assigned-task-submit] button[type="submit"]').click()]);
        assert.equal(await worker.locator('[data-bonus-score]').textContent(), score);
        assert.equal(await worker.locator('.task-bonus--animate').count(), 0, 'delivery with unchanged score does not animate');
        assert(!(await worker.locator('#plan-tasks').innerText()).includes('calificación del bono sin cambios'), 'unchanged delivery does not show routine bonus notice');
        await supervisor.bringToFront();
        await supervisor.waitForFunction(({ id, old }) => document.querySelector(`[data-bonus-person="${id}"] [data-task-bonus]`).dataset.version !== old,
            { id: fixture.person, old: oldVersion }, { timeout: 9000 });
        assert.equal(await person.locator('[name="Reason"]').inputValue(), 'Borrador que debe conservarse durante el refresco');
        const deliveredRow = person.locator(`[data-bonus-rows] tr`).filter({ has: supervisor.locator(`a[href="/validaciones?task=${task}"]`) });
        if (await deliveredRow.locator('.mobile-record-toggle').count()) await deliveredRow.locator('.mobile-record-toggle').click();
        assert((await deliveredRow.innerText()).includes('Enviada a revisión'));
        results.push({ engine, scenario: 'real delivery, unchanged score, return notice, remote supervision refresh preserves form', status: 'approved' });
        await context.close(); await browser.close();
    }
    fs.writeFileSync(`${dir}/browser-flow-results.json`, JSON.stringify(results, null, 2));
    console.log(`PASS ${results.length} complete browser workflows`);
})().catch(e => { console.error(e); process.exit(1); });
