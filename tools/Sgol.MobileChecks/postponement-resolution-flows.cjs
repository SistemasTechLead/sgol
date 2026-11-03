const {chromium}=require('playwright');
const fs=require('fs'),assert=require('assert/strict');
const out=process.env.SGOL_POSTPONEMENT_VISUAL_DIR || '../../.local/postponement-resolution-20261103';
(async()=>{
 const browser=await chromium.launch({channel:'msedge'});
 const page=await browser.newPage({ignoreHTTPSErrors:true,viewport:{width:1440,height:1000}});
 const origin='https://localhost:7291';
 const fixture=await (await page.request.get(origin+'/test-fixture')).json();
 const noCsrf=await page.request.post(origin+'/validaciones?handler=CarryoverItem',{form:{confirm:'true'}});
 assert.equal(noCsrf.status(),400);
 const results=[];
 for(const action of ['REASSIGN','RESCHEDULE','APPROVE','REJECT']){
  for(let pageNumber=1;pageNumber<=20;pageNumber++) {
   await page.goto(origin+'/validaciones?CarryoverPage='+pageNumber);
   if(await page.locator('.postponement-item').filter({hasText:fixture.taskCode}).count())break;
  }
  const item=page.locator('.postponement-item').filter({hasText:fixture.taskCode}).first();
  assert(await item.count(),'fixture pending task '+action);
  const id=await item.getAttribute('id');
  await item.locator('[name="selected-postponement"]').check();
  await page.locator('[data-postponement-toolbar] [data-item-action="'+action+'"]').click();
  const form=item.locator('form');
  if(action==='REASSIGN')await form.locator('[name="ItemDecision.Recipient"]').selectOption(fixture.recipient);
  if(['REASSIGN','RESCHEDULE'].includes(action)) {
   assert.equal(await form.locator('[data-item-day-mode]').inputValue(),'same');
   assert.equal(await form.locator('[name="ItemDecision.Day"]').inputValue(),await form.locator('[data-item-day-mode]').getAttribute('data-requested-day'));
   if(action==='REASSIGN') {
    await form.locator('[data-item-day-mode]').selectOption('other');
    await form.locator('[name="ItemDecision.Day"]').fill(await form.locator('[name="ItemDecision.Day"]').getAttribute('min'));
   }
  }
  await form.locator('textarea').fill('Formulario real: '+action+' con comentario conservado');
  await Promise.all([page.waitForNavigation(),form.getByRole('button',{name:'Revisar resultado',exact:true}).click()]);
  let current=page.locator('#'+id);
  assert(await current.locator('[name="ItemFingerprint"]').count(),await page.locator('main').innerText());
  assert.equal(await current.locator('textarea').inputValue(),'Formulario real: '+action+' con comentario conservado');
  if(action==='RESCHEDULE') {
   await current.locator('[data-item-day-mode]').selectOption('other');
   assert.equal(await current.locator('[name="ItemFingerprint"]').count(),0);
   await current.locator('[name="ItemDecision.Day"]').fill(await current.locator('[name="ItemDecision.Day"]').getAttribute('min'));
  }
  // Editing invalidates confirmation, forcing another genuine preview.
  await current.locator('textarea').fill('Formulario real: '+action+' confirmado');
  assert.equal(await current.locator('[name="ItemFingerprint"]').count(),0);
  await Promise.all([page.waitForNavigation(),current.getByRole('button',{name:'Revisar resultado',exact:true}).click()]);
  current=page.locator('#'+id);
  assert(await current.locator('[name="ItemFingerprint"]').count(),await page.locator('main').innerText());
  await page.screenshot({path:out+'/preview-'+action+'.png'});
  await Promise.all([page.waitForNavigation(),current.locator('button[name="confirm"][value="true"]').click()]);
  assert(await page.getByText('Decisión guardada para esta tarea.',{exact:false}).count(),await page.locator('main').innerText());
  assert.equal(await page.locator('#'+id).count(),0,'only resolved task leaves pending list');
  results.push({action,status:'passed'});
 }
 await page.goto(origin+'/mi-trabajo/arrastre');
 assert((await page.locator('main').textContent()).includes('Formulario real:'),'requester history contains decisions');
 fs.writeFileSync(out+'/flows-results.json',JSON.stringify(results,null,2));
 console.log('PASS real forms: four actions, preview invalidation, comments, CSRF, requester history');await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
