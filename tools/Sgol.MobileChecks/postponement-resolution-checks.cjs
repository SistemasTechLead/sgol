const {chromium,webkit}=require('playwright');
const fs=require('fs'), assert=require('assert/strict');
const out=process.env.SGOL_POSTPONEMENT_VISUAL_DIR || '../../.local/postponement-resolution-20261103';
(async()=>{
 const result=[];
 for(const [name,engine] of Object.entries({chromium,webkit})){
  const b=await engine.launch(name==='chromium'?{channel:'msedge'}:{});
  const page=await b.newPage({ignoreHTTPSErrors:true});const errors=[];page.on('pageerror',e=>errors.push(e.message));
  const response=await page.goto('https://localhost:7291/validaciones');
  assert.equal(response.status(),200,(await page.textContent('body')).slice(0,1500));
  assert(await page.locator('.postponement-item').count()>1);
  for(const width of [320,768,1440])for(const theme of ['light','dark','special']){
   await page.setViewportSize({width,height:1000});await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
   await page.locator('#carryovers').scrollIntoViewIfNeeded();
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'overflow '+width);
   const item=page.locator('.postponement-item').first();assert(await item.locator('h3').isVisible());assert(await item.locator('.postponement-explanation').isVisible());
   assert.equal(await page.locator('.postponement-editor:visible').count(),0);
   const alignment=await item.evaluate(e=>{const title=e.querySelector('h3').getBoundingClientRect(),radio=e.querySelector('.task-selection').getBoundingClientRect();return Math.abs(radio.y+radio.height/2-(title.y+parseFloat(getComputedStyle(e.querySelector('h3')).lineHeight)/2));});
   assert(alignment<4,'selector aligned with title first line: '+alignment);
   assert.equal(await item.locator('.postponement-schedule time').count(),2);
   assert.equal(await item.locator('.postponement-meta > span').count(),3);
   await item.locator('[name="selected-postponement"]').check();
   const extra=page.locator('[data-postponement-toolbar] [data-mobile-control="task-secondary"]');
   if(!(await page.locator('[data-postponement-toolbar] [data-item-action="REASSIGN"]').isVisible()))await extra.click();
   await page.locator('[data-postponement-toolbar] [data-item-action="REASSIGN"]').click();
   assert(await item.locator('[data-item-recipient]').isVisible());
   assert(!(await item.locator('[data-item-recipient]').innerText()).includes('rol y especialidad compatibles'));
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'editor overflow '+width);
   if(theme==='dark')await page.screenshot({path:out+'/editor-'+name+'-'+width+'.png'});
   await item.locator('[data-item-close]').click();
   const block=item.locator('.postponement-obstacle');
   if(await block.count()) {const text=await block.innerText();assert(!text.includes(await item.locator('h3').innerText()),'block must not repeat task');assert(text.includes('Dar seguimiento a pedidos'),'actual blocking task named');}
   result.push({name,width,theme,height:(await item.boundingBox()).height});
   if(theme==='dark')await page.screenshot({path:out+'/'+name+'-'+width+'.png'});
  }
  const item=page.locator('.postponement-item').filter({hasText:'Resolución QA'}).first();
  await item.locator('[name="selected-postponement"]').check();
  const bar=page.locator('[data-postponement-toolbar]');
  assert.equal(await page.locator('[data-item-action="REJECT"]').count(),1);
  await bar.locator('[data-item-action="REJECT"]').focus();await page.keyboard.press('Enter');assert(await item.locator('textarea').isVisible());
  await item.locator('textarea').fill('Borrador que debe permanecer');await item.locator('[data-item-close]').click();
  await bar.locator('[data-item-action="REJECT"]').click();assert.equal(await item.locator('textarea').inputValue(),'Borrador que debe permanecer');
  await page.setViewportSize({width:320,height:1000});await page.evaluate(()=>document.documentElement.style.fontSize='32px');
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'200 percent overflow');
  await page.evaluate(()=>document.documentElement.style.fontSize='');
  await item.locator('[data-item-close]').click();
  await page.locator('.postponement-item').last().scrollIntoViewIfNeeded();
  let bounds=await bar.boundingBox();
  assert(bounds.y + bounds.height <= 1000 + 1,'bar stays in viewport while within section');
  await page.locator('.supervision-section').last().scrollIntoViewIfNeeded();
  bounds=await bar.boundingBox();
  assert(bounds.y + bounds.height <= 0,'bar remains in its section after leaving');assert.equal(errors.length,0,errors.join(';'));await b.close();
 }
 fs.writeFileSync(out+'/browser-results.json',JSON.stringify(result,null,2));console.log('PASS '+result.length+' responsive/theme cases, keyboard, drafts, 200% reflow');
})().catch(e=>{console.error(e);process.exit(1)});
