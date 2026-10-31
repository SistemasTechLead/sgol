/* Real /acceso UI. NODE_PATH must supply Playwright. Usage:
 * node access-mobile-checks.cjs <private-evidence-dir> [--write]
 * origin.txt is required. --write additionally requires isolated-database.txt,
 * localhost:7284, and optional credentials.json {userName,password}.
 * Screenshots never contain entered passwords, session state or recovery codes.
 * Keyboard automation and WebKit are not physical-device validation.
 */
const { chromium, webkit } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const dir = path.resolve(process.argv[2]);
const origin = fs.readFileSync(path.join(dir, 'origin.txt'), 'utf8').trim();
const writes = process.argv.includes('--write');
if (writes) {
    assert.equal(origin, 'https://localhost:7284');
    assert.match(fs.readFileSync(path.join(dir, 'isolated-database.txt'), 'utf8').trim(), /^sgol_task_checks_[a-z0-9_]+$/);
}
const results = [];
const baselinePath = path.join(dir, 'baseline.json');
const baseline = fs.existsSync(baselinePath) ? JSON.parse(fs.readFileSync(baselinePath)) : [];
async function measure(page) {
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    return page.evaluate(() => {
        const rect = s => document.querySelector(s).getBoundingClientRect().toJSON();
        return { card: rect('.bienvenida-acceso'), brand: rect('.bienvenida-acceso__marca'),
            access: rect('.acceso'), title: rect('h1'), version: rect('.acceso__version-movil'),
            overflow: document.documentElement.scrollWidth - document.documentElement.clientWidth,
            documentHeight: document.documentElement.scrollHeight, scrollY: window.scrollY,
            helpGap: rect('.acceso__ayuda').top-rect('h1').bottom,
            versionGap: rect('.acceso__version-movil').top-rect('.acceso').bottom,
            fieldGap: document.querySelectorAll('.campo')[1].getBoundingClientRect().top-document.querySelector('.campo').getBoundingClientRect().bottom,
            labelGap: rect('#userName').top-rect('label[for=userName]').bottom,
            headings: [...document.querySelectorAll('h1')].filter(e => e.checkVisibility()).map(e => e.innerText),
            versions: [...document.querySelectorAll('.version-sistema')].filter(e => e.checkVisibility()).length,
            controls: [...document.querySelectorAll('.campo__control,button[type=submit]')].map(e=>e.getBoundingClientRect().toJSON()) };
    });
}
(async () => {
 for (const [engine, type] of Object.entries({ chromium, webkit })) {
    const browser = await type.launch({ headless: true });
    const context = await browser.newContext({ ignoreHTTPSErrors: true });
    const page = await context.newPage();
    const check = async (scenario, action) => {
        try { const evidence = await action(); results.push({ engine, scenario, state: 'passed', evidence }); }
        catch (e) { results.push({ engine, scenario, state: 'failed', reason: e.message }); }
        fs.writeFileSync(path.join(dir, 'results.json'), JSON.stringify(results, null, 2));
    };
    const open = async () => {
        const response = await page.goto(origin + '/acceso'); assert.equal(response.status(), 200);
        await page.evaluate(() => document.fonts.ready);
    };
    for (const [width, height] of [[320,568],[360,640],[390,844],[430,932],[767,900],[768,900],[769,900],[1280,800],[1440,900],[844,390]]) {
      await page.setViewportSize({ width, height }); await open();
      for (const theme of ['light','dark']) await check(`${width}x${height}-${theme}`, async () => {
        await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
        const m = await measure(page); assert(m.overflow <= 1); assert.equal(m.versions, 1);
        if (width <= 768) {
            assert.deepEqual(m.headings, ['Acceso a SGOL']);
            assert(m.card.width <= 360 && m.card.x >= 24);
            assert(Math.abs(m.access.top - m.brand.bottom - 24) <= 1);
            assert(Math.abs(m.helpGap-8)<=1 && Math.abs(m.versionGap-24)<=1);
            assert(Math.abs(m.fieldGap-16)<=1 && Math.abs(m.labelGap-8)<=1);
            assert(m.controls.every(r => r.height >= 48 && Math.abs(r.x-m.card.x)<1 && Math.abs(r.width-m.card.width)<1));
            assert(m.card.top >= 23);
            if (m.card.height + 48 <= height) assert(Math.abs(m.card.top - (height-m.card.bottom)) <= 8);
            await page.locator('.acceso__version-movil').scrollIntoViewIfNeeded();
            assert(await page.locator('.acceso__version-movil').isVisible());
        } else {
            assert.deepEqual(m.headings,['Acceso']);
            const old = baseline.find(r=>r.engine===engine&&r.width===width&&r.height===height&&r.theme===theme);
            if(old) for(const [s,key] of [['.bienvenida-acceso','card'],['.bienvenida-acceso__marca','brand'],['.acceso','access'],['h1','title']])
                for(const dimension of ['x','y','width','height']) assert(Math.abs(old.boxes[s][dimension]-m[key][dimension])<1, `Desktop ${s} ${dimension} changed`);
        }
        await page.screenshot({path:path.join(dir,`${engine}-${width}-${theme}.png`),fullPage:true}); return m;
      });
    }
    await check('saved layout preferences and transitions', async()=>{
        await page.setViewportSize({width:390,height:844});
        for(const [mode,mobile] of [['desktop',false],['mobile',true],['auto',true]]) {
            await page.evaluate(mode=>localStorage.setItem('sgol.appearance.layout',mode),mode);await open();
            assert.equal(await page.locator('.acceso__titulo-movil').isVisible(),mobile);
            assert.equal((await measure(page)).versions,1);
        }
    });
    await check('automated keyboard, toggle and empty validation',async()=>{
        await open();await page.locator('#userName').focus();
        await page.keyboard.press('Tab');assert.equal(await page.locator('#password').evaluate(e=>e===document.activeElement),true);
        await page.keyboard.press('Tab');assert.equal(await page.locator('[data-credential-toggle]').evaluate(e=>e===document.activeElement),true);
        assert.notEqual(await page.locator('[data-credential-toggle]').evaluate(e=>getComputedStyle(e).outlineStyle),'none');
        await page.keyboard.press('Enter');assert.equal(await page.locator('#password').getAttribute('type'),'text');
        assert.equal(await page.locator('[data-credential-toggle]').getAttribute('aria-pressed'),'true');
        await page.keyboard.press('Tab');assert.equal(await page.locator('[type=submit]').evaluate(e=>e===document.activeElement),true);
        await page.keyboard.press('Shift+Tab');await page.keyboard.press('Enter');
        assert.equal(await page.locator('#password').getAttribute('type'),'password');
        await page.locator('[type=submit]').click();assert(await page.locator('#userName').evaluate(e=>!e.validity.valid));
    });
    if(writes) {
      await check('real invalid login, slow submit and enlarged error', async()=>{
        await page.locator('#userName').fill('access-mobile-nonexistent-fixture');await page.locator('#password').fill('invalid-fixture');
        await page.route('**/acceso',async route=>{if(route.request().method()==='POST')await new Promise(r=>setTimeout(r,750));await route.continue();});
        await page.locator('[type=submit]').click({noWaitAfter:true});
        await page.waitForFunction(()=>document.querySelector('[type=submit]').disabled);
        await page.locator('#acceso-error').waitFor();await page.unroute('**/acceso');
        assert.equal(await page.locator('#password').getAttribute('aria-invalid'),'true');
        assert.equal(await page.locator('#password').getAttribute('aria-describedby'),'password-error');
        await page.setViewportSize({width:320,height:390});
        await page.evaluate(()=>{
            // Tokens also contain px fonts: doubling the root alone is not 200% text.
            const sizes=[...document.querySelectorAll('.bienvenida-acceso--login, .bienvenida-acceso--login *')].map(e=>{
                const s=getComputedStyle(e);return {e,font:parseFloat(s.fontSize),line:parseFloat(s.lineHeight)};
            });
            for(const {e,font,line} of sizes){
                e.style.fontSize=font*2+'px';
                e.style.lineHeight=(Number.isFinite(line)&&line>=font ? line : font*1.5)*2+'px';
            }
        });
        const m=await measure(page);assert(m.overflow<=1, `Horizontal overflow: ${m.overflow}px`);assert(m.card.top+m.scrollY>=23);
        await page.locator('[type=submit]').scrollIntoViewIfNeeded();
        const button=await page.locator('[type=submit]').boundingBox();assert(button.y>=0&&button.y+button.height<=391);
        await page.screenshot({path:path.join(dir,`${engine}-error-200.png`),fullPage:true});return m;
      });
      const credentialsPath=path.join(dir,'credentials.json');
      if(fs.existsSync(credentialsPath)) await check('real successful login',async()=>{
        const credentials=JSON.parse(fs.readFileSync(credentialsPath));await open();
        await page.locator('#userName').fill(credentials.userName);await page.locator('#password').fill(credentials.password);
        await page.locator('[type=submit]').click();await page.waitForURL('**/mi-trabajo');
        return {destination:new URL(page.url()).pathname};
      });
      else results.push({engine,scenario:'real successful login',state:'not-run',reason:'No isolated credentials supplied'});
    }
    await browser.close();
 }
 fs.writeFileSync(path.join(dir,'results.json'),JSON.stringify(results,null,2));
 console.log(JSON.stringify({passed:results.filter(r=>r.state==='passed').length,failed:results.filter(r=>r.state==='failed'),notRun:results.filter(r=>r.state==='not-run')}));
 if(results.some(r=>r.state==='failed'))process.exitCode=1;
})().catch(e=>{console.error(e.message);process.exitCode=1;});
