const { chromium, webkit } = require('playwright');
const fs = require('fs');
const assert = require('assert/strict');
(async () => {
    const dir = process.argv[2];
    const fixture = JSON.parse(fs.readFileSync(`${dir}/fixture.json`, 'utf8'));
    const results = [];
    for (const [engine, type] of [['chromium', chromium], ['webkit', webkit]]) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        const context = await browser.newContext({ ignoreHTTPSErrors: true });
        const page = await context.newPage();
        const errors = []; page.on('pageerror', e => errors.push(e.message));
        await page.request.post(`${fixture.origin}/__test/clock/2/10`);
        for (const path of ['/mi-trabajo', '/supervision/bono-tareas', '/tablero']) {
            assert.equal((await page.goto(fixture.origin + path, { waitUntil: 'domcontentloaded' })).status(), 200);
            const gauge = page.locator(`[data-task-bonus][data-person="${fixture.person}"]`);
            await gauge.waitFor();
            for (const theme of ['light', 'dark', 'special']) {
                await page.evaluate(theme => document.documentElement.dataset.theme = theme, theme);
                for (const width of [320, 375, 768, 1280, 1920]) {
                    await page.setViewportSize({ width, height: 900 });
                    await page.waitForTimeout(250);
                    if (path === '/tablero') {
                        const people = page.locator('[data-mobile-control="board-people"]');
                        await page.waitForFunction(() => document.getElementById('live-board')?.getAttribute('aria-busy') !== 'true');
                        if (await people.isVisible() && await people.getAttribute('aria-expanded') === 'false') await people.click();
                        try { await gauge.waitFor({ state: 'visible', timeout: 3000 }); }
                        catch (e) {
                            console.log(engine, theme, width, await people.evaluateAll(es => es.map(e => ({ html: e.outerHTML, display: getComputedStyle(e).display }))));
                            console.log(await gauge.evaluate(g => { const a=[]; for(let e=g;e;e=e.parentElement) a.push({tag:e.tagName, cls:e.className, display:getComputedStyle(e).display}); return a; }));
                            throw e;
                        }
                    }
                    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `${engine} ${path} ${theme} ${width}: global overflow`);
                    await gauge.scrollIntoViewIfNeeded();
                    const box = await gauge.boundingBox(); assert(box && box.width > 0 && box.x >= 0 && box.x + box.width <= width + 1, `${engine} ${path} ${theme} ${width}: gauge fits viewport ${JSON.stringify(box)}`);
                    if (path === '/mi-trabajo' && width >= 1280) {
                        const title = await page.locator('h1').boundingBox();
                        assert(Math.abs(box.y - title.y) < 24 && box.x > title.x, 'desktop gauge occupies the top right beside the heading');
                    }
                    const summary = gauge.locator(':scope > summary');
                    await summary.focus(); await page.keyboard.press('Enter');
                    assert.equal(await gauge.getAttribute('open'), '');
                    await expectVisible(gauge.locator('.task-bonus__explanation'));
                    assert((await gauge.innerText()).includes('12:00'));
                    await page.keyboard.press('Escape');
                    assert.equal(await gauge.getAttribute('open'), null);
                    assert(await summary.evaluate(e => e === document.activeElement), `${engine} ${path} ${theme} ${width}: Escape restores focus`);
                    if (path === '/tablero') {
                        const sizes = await gauge.evaluate(g => {
                            const row = g.closest('.board-progress'); const before = row.getBoundingClientRect().height;
                            const parent = g.parentNode, next = g.nextSibling; g.remove(); const after = row.getBoundingClientRect().height; parent.insertBefore(g, next);
                            return { before, after };
                        });
                        assert(Math.abs(sizes.before - sizes.after) <= 1, `${engine} ${theme} ${width}: bar height changed ${JSON.stringify(sizes)}`);
                    }
                    results.push({ engine, path, theme, width, status: 'approved' });
                    if (theme === 'light' && [375, 1280].includes(width)) await page.screenshot({ path: `${dir}/${engine}-${path.split('/').pop()}-${width}.png` });
                }
            }
        }
        await page.setViewportSize({ width: 1280, height: 900 });
        await page.goto(`${fixture.origin}/mi-trabajo`);
        await page.locator('#my-bonus-week').selectOption('2026-10-26');
        await Promise.all([page.waitForURL('**BonusWeek=2026-10-26'), page.locator('.bonus-week button').click()]);
        assert((await page.locator('[data-task-bonus]').innerText()).includes('Evaluación aún no vigente'));
        assert.equal(await page.locator('[role="meter"]').count(), 0);
        await page.goto(`${fixture.origin}/mi-trabajo`);
        const before = await page.locator('[data-bonus-score]').textContent();
        await page.request.post(`${fixture.origin}/__test/clock/3/10`);
        await page.waitForFunction(before => document.querySelector('[data-bonus-score]').textContent !== before, before, { timeout: 9000 });
        results.push({ engine, scenario: 'clock transition received within 9s including rendering', status: 'approved' });
        const bonusRequest = url => url.pathname === '/mi-trabajo' && url.searchParams.get('handler') === 'Bonus';
        await page.route(bonusRequest, route => route.abort());
        await page.waitForFunction(() => document.querySelector('[data-bonus-status]').textContent.includes('desactualizado'), null, { timeout: 15000 });
        assert.notEqual(await page.locator('[data-bonus-score]').textContent(), '0%');
        await page.unroute(bonusRequest);
        await page.evaluate(() => window.dispatchEvent(new Event('focus')));
        await page.waitForFunction(() => !document.querySelector('[data-task-bonus]').classList.contains('task-bonus--stale'));
        await page.emulateMedia({ reducedMotion: 'reduce' });
        assert.equal(await page.locator('.task-bonus__needle').evaluate(e => getComputedStyle(e).transitionDuration), '0s');
        await page.evaluate(() => document.documentElement.style.fontSize = '200%');
        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), '200% text reflow');
        results.push({ engine, scenario: 'network failure/reconnect, reduced motion and 200% text', status: 'approved' });
        await page.route(bonusRequest, route => route.fulfill({ status: 403, body: '' }));
        await page.evaluate(() => window.dispatchEvent(new Event('focus')));
        await page.waitForFunction(() => !document.querySelector('[data-task-bonus]'));
        assert((await page.locator('[data-bonus-status]').innerText()).includes('sesión terminó'));
        results.push({ engine, scenario: 'revoked session clears economic data', status: 'approved' });
        const kiosk = await browser.newContext({ ignoreHTTPSErrors: true });
        await kiosk.addCookies([{ name: 'bonus-role', value: 'screen', url: fixture.origin }]);
        const screen = await kiosk.newPage();
        await screen.goto(`${fixture.origin}/tablero`);
        assert.equal(await screen.locator('[data-task-bonus]').count(), 0);
        const refresh = await screen.request.get(`${fixture.origin}/tablero?handler=Refresh`);
        assert(!(await refresh.text()).includes('data-bonus-score'));
        for (const path of ['/mi-trabajo?handler=Bonus', '/supervision/bono-tareas?handler=Snapshot'])
            assert.equal((await screen.request.get(fixture.origin + path)).status(), 403);
        await kiosk.close();
        const noJs = await browser.newContext({ ignoreHTTPSErrors: true, javaScriptEnabled: false });
        const serverPage = await noJs.newPage(); await serverPage.goto(`${fixture.origin}/mi-trabajo`);
        assert(await serverPage.locator('[data-bonus-score]').isVisible());
        assert.equal(await serverPage.locator('[role="meter"]').count(), 1);
        await noJs.close();
        results.push({ engine, scenario: 'kiosk HTML/refresh/direct handlers exclude bonus; SSR without JavaScript', status: 'approved' });
        assert.deepEqual(errors, [], 'no JS exceptions');
        await context.close(); await browser.close();
    }
    fs.writeFileSync(`${dir}/browser-results.json`, JSON.stringify(results, null, 2));
    console.log(`PASS ${results.length} browser cases`);
    async function expectVisible(locator) { assert(await locator.isVisible(), 'explanation visible'); }
})().catch(e => { console.error(e); process.exit(1); });
