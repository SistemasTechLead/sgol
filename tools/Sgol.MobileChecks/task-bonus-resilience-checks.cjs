const { chromium, webkit } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async () => {
    const dir=process.argv[2], fixture=JSON.parse(fs.readFileSync(`${dir}/fixture.json`,'utf8')), results=[];
    for(const [engine,type] of [['chromium',chromium],['webkit',webkit]]) {
        const browser=await type.launch(engine==='chromium'?{channel:'msedge'}:{});
        const context=await browser.newContext({ignoreHTTPSErrors:true}); const page=await context.newPage();
        await page.goto(`${fixture.origin}/mi-trabajo`);
        const score=await page.locator('[data-bonus-score]').textContent();
        await page.goto(`${fixture.origin}/mi-trabajo?WorkSearch=NO_EXISTE_EN_ESTA_PRUEBA&WorkPage=2`);
        assert.equal(await page.locator('[data-work-task]').count(),0);
        assert.equal(await page.locator('[data-bonus-score]').textContent(),score,'filtered/paginated empty tray retains the weekly score');
        results.push({engine,scenario:'empty search and pagination do not change weekly score',status:'approved'});
        const match=url=>url.pathname==='/mi-trabajo'&&url.searchParams.get('handler')==='Bonus';
        let held, release, requests=0; const arrived=new Promise(r=>held=r), gate=new Promise(r=>release=r);
        await page.route(match,async route=>{
            requests++; const actual=await route.fetch(); held(); await gate;
            try { await route.fulfill({response:actual}); } catch { /* navigation/timeout cancelled the request */ }
        });
        await page.evaluate(()=>window.dispatchEvent(new Event('focus'))); await arrived;
        await page.evaluate(()=>window.dispatchEvent(new Event('focus'))); await page.waitForTimeout(300);
        assert.equal(requests,1,'only one request in flight');
        await page.waitForFunction(()=>document.querySelector('[data-bonus-status]').textContent.includes('desactualizado'),null,{timeout:12000});
        assert.equal(await page.locator('[data-bonus-score]').textContent(),score,'timeout retains last value');
        await page.goto(`${fixture.origin}/mi-trabajo?BonusWeek=2026-10-26`);
        release(); await page.waitForTimeout(500);
        assert((await page.locator('[data-task-bonus]').innerText()).includes('Evaluación aún no vigente'));
        assert.equal(await page.locator('[role="meter"]').count(),0,'late old-period response does not replace selected period');
        await page.unroute(match);
        await page.goto(`${fixture.origin}/mi-trabajo`);
        await page.evaluate(()=>{window.dispatchEvent(new PageTransitionEvent('pagehide',{persisted:true}));window.dispatchEvent(new PageTransitionEvent('pageshow',{persisted:true}));});
        await page.waitForFunction(()=>document.querySelector('[data-bonus-status]').textContent.includes('Actualizado'));
        results.push({engine,scenario:'timeout, single-flight, late response after period change, restored-page refresh',status:'approved'});
        await context.close();await browser.close();
    }
    fs.writeFileSync(`${dir}/browser-resilience-results.json`,JSON.stringify(results,null,2)); console.log(`PASS ${results.length} resilience cases`);
})().catch(e=>{console.error(e);process.exit(1);});
