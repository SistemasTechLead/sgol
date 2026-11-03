// Real authenticated pages, no operational writes. Existing isolated fixture on 7284.
const {chromium, webkit}=require('playwright');
const fs=require('node:fs'), path=require('node:path'), assert=require('node:assert/strict');
const fixture=path.resolve(process.argv[2]), out=path.resolve(process.argv[3]);
fs.mkdirSync(out,{recursive:true});
const origin=fs.readFileSync(path.join(fixture,'origin.txt'),'utf8').trim();
assert.equal(origin,'https://localhost:7284');
const routes=['/mi-trabajo','/mi-trabajo/procesos','/mi-trabajo/arrastre','/tareas','/tareas/procesos','/tareas/procesos/items','/tareas/actualizar','/asignar-tarea','/validaciones','/validaciones?Validation=true','/supervision/carga','/supervision/bono-tareas','/planificacion','/planificacion/procesos','/personas-y-accesos','/personas-y-accesos/especializaciones','/configuracion','/reportes','/tablero','/tablero/rol-de-tareas'];
const results=[];
const settle=p=>p.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
(async()=>{
 for(const [engine,type] of Object.entries({chromium,webkit})){
  const browser=await type.launch(engine==='chromium'?{channel:'msedge'}:{});
  try{
   const page=await browser.newPage({ignoreHTTPSErrors:true,storageState:path.join(fixture,'state-alex.json'),viewport:{width:320,height:844}});
   const errors=[];page.on('pageerror',e=>errors.push(e.message));
   for(const [index,route] of routes.entries()){
    await page.setViewportSize({width:320,height:844});
    let release, requested;
    const held=new Promise(r=>release=r), intercepted=new Promise(r=>requested=r);
    await page.route('**/js/desktop-workspace.js*',async r=>{requested();await held;await r.continue()});
    const navigation=page.goto(origin+route);navigation.catch(()=>{});
    await Promise.race([intercepted,navigation.then(()=>{throw Error('Script not requested: '+page.url())})]);
    let initial;
    try{
     await page.waitForFunction(()=>[...document.styleSheets].some(s=>s.href?.includes('/css/desktop-workspace.css')));
     await page.locator('.desktop-screen-title').waitFor({state:'visible'});
     assert.equal(await page.locator('.encabezado-aplicacion__marca').count(),0,'No SGOL markup in operational header');
     assert.equal(await page.locator('h1:visible').count(),1,`One title before JS: ${engine} ${route}`);
     initial=await page.locator('.desktop-screen-title').innerText();
    }finally{release()}
    await navigation;await page.unroute('**/js/desktop-workspace.js*');
    for(const width of [320,390,768,1024,1440,320]){
     await page.setViewportSize({width,height:844});await settle(page);
     assert.equal(await page.locator('h1:visible').count(),1,`${route} ${width}`);
     assert.equal(await page.locator('.desktop-screen-title').innerText(),initial,'Stable title');
     assert(await page.locator('.desktop-screen-title').isVisible());
     assert.equal(await page.locator('main .mobile-info-toggle:visible, main .my-work-help:visible').count(),0);
     if(width<=768) assert.equal(await page.locator('.desktop-filter').count(),0,'Mobile filters unchanged');
     const header=await page.locator('.encabezado-aplicacion').boundingBox();
     assert(header.x>=0&&header.x+header.width<=width+1,'Header fits');
     const titleBox=await page.locator('.desktop-screen-title').boundingBox();
     assert(titleBox.x>=header.x&&titleBox.x+titleBox.width<=header.x+header.width,'Title fits');
    }
    const info=page.locator('.desktop-screen-info');
    for(const theme of ['light','dark','special']){
     await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
     if(await info.isVisible()){
      const before=await page.locator('main').boundingBox();await info.focus();await page.keyboard.press('Enter');
      const help=page.locator('#desktop-screen-help');assert(await help.isVisible());
      assert.deepEqual(await page.locator('main').boundingBox(),before,'No content shift');
      const box=await help.boundingBox();assert(box.x>=0&&box.x+box.width<=320&&box.y+box.height<=844,'Help fits');
      if(index===0||index===3)await page.screenshot({path:path.join(out,`${engine}-${index}-${theme}.png`)});
      await page.keyboard.press('Escape');assert(!(await help.isVisible()));assert(await info.evaluate(e=>document.activeElement===e));
      await info.click();await page.locator('.desktop-screen-title').click();assert(!(await help.isVisible()),'Outside closes');
     }
    }
    await page.emulateMedia({media:'print'});await settle(page);assert(!(await page.locator('.desktop-screen-title').isVisible()));
    await page.emulateMedia({media:'screen'});await settle(page);
    results.push({engine,route,state:'passed',initialPaint:true,widths:6,themes:3});
    fs.writeFileSync(path.join(out,'results.json'),JSON.stringify(results,null,2));
   }
   assert.deepEqual(errors,[]);
   console.log(`PASS ${engine}: 20 routes, initial paint, resize, mobile filters, help/keyboard/themes/print.`);
  }finally{await browser.close()}
 }
})().catch(e=>{console.error(e);process.exitCode=1});
