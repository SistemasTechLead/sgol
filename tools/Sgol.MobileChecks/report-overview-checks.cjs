const {chromium,webkit}=require('playwright');
const fs=require('fs');const assert=require('assert/strict');
(async()=>{const results=[];const dir=process.argv[2];
for(const [name,type] of [['chromium',chromium],['webkit',webkit]]){
 const browser=await type.launch(name==='chromium'?{channel:'msedge'}:{});
 try {const page=await browser.newPage({ignoreHTTPSErrors:true}); const errors=[];page.on('pageerror',e=>errors.push(e.message));
 for(const theme of ['light','dark','special']) for(const width of [320,390,768,1440]){
 await page.setViewportSize({width,height:1000});await page.goto('https://localhost:7297/reportes');await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
 assert.equal(await page.locator('[data-report-disclosure]').getAttribute('open'),null);
 assert(await page.locator('#report-title').isVisible());assert((await page.locator('#report-title').boundingBox()).y<400);
 assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));
 assert.equal(await page.locator('.report-footer [data-report-save]').count(),1);
 const summary=page.locator('[data-report-disclosure]>summary');await summary.focus();await page.keyboard.press('Enter');assert(await page.locator('#report-filters').isVisible());
 await summary.click();
 await page.screenshot({path:`${dir}/${name}-${theme}-${width}.png`});results.push({name,theme,width});
 }
 await page.setViewportSize({width:1440,height:1000});await page.goto('https://localhost:7297/reportes');
 for(const index of [0,1]){const card=page.locator('.report-metric--queue:not([data-report-carryovers])').nth(index);const count=await card.locator('strong').innerText();await card.getByRole('link').click();assert((await page.locator('.validation-section-title').innerText()).includes(`de ${count} tareas`));await page.getByRole('button',{name:'Filtrar',exact:true}).click();assert(new URL(page.url()).searchParams.get('pending.reviewer'));await page.goto('https://localhost:7297/reportes');}
 const carryover=page.locator('[data-report-carryovers]'); const requestCount=await carryover.locator('strong').innerText(); await carryover.getByRole('link').focus(); await page.keyboard.press('Enter'); await page.waitForURL('**/validaciones#carryovers'); assert.equal(await page.locator('#carryovers-title .badge').innerText(),requestCount); await page.goto('https://localhost:7297/reportes');
 await page.locator('[data-report-save]').click();await page.locator('#TemplateName').fill('Resumen QA navegador '+name);await Promise.all([page.waitForNavigation(),page.locator('#report-save-form button[type=submit]').click()]);assert(await page.getByText('Plantilla guardada.',{exact:true}).isVisible());
 await page.locator('[data-report-export]').click();assert(await page.locator('#report-pdf-dialog').isVisible());await page.locator('#report-preview').click();await page.waitForFunction(()=>!document.querySelector('#report-download').disabled);await page.locator('#report-pdf-dialog [data-report-close]').click();
 await page.goto('https://localhost:7297/reportes?F.Range=custom&F.From=2026-11-10&F.To=2026-11-01');assert(await page.locator('[role=alert]').isVisible());assert(await page.locator('[data-report-disclosure]').getAttribute('open')!==null);
 assert.deepEqual(errors,[]);
 }finally{await browser.close();}
}fs.writeFileSync(`${dir}/browser-results.json`,JSON.stringify(results,null,2));console.log(`PASS ${results.length} layouts; queue links, retained filters, template save, PDF preview and invalid dates in both engines`);
})().catch(e=>{console.error(e);process.exitCode=1});
