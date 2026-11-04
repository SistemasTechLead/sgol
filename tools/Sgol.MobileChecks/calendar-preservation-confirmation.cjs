const {chromium,webkit}=require('playwright'),fs=require('fs'),assert=require('assert/strict');
const out='../../.local/calendar-preservation-20261104';
(async()=>{
 const results=[];
 for(const [name,engine] of Object.entries({chromium,webkit})){
  const browser=await engine.launch(name==='chromium'?{channel:'msedge'}:{});
  const page=await browser.newPage({ignoreHTTPSErrors:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('https://localhost:7298/tareas/actualizar');
  const choice=page.locator('[name="PreserveCalendarAssignmentIds"][type=checkbox]');
  await choice.focus();await page.keyboard.press('Space');assert(await choice.isChecked());
  await page.locator('[data-refresh-options] button[type=submit]').focus();
  await Promise.all([page.waitForNavigation(),page.keyboard.press('Enter')]);
  assert.equal(await page.locator('.assignment-refresh__summary strong').first().innerText(),'0');
  assert.equal(await page.locator('.assignment-refresh__task').count(),0);
  assert(await page.getByRole('button',{name:'Confirmar conservación',exact:true}).isVisible());
  assert((await page.locator('[role=status]').allTextContents()).some(t=>t.includes('decisiones de conservación por confirmar')));
  for(const width of [320,768,1440])for(const theme of ['light','dark','special']){
   await page.setViewportSize({width,height:1000});await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
   const detail=page.locator('.assignment-refresh__case details').first();await detail.locator('summary').focus();await page.keyboard.press('Enter');
   assert(await detail.getAttribute('open')!==null);await page.keyboard.press('Enter');
   await page.locator('textarea[name=Reason]').fill('Conservar la excepción confirmada');
   await page.getByRole('button',{name:'Confirmar conservación',exact:true}).focus();
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'overflow');
   if(theme==='dark')await page.screenshot({path:`${out}/${name}-${width}.png`});
   results.push({engine:name,width,theme,result:'passed'});
  }
  assert.deepEqual(errors,[]);await browser.close();
 }
 fs.writeFileSync(`${out}/browser-results.json`,JSON.stringify(results,null,2));
 fs.writeFileSync('../../.local/refresh-search-20261104/ui-done','complete');console.log(`PASS ${results.length} preservation-only browser cases`);
})().catch(e=>{console.error(e);process.exit(1)});
