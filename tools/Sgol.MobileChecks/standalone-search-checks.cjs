// Component regression using the actual Razor form markup and product CSS/JS.
// Does not authenticate or claim server-side search integration.
const { chromium, webkit } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../../src/Sgol.Web');
const out = path.resolve(process.argv[2] || '.local/standalone-search');
fs.mkdirSync(out, { recursive: true });
const read = file => fs.readFileSync(path.join(root, file), 'utf8');
const form = (file, marker) => {
    const source = read(`Pages/${file}`);
    const index = source.indexOf(marker);
    assert(index >= 0);
    const start = source.lastIndexOf('<form', index);
    return source.slice(start, source.indexOf('</form>', index) + 7)
        .replace(/@foreach[^\n]+/g, '<input type="hidden" name="F.Kind" value="tasks" />')
        .replace(/value="@Model.Request.Query\["WorkSearch"\]"/g, 'value=""')
        .replace(/value="@[^\"]*"/g, 'value="context"')
        .replace('asp-for="Search"', 'name="Search"');
};
const cases = [
    ['work', form('MyWork/_OperationalTasks.cshtml', 'class="supervision-filters"')],
    ['occasional', form('Occasional/Index.cshtml', 'class="tasks-search occasional-search"')],
    ['process', form('Tasks/Processes.cshtml', 'data-process-search')],
    ['report', `<div class="report-detail-heading">${form('Reports/Index.cshtml', 'name="DetailSearch"')}</div>`]
];
const css = ['tokens', 'components', 'mobile', 'navigation', 'mobile-hierarchy', 'desktop-workspace', 'interactive-controls']
    .map(name => `<style>${read(`wwwroot/css/${name}.css`)}</style>`).join('');
const tick = page => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
(async () => {
    const results = [];
    for (const [engine, type] of Object.entries({ chromium, webkit })) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        try {
            for (const [name, markup] of cases) {
                const page = await browser.newPage({ viewport: { width: 320, height: 900 } });
                const errors = [];
                page.on('pageerror', error => errors.push(error.message));
                await page.setContent(`<!doctype html><html lang="es"><head>${css}</head><body data-desktop-workspace="true"><div class="desktop-screen-heading"></div><button class="desktop-screen-info">Info</button><div id="desktop-screen-help" hidden></div><main id="contenido-principal"><h1>Buscar</h1><section id="subject">${markup}</section><section><form class="supervision-filters" id="multiple"><label class="campo"><span class="campo__etiqueta">Buscar</span><input type="search" name="q"></label><label class="campo"><span class="campo__etiqueta">Estado</span><select name="state"><option value="all">Todos</option></select><button>Buscar</button></form></section></main><template id="mobile-info-icon"></template></body></html>`);
                for (const script of ['search-text', 'mobile', 'desktop-workspace']) await page.addScriptTag({ content: read(`wwwroot/js/${script}.js`) });
                const search = page.locator('#subject input[type=search]');
                await search.fill('Búsqueda extensa con acentos y código 12345');
                const values = () => page.locator('#subject form').evaluate(form => [...new FormData(form)]);
                const original = await values();
                for (const width of [320, 768, 1440, 320]) {
                    await page.setViewportSize({ width, height: 900 });
                    await tick(page);
                    for (const theme of ['light', 'dark', 'special']) {
                        await page.evaluate(theme => document.documentElement.dataset.theme = theme, theme);
                        assert(await search.isVisible(), `${name}: visible at ${width}`);
                        assert.equal(await page.locator('#subject details, #subject .mobile-fold-toggle').count(), 0);
                        assert.deepEqual(await values(), original, 'Preserve query and hidden context');
                        await search.focus();
                        await page.keyboard.press('Tab');
                        assert(await page.locator('#subject button').evaluate(e => e === document.activeElement), 'Search action follows input');
                        assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), `${name}: no overflow ${width}`);
                        if (width > 768) assert.equal(await page.locator('#multiple .desktop-filter').count(), 2);
                        else assert(await page.locator('[data-mobile-control^="supervision-"]').count() > 0, 'Multiple filters still fold');
                        results.push({ engine, name, width, theme });
                    }
                    if (width === 1440 || width === 320) await page.screenshot({ path: path.join(out, `${engine}-${name}-${width}.png`) });
                }
                await page.emulateMedia({ media: 'print' });
                await tick(page);
                assert(await search.isVisible());
                assert.deepEqual(await values(), original);
                await page.emulateMedia({ media: 'screen' });
                await tick(page);
                await page.locator('#subject form').evaluate(form => form.addEventListener('submit', event => {
                    event.preventDefault();
                    window.submittedSearch = [...new FormData(form)];
                }));
                await search.press('Enter');
                assert.deepEqual(await page.evaluate(() => window.submittedSearch), original, 'Enter submits the same fields');
                assert.deepEqual(errors, []);
                await page.close();
            }
        } finally { await browser.close(); }
    }
    fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(results, null, 2));
    console.log(`${results.length} component scenarios passed (including resize round trips).`);
})().catch(error => { console.error(error); process.exit(1); });
