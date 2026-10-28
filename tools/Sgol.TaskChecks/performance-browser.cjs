// Bounded real-browser probe of the synthetic dedicated host. No operational environment URL is accepted.
const fs=require('fs'),crypto=require('crypto'),pw=require('playwright');
const origin='https://localhost:7267',out=process.env.SGOL_PERF_BROWSER_OUTPUT||'artifacts/performance-browser';
const password=JSON.parse(fs.readFileSync('.local/performance-environment.json','utf8')).Password;
function totp(secret){let buffer=0,bits=0,bytes=[];for(const c of secret){buffer=(buffer<<5)|'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567'.indexOf(c);bits+=5;if(bits>=8){bits-=8;bytes.push((buffer>>bits)&255);}}const counter=Buffer.alloc(8);counter.writeBigInt64BE(BigInt(Math.floor(Date.now()/30000)));const hash=crypto.createHmac('sha1',Buffer.from(bytes)).update(counter).digest(),o=hash[hash.length-1]&15;return String((hash.readUInt32BE(o)&0x7fffffff)%1000000).padStart(6,'0');}
(async()=>{fs.mkdirSync(out,{recursive:true});const results=[];
let state;
for(const engine of ['chromium','webkit','firefox']){
 let browser;try{browser=await pw[engine].launch({headless:true});}catch(e){results.push({engine,state:'not-executed',reason:e.message.split('\n')[0]});continue;}
 try{
  const context=await browser.newContext({ignoreHTTPSErrors:true,...(state?{storageState:state}:{})});
  if(!state){
   async function api(path,data){const csrf=await(await context.request.get(origin+'/api/v1/auth/csrf')).json();const r=await context.request.post(origin+path,{headers:{'X-CSRF-TOKEN':csrf.data.requestToken},data});if(!r.ok())throw Error('Authentication setup '+path+' status '+r.status());return(await r.json()).data;}
   await api('/api/v1/auth/login',{userName:process.env.SGOL_PERF_BROWSER_USER||'perf-browser',password});const grant=await api('/api/v1/auth/mfa/enroll',{});await api('/api/v1/auth/mfa/confirm',{totpCode:totp(grant.manualKey)});state=await context.storageState();
  }
  const page=await context.newPage();let cdp=engine==='chromium'?await context.newCDPSession(page):null;
  await page.goto(origin+'/health/live');
  await page.addInitScript(()=>{window.__longTasks=[];window.__longTaskSupported=PerformanceObserver.supportedEntryTypes?.includes('longtask')||false;try{new PerformanceObserver(list=>{for(const e of list.getEntries())window.__longTasks.push(e.duration);}).observe({type:'longtask',buffered:true});}catch{}});
  for(const width of (process.env.SGOL_PERF_BROWSER_FLOWS_ONLY==='1'?[]:[1366,390,320]))for(const theme of ['light','dark']){
   await page.setViewportSize({width,height:900});
   await page.evaluate(theme=>localStorage.setItem('sgol.appearance.theme',theme),theme);
   if(cdp){await cdp.send('Emulation.setCPUThrottlingRate',{rate:width<768?4:1});await cdp.send('Network.enable');await cdp.send('Network.emulateNetworkConditions',{offline:false,latency:width<768?150:0,downloadThroughput:width<768?200000:-1,uploadThroughput:width<768?93750:-1});}
   for(const route of ['/tareas','/reportes','/validaciones','/mi-trabajo']){
    const errors=[],consoleErrors=[],failed=[];const error=e=>errors.push(e.name+': '+e.message.slice(0,200));const consoleListener=m=>{if(m.type()==='error')consoleErrors.push(m.text().slice(0,200));};const requestListener=r=>failed.push({path:new URL(r.url()).pathname,error:r.failure()?.errorText});page.on('pageerror',error);page.on('console',consoleListener);page.on('requestfailed',requestListener);
    const start=Date.now();let row={engine,width,theme,route};
    try{const response=await page.goto(origin+route,{waitUntil:'load',timeout:90000});await page.waitForTimeout(300);row={...row,status:response.status(),...await page.evaluate(()=>({path:location.pathname,dom:document.querySelectorAll('*').length,overflow:document.documentElement.scrollWidth>innerWidth+1,navigationMs:performance.getEntriesByType('navigation')[0].loadEventEnd,encodedBytes:performance.getEntriesByType('navigation')[0].encodedBodySize,longTaskCount:window.__longTasks.length,longTaskMaxMs:Math.max(0,...window.__longTasks),theme:document.documentElement.dataset.theme,dialogSupported:typeof HTMLDialogElement!=='undefined'}))};
     // Exercise the actual mobile navigation dialog and focus on the rendered page.
     if(width<768){const menu=page.locator('[aria-controls="navegacion-movil"]');if(await menu.isVisible()){await menu.click();row.mobileMenuOpened=await page.locator('#navegacion-movil').evaluate(e=>e.open);await page.keyboard.press('Escape');row.mobileMenuClosed=await page.locator('#navegacion-movil').evaluate(e=>!e.open);}}
     await page.screenshot({path:out+'/'+engine+'-'+width+'-'+theme+'-'+route.slice(1)+'.png'});
    }catch(e){row.error=e.message.split('\n')[0];}
    Object.assign(row,{elapsedWithDiagnosticMs:Date.now()-start,errors,consoleErrors,failed});results.push(row);console.log(JSON.stringify(row));fs.writeFileSync(out+'/results.json',JSON.stringify(results,null,2));page.off('pageerror',error);page.off('console',consoleListener);page.off('requestfailed',requestListener);
   }
  }
  if(engine==='chromium'){
   const component=await context.newPage();await component.clock.install();
   await component.addInitScript(()=>{window.__hidden=true;Object.defineProperty(document,'hidden',{get:()=>window.__hidden});window.EventSource=undefined;window.__requests=0;window.fetch=async()=>{window.__requests++;return {ok:false,status:503,redirected:false};};});
   await component.route(origin+'/performance-background-fixture',r=>r.fulfill({contentType:'text/html',body:'<div id="live-board"><div class="board-content" data-plan="" data-task="" data-server-now="0"><div class="board-viewport"><div class="board-grid"></div></div><p data-board-empty></p></div></div><p id="board-connection" hidden></p><script src="/js/live-board.js"></script>'}));
   await component.goto(origin+'/performance-background-fixture');await component.clock.runFor(16000);
   const hiddenRequests=await component.evaluate(()=>window.__requests);
   await component.evaluate(()=>{window.__hidden=false;document.dispatchEvent(new Event('visibilitychange'));});await component.clock.runFor(50);
   const resumedRequests=await component.evaluate(()=>window.__requests);
   results.push({scenario:'hidden-board-component',hiddenRequests,resumedRequests,scope:'Minimal component fixture with real production JS, fake visibility/time and intercepted fetch; no physical background-tab or server load claim'});
   await component.close();
  }
  if(process.env.SGOL_PERF_BROWSER_VERIFY==='1'){
   if(cdp){await cdp.send('Emulation.setCPUThrottlingRate',{rate:1});await cdp.send('Network.emulateNetworkConditions',{offline:false,latency:0,downloadThroughput:-1,uploadThroughput:-1});}
   await page.setViewportSize({width:1366,height:900});
   const checks=[];
   for(const route of ['/tareas','/reportes','/validaciones','/mi-trabajo']){
    await page.goto(origin+route);
    const next=page.getByRole('link',{name:'Siguiente',exact:true}).first();
    if(await next.count()){await next.focus();checks.push({route,keyboardFocus:await next.evaluate(e=>e===document.activeElement)});await next.press('Enter');await page.waitForLoadState('load');checks.push({route,page2:(await page.locator('nav.acciones [role="status"]').allTextContents()).some(t=>t.startsWith('Página 2 ')),url:page.url()});}
   }
   await page.goto(origin+'/reportes');
   const report=await context.request.get(origin+'/reportes?handler=Export');const exportData=await report.json();
   checks.push({scenario:'complete-export',status:report.status(),rows:exportData.rows?.length,moreThanOnePage:exportData.rows?.length>50});
   const search=page.locator('[name="DetailSearch"]');await search.fill('sin-coincidencias-aisladas-987654321');await search.press('Enter');await page.waitForLoadState('load');
   checks.push({scenario:'empty-report-search',rows:await page.locator('.report-table tbody tr').count(),searchRetained:await page.locator('[name="DetailSearch"]').inputValue()});
   await page.goto(origin+'/mi-trabajo');const detail=page.locator('[data-assigned-task]').first();if(await detail.count()){await detail.click();await page.waitForLoadState('load');const dialog=page.locator('dialog[data-assigned-task-detail]');checks.push({scenario:'my-work-detail',open:await dialog.evaluate(e=>e.open)});await page.keyboard.press('Escape');}
   await page.goto(origin+'/validaciones');const tableNext=page.locator('nav.acciones a[href*=".page=2"]').first();
   if(await tableNext.count()){await tableNext.focus();await tableNext.press('Enter');await page.waitForLoadState('load');checks.push({scenario:'assignment-table-page2',url:page.url(),page2:(await page.locator('nav.acciones [role="status"]').allTextContents()).some(t=>t.startsWith('Página 2 '))});}
   await page.setViewportSize({width:320,height:900});await page.evaluate(()=>localStorage.setItem('sgol.appearance.theme','dark'));
   await page.goto(origin+'/validaciones?Validation=true&Section=PISO_VENTAS');checks.push({scenario:'real-validation-cards',cards:await page.locator('[data-validation-card]').count(),overflow:await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1)});
   await page.screenshot({path:out+'/'+engine+'-320-dark-validation-cards.png'});
   const cardNext=page.getByRole('link',{name:'Siguiente',exact:true}).first();if(await cardNext.count()){await cardNext.focus();await cardNext.press('Enter');await page.waitForLoadState('load');checks.push({scenario:'validation-card-page2',cards:await page.locator('[data-validation-card]').count(),url:page.url()});}
   const reviewForm=await page.locator('[data-validation-batch]').evaluate(e=>({action:e.action,fields:Object.fromEntries(new FormData(e))}));
   const emptyReview=await context.request.post(reviewForm.action,{form:reviewForm.fields});checks.push({scenario:'negative-real-review-form-empty-selection',status:emptyReview.status(),atomicMessage:(await emptyReview.text()).includes('No se guard')});
   const noCsrf={...reviewForm.fields};delete noCsrf.__RequestVerificationToken;checks.push({scenario:'review-form-csrf-required',status:(await context.request.post(reviewForm.action,{form:noCsrf})).status()});
   await page.setViewportSize({width:1366,height:900});await page.goto(origin+'/tareas');const field=page.locator('[name="Search"]');await field.fill('Conservar búsqueda y foco');
   for(const width of [1366,320,1366,390]){await page.setViewportSize({width,height:900});await page.waitForTimeout(100);}
   checks.push({scenario:'mobile-resize-preserves-form',value:await field.inputValue(),rows:await page.locator('[data-task-row]').count(),focused:await field.evaluate(e=>e===document.activeElement),visible:await field.isVisible()});
   for(const [width,theme] of [[320,'light'],[390,'dark'],[1366,'light'],[1366,'dark']]){
    await page.setViewportSize({width,height:900});await page.evaluate(theme=>localStorage.setItem('sgol.appearance.theme',theme),theme);await page.goto(origin+'/mi-trabajo');
    const toggle=page.locator('[data-mobile-control="supervision-plan-tasks"]');if(width<768){if(await toggle.isVisible())await toggle.click();checks.push({scenario:'my-work-global-search-visible',width,theme,toggle:await toggle.isVisible(),search:await page.locator('[name="WorkSearch"]').isVisible()});}
    checks.push({scenario:'header-long-name',width,theme,nonOverlapping:await page.evaluate(()=>{const a=document.querySelector('.encabezado-aplicacion__marca').getBoundingClientRect(),b=document.querySelector('.menu-sesion').getBoundingClientRect();return a.right<=b.left+1;})});
    await page.screenshot({path:out+'/'+engine+'-'+width+'-'+theme+'-work-search.png'});
   }
   await page.setViewportSize({width:1366,height:900});
   if(engine==='chromium'){
    await page.goto(origin+'/planificacion?mode=create');await page.locator('[name="name"]').fill('Diagnóstico anual aislado');await page.locator('[name="area"]').fill('Synthetic performance');await page.locator('[name="periodType"]').selectOption('ANUAL');await page.locator('[name="year"]').fill('2027');
    await page.locator('#direction-search').fill('Planner volume 50-3-');
    const choices=page.locator('[data-task-option][data-name^="Planner volume 50-3-"] [data-direction-add]');const count=await choices.count();
    for(let i=0;i<count;i++)await choices.nth(i).click();
    await Promise.all([page.waitForNavigation(),page.locator('button[form="direction-create-form"][type="submit"]').click()]);
    const text=await page.locator('body').innerText();checks.push({scenario:'annual-search-size-diagnostic',selected:count,explainsSize:text.includes('2686')&&text.includes('1000'),doesNotOfferIneffectiveBudgetRetry:await page.locator('[name="searchMultiplier"]').inputValue()==='1'});
    await page.screenshot({path:out+'/planning-search-diagnostic.png'});
   }
   results.push({engine,scenario:'real-interface-flows',checks});
  }
  await context.close();
 }catch(e){results.push({engine,error:e.message.split('\n')[0]});console.log(engine+': '+e.message.split('\n')[0]);}
 finally{await browser.close();}
}
fs.writeFileSync(out+'/results.json',JSON.stringify(results,null,2));
})().catch(e=>{console.error(e.message);process.exit(1)});
