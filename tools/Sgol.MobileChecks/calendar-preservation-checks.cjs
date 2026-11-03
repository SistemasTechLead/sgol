const {chromium}=require('playwright');
const assert=require('node:assert/strict');
const fs=require('node:fs');
(async()=>{
 const browser=await chromium.launch({channel:'msedge'});
 const folder='.local/compact-refresh-ui';fs.mkdirSync(folder,{recursive:true});
 for(const width of [320,1280]) for(const theme of ['light','dark','special']){
  const page=await browser.newPage({viewport:{width,height:900},ignoreHTTPSErrors:true});
  await page.goto('https://localhost:7297/tareas/actualizar');
  await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
  const checkbox=page.locator('input[name="PreserveCalendarAssignmentIds"][type="checkbox"]');
  assert.equal(await checkbox.count(),1);
  assert.equal(await page.locator('[data-refresh-apply]').count(),0);
  await checkbox.focus();await page.keyboard.press('Space');
  assert.ok(await checkbox.isChecked());
  const inactive=page.locator('input[type="checkbox"][name="RetireInactiveAssignments"]');
  if(await inactive.count()){
   const group=inactive.locator('xpath=ancestor::article');
   assert.equal(await group.locator('.assignment-refresh__case-tasks li').count(),2);
   await inactive.check();
  }
  const card=checkbox.locator('xpath=ancestor::article');
  assert.ok(await card.innerText().then(t=>t.includes('Motivo') && t.includes('Conservar este pendiente')));
  const details=card.locator('details');
  assert.equal(await details.getAttribute('open'),null);
  await details.locator('summary').focus();await page.keyboard.press('Enter');
  assert.notEqual(await details.getAttribute('open'),null);
  await page.keyboard.press('Enter');
  await page.getByRole('button',{name:'Revisar cambios',exact:true}).click();
  await page.locator('[data-refresh-apply]').waitFor();
  assert.ok(await checkbox.isChecked());
  assert.equal(await page.locator('[data-refresh-apply] input[name="PreserveCalendarAssignmentIds"]').count(),1);
  assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));
  await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
  await page.screenshot({path:folder+'/'+width+'-'+theme+'.png',fullPage:true});
  await checkbox.uncheck();
  assert.ok(await page.locator('[data-refresh-dirty]').isVisible());
  assert.ok(await page.locator('[data-refresh-apply] button').isDisabled());
  await page.close();
 }
 await browser.close();console.log('PASS real Razor: keyboard checkbox, review, hidden consent, dirty confirmation disabled; 320/1280 x 3 themes, no overflow');
 fs.writeFileSync('.local/calendar-ui-done','done');
})().catch(e=>{console.error(e);process.exit(1)});
