const { chromium, webkit } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async()=>{
 const dir=process.argv[2] || '.local/workload-20261104';
 const fixture=JSON.parse(fs.readFileSync(`${dir}/fixture.json`));
 const base='https://localhost:7293';const results=[];
 for(const [engine,type] of [['chromium',chromium],['webkit',webkit]]) {
  const browser=await type.launch(engine==='chromium'?{channel:'msedge'}:{});
  try {
   const page=await browser.newPage({ignoreHTTPSErrors:true});page.setDefaultTimeout(60000);
   const errors=[];page.on('pageerror',e=>errors.push(e.message));
   await page.goto(`${base}/validaciones`);assert.equal(await page.locator('#capacity').count(),0);
   await page.getByRole('link',{name:'Carga y reasignación',exact:true}).click();
   const summary=page.locator('#capacity > summary');await summary.focus();await page.keyboard.press('Enter');assert(await page.locator('#capacity').evaluate(e=>e.open));
   const form=page.locator('[data-capacity-form]').filter({has:page.locator(`[name="Adjustment.PersonId"][value="${fixture.other}"]`)});
   await form.locator('..').locator(':scope > summary').click();
   const reason=form.locator('textarea');await reason.fill('Revisión de carga por otras responsabilidades. '.repeat(20));
   const slider=form.locator('[data-capacity-slider]');await slider.focus();await page.keyboard.press('Home');await page.keyboard.press('ArrowRight');assert.equal(await slider.inputValue(),'5');assert.equal(await form.locator('output').innerText(),'5%');
   await slider.fill('80');
   for(const theme of ['light','dark','special']) {
    await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
    for(const width of [320,390,768,1280]) {
     await page.setViewportSize({width,height:900});
     assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),`${engine} ${theme} ${width} overflow`);
     assert(await reason.isVisible());results.push({engine,theme,width,state:'editor'});
    }
   }
   await page.setViewportSize({width:390,height:844});await form.scrollIntoViewIfNeeded();await page.screenshot({path:`${dir}/editor-${engine}.png`});
   await form.getByRole('button',{name:'Ver redistribución propuesta'}).click();
   await page.locator('[data-capacity-preview]').waitFor();assert(await page.locator('[data-capacity-preview]').evaluate(e=>document.activeElement===e));
   const apply=page.getByRole('button',{name:'Aplicar capacidad y redistribuir'});
   await apply.scrollIntoViewIfNeeded();await page.screenshot({path:`${dir}/preview-${engine}.png`});
   await apply.click();await page.waitForURL(/#capacity$/);assert(await page.getByRole('status').filter({hasText:'Capacidad actualizada'}).isVisible());
   // Reopen the persisted value and exercise server rejection via a real form submission.
   const saved=page.locator('[data-capacity-form]').filter({has:page.locator(`[name="Adjustment.PersonId"][value="${fixture.other}"]`)});
   await saved.locator('..').locator(':scope > summary').click();assert.equal(await saved.locator('[data-capacity-slider]').inputValue(),'80');
   await saved.locator('textarea').fill('Motivo conservado al corregir un error');
   await saved.locator('[name="Adjustment.DailyMinutes"]').evaluate(e=>{e.value='999';e.form.noValidate=true;});
   await saved.getByRole('button').click();await page.locator('[data-capacity-error]').waitFor();
   assert(await page.locator('[data-capacity-error]').evaluate(e=>document.activeElement===e));
   const attempted=page.locator('[data-capacity-form]').filter({has:page.locator(`[name="Adjustment.PersonId"][value="${fixture.other}"]`)});
   assert.equal(await attempted.locator('textarea').inputValue(),'Motivo conservado al corregir un error');assert(await attempted.isVisible());
   assert.equal(await page.locator('[data-capacity-preview]').count(),0);assert.deepEqual(errors,[]);
   results.push({engine,state:'preview-save-reopen-error-focus-retention',passed:true});
  } finally {await browser.close();}
 }
 fs.writeFileSync(`${dir}/browser-results.json`,JSON.stringify(results,null,2));console.log(`PASS ${results.length} workload browser scenarios`);
})().catch(e=>{console.error(e);process.exit(1)});
