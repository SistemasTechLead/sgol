/* Authenticated, read-only integration checks against an explicitly supplied host.
 * Supply origin.txt, state-alex.json and routes-extended.json in a private directory.
 * Never records cookies, form values, names or credentials in test reports. */
const { chromium, webkit } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
(async () => {
 const dir = path.resolve(process.argv[2]);
 const origin = fs.readFileSync(path.join(dir,'origin.txt'),'utf8').trim();
 const manifest = JSON.parse(fs.readFileSync(path.join(dir,'routes-extended.json')));
 const routes = [...new Set(manifest.map(x=>x.route).concat(['/mi-trabajo/arrastre','/tareas/actualizar','/supervision/carga','/tareas?AdjustClosure=true','/tareas/procesos/items']))];
 const out=path.join(dir,'compact'); fs.mkdirSync(out,{recursive:true});
 const resultsPath=path.join(out,process.argv.includes('--themes-only')?'themes-results.json':process.argv.includes('--interactions-only')?'interactions-results.json':'results.json');
 const results=[], errors=[];
 for(const engine of (process.argv.includes('--chromium-only')?['chromium']:['chromium','webkit'])) {
  let browser;
  try { browser=await (engine==='webkit'?webkit:chromium).launch(engine==='chromium'?{channel:'msedge',headless:true}:{headless:true}); }
  catch(e){results.push({engine,state:'not-run',reason:e.message.split('\n')[0]});continue;}
  const context=await browser.newContext({ignoreHTTPSErrors:true,storageState:path.join(dir,'state-alex.json')});
  // Keep the real browser cache; intercepting every asset disables it and can
  // exhaust the real per-client quota. This host must be an isolated test copy.
  const writes=[]; context.on('request',r=>{if(!['GET','HEAD'].includes(r.method()))writes.push(r.method());});
  const page=await context.newPage();page.on('pageerror',e=>errors.push(e.message));
  const navigate=async route=>{
   let response=await page.goto(origin+route);
   if(response.status()===429 || response.status()===503 && (manifest.find(x=>x.route===route)?.status??200)===200){console.log('Retrying transient HTTP failure:',response.status(),route);await page.waitForTimeout(60000);response=await page.goto(origin+route);}
   assert.notEqual(response.status(),429,'HTTP quota remains exhausted');return response;
  };
  for(const width of (process.argv.includes('--interactions-only')?[]:process.argv.includes('--themes-only')?[320]:[320,390,430,768,769,1440])) {
   await page.setViewportSize({width,height:900});
   for(const route of routes) {
    const response=await navigate(route);await page.evaluate(()=>document.fonts.ready);
    const measure=await page.evaluate(()=>({overflow:document.documentElement.scrollWidth-document.documentElement.clientWidth,heading:!!document.querySelector('main h1'),login:!!document.querySelector('[name=userName]')}));
    assert(!measure.login,route+': session lost');
    const expected=manifest.find(x=>x.route===route)?.status??200;
    results.push({engine,route,width,status:response.status(),expected,...measure,state:response.status()!==expected||width<=768&&measure.overflow>1?'failed':'passed'});
    if(process.argv.includes('--themes-only')) for(const theme of ['light','dark','special']) for(const scale of [1,2]) {
     await page.evaluate(async({theme,scale})=>{document.documentElement.dataset.theme=theme;document.documentElement.style.fontSize=16*scale+'px';await new Promise(requestAnimationFrame);await new Promise(requestAnimationFrame);},{theme,scale});
     const overflow=await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth);
     results.push({engine,route,width,theme,scale,overflow,state:overflow>1?'failed':'passed'});
    }
   }
   console.log(engine,width,routes.length,'routes checked');
   fs.writeFileSync(resultsPath,JSON.stringify({results,errors},null,2));
  }
  if(process.argv.includes('--themes-only')) {await browser.close();continue;}
  for(const theme of ['light','dark','special']) for(const scale of [1,2]) {
   await page.setViewportSize({width:320,height:700});
   for(const route of ['/mi-trabajo','/personas-y-accesos','/tareas','/configuracion','/planificacion','/reportes']) {
    await navigate(route);
    await page.evaluate(({theme,scale})=>{document.documentElement.dataset.theme=theme;document.documentElement.style.fontSize=16*scale+'px';},{theme,scale});
    const trigger=page.locator('.mobile-info-toggle,.my-work-help summary').first();
    if(await trigger.count()) {
     const header=page.locator('.encabezado-pagina').first();const before=await header.boundingBox();
     await trigger.focus();await page.keyboard.press('Enter');
     const id=await trigger.getAttribute('aria-controls');const help=page.locator('#'+id);await help.waitFor({state:'visible'});
     const after=await header.boundingBox();assert(Math.abs(before.height-after.height)<1,'Help shifts header');
     const box=await help.boundingBox();assert(box.x>=0&&box.x+box.width<=321,'Help escapes viewport');
     const hit=await trigger.boundingBox();assert(hit.width>=44&&hit.height>=44,'Small help target');
     await page.keyboard.press('Escape');assert.equal(await trigger.getAttribute('aria-expanded'),'false');assert(await trigger.evaluate(e=>e===document.activeElement));
     await trigger.click();await trigger.click();assert.equal(await trigger.getAttribute('aria-expanded'),'false');
     await trigger.click();await page.locator('h1').first().click();assert.equal(await trigger.getAttribute('aria-expanded'),'false');
    }
    const overflow=await page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth);
    results.push({engine,route,theme,scale,width:320,overflow,state:overflow>1?'failed':'passed'});
    if(scale===1) await page.screenshot({path:path.join(out,`${engine}-${theme}-${route.split('/').pop()}.png`)});
   }
  }
  await navigate('/tareas');await page.setViewportSize({width:390,height:844});
  await page.evaluate(()=>document.documentElement.style.removeProperty('font-size'));
  const values=()=>page.evaluate(()=>[...document.forms].map(f=>JSON.stringify([...new FormData(f)].map(([k,v])=>[k,typeof v==='string'?v:v.name]))));
  assert(await page.locator('[data-task-edit]').isDisabled());
  if(await page.locator('.task-catalog-refined').count()) assert.equal(await page.locator('.task-action-panel').isVisible(),false);
  await page.locator('.task-selection').first().check();
  const before=await values(); const more=page.locator('[data-mobile-control=task-secondary]');await more.click();await more.click();assert.deepEqual(await values(),before);
  assert(await more.evaluate(e=>e.getAttribute('aria-controls').split(' ').every(id=>document.getElementById(id))), 'Disclosure has invalid controlled IDs');
  for(const kind of ['true','false']) {
   const row=page.locator(`[data-task-assignable="${kind}"]`).first();if(!await row.count())continue;
   await row.locator('.task-selection').check();await more.click();
   assert.equal(await page.locator('[data-task-assign]').isDisabled(),kind==='false');
   assert(await page.locator('[data-task-selection-hint]').isVisible());
   await page.locator('[data-task-delete]').click();await page.locator('#task-delete [data-dialog-close]').click();
   assert.equal(await page.locator('#task-delete').evaluate(d=>d.open),false);await more.click();
  }
  await page.setViewportSize({width:320,height:568});
  const lastRadio=page.locator('.task-selection').last();
  for(const expanded of [false,true]) {
   if((await more.getAttribute('aria-expanded')==='true')!==expanded) await more.click();
   await lastRadio.focus();
   const focused=await lastRadio.boundingBox(), panel=await page.locator('.task-action-panel').boundingBox();
   assert(focused.y>=0 && focused.y+focused.height<=panel.y,'Task panel covers focused last row');
  }
  await navigate('/personas-y-accesos');const original=await values();
  await page.locator('.mobile-record-toggle').first().click();
  await page.setViewportSize({width:1440,height:900});assert.deepEqual(await values(),original);
  assert.equal(await page.locator('.mobile-record-toggle:visible,.mobile-info-toggle:visible').count(),0);
  await page.setViewportSize({width:390,height:844});assert.deepEqual(await values(),original);
  await page.evaluate(()=>document.documentElement.dataset.layout='desktop');
  assert(await page.evaluate(()=>document.documentElement.getBoundingClientRect().width>=1280));
  await page.setViewportSize({width:1440,height:900});await page.evaluate(()=>document.documentElement.dataset.layout='mobile');
  assert(await page.evaluate(()=>document.documentElement.getBoundingClientRect().width<=420));
  await page.emulateMedia({media:'print'});assert.equal(await page.locator('.mobile-info-toggle:visible,.mobile-record-toggle:visible').count(),0);
  assert.deepEqual(writes,[],'Unexpected write during read-only traversal');
  await browser.close();
 }
 fs.writeFileSync(resultsPath,JSON.stringify({results,errors},null,2));
 const failed=results.filter(x=>x.state==='failed');console.log('Results:',results.length,'failed:',failed.length,'script errors:',errors.length);console.log(JSON.stringify(failed));assert.equal(failed.length,0);assert.deepEqual(errors,[]);
})().catch(e=>{console.error(e);process.exit(1)});
