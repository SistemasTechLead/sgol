const { chromium, webkit } = require('playwright-core');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const out = process.env.SGOL_CARRYOVER_BROWSER_OUTPUT;
(async () => {
  let checks = 0;
  for (const [engine, type] of Object.entries({ chromium, webkit })) {
    const browser = await type.launch();
    const page = await browser.newPage({ ignoreHTTPSErrors: true });
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    await page.goto('https://localhost:7291/mi-trabajo/arrastre');
    await page.locator('#postponement-form').waitFor();
    if (engine === 'chromium') {
      await page.locator(`input[name="Selected"][value="${process.argv[2]}"]`).check();
      await page.locator('[name="PostponeExplanation"]').fill('Prueba de autorización automática desde navegador');
      await Promise.all([page.waitForNavigation(), page.getByRole('button', { name: 'Preparar petición', exact: true }).click()]);
    }
    for (const width of [320, 768, 1440]) for (const theme of ['light', 'dark', 'special']) {
      await page.setViewportSize({ width, height: 1000 });
      await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
      const notice = page.getByText('No requiere otra aprobación en Supervisión.', { exact: false });
      if (out) { fs.mkdirSync(out, { recursive: true }); fs.writeFileSync(path.join(out, `${engine}-page.html`), await page.content()); }
      await notice.waitFor({ state: 'visible' });
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 2);
      assert(!overflow, `${engine}/${width}/${theme}: page overflow`);
      assert.equal(await page.locator('.encabezado-aplicacion').count(), 0, 'operational top header removed');
      if (width <= 768) {
        await page.locator('button.workspace-menu').click();
        const dialog = page.locator('#navegacion-movil');
        assert(await dialog.isVisible());
        const account = dialog.locator('.sidebar-account');
        assert(await account.locator('.sidebar-account__identity').isVisible());
        await account.locator('summary').click();
        assert(await account.getByRole('button', { name: 'Cerrar sesión' }).isVisible());
        await page.keyboard.press('Escape');
        const closed = page.evaluate(() => new Promise(resolve => document.querySelector('#navegacion-movil').addEventListener('close', () => requestAnimationFrame(() => resolve()), { once: true })));
        await dialog.getByRole('button', { name: 'Cerrar navegación' }).click();
        await closed;
        assert(!(await dialog.isVisible()));
        await page.waitForFunction(() => document.activeElement?.matches('button.workspace-menu'));
      } else {
        const resize = page.locator('[data-sidebar-resize]');
        await resize.focus(); await page.keyboard.press('Home');
        const account = page.locator('.barra-lateral > .sidebar-account');
        assert(!(await account.locator('.sidebar-account__identity').isVisible()));
        assert(await account.locator('.avatar-sesion').isVisible());
        await resize.focus(); await page.keyboard.press('End');
        assert(await account.locator('.sidebar-account__identity').isVisible());
        await account.locator('summary').click();
        assert(await account.getByRole('button', { name: 'Cerrar sesión' }).isVisible());
        await page.keyboard.press('Escape');
      }
      if (engine === 'chromium') {
        const button = page.getByRole('button', { name: 'Confirmar y aplicar arrastre', exact: true });
        await button.focus();
        try { await page.waitForFunction(() => document.activeElement?.matches('[data-postponement-submit]'), null, { timeout: 2000 }); }
        catch (error) {
          console.log(JSON.stringify({ engine, width, theme, focus: await page.evaluate(() => ({active: document.activeElement?.outerHTML, dialogs: [...document.querySelectorAll('dialog')].map(d => ({id: d.id, open:d.open})), button: document.querySelector('[data-postponement-submit]')?.outerHTML, inert:[...document.querySelectorAll('[inert]')].map(e=>e.outerHTML.slice(0,200))})) }));
          throw error;
        }
      }
      if (out) { fs.mkdirSync(out, { recursive: true }); await page.screenshot({ path: path.join(out, `${engine}-${width}-${theme}.png`), fullPage: true }); }
      checks++;
    }
    if (engine === 'chromium') {
      await Promise.all([page.waitForNavigation(), page.getByRole('button', { name: 'Confirmar y aplicar arrastre', exact: true }).press('Enter')]);
      assert(await page.getByRole('status').filter({ hasText: 'Petición autorizada automáticamente' }).isVisible());
      checks++;
    }
    assert.deepEqual(errors, []);
    await browser.close();
  }
  console.log(`PASS ${checks} automatic carryover browser checks`);
})().catch(e => { console.error(e); process.exit(1); });
