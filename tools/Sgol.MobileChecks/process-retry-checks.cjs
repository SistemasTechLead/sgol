const { chromium, webkit } = require('playwright');
const assert = require('assert/strict');
const fs = require('fs');
(async () => {
    const [id, file, folder] = process.argv.slice(2), results = [];
    for (const [engine, type] of [['chromium', chromium], ['webkit', webkit]]) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        for (const width of [320, 1280]) {
            const page = await browser.newPage({ ignoreHTTPSErrors: true, viewport: { width, height: 900 } });
            await page.goto(`https://localhost:7259/mi-trabajo?assignedTask=${id}`);
            const form = page.locator('[data-process-delivery]');
            await form.locator('[name=Document]').setInputFiles(file);
            await form.locator('[name=conclusion]').fill('Archivo conservado durante correcciones');
            await form.locator('[name=Declared]').check();
            await form.locator('[name=ItemQuantity]').fill('20');
            const submit = form.locator('button[value=ENTREGA]');
            const send = async expected => {
                const response = page.waitForResponse(r=>r.url().includes('handler=CompleteProcess') && r.request().method()==='POST');
                await submit.click();assert.equal((await response).status(),expected);
                await page.waitForFunction(()=>!document.querySelector('[data-process-delivery]').hasAttribute('aria-busy'));
                assert.equal(await form.locator('[name=Document]').evaluate(e=>e.files[0]?.name),'evidence.xlsx');
                assert.equal(await form.locator('[name=conclusion]').inputValue(),'Archivo conservado durante correcciones');
            };
            await send(409);
            assert.equal(await form.locator('[data-item-conflict]').count(),1);
            assert.equal(await form.locator('[data-item-conflict]').evaluate(e=>e===document.activeElement),true);
            await form.locator('[name=CapacityResolution]').selectOption('LOWEST');
            await form.locator('[name=CapacityFingerprint]').evaluate(e=>e.value='stale');
            await send(409);
            for (const theme of ['light','dark','special']) {
                await page.evaluate(theme=>document.documentElement.dataset.theme=theme,theme);
                const choice=form.locator('[name=CapacityResolution]');
                await choice.scrollIntoViewIfNeeded();await choice.focus();await page.keyboard.press('Tab');
                const box=await choice.boundingBox();assert(box.x>=0 && box.x+box.width<=width+1);
                await page.screenshot({path:`${folder}/${engine}-${width}-${theme}.png`});
            }
            await form.locator('[name=CapacityResolution]').selectOption('LOWEST');
            await form.locator('[name=ItemQuantity]').fill('');
            // Native validation blocks an empty count without losing the file.
            assert.equal(await form.locator('[name=ItemQuantity]').evaluate(e=>e.checkValidity()),false);
            await form.locator('[name=ItemQuantity]').fill('20');
            await form.locator('[name=Document]').setInputFiles({name:'replacement.xlsx',mimeType:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',buffer:fs.readFileSync(file)});
            assert.equal(await form.locator('[name=Document]').evaluate(e=>e.files[0].name),'replacement.xlsx');
            await page.route('**/*handler=CompleteProcess',route=>route.abort('failed'));
            await submit.click();await page.waitForFunction(()=>!document.querySelector('[data-process-delivery]').hasAttribute('aria-busy'));
            assert.equal(await form.locator('[name=Document]').evaluate(e=>e.files[0].name),'replacement.xlsx');
            assert(await form.locator('[data-process-submit-status]').isVisible());
            await page.screenshot({path:`${folder}/${engine}-${width}.png`});
            results.push({engine,width,passed:true});await page.close();
        }
        await browser.close();
    }
    fs.writeFileSync(`${folder}/results.json`,JSON.stringify(results,null,2));
    console.log(`PASS ${results.length} real browser retry flows: file retention, repeated conflicts, focus, replacement, network error`);
})().catch(e=>{console.error(e);process.exit(1);});
