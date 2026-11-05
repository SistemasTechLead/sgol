const { chromium, webkit } = require('playwright-core');
const assert = require('node:assert/strict');
const fs = require('node:fs');
(async () => {
  const id=process.argv[2], folder='.local/count-origin-20261105';fs.mkdirSync(folder,{recursive:true});
  for(const [name,type] of [['chromium',chromium],['webkit',webkit]]) {
    const browser=await type.launch(name==='chromium'?{channel:'msedge'}:{});
    for(const width of [320,768,1440]) for(const theme of ['light','dark','special']) {
      const page=await browser.newPage({ignoreHTTPSErrors:true,viewport:{width,height:950}});
      await page.addInitScript(theme=>localStorage.setItem('sgol.appearance.theme',theme),theme);
      const errors=[];page.on('pageerror',e=>errors.push(e.message));
      await page.goto(`https://localhost:7259/tareas/procesos?id=${id}`);
      assert.equal(await page.locator('html').getAttribute('data-theme'),theme);
      await page.locator('[data-process-stage]').nth(2).locator('[data-process-edit]').click();
      const dialog=page.locator('dialog[open]'),source=dialog.locator('[data-count-source]');
      assert.equal(await source.count(),1);
      const selected=await source.inputValue();assert.ok(selected);
      assert.match(await source.locator('option:checked').innerText(),/Etapa 1/);
      await source.focus();await page.keyboard.press('Tab');
      assert.equal(await dialog.evaluate(el=>el.contains(document.activeElement)),true);
      await source.selectOption('');assert.equal(await page.locator('[data-process-save]').isEnabled(),true);
      await source.selectOption(selected);assert.equal(await page.locator('[data-process-save]').isDisabled(),true);
      if(width===1440&&theme==='light') {
        const position=dialog.locator('[name$=".Position"]');await position.fill('1');await position.dispatchEvent('change');
        assert.equal(await page.locator('dialog[open] [data-count-source]').evaluate(el=>el.validity.valid),false);
        await page.locator('dialog[open] [name$=".Position"]').fill('3');await page.locator('dialog[open] [name$=".Position"]').dispatchEvent('change');
        assert.equal(await page.locator('dialog[open] [data-count-source]').inputValue(),selected);
        assert.equal(await page.locator('[data-process-save]').isDisabled(),true);
      }
      assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);
      assert.equal(await dialog.evaluate(el=>el.scrollWidth<=el.clientWidth+2),true);
      await page.screenshot({path:`${folder}/editor-${name}-${width}-${theme}.png`,fullPage:true});
      await page.keyboard.press('Escape');assert.equal(await page.locator('dialog[open]').count(),0);
      await page.locator('[data-process-stage]').nth(2).locator('[data-process-edit]').focus();await page.keyboard.press('Enter');
      assert.equal(await page.locator('dialog[open]').count(),1);
      assert.deepEqual(errors,[]);console.log(`PASS UI ${name} ${width} ${theme}: real editor, optional source, dirty/revert, keyboard, no overflow`);
      await page.close();
    }
    const editor=await browser.newPage({ignoreHTTPSErrors:true,viewport:{width:1440,height:950}});
    await editor.goto(`https://localhost:7259/tareas/procesos?id=${id}`);
    const editB=async()=>{await editor.locator('[data-process-stage]').nth(1).locator('[data-process-edit]').click();return editor.locator('dialog[open] [data-count-source]');};
    let select=await editB();const origin=await select.locator('option').nth(1).getAttribute('value');
    for(const value of [origin,'']) {
      await select.selectOption(value);await editor.keyboard.press('Escape');
      const response=editor.waitForResponse(r=>r.url().includes('handler=Save')&&r.request().method()==='POST');
      await editor.locator('[data-process-save]').click();assert.equal((await response).status(),302);await editor.waitForLoadState('networkidle');
      select=await editB();assert.equal(await select.inputValue(),value);
    }
    console.log(`PASS UI ${name}: save/reopen optional source through real form and restore own load`);await editor.close();
    await browser.close();
  }
})().catch(e=>{console.error(e);process.exit(1);});
