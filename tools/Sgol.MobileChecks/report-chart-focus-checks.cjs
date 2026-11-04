const {chromium,webkit}=require('playwright');const fs=require('fs'),assert=require('assert/strict');
(async()=>{const dir=process.argv[2];let checks=0;
for(const [engine,type] of [['chromium',chromium],['webkit',webkit]]){const browser=await type.launch(engine==='chromium'?{channel:'msedge'}:{});try{
 const page=await browser.newPage({ignoreHTTPSErrors:true});await page.goto('https://localhost:7297/reportes');
 const values=()=>page.locator('.report-metrics:not(.report-metrics--queues) strong').allTextContents();const original=await values();
 const detail=await page.locator('.report-table tbody').innerText();const base=await (await page.request.get('https://localhost:7297/reportes?handler=Export')).json();
 for(const status of ['VENCIDA','PENDIENTE_REVISION','CONCLUIDA']){
 await page.locator(`.report-metric[data-report-status="${status}"]`).click();await page.waitForURL(u=>u.searchParams.get('ChartStatus')===status);
 assert.deepEqual(await values(),original);assert.equal(await page.locator('.report-table tbody').innerText(),detail);
 assert.equal(await page.locator(`.report-metric[data-report-status="${status}"]`).getAttribute('aria-pressed'),'true');
 const url=new URL(page.url());url.searchParams.set('handler','Export');const focused=await (await page.request.get(url.href)).json();
 assert.deepEqual(focused.metrics,base.metrics);assert.deepEqual(focused.rows,base.rows);assert.equal(focused.charts.length,3);
 const count=base.charts[0].points.find(p=>p.key===status).value;assert.equal(focused.charts[2].points.reduce((n,p)=>n+p.value,0),count);checks++;
 }
 await page.locator('.report-metric[data-report-status="CONCLUIDA"]').click();await page.waitForURL(u=>!u.searchParams.has('ChartStatus'));assert.deepEqual(await values(),original);checks++;
 await page.locator('.report-metric[data-report-status="VENCIDA"]').click();await page.waitForURL(u=>u.searchParams.has('ChartStatus'));
 for(const theme of ['light','dark','special'])for(const width of [320,390,768,1440]){await page.setViewportSize({width,height:1000});await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));assert.equal(await page.locator('.report-chart').count(),3);await page.locator('#report-charts').scrollIntoViewIfNeeded();await page.screenshot({path:`${dir}/focus-${engine}-${theme}-${width}.png`});checks++;}
 await page.locator('[data-report-clear-focus]').click();await page.waitForURL(u=>!u.searchParams.has('ChartStatus'));assert.deepEqual(await values(),original);checks++;
 await page.goto('https://localhost:7297/reportes?F.Status=VENCIDA&ChartStatus=CONCLUIDA');assert.equal(await page.locator('.report-chart').last().locator('.report-bar-row').count(),0);assert(await page.locator('.report-chart').last().getByText('Sin registros para esta gráfica.').isVisible());checks++;
 await page.locator('[data-report-export]').click();await page.locator('#report-preview').click();await page.waitForFunction(()=>!document.querySelector('#report-download').disabled);checks++;
}finally{await browser.close();}}
console.log(`PASS ${checks} focus checks: stable cards/detail, exact person totals, toggling, empty focus, PDF and 24 layouts`);
})().catch(e=>{console.error(e);process.exitCode=1});
