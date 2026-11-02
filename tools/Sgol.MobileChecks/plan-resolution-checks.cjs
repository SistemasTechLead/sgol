const { chromium, webkit } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async () => {
    const out = process.argv[2]; fs.mkdirSync(out, { recursive: true });
    const moves = process.argv[3] === 'moves';
    const results = [];
    for (const [name, engine] of [['chromium', chromium], ['webkit', webkit]]) {
        const browser = await engine.launch(name === 'chromium' ? { channel: 'msedge' } : {});
        try {
            const page = await browser.newPage({ ignoreHTTPSErrors: true });
            const errors=[];page.on('pageerror', e=>errors.push(e.message));
            await page.goto('https://localhost:7257/planificacion?mode=operations');
            await Promise.all([page.waitForNavigation(), page.locator('button[form="direction-create-form"][type="submit"]').click()]);
            const opener=page.locator('[data-selected-code] [aria-controls="direction-conflict-review"]');
            assert.equal(await opener.count(),1);
            const dialog=page.locator('#direction-conflict-review');
            for (const theme of ['light','dark','special']) for (const width of [320,390,768,1280]) {
                await page.setViewportSize({width,height:900});
                await page.evaluate(t=>document.documentElement.dataset.theme=t,theme);
                await opener.click();
                if(moves) {
                    const options=dialog.locator('details:has([name="moveChoice"])').first();
                    if(await options.getAttribute('open')===null) await options.locator(':scope > summary').click();
                }
                assert(await dialog.isVisible());
                assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1));
                assert(await dialog.evaluate(e=>e.scrollWidth<=e.clientWidth+1));
                assert(await dialog.locator('[name="resolutionAdjustment"]').first().isVisible());
                if(moves) {
                    await dialog.locator('[name="moveChoice"]').first().scrollIntoViewIfNeeded();
                    assert(await dialog.locator('[name="moveChoice"]').first().isVisible());
                }
                if(width===390||width===1280) await page.screenshot({path:`${out}/${name}-${theme}-${width}.png`});
                await page.keyboard.press('Escape');
                assert(!(await dialog.isVisible()));
                await page.waitForFunction(()=>document.activeElement?.matches('[data-selected-code] [aria-controls="direction-conflict-review"]'),null,{timeout:2000});
                results.push({engine:name,theme,width});
            }
            await opener.focus();await page.keyboard.press('Enter');
            for(let i=0;i<5;i++) {
                await page.keyboard.press('Tab');
                assert(await dialog.evaluate(e=>e.contains(document.activeElement)), 'Focus remains within dialog');
            }
            await Promise.all([page.waitForNavigation(),dialog.locator(moves?'[name="moveChoice"]':'[name="resolutionAdjustment"]').first().click()]);
            const adjustments=JSON.parse(await page.locator(moves?'[name="occurrenceMoves"]':'[name="capacityAdjustments"]').inputValue());
            assert(moves ? adjustments.some(a=>a.fromDay&&a.toDay) : adjustments.some(a=>a.day&&a.dailyMinutes));
            const confirmation=page.locator('#direction-plan-confirm');
            if(await confirmation.isVisible()) await confirmation.locator('[data-dialog-close]').click();
            const undo=moves?'Deshacer cambio de fecha':'Deshacer ajuste';
            assert(await page.getByRole('button',{name:undo,exact:true}).count()>0);
            await Promise.all([page.waitForNavigation(),page.getByRole('button',{name:undo,exact:true}).first().click()]);
            assert.equal(JSON.parse(await page.locator(moves?'[name="occurrenceMoves"]':'[name="capacityAdjustments"]').inputValue()).filter(a=>moves?a.fromDay:a.day).length,0);
            assert.deepEqual(errors,[]);
        } finally { await browser.close(); }
    }
    fs.writeFileSync(`${out}/results.json`,JSON.stringify(results,null,2));
    console.log(`PASS ${results.length} real-page dialog cases, keyboard, resolution and undo in both engines`);
})().catch(e=>{console.error(e);process.exit(1);});
