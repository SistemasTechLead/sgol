const {chromium,webkit}=require('playwright'),fs=require('fs'),assert=require('assert/strict');
const out='../../.local/refresh-search-20261104';
(async()=>{
 const results=[];
 for(const [name,engine] of Object.entries({chromium,webkit})){
  const browser=await engine.launch(name==='chromium'?{channel:'msedge'}:{});
  const page=await browser.newPage({ignoreHTTPSErrors:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('https://localhost:7298/tareas/actualizar');
  for(const check of await page.locator('[data-refresh-options] input[type=checkbox]').all())await check.check();
  const review=page.locator('[data-refresh-options] button[type=submit]');await review.focus();
  await Promise.all([page.waitForNavigation(),page.keyboard.press('Enter')]);
  assert(await page.locator('[data-refresh-apply]').count(),'confirmation available after review');
  assert.equal(await page.locator('.assignment-refresh__blocked').count(),0,'no unresolved search block');
  assert((await page.locator('.assignment-refresh__notice').allTextContents()).some(t=>t.includes('no se reprograman')));
  for(const width of [320,768,1440])for(const theme of ['light','dark','special']){
   await page.setViewportSize({width,height:1000});await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'horizontal overflow');
   const details=page.locator('.assignment-refresh__task details').first();await details.locator('summary').focus();await page.keyboard.press('Enter');
   assert(await details.getAttribute('open')!==null,'keyboard details');await page.keyboard.press('Enter');
   if(theme==='dark')await page.screenshot({path:`${out}/${name}-${width}.png`});
   results.push({engine:name,width,theme,result:'passed'});
  }
  assert.deepEqual(errors,[]);await browser.close();
 }
 fs.writeFileSync(`${out}/browser-results.json`,JSON.stringify(results,null,2));
 fs.writeFileSync(`${out}/ui-done`,'complete');console.log(`PASS ${results.length} real refresh browser cases`);
})().catch(e=>{console.error(e);process.exit(1)});
