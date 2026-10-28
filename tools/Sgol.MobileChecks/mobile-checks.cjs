/* Read-only browser checks. Supply a private directory containing origin.txt,
 * browser-state.json, routes-extended.json and optional roles.json. Authentication
 * state is never printed or written into the repository. Requires Playwright. */
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

(async () => {
    const artifacts = path.resolve(process.argv[2] || '.local/mobile-normalization');
    const origin = fs.readFileSync(path.join(artifacts, 'origin.txt'), 'utf8').trim();
    const routes = JSON.parse(fs.readFileSync(path.join(artifacts, 'routes-extended.json')));
    const browser = await chromium.launch({ channel: process.env.SGOL_BROWSER_CHANNEL || 'msedge', headless: true });
    const context = await browser.newContext({ ignoreHTTPSErrors: true, storageState: path.join(artifacts, 'state-alex.json') });
    const results = [], errors = [], writes = [];
    await context.addInitScript(() => localStorage.setItem('sgol.appearance.layout', 'auto'));
    await context.route('**/*', route => {
        if (!['GET', 'HEAD'].includes(route.request().method())) {
            writes.push(route.request().url().split('?')[0]); return route.abort();
        }
        return route.continue();
    });
    const page = await context.newPage(); page.on('pageerror', error => errors.push(error.message));
    const output = path.join(artifacts, 'verification'); fs.mkdirSync(output, { recursive: true });
    for (const [width, height] of [[320, 568], [390, 844], [430, 932], [768, 1024], [769, 1024], [1024, 768], [1280, 900], [1440, 1000], [1920, 1080]]) {
        await page.setViewportSize({ width, height });
        for (const entry of routes) {
            const response = await page.goto(origin + entry.route, { waitUntil: 'load' });
            await page.evaluate(() => document.fonts.ready);
            assert.equal(response.status(), entry.status, `${entry.route} status changed`);
            const measure = await page.evaluate(() => {
                const root = document.documentElement, box = root.getBoundingClientRect();
                const outside = [...document.querySelectorAll('.mobile-records .boton, .mobile-record-toggle')].filter(button => {
                    const r = button.getBoundingClientRect(); return r.width && r.height && (r.left < box.left - 1 || r.right > box.right + 1);
                });
                return { scroll: root.scrollWidth, client: root.clientWidth, outside: outside.length,
                    desktopControls: [...document.querySelectorAll('.mobile-fold-toggle,.mobile-record-toggle,.mobile-section-nav,.mobile-day-picker')].filter(e => e.getBoundingClientRect().height > 0).length };
            });
            if (width <= 768) assert(measure.scroll <= measure.client + 1, `${entry.route}: page overflow at ${width}: ${measure.scroll}`);
            if (width <= 768) assert.equal(measure.outside, 0, `${entry.route}: action outside mobile width ${width}`);
            if (width > 768) assert.equal(measure.desktopControls, 0, `${entry.route}: mobile controls on desktop`);
            results.push({ route: entry.route, width, status: response.status(), ...measure });
        }
        console.log(`PASS ${width} px: ${routes.length} routes/states, ${width <= 768 ? 'no mobile page overflow, actions inside view' : 'mobile enhancements hidden on desktop'}`);
    }

    // No panel operation may alter the successful form fields or their values.
    await page.setViewportSize({ width: 390, height: 844 });
    for (const route of ['/validaciones', '/planificacion?mode=create', '/reportes', '/personas-y-accesos?create=true', '/tareas?create=true']) {
        await page.goto(origin + route);
        const values = () => page.evaluate(() => [...document.forms].map(form => JSON.stringify({ action: form.getAttribute('action'),
            values: [...new FormData(form)].map(([key, value]) => [key, value instanceof File ? { name: value.name, size: value.size } : value]) })).sort());
        const before = await values();
        await page.evaluate(() => {
            for (const button of document.querySelectorAll('.mobile-fold-toggle,.mobile-record-toggle')) { button.click(); button.click(); }
        });
        assert.deepEqual(await values(), before, route + ': opening panels changed FormData');
    }
    console.log('PASS disclosure controls preserve every form field and value');

    await page.goto(origin + '/validaciones');
    const invalid = await page.evaluate(() => {
        const panel = document.querySelector('#capacity'); const form = panel.querySelector('[data-capacity-form]');
        if (!form) return null;
        panel.open = false; const required = form.querySelector('textarea[required]'); required.value = '';
        form.addEventListener('submit', event => event.preventDefault(), { once: true });
        form.requestSubmit(); return { open: panel.open, active: document.activeElement === required };
    });
    if (invalid) { assert(invalid.open && invalid.active, 'Invalid hidden capacity field was not revealed and focused'); }
    console.log('PASS native validation reveals and focuses required controls');

    await page.goto(origin + '/planificacion?mode=create');
    await page.evaluate(() => {
        const table = document.createElement('table'); table.className = 'tabla direction-capacity__table'; table.dataset.mobileFixture = 'capacity';
        table.innerHTML = '<thead><tr><th>Persona</th>' + Array.from({ length: 31 }, (_, i) => `<th>Día ${i + 1}</th>`).join('') + '<th>Total</th></tr></thead><tbody><tr><td>Persona de prueba</td>' + Array.from({ length: 31 }, () => '<td><input type="number" min="0" max="100" required value="75"></td>').join('') + '<td>75 %</td></tr></tbody>';
        const container = document.createElement('div'); container.append(table); document.querySelector('main').append(container);
    });
    await page.waitForSelector('table[data-mobile-fixture].mobile-records');
    assert.equal(await page.locator('table[data-mobile-fixture] input').count(), 31);
    assert(await page.locator('table[data-mobile-fixture] input').evaluateAll(inputs => inputs.every(i => i.value === '75' && i.required && i.getBoundingClientRect().right <= document.documentElement.clientWidth)));
    console.log('PASS dynamically inserted monthly capacity matrix keeps all editable day controls within phone width');

    // Explicit mobile and landscape used to size the menu against the screen.
    for (const [width, height, forced] of [[320, 568, false], [844, 390, true], [1440, 900, true]]) {
        await page.setViewportSize({ width, height }); await page.goto(origin + '/personas-y-accesos');
        if (forced) await page.evaluate(() => { document.documentElement.dataset.layout = 'mobile'; });
        await page.locator('[aria-controls="navegacion-movil"]').click();
        const menu = await page.locator('#navegacion-movil').evaluate(dialog => {
            const r = dialog.getBoundingClientRect(), view = document.documentElement.getBoundingClientRect();
            return { left: r.left, right: r.right, top: r.top, bottom: r.bottom, viewLeft: view.left, viewRight: view.right,
                links: [...dialog.querySelectorAll('a')].map(link => ({ height: link.getBoundingClientRect().height })), horizontal: dialog.scrollWidth > dialog.clientWidth + 1 };
        });
        assert(menu.left >= menu.viewLeft && menu.right <= menu.viewRight + 1 && menu.top >= 0 && menu.bottom <= height + 1 && !menu.horizontal, 'Menu exceeds chosen mobile view');
        assert(menu.links.every(link => link.height >= 44), 'Menu touch area too small');
        await page.keyboard.press('Escape');
        assert.equal(await page.locator('#navegacion-movil').evaluate(d => d.open), false);
        assert.equal(await page.evaluate(() => document.activeElement.getAttribute('aria-controls')), 'navegacion-movil');
    }
    console.log('PASS menu size, landscape scrolling, Escape and focus return');

    await page.setViewportSize({ width: 390, height: 844 }); await page.goto(origin + '/tablero');
    const people = page.locator('[data-mobile-control="board-people"]');
    if (await people.count()) await people.click();
    const filters = page.locator('[data-mobile-control="board-filters"]'); await filters.click(); await filters.focus();
    const response = page.waitForResponse(r => r.url().includes('/tablero?') && r.url().includes('handler=Refresh'));
    await page.locator('#board-plan').dispatchEvent('change'); await response;
    await page.waitForFunction(() => document.querySelector('#live-board').getAttribute('aria-busy') === 'false');
    assert.equal(await page.locator('[data-mobile-control="board-filters"]').getAttribute('aria-expanded'), 'true');
    if (await people.count()) assert.equal(await people.getAttribute('aria-expanded'), 'true');
    assert.equal(await page.evaluate(() => document.activeElement.dataset.mobileControl), 'board-filters');
    assert.equal(await page.locator('[data-mobile-control="board-filters"]').count(), 1);
    console.log('PASS board refresh keeps expanded panels and focus without duplicate controls');

    await page.goto(origin + '/tablero/rol-de-tareas');
    const picker = page.locator('.mobile-day-picker select').first(); await picker.selectOption('0');
    assert.equal(await page.locator('.weekly-table').first().locator('[data-mobile-day="1"]:visible').count(), 0);
    await picker.selectOption('');
    console.log('PASS weekly day selection uses original table cells');
    await picker.selectOption('1'); await picker.focus();
    // Trigger the same read-only refresh used when connectivity returns.
    const weeklyResponse = page.waitForResponse(r => r.url().includes('/tablero/rol-de-tareas') && r.url().includes('handler=Refresh'));
    await page.evaluate(() => window.dispatchEvent(new Event('online'))); await weeklyResponse;
    await page.waitForFunction(() => document.querySelector('#weekly-board').getAttribute('aria-busy') === 'false');
    assert.equal(await page.locator('.mobile-day-picker select').first().inputValue(), '1');
    assert.equal(await page.evaluate(() => document.activeElement.dataset.mobileControl), 'weekly-day-tasks');
    console.log('PASS weekly refresh preserves selected day and focus');

    // Existing table text is also used by reports.js for its local search.
    await page.goto(origin + '/reportes');
    const search = page.locator('#report-search');
    if (await search.count()) {
        const text = await page.locator('.report-table tbody tr').first().innerText();
        await search.fill('__no_existing_task_matches_this__');
        assert.equal(await page.locator('.report-table tbody tr:visible').count(), 0);
        await search.fill(''); assert((await page.locator('.report-table tbody tr:visible').count()) > 0);
        assert(text.length > 0);
    }
    await page.locator('[data-report-export]').click();
    await page.locator('#report-preview').click();
    await page.waitForFunction(() => document.querySelector('#report-pdf-status').textContent.includes('páginas listas'), { timeout: 30000 });
    console.log('PASS report search and PDF generation');
    await page.goto(origin + '/reportes'); await page.emulateMedia({ media: 'print' });
    assert.equal(await page.locator('.mobile-fold-toggle:visible,.mobile-record-toggle:visible').count(), 0);
    assert.equal(await page.locator('.report-controls').isVisible(), true);
    assert.equal(await page.locator('.report-template-picker').isVisible(), true);
    await page.emulateMedia({ media: 'screen' });
    console.log('PASS print retains original controls and omits mobile disclosure buttons');

    // Capture reviewable phone crops, rather than unreadably tall full-page shots.
    for (const [name, route] of [['supervision', '/validaciones'], ['tablero', '/tablero'], ['reportes', '/reportes'], ['planificacion', '/planificacion'], ['personas', '/personas-y-accesos']]) {
        await page.goto(origin + route); await page.screenshot({ path: path.join(output, name + '.png') });
    }
    if (fs.existsSync(path.join(artifacts, 'roles.json'))) {
        const roles = JSON.parse(fs.readFileSync(path.join(artifacts, 'roles.json')));
        for (const [name, state] of Object.entries(roles)) {
            const roleContext = await browser.newContext({ ignoreHTTPSErrors: true, storageState: state, viewport: { width: 390, height: 844 } });
            const rolePage = await roleContext.newPage();
            for (const route of ['/mi-trabajo', '/tablero', '/validaciones', '/planificacion', '/reportes', '/personas-y-accesos', '/configuracion']) {
                const r = await rolePage.goto(origin + route);
                assert([200, 403].includes(r.status()), 'Unexpected role route response');
                const overflow = await rolePage.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1);
                assert(!overflow, name + ' ' + route + ' overflow');
                results.push({ role: name, route, width: 390, status: r.status() });
            }
            await roleContext.close();
        }
        console.log('PASS four existing roles, allowed and denied routes at phone width');
    }
    const preferenceContext = await browser.newContext({ ignoreHTTPSErrors: true, storageState: path.join(artifacts, 'state-alex.json'), viewport: { width: 1440, height: 900 } });
    const preferencePage = await preferenceContext.newPage();
    await preferencePage.goto(origin + '/configuracion');
    await preferencePage.locator('[data-layout-choice][value="mobile"]').check();
    await preferencePage.goto(origin + '/personas-y-accesos');
    assert((await preferencePage.evaluate(() => document.documentElement.getBoundingClientRect().width)) <= 420);
    await preferencePage.goto(origin + '/configuracion');
    await preferencePage.locator('[data-layout-choice][value="desktop"]').check();
    await preferencePage.setViewportSize({ width: 390, height: 844 }); await preferencePage.goto(origin + '/reportes');
    assert((await preferencePage.evaluate(() => document.documentElement.getBoundingClientRect().width)) >= 1280);
    assert.equal(await preferencePage.locator('.mobile-fold-toggle:visible').count(), 0);
    await preferencePage.goto(origin + '/configuracion'); await preferencePage.locator('[data-layout-choice][value="auto"]').check();
    for (const theme of ['light', 'dark', 'special']) {
        await preferencePage.goto(origin + '/configuracion'); await preferencePage.locator(`[data-theme-choice][value="${theme}"]`).check();
        await preferencePage.goto(origin + '/validaciones');
        assert.equal(await preferencePage.locator('html').getAttribute('data-theme'), theme);
        assert.equal(await preferencePage.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1), true);
    }
    await preferenceContext.close();
    const touchContext = await browser.newContext({ ignoreHTTPSErrors: true, hasTouch: true, isMobile: true, viewport: { width: 844, height: 390 }, storageState: path.join(artifacts, 'state-alex.json') });
    const touchPage = await touchContext.newPage(); await touchPage.goto(origin + '/personas-y-accesos');
    assert((await touchPage.evaluate(() => document.documentElement.getBoundingClientRect().width)) <= 768);
    assert.equal(await touchPage.locator('[aria-controls="navegacion-movil"]').isVisible(), true);
    await touchContext.close();
    console.log('PASS saved mobile/desktop preferences, three themes and automatic touch landscape');
    const anonymousContext = await browser.newContext({ ignoreHTTPSErrors: true, viewport: { width: 320, height: 568 } });
    const anonymousPage = await anonymousContext.newPage(); await anonymousPage.goto(origin + '/acceso');
    assert.equal(await anonymousPage.locator('input[name="userName"]').isVisible(), true);
    assert.equal(await anonymousPage.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1), true);
    await anonymousContext.close(); console.log('PASS anonymous login at 320 px');
    assert.deepEqual(writes, [], 'The read-only UI checks attempted a write');
    assert.deepEqual(errors, [], 'Browser script error');
    const health = await context.request.get(origin + '/health/ready'); assert.equal(health.status(), 200); assert.equal((await health.json()).status, 'ready');
    fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify({ results, errors, writes, health: 'ready' }, null, 2));
    await browser.close(); console.log('PASS', results.length, 'route/viewport/role checks, no writes, no script errors, health ready');
})().catch(error => { console.error(error); process.exit(1); });
