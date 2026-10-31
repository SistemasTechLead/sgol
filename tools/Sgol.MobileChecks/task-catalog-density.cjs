/* Real catalog, read-only. Private directory: origin.txt, state-alex.json,
   baseline-state.json (previous published version on localhost:7285). */
const { chromium, webkit } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
(async () => {
    const dir = path.resolve(process.argv[2]);
    const origin = fs.readFileSync(path.join(dir, 'origin.txt'), 'utf8').trim();
    const results = [], errors = [];
    const formData = page => page.evaluate(() => [...document.forms].map(f => ({
        action: f.getAttribute('action'), fields: [...new FormData(f)].filter(([k]) => !k.includes('Token'))
    })));
    const desktop = page => page.evaluate(() => [...document.querySelectorAll('main h1, main p, main .encabezado-pagina__descripcion, main nav a, main input:not([type=hidden]), main select, main textarea, .task-action-panel button')]
        .filter(e => !e.matches('.mobile-fold-toggle'))
        .map(e => { const r = e.getBoundingClientRect(); return { tag: e.matches('.encabezado-pagina__descripcion') ? 'DESCRIPTION' : e.tagName, name: e.getAttribute('name'), text: e.matches('p,h1,a,button,.encabezado-pagina__descripcion') ? e.textContent.trim() : null,
            box: [r.x,r.y,r.width,r.height].map(Math.round) }; }).filter(e => e.box[2] && e.box[3]));
    for (const [engine, type] of [['chromium', chromium], ['webkit', webkit]]) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        const ctx = await browser.newContext({ ignoreHTTPSErrors: true, storageState: path.join(dir, 'state-alex.json') });
        const page = await ctx.newPage();
        page.on('pageerror', e => errors.push({engine, message: e.message}));
        for (const width of [320, 390, 430, 768, 769, 1440]) {
            await page.setViewportSize({width, height: 844});
            assert.equal((await page.goto(origin + '/tareas')).status(), 200);
            await page.locator('[data-task-row]').first().waitFor();
            if (width <= 768) await page.locator('.task-catalog-management').waitFor();
            for (const theme of ['light','dark','special']) for (const scale of (width === 320 ? [1,2] : [1])) {
                await page.evaluate(async ({theme,scale}) => {
                    document.documentElement.dataset.theme=theme; document.documentElement.style.fontSize=16*scale+'px';
                    await new Promise(requestAnimationFrame); await new Promise(requestAnimationFrame);
                }, {theme,scale});
                assert(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1), `${engine} ${width} ${theme} ${scale} overflow`);
                if (width <= 768) {
                    assert.equal(await page.locator('.task-action-panel').isVisible(),false,'Unselected panel consumes space');
                    assert.equal(await page.locator('[data-task-catalog-guidance]').isVisible(),false);
                    assert.equal(await page.locator('#task-selection-help').isVisible(),false);
                    const first=await page.locator('[data-task-row]').first().boundingBox();
                    results.push({engine,width,theme,scale,firstTaskTop:first.y});
                    if (scale===1 && width===390) await page.screenshot({path:path.join(dir,`${engine}-${theme}-after.png`)});
                }
            }
        }
        await page.setViewportSize({width:390,height:844}); await page.goto(origin+'/tareas');
        await page.locator('.task-catalog-management').waitFor();
        const fields=await formData(page);
        const summary=page.locator('.task-catalog-management > summary');
        await summary.focus(); await page.keyboard.press('Enter');
        assert(await page.locator('.tasks-global-refresh button').isVisible());
        assert(await page.locator('[data-presentation=tertiary]').isVisible());
        assert(await page.locator('#global-refresh-status').isVisible());
        assert.deepEqual(await formData(page),fields);
        await page.keyboard.press('Enter');
        assert.equal(await page.locator('.tasks-global-refresh button').isVisible(),false);
        await page.locator('.mobile-info-toggle').click();
        assert(await page.locator('[data-task-catalog-guidance]').isVisible());
        assert(await page.locator('#task-selection-help').isVisible()); await page.keyboard.press('Escape');
        const row=page.locator('[data-task-row]').first(); await row.locator('.task-selection').check();
        await page.locator('.task-action-panel').waitFor();
        assert.equal(await page.locator('[data-task-selection-hint]').isVisible(),false);
        await page.locator('[data-mobile-control=task-secondary]').click();
        assert(await page.locator('[data-task-selection-hint]').isVisible());
        assert.equal(await page.locator('[data-task-assign]').isDisabled(),await row.getAttribute('data-task-assignable')!=='true');
        await page.locator('[data-task-delete]').click(); await page.locator('#task-delete [data-dialog-close]').click();
        const selectedFields=await formData(page);
        await page.setViewportSize({width:1440,height:1000});
        await page.waitForFunction(() => document.querySelector('#global-refresh-status').parentElement.matches('[data-task-catalog]'));
        assert.deepEqual(await formData(page), selectedFields);
        assert.equal(await page.locator('.task-catalog-management').isVisible(),false);
        assert(await page.locator('[data-task-catalog-guidance]').isVisible());
        await page.setViewportSize({width:390,height:844});
        await page.locator('.task-catalog-management').waitFor();
        await page.reload(); await page.locator('.task-catalog-management').waitFor();
        assert.equal(await page.locator('.task-selection:checked').count(),0);
        assert.equal(await page.locator('.task-action-panel').isVisible(),false);
        const oldCtx = await browser.newContext({ignoreHTTPSErrors:true,storageState:path.join(dir,'baseline-state.json'),viewport:{width:1440,height:1000}});
        const old = await oldCtx.newPage();
        for (const route of ['/tareas','/tareas?create=true','/tareas?AdjustClosure=true','/tareas?Search=zzzzemptyfixture']) {
            await page.setViewportSize({width:1440,height:1000}); await page.goto(origin+route); await old.goto('https://localhost:7285'+route);
            await page.evaluate(()=>document.fonts.ready); await old.evaluate(()=>document.fonts.ready);
            assert.deepEqual(await desktop(page),await desktop(old),route+' desktop changed');
            const original=await formData(page);
            await page.setViewportSize({width:390,height:844}); await page.locator('.task-catalog-management').waitFor();
            await page.setViewportSize({width:1440,height:1000});
            await page.waitForFunction(()=>document.querySelector('#global-refresh-status').parentElement.matches('[data-task-catalog]'));
            assert.deepEqual(await formData(page),original,route+' form changed across views');
            assert.deepEqual(await desktop(page),await desktop(old),route+' restored desktop changed');
            await page.emulateMedia({media:'print'}); await old.emulateMedia({media:'print'});
            assert.deepEqual(await desktop(page),await desktop(old),route+' print changed');
            await page.emulateMedia({media:'screen'}); await old.emulateMedia({media:'screen'});
        }
        await old.setViewportSize({width:390,height:844}); await old.goto('https://localhost:7285/tareas');
        await old.locator('.mobile-records').waitFor(); await old.screenshot({path:path.join(dir,engine+'-before.png')});
        const beforeTop=(await old.locator('[data-task-row]').first().boundingBox()).y;
        const afterTop=results.find(r=>r.engine===engine&&r.width===390&&r.theme==='light'&&r.scale===1).firstTaskTop;
        assert(afterTop<beforeTop-150,'No meaningful density improvement');
        results.push({engine,beforeTop,afterTop,savedHeight:beforeTop-afterTop});
        await browser.close();
    }
    fs.writeFileSync(path.join(dir,'density-results.json'),JSON.stringify({results,errors},null,2));
    assert.deepEqual(errors,[]); console.log('PASS density, disclosures, selection, fields, desktop and print',results.length);
})().catch(e=>{console.error(e);process.exit(1)});
