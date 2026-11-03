const {chromium,webkit}=require('playwright');
const assert=require('node:assert/strict');
const fs=require('node:fs');
(async()=>{
 const folder=process.argv[2]||'.local/process-recurrence-ui';fs.mkdirSync(folder,{recursive:true});
 for(const [engine,type] of Object.entries({chromium,webkit})){
  const browser=await type.launch();
  for(const width of [320,1280]) for(const theme of ['light','dark','special']) {
   const page=await browser.newPage({ignoreHTTPSErrors:true,viewport:{width,height:900}});
   const errors=[];page.on('pageerror',e=>errors.push(e.message));
   await page.goto('https://localhost:7269/tareas/procesos');
   await page.getByRole('link',{name:'Proceso recurrente de prueba',exact:true}).click();
   await page.locator('.process-settings summary').click();
   await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
   await page.locator('[name="Input.StartKind"]').selectOption('SEMANAL');
   await page.locator('[name="Input.Schedule.Mode"]').selectOption('DIAS_FIJOS');
   await page.locator('[name="ProcessDays"][value="2"]').check();
   await page.locator('[name="Input.Schedule.Time"]').fill('10:35');
   await page.locator('[name="Input.Schedule.WeeklyOperations"][type="checkbox"]').check();
   // Each iteration changes the draft name to exercise a real form submit.
   await page.locator('[name="Input.Description"]').fill(engine+' '+width+' '+theme);
   await page.locator('[data-process-save]').click();
   await page.waitForLoadState('networkidle');
   assert.ok(!(await page.locator('[role="alert"]').count()),await page.locator('body').innerText());
   await page.locator('.process-settings summary').click();
   assert.equal(await page.locator('[name="Input.Schedule.Time"]').inputValue(),'10:35');
   assert.ok(await page.locator('[name="ProcessDays"][value="2"]').isChecked());
   assert.ok(await page.locator('[name="Input.Schedule.WeeklyOperations"][type="checkbox"]').isChecked());
   assert.ok(await page.locator('[data-process-save]').isDisabled(),'Saved state must not appear dirty');
   await page.locator('[name="Input.StartKind"]').selectOption('MENSUAL');
   assert.ok(await page.locator('[data-process-weekly]').isHidden());
   await page.locator('[name="Input.Schedule.MonthDays"]').fill('8,22');
   await page.locator('[data-process-save]').click();await page.waitForLoadState('networkidle');
   assert.ok(!(await page.locator('[role="alert"]').count()),await page.locator('body').innerText());
   await page.locator('.process-settings summary').click();
   assert.equal(await page.locator('[name="Input.Schedule.MonthDays"]').inputValue(),'8,22');
   assert.equal(await page.locator('[name="Input.Schedule.Repetitions"]').inputValue(),'2');
   assert.ok(await page.locator('[data-process-save]').isDisabled(),'Monthly saved state');
   assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'horizontal overflow');
   await page.locator('[name="Input.StartKind"]').focus();await page.keyboard.press('Tab');
   assert.ok(await page.evaluate(()=>document.activeElement.tagName==='SELECT'),'keyboard enters visible recurrence');
   await page.screenshot({path:folder+'/'+engine+'-'+width+'-'+theme+'.png',fullPage:true});
   assert.deepEqual(errors,[]);console.log('PASS '+engine+' '+width+' '+theme+' save/reopen weekly/monthly, checkbox, keyboard, overflow');await page.close();
  }await browser.close();
 }console.log('PROCESS RECURRENCE BROWSER CHECKS PASSED');
})().catch(e=>{console.error(e);process.exit(1)});
