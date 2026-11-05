const { chromium, webkit } = require('playwright-core');
const assert = require('node:assert/strict');
const fs = require('node:fs');

(async () => {
  const base = process.env.SGOL_CALENDAR_TEST_URL || 'https://localhost:7269';
  assert.ok(base.startsWith('https://localhost:'), 'Use the isolated Razor fixture');
  const folder = '.local/automatic-calendar-20261105';
  fs.mkdirSync(folder, { recursive: true });
  const kind = process.env.SGOL_CALENDAR_TEST_KIND || 'SEMESTRAL';
  let processUrl, planUrl;
  for (const [engine, type] of [['chromium', chromium], ['webkit', webkit]]) {
    const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await context.newPage();
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    await page.goto(base + '/tareas/procesos');
    processUrl = await page.getByRole('link', { name: `Calendario automático ${kind}`, exact: true }).getAttribute('href');
    await page.goto(base + processUrl);
    planUrl = await page.getByRole('link', { name: /Abrir Calendario anual/ }).first().getAttribute('href');
    for (const width of [320, 768, 1440]) for (const theme of ['light', 'dark', 'special']) {
      await page.setViewportSize({ width, height: 950 });
      await page.evaluate(theme => localStorage.setItem('sgol.appearance.theme', theme), theme);
      await page.goto(base + planUrl);
      assert.equal(await page.locator('html').getAttribute('data-theme'), theme);
      assert.match(await page.locator('body').innerText(), /Calendario de 12 meses/);
      assert.ok(await page.getByRole('button', { name: 'Reintentar fechas pendientes' }).count());
      assert.equal(await page.getByRole('button', { name: 'Reintentar fechas pendientes' }).isDisabled(), true);
      await page.getByRole('button', { name: 'Reprogramar fecha', exact: true }).first().focus();
      assert.equal(await page.evaluate(() => document.activeElement.textContent.trim()), 'Reprogramar fecha');
      await page.keyboard.press('Tab');
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), true);
      await page.evaluate(() => scrollTo(0, 0));
      await page.screenshot({ path: `${folder}/${engine}-${width}-${theme}.png`, fullPage: true });
      console.log(`PASS ${engine} ${width} ${theme}: pending calendar, keyboard and no overflow`);
    }
    assert.deepEqual(errors, []);
    if (engine === 'chromium') {
      // Real antiforgery: a forged POST cannot trigger a retry.
      const id = new URL(base + planUrl).searchParams.get('planId');
      const response = await context.request.post(base + '/planificacion/procesos?handler=RetryAutomatic', { form: { planId: id } });
      assert.equal(response.status(), 400);
      const first = page.locator('form[action*="RescheduleAutomatic"]').first();
      const before = await first.locator('[name="revision"]').inputValue();
      await first.locator('[name="day"]').fill('2026-12-28');
      await Promise.all([page.waitForURL(/planId=/), first.getByRole('button', { name: 'Reprogramar fecha' }).click()]);
      await page.waitForLoadState('networkidle');
      assert.match(await page.locator('body').innerText(), /Fecha original:/);
      assert.notEqual(await page.locator('form[action*="RescheduleAutomatic"]').first().locator('[name="revision"]').inputValue(), before);
      console.log('PASS real POST reschedules pending date and retains original date; missing CSRF rejected');
    }
    await context.close(); await browser.close();
  }
  const browser = await chromium.launch({ channel: 'msedge' });
  const page = await browser.newPage({ ignoreHTTPSErrors: true, viewport: { width: 1440, height: 950 } });
  await page.goto(base + processUrl);
  await page.getByRole('button', { name: 'Activar versión publicada', exact: true }).click();
  await page.waitForLoadState('networkidle');
  assert.ok(await page.getByRole('button', { name: 'Desactivar nuevos inicios', exact: true }).count());
  await page.getByRole('link', { name: /Abrir Calendario anual/ }).first().click();
  await page.waitForLoadState('networkidle');
  if (await page.getByRole('button', { name: 'Reintentar fechas pendientes' }).count()) {
    // Reusing the fixture after other browser runs can occupy 28 December.
    // Verify the conflict is exposed, then recover through the real form.
    assert.match(await page.locator('.alerta--aviso').first().innerText(), /Etapa/);
    const pending = page.locator('form[action*="RescheduleAutomatic"]').first();
    await pending.locator('[name="day"]').fill('2026-12-29');
    await pending.getByRole('button', { name: 'Reprogramar fecha' }).click();
    await page.waitForLoadState('networkidle');
    console.log('PASS assigned calendar exposes a capacity conflict and recovers by manual rescheduling');
  }
  assert.equal(await page.getByRole('button', { name: 'Reintentar fechas pendientes' }).count(), 0);
  assert.ok(await page.getByRole('link', { name: new RegExp(`Abrir Calendario automático ${kind}`) }).count() >= 1);
  await page.screenshot({ path: `${folder}/assigned-desktop.png`, fullPage: true });
  console.log('PASS real activation form automatically assigns the saved calendar and exposes process executions');
  await browser.close();
})().catch(error => { console.error(error); process.exit(1); });
