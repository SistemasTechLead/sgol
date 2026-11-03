// Isolated interaction regression; does not replace Razor/service integration.
const { chromium, webkit } = require('playwright');
const fs = require('fs');
const path = require('path');
const assert = require('assert/strict');
const root = path.resolve(__dirname, '../../src/Sgol.Web/wwwroot');
const row = (id, eligible, approve = false, delivered = false) => `
<section class="postponement-item" id="${id}" data-item-reassign="${eligible}" data-item-approve="${approve}" data-item-move="${!delivered}" data-item-delivered="${delivered}">
<input type="radio" name="selected-postponement"><h3>Tarea ${id}</h3><span data-item-person>Responsable</span>
${!eligible && !delivered ? `<div id="${id}-reassign-block" class="postponement-blocks"><div class="postponement-obstacle"><strong>Sin personas para reasignar</strong><div class="postponement-obstacle__content"><p>No hay otra persona activa elegible con la especialidad Administración dentro de tu alcance de supervisión.</p></div></div></div>` : ''}
<form data-item-editor hidden><select name="ItemDecision.Action" data-item-mode><option>APPROVE</option><option ${eligible ? '' : 'disabled'}>REASSIGN</option><option>RESCHEDULE</option><option>REJECT</option></select>
<div data-item-recipient><select name="ItemDecision.Recipient"><option value="">Selecciona</option>${eligible ? '<option value="other">Otra persona</option>' : ''}</select></div>
<div data-item-day><select data-item-day-mode data-requested-day="2026-11-05"><option value="same">Mismo día</option><option value="other">Otro día</option></select><label data-item-date-label><input name="ItemDecision.Day" value="2026-11-05"></label></div>
<textarea name="ItemDecision.Comment"></textarea><button type="button" data-item-close>Cerrar editor</button><button name="confirm" value="false">Revisar resultado</button></form></section>`;
(async () => {
  let cases = 0;
  for (const [name, engine] of Object.entries({ chromium, webkit })) {
    const browser = await engine.launch(name === 'chromium' ? { channel: 'msedge' } : {});
    try {
      for (const width of [320, 768, 1440]) for (const theme of ['light', 'dark', 'special']) {
        const page = await browser.newPage({ viewport: { width, height: 1000 } });
        await page.route('https://sgol.test/**', route => route.fulfill({ contentType: 'text/html', body: '<html><body></body></html>' }));
        await page.goto('https://sgol.test/');
        await page.setContent(`<html data-theme="${theme}"><body><div class="postponement-review-scope">${row('empty', false)}${row('eligible', true)}${row('clear', false, true)}${row('delivered', false, true, true)}<div data-postponement-toolbar><span data-postponement-selection></span>${['APPROVE','REASSIGN','RESCHEDULE','REJECT'].map(a => `<button data-item-action="${a}">${a}</button>`).join('')}</div></div></body></html>`);
        for (const css of ['tokens.css', 'postponement-review.css']) {
          const file = path.join(root, 'css', css);
          if (fs.existsSync(file)) await page.addStyleTag({ path: file });
        }
        await page.addScriptTag({ path: path.join(root, 'js/postponement-review.js') });
        const button = action => page.locator(`[data-item-action="${action}"]`);
        const select = id => page.locator(`#${id} [name="selected-postponement"]`).check();
        await select('empty');
        assert(await button('REASSIGN').isDisabled());
        assert(await button('APPROVE').isDisabled());
        assert(await button('RESCHEDULE').isEnabled());
        assert.equal(await button('REASSIGN').getAttribute('aria-describedby'), 'empty-reassign-block');
        await page.locator('#empty').evaluate(e => e.dispatchEvent(new CustomEvent('postponement-action', { detail: 'REASSIGN' })));
        assert(await page.locator('#empty form').isHidden());
        await button('RESCHEDULE').click();
        assert(await page.locator('#empty option[value="REASSIGN"], #empty option:has-text("REASSIGN")').isDisabled());
        // Reproduce a draft restored after its recipient stopped qualifying.
        await page.locator('#empty [data-item-mode]').evaluate(e => { e.value = 'REASSIGN'; e.dispatchEvent(new Event('change', { bubbles: true })); });
        assert(await page.locator('#empty button[name="confirm"]').isDisabled());
        assert.equal(await page.locator('#empty form').evaluate(e => e.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))), false);
        await select('eligible');
        assert(await button('REASSIGN').isEnabled());
        assert.equal(await button('REASSIGN').getAttribute('aria-describedby'), null);
        await button('REASSIGN').focus(); await page.keyboard.press('Enter');
        assert(await page.locator('#eligible [data-item-recipient]').isVisible());
        assert(await page.locator('#eligible button[name="confirm"]').isEnabled());
        await select('clear');
        assert(await button('APPROVE').isEnabled());
        assert(await button('REASSIGN').isDisabled());
        await select('delivered');
        assert(await button('REASSIGN').isDisabled());
        assert(await button('RESCHEDULE').isDisabled());
        assert(await button('REJECT').isEnabled());
        assert.equal(await button('APPROVE').textContent(), 'Cerrar sin cambios');
        await page.close(); cases++;
      }
    } finally { await browser.close(); }
  }
  console.log(`PASS ${cases} component cases: empty/eligible recipients, independent approval, delivered task, keyboard, stale draft and submit guard. Razor and database integration not exercised.`);
})().catch(error => { console.error(error); process.exitCode = 1; });
