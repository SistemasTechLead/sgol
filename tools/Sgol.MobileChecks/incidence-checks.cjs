const {chromium, webkit} = require('playwright');
const fs = require('fs'), path = require('path'), assert = require('assert/strict');
const output = path.resolve(__dirname, '../../.local/incidence-ui');
const fixture = JSON.parse(fs.readFileSync(path.join(output, 'fixture.json')));
(async () => {
  const results = [];
  for (const [name, engine] of Object.entries({chromium, webkit})) {
    const browser = await engine.launch(name === 'chromium' ? {channel:'msedge'} : {});
    const page = await browser.newPage({ignoreHTTPSErrors:true}); const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('response', response => { if (response.request().isNavigationRequest()) console.log(name, response.status(), response.url()); });
    const response = await page.goto(`${fixture.origin}/supervision/incidencias?Day=${fixture.day}`);
    assert.equal(response.status(), 200);
    assert.equal((await page.request.get(`${fixture.origin}/css/tokens.css`)).status(), 200);
    assert(await page.locator('.incidence-register').evaluate(el => !!(el.compareDocumentPosition(document.querySelector('.incidence-search')) & Node.DOCUMENT_POSITION_FOLLOWING)));
    assert.equal(await page.locator('#Day').count(), 1);
    await page.locator('#PersonId').selectOption(fixture.person);
    await page.locator('#Reason').fill('Ausencia sintética desde navegador; conservar evidencia e historial. ' + 'Motivo extenso. '.repeat(30));
    await page.getByRole('button', {name:'Revisar cambios', exact:true}).focus();
    await Promise.all([page.waitForNavigation(), page.keyboard.press('Enter')]);
    assert(await page.getByRole('button', {name:'Confirmar ausencia y cambios', exact:true}).count(), await page.locator('main').innerText());
    assert.deepEqual(await page.locator('[data-incidence-action]').evaluateAll(rows => rows.map(r => r.dataset.incidenceAction)), ['TRANSFER', 'RETIRE']);
    for (const width of [320, 768, 1440]) for (const theme of ['light','dark','special']) {
      await page.setViewportSize({width, height:1000});
      await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
      assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `overflow ${name}/${width}/${theme}`);
      const confirm = page.getByRole('button', {name:'Confirmar ausencia y cambios', exact:true});
      await confirm.focus(); assert(await confirm.evaluate(el => el === document.activeElement));
      await page.screenshot({path:path.join(output, `${name}-${width}-${theme}.png`), fullPage:true});
      results.push({engine:name,width,theme,state:'preview',status:'passed'});
    }
    // Tampering with the reviewed reason must invalidate the whole proposal.
    await page.locator('#Reason').fill('Motivo cambiado después de revisar la propuesta');
    assert(await page.getByRole('button', {name:'Confirmar ausencia y cambios',exact:true}).isDisabled());
    // Bypass the client guard deliberately to verify the server's fingerprint as well.
    await page.getByRole('button', {name:'Confirmar ausencia y cambios',exact:true}).evaluate(el => el.disabled = false);
    await Promise.all([page.waitForNavigation(), page.getByRole('button', {name:'Confirmar ausencia y cambios',exact:true}).click()]);
    assert(await page.getByRole('alert').count());
    await Promise.all([page.waitForNavigation(), page.getByRole('button', {name:'Revisar cambios',exact:true}).click()]);
    if (name === 'webkit') {
      await Promise.all([page.waitForNavigation(), page.getByRole('button', {name:'Confirmar ausencia y cambios',exact:true}).click()]);
      await page.getByRole('heading', {name:/Ausencia confirmada/}).waitFor({timeout:15000});
      if (!await page.getByRole('heading', {name:/Ausencia confirmada/}).count()) {
        await page.screenshot({path:path.join(output,'confirmation-failure.png'),fullPage:true});
        fs.writeFileSync(path.join(output,'confirmation-failure.html'),await page.content());
        throw new Error('Receipt missing: ' + await page.locator('[role="alert"]').allTextContents());
      }
      results.push({engine:name,scenario:'review, stale proposal, re-review, joint confirm, receipt',status:'passed'});
    }
    if (name === 'chromium') {
      await page.goto(`${fixture.origin}/supervision/incidencias?Day=${fixture.day}`);
      await page.locator('#PersonId').selectOption(fixture.conflictPerson);
      await page.locator('#Reason').fill('Transferencia revisada de evidencia protegida con sobrecarga explícita');
      await Promise.all([page.waitForNavigation(),page.getByRole('button',{name:'Revisar cambios',exact:true}).click()]);
      assert.equal(await page.getByRole('button',{name:'Confirmar ausencia y cambios',exact:true}).count(),0);
      assert(await page.locator('.incidence-task--decision').evaluate(el => !!(el.compareDocumentPosition(document.querySelector('[data-incidence-action="RETIRE"]')) & Node.DOCUMENT_POSITION_FOLLOWING)));
      const recipients = await page.locator('select[name$=".RecipientId"] option').evaluateAll(options => options.map(o => o.value));
      assert(!recipients.includes(fixture.ineligibleRecipient), 'Subcoordination cannot be selected for a sales-floor task');
      assert(recipients.includes(fixture.conflictRecipient), 'eligible recipient with shortage remains available for explicit overload');
      const transferOptions = page.locator('[data-incidence-transfer]');
      assert(await transferOptions.isHidden());
      assert(await page.locator('select[name$=".RecipientId"]').isDisabled());
      await page.locator('select[name$=".Action"]').selectOption('TRANSFER');
      assert(await transferOptions.isVisible());
      await page.locator('select[name$=".RecipientId"]').selectOption(fixture.conflictRecipient);
      await page.locator('input[name$=".AuthorizeOverload"][type="checkbox"]').check();
      await page.locator('select[name$=".Action"]').selectOption('RETIRE');
      assert(await transferOptions.isHidden());
      assert(await page.locator('select[name$=".RecipientId"]').isDisabled());
      assert(await page.locator('[data-incidence-form]').evaluate(form => ![...new FormData(form).keys()].some(k => k.endsWith('.RecipientId') || k.endsWith('.AuthorizeOverload'))));
      await page.locator('select[name$=".Action"]').focus();
      await page.keyboard.press('Tab');
      assert(await transferOptions.evaluate(el => !el.contains(document.activeElement)));
      await page.locator('select[name$=".Action"]').selectOption('TRANSFER');
      assert.equal(await page.locator('select[name$=".RecipientId"]').inputValue(), '');
      assert(!await page.locator('input[name$=".AuthorizeOverload"][type="checkbox"]').isChecked());
      await page.locator('select[name$=".RecipientId"]').selectOption(fixture.conflictRecipient);
      await page.locator('input[name$=".AuthorizeOverload"][type="checkbox"]').check();
      await page.setViewportSize({width:320,height:1000});
      await page.locator('select[name$=".RecipientId"]').selectOption('');
      await page.locator('#Reflow').check();
      await page.screenshot({path:path.join(output,'conflict-320.png'),fullPage:true});
      await Promise.all([page.waitForNavigation(),page.getByRole('button',{name:'Revisar cambios',exact:true}).click()]);
      assert(await page.getByText(/Sobrecarga autorizada para/).count());
      assert.equal(await page.locator('input[name$=".AuthorizeOverload"][type="hidden"]').inputValue(),'true');
      await Promise.all([page.waitForNavigation(),page.getByRole('button',{name:'Confirmar ausencia y cambios',exact:true}).click()]);
      await page.getByRole('heading',{name:/Ausencia confirmada/}).waitFor();
      results.push({engine:name,scenario:'protected evidence, retire/transfer toggles, automatic recipient plus overload and reflow, preserved decisions and atomic confirmation',status:'passed'});
    }
    assert.deepEqual(errors, []); await browser.close();
  }
  fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify(results,null,2));
  console.log(`PASS ${results.length} incidence browser scenarios`);
})().catch(error => { console.error(error); process.exit(1); });
