const {chromium,webkit}=require('playwright');
const assert=require('node:assert/strict');const fs=require('node:fs');
(async()=>{
 const folder='.local/process-recurrence-http';fs.mkdirSync(folder,{recursive:true});
 const browser=await chromium.launch();const page=await browser.newPage({ignoreHTTPSErrors:true});
 await page.goto('https://localhost:7269/planificacion?mode=operations');
 await page.getByRole('button',{name:'Asignar cargas de trabajo',exact:true}).click();
 await page.getByRole('button',{name:'Usar estos ajustes',exact:true}).click();
 await page.getByRole('button',{name:'Guardar plan',exact:true}).click();
 const modal=page.locator('dialog[open]');await modal.waitFor();
 assert.ok((await modal.innerText()).includes('Carga por fecha de inicio'),'Preview includes workload');
 const previewText=await page.locator('body').textContent();assert.ok(previewText.includes('Etapa recurrente 0') && previewText.includes('Etapa recurrente 1'),'Preview includes both process stages');
 const form=page.locator('form[action*="SaveDirectionPlan"]');
 const posted=await form.evaluate(f=>({url:f.action,fields:Object.fromEntries(new FormData(f))}));
 await modal.getByRole('button',{name:'Guardar plan',exact:true}).click();await page.waitForLoadState('networkidle');
 assert.match(page.url(),/planId=/);assert.match(page.url(),/mode=operations/);const saved=page.url();
 await page.getByRole('heading',{name:'Procesos del plan',exact:true}).waitFor();
 assert.equal(await page.locator('details > summary').filter({hasText:'Proceso recurrente de prueba'}).count(),2);
 const replay=await page.request.post(posted.url,{form:posted.fields,maxRedirects:0});
 assert.equal(replay.status(),302);assert.equal(new URL(replay.headers().location,saved).search,new URL(saved).search);
 console.log('PASS real weekly form: preview, API creation, stage reservations, idempotent form replay');await browser.close();
 for(const [name,engine] of Object.entries({chromium,webkit})){
  const b=await engine.launch();for(const width of [320,1280])for(const theme of ['light','dark','special']){
   const p=await b.newPage({ignoreHTTPSErrors:true,viewport:{width,height:900}});const errors=[];p.on('pageerror',e=>errors.push(e.message));
   await p.goto(saved);assert.equal(await p.locator('[data-section="planning"] a[aria-current="page"]').getAttribute('href'),'/planificacion?mode=operations');await p.evaluate(t=>document.documentElement.dataset.theme=t,theme);
   const summaries=p.locator('details > summary').filter({hasText:'Proceso recurrente de prueba'});assert.equal(await summaries.count(),2);
   await summaries.first().focus();await p.keyboard.press('Enter');
   const detail=summaries.first().locator('..');assert.equal(await detail.locator('li').count(),2);
   assert.ok((await detail.innerText()).includes('Espera la etapa anterior'));
   assert.ok(await p.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'horizontal overflow');
   await p.screenshot({path:folder+'/plan-'+name+'-'+width+'-'+theme+'.png',fullPage:true});assert.deepEqual(errors,[]);
   console.log('PASS weekly detail '+name+' '+width+' '+theme);await p.close();
  }await b.close();
 }console.log('WEEKLY PROCESS BROWSER CHECKS PASSED');
})().catch(e=>{console.error(e);process.exit(1)});
