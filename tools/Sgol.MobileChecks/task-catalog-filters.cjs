const {chromium,webkit}=require('playwright');
const assert=require('node:assert/strict');
const fs=require('node:fs');
(async()=>{
 const dir=process.argv[2];const log=fs.readFileSync(dir+'/checks.log','utf8');const url=log.match(/FILTER_UI_READY (\S+)/)[1];const results=[];
 for(const [name,engine] of [['chromium',chromium],['webkit',webkit]]){
  const browser=await engine.launch(name==='chromium'?{channel:'msedge'}:{});const page=await browser.newPage({ignoreHTTPSErrors:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
  for(const width of [320,390,768,1440]){
   await page.setViewportSize({width,height:900});assert.equal((await page.goto(url,{waitUntil:"domcontentloaded",timeout:60000})).status(),200);
   const fold=page.locator('[data-mobile-control="task-search"]');if(await fold.isVisible()){await fold.focus();await page.keyboard.press('Enter');}
   await page.locator('[name="Filter.Requirement"]').waitFor({state:'visible'});
   for(const theme of ['light','dark','special']){
    await page.evaluate(theme=>document.documentElement.dataset.theme=theme,theme);
    assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),`${name} ${width} ${theme} page overflow`);
    for(const control of await page.locator('.task-catalog-filters input:not([type=hidden]),.task-catalog-filters select,.task-catalog-filters button,.task-catalog-filters a').all()){
     const box=await control.boundingBox();assert(box&&box.x>=0&&box.x+box.width<=width+1,`${name} ${width} control overflow`);
    }
    if(width===390||width===1440)await page.screenshot({path:`${dir}/${name}-${width}-${theme}.png`,fullPage:true});
    results.push({engine:name,width,theme,status:'passed'});
   }
  }
  await page.locator('[name="Filter.Requirement"]').selectOption('COMENTARIO');await page.locator('[name="Filter.Repetitions"]').fill('3');
  await Promise.all([page.waitForURL(/Filter.Requirement=COMENTARIO/),page.getByRole('button',{name:'Aplicar filtros',exact:true}).click()]);
  assert(await page.getByText('61 tareas encontradas.',{exact:true}).isVisible());assert.equal(await page.locator('tbody tr').count(),50);
  const next=page.locator('a[href*="PageNumber=2"]').first();await Promise.all([page.waitForURL(/PageNumber=2/),next.click()]);assert.equal(await page.locator('tbody tr').count(),11);assert.equal(await page.locator('[name="Filter.Requirement"]').inputValue(),'COMENTARIO');
  await page.locator('[name="Filter.Requirement"]').selectOption('FOTOGRAFIA');await page.getByRole('button',{name:'Aplicar filtros',exact:true}).click();await page.getByText('No se encontraron tareas.',{exact:true}).waitFor();assert(!page.url().includes('PageNumber'));
  await Promise.all([page.waitForURL(u=>u.search===''),page.getByRole('link',{name:'Limpiar filtros',exact:true}).click()]);assert.equal(new URL(page.url()).search,'');
  await page.goto(url+'&Filter.Repetitions=bad');assert(await page.getByRole('alert').isVisible());
  assert.deepEqual(errors,[]);await browser.close();
 }
 fs.writeFileSync(dir+'/browser-results.json',JSON.stringify(results,null,2));console.log('PASS 24 layout/theme cases; keyboard, GET filtering, paging, empty state, clear, invalid input; both engines');
})().catch(e=>{console.error(e);process.exit(1)});
