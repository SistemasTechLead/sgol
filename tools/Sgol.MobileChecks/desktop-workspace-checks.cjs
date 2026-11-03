/* Real authenticated Razor pages. Read-only GET checks on an isolated host.
 * Usage: node desktop-workspace-checks.cjs <private directory with origin.txt and state-alex.json>
 */
const { chromium, webkit } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const dir = path.resolve(process.argv[2]);
const origin = fs.readFileSync(path.join(dir, 'origin.txt'), 'utf8').trim();
assert.equal(origin, 'https://localhost:7284', 'Dedicated isolated preview required');
const routes = ['/mi-trabajo', '/mi-trabajo/procesos', '/mi-trabajo/arrastre', '/tareas', '/tareas/procesos', '/tareas/procesos/items', '/tareas/actualizar', '/asignar-tarea', '/validaciones', '/validaciones?Validation=true', '/supervision/carga', '/supervision/bono-tareas', '/planificacion', '/planificacion/procesos', '/personas-y-accesos', '/personas-y-accesos/especializaciones', '/configuracion', '/reportes', '/tablero', '/tablero/rol-de-tareas'];
const results = [];
const values = page => page.locator('main form').evaluateAll(forms => forms.map(form => [...new FormData(form)].filter(([key]) => !key.includes('VerificationToken'))).sort((a, b) => JSON.stringify(a).localeCompare(JSON.stringify(b))));
const settle = page => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
(async () => {
    for (const [engine, type] of Object.entries({ chromium, webkit })) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        try {
            const page = await browser.newPage({ ignoreHTTPSErrors: true, storageState: path.join(dir, 'state-alex.json'), viewport: { width: 1440, height: 900 } });
            const errors = [];
            page.on('pageerror', error => errors.push(error.message));
            for (const [index, route] of routes.entries()) {
                assert.equal((await page.goto(origin + route)).status(), 200, route);
                await settle(page);
                assert(await page.locator('body.desktop-workspace').count(), route);
                assert.equal(await page.locator('h1:visible').count(), 1, `Single title: ${route}`);
                assert(await page.locator('.desktop-screen-title').isVisible(), route);
                const original = await values(page);
                const info = page.locator('.desktop-screen-info');
                if (await info.isVisible()) {
                    const before = await page.locator('main').boundingBox();
                    await info.focus(); await page.keyboard.press('Enter');
                    assert(await page.locator('#desktop-screen-help').isVisible(), `Information ${route}`);
                    assert.deepEqual(await page.locator('main').boundingBox(), before, `Floating information ${route}`);
                    await page.screenshot({ path: path.join(dir, `information-${engine}-${index}.png`) });
                    await page.keyboard.press('Escape');
                    assert(await info.evaluate(e => document.activeElement === e && e.getAttribute('aria-expanded') === 'false'));
                }
                for (const theme of ['light', 'dark', 'special']) {
                    await page.evaluate(theme => document.documentElement.dataset.theme = theme, theme);
                    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `Overflow ${route} ${theme}`);
                    if (engine === 'chromium' && (theme === 'light' || index === 0)) await page.screenshot({ path: path.join(dir, `desktop-${index}-${theme}.png`) });
                }
                const summaries = page.locator('.desktop-filter > summary');
                const indexOfVisible = await summaries.evaluateAll(elements => elements.findIndex(e => {
                    for (let p = e.parentElement.parentElement; p; p = p.parentElement)
                        if (p.matches('details:not([open])') || p.hidden) return false;
                    return e.getBoundingClientRect().height > 0;
                }));
                if (indexOfVisible >= 0) {
                    const summary = summaries.nth(indexOfVisible);
                    await summary.evaluate(e => e.parentElement.open = false);
                    await summary.focus(); await page.keyboard.press('Enter');
                    assert(await summary.evaluate(e => e.parentElement.open), route);
                    await page.keyboard.press('Tab');
                    assert(await summary.evaluate(e => e.parentElement.contains(document.activeElement)), `Keyboard control: ${route}`);
                    await page.keyboard.press('Escape');
                    assert(await summary.evaluate(e => !e.parentElement.open && document.activeElement === e), `Escape focus: ${route}`);
                }
                for (const width of [320, 768, 1024, 1440]) {
                    await page.setViewportSize({ width, height: 900 }); await settle(page);
                    assert.deepEqual(await values(page), original, `Form values ${route} ${width}`);
                    assert.equal(await page.locator('h1:visible').count(), 1, `Responsive title ${route} ${width}`);
                    if (width <= 768) assert.equal(await page.locator('.desktop-filter').count(), 0, 'Restore mobile controls');
                }
                await page.emulateMedia({ media: 'print' }); await settle(page);
                assert.deepEqual(await values(page), original, `Print values ${route}`);
                assert.equal(await page.locator('.desktop-filter').count(), 0, `Print restore ${route}`);
                await page.emulateMedia({ media: 'screen' }); await settle(page);
                results.push({ engine, route, state: 'passed', themes: 3, widths: [320, 768, 1024, 1440], formPreservation: true, print: true });
                fs.writeFileSync(path.join(dir, 'results.json'), JSON.stringify(results, null, 2));
            }
            // Exercise real GET binding and reset, including an empty result.
            for (const [route, field, term] of [['/mi-trabajo', 'WorkSearch', 'SGOL-no-match-desktop'], ['/tareas', 'Search', 'SGOL-no-match-desktop'], ['/personas-y-accesos', 'Search', 'SGOL-no-match-desktop']]) {
                await page.goto(origin + route);
                const input = page.locator(`input[name="${field}"]`);
                await input.locator('xpath=ancestor::details[1]/summary').click();
                await input.fill(term);
                const form = input.locator('xpath=ancestor::form');
                await form.locator('button[type="submit"]').click();
                await page.waitForURL(url => url.searchParams.get(field) === term);
                assert.equal(await input.inputValue(), term);
                assert((await input.locator('xpath=ancestor::details[1]/summary').innerText()).includes(term));
                await form.getByRole('link', { name: /Limpiar/ }).click();
                await page.waitForURL(url => !url.searchParams.has(field));
                assert.equal(await input.inputValue(), '');
            }
            assert.deepEqual(errors, [], 'Client errors');
            console.log(`PASS ${engine}: ${routes.length} routes, themes, keyboard, responsive/print form preservation, GET search/reset.`);
        } finally { await browser.close(); }
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
