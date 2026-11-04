// Isolated catalog component with product CSS and responsive adapter; no server/data writes.
const { chromium, webkit } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../../src/Sgol.Web');
const out = path.resolve('.local/task-description-columns');
fs.mkdirSync(out, { recursive: true });
const read = file => fs.readFileSync(path.join(root, file), 'utf8');
const css = ['tokens', 'markdown', 'components', 'mobile', 'navigation', 'mobile-hierarchy', 'desktop-workspace', 'interactive-controls', 'task-actions', 'task-catalog-mobile'].map(n => `<style>${read(`wwwroot/css/${n}.css`)}</style>`).join('');
const description = `<h3>Preparación y revisión</h3>${Array.from({ length: 12 }, (_, i) => `<p>Paso ${i + 1}. Revisa el material disponible y registra el resultado de la actividad. Conserva el orden de las instrucciones y confirma que no falte información antes de continuar con el siguiente paso.</p>`).join('')}<ul><li>Conservar el material.</li><li>Verificar las cantidades.</li></ul><p><a href="https://example.com">Consultar instrucciones</a></p>`;
const row = (i, body) => `<tr class="tabla__fila"><td>General</td><td class="task-catalog-table__identity"><div class="task-catalog-table__name"><strong>Tarea ${i}</strong></div><div class="task-catalog-table__meta"><small>T-00${i}</small><details data-task-description><summary>Ver descripción</summary><div class="markdown-content">${body}</div></details></div></td><td>Operador</td><td>No requiere</td><td>Comentario</td><td>Semanal</td><td>1</td><td>Flexible</td><td>15 min</td><td>Activa</td></tr>`;
const html = `<!doctype html><html lang="es"><head>${css}</head><body><main id="contenido-principal"><section data-task-catalog><div class="tabla-contenedor"><table class="tabla task-catalog-table"><thead><tr>${['Proceso','Tarea','Rol asignado','Especialización','Requisito','Periodicidad','Repeticiones','Criticidad','Carga','Estado'].map(s => `<th>${s}</th>`).join('')}</tr></thead><tbody>${row(1, description)}${row(2, '<p>Descripción breve.</p>')}</tbody></table></div></section></main></body></html>`;
const tick = page => page.waitForTimeout(100);
(async () => {
    const results = [];
    for (const [engine, type] of Object.entries({ chromium, webkit })) {
        const browser = await type.launch(engine === 'chromium' ? { channel: 'msedge' } : {});
        const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
        const errors = [];
        page.on('pageerror', e => errors.push(e.message));
        await page.setContent(html);
        await page.addScriptTag({ content: read('wwwroot/js/mobile.js') });
        // Register as in the deferred page script, then fire its startup in this component harness.
        await page.addScriptTag({ content: read('wwwroot/js/task-descriptions.js') });
        await page.evaluate(() => document.dispatchEvent(new Event('DOMContentLoaded')));
        const summary = page.locator('[data-task-description] > summary').first();
        const content = page.locator('#task-description-content-0');
        const original = await content.textContent();
        const closed = await page.locator('table').boundingBox();
        await summary.focus(); await page.keyboard.press('Enter'); await tick(page);
        assert.equal(await page.locator('.task-catalog-description-row').count(), 1);
        assert((await content.boundingBox()).width > (await summary.boundingBox()).width * 2);
        const expandedHeight = (await content.boundingBox()).height;
        await page.evaluate(() => document.querySelector('.task-catalog-description-row .markdown-content').style.columnCount = '1');
        assert((await content.boundingBox()).height > expandedHeight * 1.4, 'Columns reduce height');
        await content.evaluate(e => e.style.removeProperty('column-count'));
        await page.locator('[data-task-description] > summary').nth(1).click(); await tick(page);
        assert.equal(await page.locator('.task-catalog-description-row').count(), 2);
        for (const width of [1440, 1024, 768, 390, 320, 1440]) {
            await page.setViewportSize({ width, height: 1000 }); await tick(page);
            for (const theme of ['light', 'dark', 'special']) {
                await page.evaluate(t => document.documentElement.dataset.theme = t, theme);
                assert.equal(await content.textContent(), original);
                assert(await content.isVisible());
                assert.equal(await page.locator('.task-catalog-description-row').count(), width > 768 ? 2 : 0);
                if (width <= 768) assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'Mobile overflow');
                results.push({ engine, width, theme });
            }
            if ([1440, 320].includes(width)) await page.screenshot({ path: path.join(out, `${engine}-${width}.png`), fullPage: true });
        }
        await summary.focus(); await page.keyboard.press('Space'); await tick(page);
        assert.equal(await summary.getAttribute('aria-expanded'), 'false');
        assert.equal(await content.isVisible(), false);
        await page.locator('[data-task-description] > summary').nth(1).click(); await tick(page);
        assert.equal(await page.locator('.task-catalog-description-row').count(), 0);
        assert(Math.abs((await page.locator('table').boundingBox()).height - closed.height) < 2, 'Closed table keeps height');
        assert.deepEqual(errors, []);
        await browser.close();
    }
    fs.writeFileSync(path.join(out, 'results.json'), JSON.stringify(results, null, 2));
    console.log(`${results.length} responsive/theme cases passed; keyboard, multiple disclosures, height and content preservation passed.`);
})().catch(e => { console.error(e); process.exit(1); });
