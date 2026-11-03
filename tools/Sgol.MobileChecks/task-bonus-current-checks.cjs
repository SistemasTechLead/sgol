const { chromium, webkit } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async () => {
  const dir = process.argv[2], fixture = JSON.parse(fs.readFileSync(dir + '/fixture.json', 'utf8'));
  for (const [engine, type] of [['chromium', chromium], ['webkit', webkit]]) {
    const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
    const page = await browser.newPage({ ignoreHTTPSErrors: true, viewport: { width: 375, height: 900 } });
    await page.request.post(fixture.origin + '/__test/clock/2/10');
    await page.goto(fixture.origin + '/mi-trabajo?assignedTask=' + fixture.tasks[4]);
    const draft = page.locator('[data-assigned-task-submit] [name="conclusion"]');
    await draft.fill('Borrador conservado al cambiar de semana');
    let navigations = 0; page.on('framenavigated', f => { if (f === page.mainFrame()) navigations++; });
    await page.request.post(fixture.origin + '/__test/clock/9/10');
    await page.evaluate(() => window.dispatchEvent(new Event('focus')));
    await page.waitForFunction(() => document.querySelector('[data-task-bonus]').dataset.week === '2026-11-09');
    assert.equal(navigations, 0);
    assert.equal(await draft.inputValue(), 'Borrador conservado al cambiar de semana');
    const gauge = page.locator('[data-task-bonus]');
    assert((await gauge.locator('[data-bonus-period]').textContent()).includes('09/11/2026'));
    assert((await gauge.locator('[data-bonus-cutoff]').textContent()).includes('16/11/2026'));
    assert.equal(await gauge.locator('[data-bonus-first-week]').isVisible(), false);
    assert.equal(await gauge.locator('[role="meter"]').count(), 0, 'no evaluable tasks do not become a fabricated percentage');
    for (const week of ['2026-10-26', 'invalid']) {
      const response = await page.request.get(fixture.origin + '/mi-trabajo?handler=Bonus&BonusWeek=' + week);
      assert.equal(response.status(), 200);
      assert.equal((await response.json())[0].period.startsOn, '2026-11-09');
    }
    await page.request.post(fixture.origin + '/__test/clock/2/10');
    console.log('PASS ' + engine + ': week rollover, draft retained, old/invalid URL, empty state');
    await browser.close();
  }
})().catch(e => { console.error(e); process.exit(1); });
