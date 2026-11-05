const { chromium, webkit } = require('playwright');
const assert = require('assert/strict');
const fs = require('fs');
(async () => {
 const dir = process.argv[2]; fs.mkdirSync(dir, {recursive:true}); const results=[];
 for (const [engine,type] of [['chromium',chromium],['webkit',webkit]]) {
  const browser=await type.launch(engine==='chromium'?{channel:'msedge'}:{});
  try {
   const page=await browser.newPage({ignoreHTTPSErrors:true}); const errors=[]; page.on('pageerror',e=>errors.push(e.message));
   for(const theme of ['light','dark','special']) for(const width of [320,390,768,1440]) {
    await page.setViewportSize({width,height:1000}); await page.goto('https://localhost:7297/reportes');
    await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
    const charts=await page.locator('.report-charts').boundingBox(), panel=await page.locator('.report-people').boundingBox();
    assert(panel.y>=charts.y+charts.height-1); assert(Math.abs(panel.width-charts.width)<2);
    assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));
    assert(await page.locator('.report-person-gauge').first().isVisible());
    const region=page.getByRole('region',{name:'Indicadores por colaborador'}); await region.focus(); assert(await region.evaluate(e=>e===document.activeElement));
    await page.locator('.report-people').screenshot({path:`${dir}/${engine}-${theme}-${width}.png`}); results.push({engine,theme,width});
   }
   for(const kind of ['overview','compliance','overdue','load','goals','review','results','plans']) {
    await page.goto('https://localhost:7297/reportes'); await page.locator('[data-report-disclosure]>summary').click();
    await page.locator('#report-template').selectOption('preset:'+kind);
    await Promise.all([page.waitForNavigation(),page.locator('#report-apply-template').click()]);
    const exportUrl=new URL(page.url()); exportUrl.searchParams.set('handler','Export');
    const data=await (await page.request.get(exportUrl.toString())).json();
    assert.equal(data.people.reduce((n,p)=>n+p.total,0),data.rows.filter(r=>!r.includes('Reasignada')).length);
    assert.equal(await page.locator('[data-report-person]').count(),data.people.length);
    for(const p of data.people) {
     const row=page.locator(`[data-report-person="${p.id}"]`);
     assert((await row.innerText()).includes(p.name)); assert.equal(p.pending>=0,true);
    }
   }
   await page.goto('https://localhost:7297/reportes');
   await page.locator('[data-report-status="PENDIENTE_REVISION"]').first().click();
   await page.waitForLoadState('networkidle');
   const focused=new URL(page.url()); focused.searchParams.set('handler','Export');
   const data=await(await page.request.get(focused.toString())).json(); assert(data.people.every(p=>p.pending===0 && p.load===0));
   await page.locator('[data-report-export]').click(); await page.locator('#report-preview').click();
   await page.waitForFunction(()=>!document.querySelector('#report-download').disabled);
   assert.deepEqual(errors,[]);
  } finally { await browser.close(); }
 }
 fs.writeFileSync(`${dir}/results.json`,JSON.stringify(results,null,2)); console.log(`PASS ${results.length} layouts, eight applied templates, focus and PDF in both engines`);
})().catch(e=>{console.error(e);process.exitCode=1});
