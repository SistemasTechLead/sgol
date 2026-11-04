const {chromium, webkit} = require('playwright');
const fs = require('fs'), path = require('path'), assert = require('assert/strict');
const output = path.resolve(__dirname, '../../.local/incidence-integration-20261104');
const fixture = JSON.parse(fs.readFileSync(path.join(output, 'browser-private.json')));
(async () => {
  const results = [];
  let offset = 0;
  for (const [name, engine] of Object.entries({chromium, webkit})) {
    const browser = await engine.launch(name === 'chromium' ? {channel:'msedge'} : {});
    try {
      const context = await browser.newContext({ignoreHTTPSErrors:true, acceptDownloads:true});
      await context.addCookies(fixture.cookies);
      const page = await context.newPage(), errors = [];
      page.on('pageerror', e => errors.push(e.message));
      assert.equal((await page.goto(fixture.origin + fixture.receiptPath)).status(), 200);
      assert.equal(await page.locator('.incidence-evidence').count(), 2);
      for (const width of [320,768,1440]) for (const theme of ['light','dark','special']) {
        await page.setViewportSize({width,height:1000});
        await page.evaluate(t => document.documentElement.dataset.theme=t, theme);
        const summary=page.locator('.incidence-evidence summary').first();
        await summary.focus(); assert(await summary.evaluate(e=>e===document.activeElement));
        if (!(await summary.evaluate(e=>e.parentElement.open))) await page.keyboard.press('Enter');
        assert(await summary.evaluate(e=>e.parentElement.open));
        const link=page.locator('.incidence-evidence a').first();
        await link.focus(); assert(await link.evaluate(e=>e===document.activeElement));
        assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),`overflow ${name}/${width}/${theme}`);
        await page.screenshot({path:path.join(output,`receipt-${name}-${width}-${theme}.png`),fullPage:true});
        results.push({engine:name,width,theme,scenario:'real receipt evidence keyboard and layout',status:'passed'});
      }
      const downloadPromise=page.waitForEvent('download'); await page.locator('.incidence-evidence a').first().click();
      const download=await downloadPromise;assert.equal(await download.failure(),null);
      results.push({engine:name,scenario:'real browser historical capture download',status:'passed'});
      const date=new Date(fixture.day+'T12:00:00Z');date.setUTCDate(date.getUTCDate()+offset++);
      const day=date.toISOString().slice(0,10);
      await page.goto(`${fixture.origin}/supervision/incidencias?Day=${day}`);
      await page.locator('#PersonId').selectOption(fixture.person);
      await page.locator('#Reason').fill('Pérdida de respuesta de red después del commit. '+ 'Motivo largo de prueba. '.repeat(25));
      await Promise.all([page.waitForNavigation(),page.getByRole('button',{name:'Revisar cambios',exact:true}).click()]);
      const confirm=page.locator('[data-incidence-confirm]');assert(await confirm.isVisible());
      let saved;
      await page.route('**/supervision/incidencias?handler=Apply',async route=>{
        const response=await route.fetch({maxRedirects:0});
        saved={status:response.status(),location:response.headers().location,body:route.request().postData()};
        await route.abort('connectionreset');
      });
      await confirm.click().catch(()=>{});
      for(let i=0;i<100 && !saved;i++)await page.waitForTimeout(100);
      assert(saved && saved.status===302,'server must commit before transport loss');
      await page.unrouteAll({behavior:'wait'});
      const replay=await context.request.post(`${fixture.origin}/supervision/incidencias?handler=Apply`,{data:saved.body,headers:{'Content-Type':'application/x-www-form-urlencoded'},maxRedirects:0});
      assert.equal(replay.status(),302);assert.equal(replay.headers().location,saved.location);
      await page.goto(fixture.origin+saved.location);assert(await page.locator('#receipt-title').isVisible());
      await page.reload();assert(await page.locator('#receipt-title').isVisible());
      const tab=await context.newPage();await tab.goto(fixture.origin+saved.location);assert(await tab.locator('#receipt-title').isVisible());await tab.close();
      assert.deepEqual(errors,[]);
      results.push({engine:name,scenario:'real transport abort after commit, same-key replay, receipt reload and second tab',status:'passed'});
    } finally {await browser.close();}
  }
  fs.writeFileSync(path.join(output,'browser-results.json'),JSON.stringify(results,null,2));
  fs.writeFileSync(path.join(output,'browser.done'),'passed');
  console.log(`PASS ${results.length} authenticated browser scenarios`);
})().catch(e=>{console.error(e);process.exitCode=1;});
