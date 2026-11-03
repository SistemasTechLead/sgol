// Component test using the real server renderer output and SGOL styles; no database writes.
const { chromium, webkit } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const cases = JSON.parse(fs.readFileSync('artifacts/markdown-checks/cases.json', 'utf8'));
const css = ['tokens', 'components', 'mobile', 'markdown'].map(name => fs.readFileSync(`src/Sgol.Web/wwwroot/css/${name}.css`, 'utf8')).join('\n');
const escape = text => text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
(async () => {
    const results = [];
    for (const [engine, type] of [['chromium', chromium], ['webkit', webkit]]) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        for (const width of [320, 1280]) for (const theme of ['light', 'dark', 'special']) {
            const page = await browser.newPage({ viewport: { width, height: 900 } });
            const requests = [], dialogs = [];
            await page.route('**/*', route => { requests.push(route.request().url()); return route.abort(); });
            page.on('dialog', async dialog => { dialogs.push(dialog.message()); await dialog.dismiss(); });
            await page.setContent(`<html lang="es" data-theme="${theme}"><head><style>${css}</style></head><body><main style="padding:16px;min-width:0"><h1>Descripción</h1><details><summary>Ver descripción</summary>${Object.entries(cases).map(([name, value]) => `<section data-case="${name}"><div class="markdown-content">${value.html}</div></section>`).join('')}</details><label>Editar descripción<textarea>${escape(cases.format.source)}</textarea></label></main></body></html>`);
            await page.locator('summary').focus(); await page.keyboard.press('Enter');
            assert(await page.locator('details').getAttribute('open') !== null);
            assert.equal(await page.locator('textarea').inputValue(), cases.format.source);
            assert.equal(await page.locator('.markdown-content script, .markdown-content img, .markdown-content svg, .markdown-content iframe').count(), 0);
            const unsafe = await page.locator('.markdown-content').evaluateAll(nodes => nodes.flatMap(node => [...node.querySelectorAll('*')]).flatMap(el => [...el.attributes].filter(a => a.name.startsWith('on') || (a.name === 'href' && a.value !== '#' && !['https:', 'http:', 'mailto:'].includes(new URL(a.value, 'https://example.com').protocol))).map(a => a.name + '=' + a.value)));
            assert.deepEqual(unsafe, []);
            assert.equal(await page.evaluate(() => globalThis.pwned), undefined);
            assert.deepEqual(dialogs, []);
            assert(!requests.some(url => url.includes('tracker') || url.includes('evil')));
            assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
            await page.locator('[data-case=format] a').focus();
            assert.equal(await page.evaluate(() => document.activeElement.tagName), 'A');
            await page.screenshot({ path: `artifacts/markdown-checks/${engine}-${width}-${theme}.png` });
            await page.locator('summary').focus(); await page.keyboard.press('Enter');
            assert.equal(await page.locator('details').getAttribute('open'), null);
            results.push({ engine, width, theme, passed: true });
            await page.close();
        }
        await browser.close();
    }
    fs.writeFileSync('artifacts/markdown-checks/browser-results.json', JSON.stringify(results, null, 2));
    console.log(`PASS ${results.length} component/browser combinations; safe DOM, raw editor text, keyboard and overflow`);
})().catch(error => { console.error(error); process.exitCode = 1; });
