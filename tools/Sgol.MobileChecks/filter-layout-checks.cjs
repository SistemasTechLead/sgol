const { chromium, webkit } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const dir = process.argv[2], candidate = process.argv[3];
const routes = ['/personas-y-accesos','/tareas','/validaciones','/validaciones?Validation=true','/supervision/carga','/supervision/bono-tareas','/reportes','/tablero','/planificacion'];
(async () => {
  const results = [];
  for (const [engine,type] of Object.entries({chromium,webkit})) {
    const browser = await type.launch(engine === 'chromium' ? {channel:'msedge'} : {});
    try {
      const page = await browser.newPage({ignoreHTTPSErrors:true,storageState:dir+'/state-alex.json'});
      if(candidate) await page.route('**/css/interactive-controls.css*', r=>r.fulfill({contentType:'text/css',body:fs.readFileSync(candidate,'utf8')}));
      for(const route of routes) {
        await page.goto('https://localhost:7286'+route);
        assert(!page.url().includes('/acceso'),'Authenticated page required');
        const values = () => page.locator('main form').evaluateAll(es=>es.map(e=>[...new FormData(e)].filter(([k])=>!k.includes('VerificationToken'))).sort((a,b)=>JSON.stringify(a).localeCompare(JSON.stringify(b))));
        const original = await values();
        for(const width of [320,768,1440,1920]) {
          await page.setViewportSize({width,height:1000});
          await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
          const filters = page.locator('main .desktop-filter, main .validation-filter-bar > details');
          await filters.evaluateAll(es=>es.forEach(e=>e.open=true));
          const dimensions = await filters.evaluateAll(es=>es.filter(e=>e.checkVisibility()).map(e=>({label:e.querySelector('summary').textContent.slice(0,60),width:e.getBoundingClientRect().width,fields:[...e.querySelectorAll('input:not([type=hidden]),select')].map(c=>c.getBoundingClientRect().width)})));
          for(const d of dimensions) {
            assert(d.width<=385,`${route} ${width}: filter too wide ${JSON.stringify(d)}`);
            assert(d.fields.every(w=>w<=321),`${route}: oversized field ${JSON.stringify(d)}`);
          }
          assert.deepEqual(await values(),original,'Opening/resizing preserves all submitted fields');
          assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),`${route} ${width}: overflow`);
          if(route==='/personas-y-accesos' && width>=1440) {
            const group=page.locator('.people-filters');
            const last=await group.locator('details').last().boundingBox(), button=await group.locator('button[type=submit]').boundingBox();
            assert(button.x-last.x-last.width<=16 && button.x>=last.x+last.width-1,'Buscar must remain next to the last filter');
            assert(button.x+button.width-(await group.boundingBox()).x<800,'Compact people filter cluster');
            await group.scrollIntoViewIfNeeded();
            await page.evaluate(()=>document.documentElement.dataset.theme='dark');
            await page.screenshot({path:`${dir}/filters-${engine}-${width}-dark.png`});
            await page.evaluate(()=>document.documentElement.dataset.theme='light');
          }
          results.push({engine,route,width,filters:dimensions.length});
        }
      }
      await page.goto('https://localhost:7286/personas-y-accesos');
      const search=page.locator('.people-filters [name=Search]');
      await search.locator('xpath=ancestor::details[1]/summary').click();
      await search.fill('alex');
      const state=page.locator('.people-filters [name=State]');
      await state.locator('xpath=ancestor::details[1]/summary').click();
      await state.selectOption('ACTIVA');
      await page.locator('.people-filters button[type=submit]').click();
      await page.waitForURL(u=>u.searchParams.get('Search')==='alex'&&u.searchParams.get('State')==='ACTIVA');
      assert.equal(await page.locator('.people-filters [name=Search]').inputValue(),'alex');
      await page.locator('.people-filters').getByRole('link',{name:'Limpiar'}).click();
      await page.waitForURL(u=>!u.search);
      assert.equal(await page.locator('.people-filters [name=Search]').inputValue(),'');
      const longSearch=page.locator('.people-filters [name=Search]');
      await longSearch.locator('xpath=ancestor::details[1]/summary').click();
      await longSearch.fill('NombreExtenso'.repeat(30));
      await longSearch.locator('xpath=ancestor::details[1]/summary').click();
      for(const width of [320,1440]) {
        await page.setViewportSize({width,height:1000});
        await page.evaluate(()=>document.documentElement.style.fontSize='32px');
        await page.evaluate(()=>new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r))));
        assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),'Long label and 200% text reflow');
        assert.equal(await longSearch.inputValue(),'NombreExtenso'.repeat(30));
        await page.evaluate(()=>document.documentElement.style.fontSize='');
      }
    } finally {await browser.close();}
  }
  fs.writeFileSync(dir+'/filter-layout-results.json',JSON.stringify(results,null,2));
  console.log(`PASS ${results.length} filter layouts, grouped search action, preserved values, GET search/state/reset`);
})().catch(e=>{console.error(e.message);process.exitCode=1;});
