const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
(async () => {
    const [url, folder] = process.argv.slice(2);
    fs.mkdirSync(folder, { recursive: true });
    const browser = await chromium.launch({ channel: 'msedge' });
    for (const width of [320, 1280]) for (const theme of ['light', 'dark', 'special']) {
        const page = await browser.newPage({ viewport: { width, height: 900 }, ignoreHTTPSErrors: true });
        await page.goto(url);
        await page.locator('[data-process-publish] button').click();
        await page.locator('.process-stage--conflict').first().waitFor();
        await page.evaluate(theme => document.documentElement.dataset.theme = theme, theme);
        assert.equal(await page.locator('.process-stage--conflict').count(), 2);
        assert.ok(await page.locator('.process-stage--conflict').first().innerText().then(t => t.includes('La tarea está inactiva')));
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), 'page overflow');
        const card = page.locator('.process-stage--conflict').first();
        await card.scrollIntoViewIfNeeded();
        await page.screenshot({ path: `${folder}/${width}-${theme}.png` });
        const edit = card.locator('[data-process-edit]');
        await edit.focus(); await page.keyboard.press('Enter');
        await page.locator('dialog[open]').waitFor();
        await page.keyboard.press('Escape');
        assert.ok(await edit.evaluate(e => e === document.activeElement), 'dialog restores focus');
        const other = page.locator('[data-process-stage]').nth(2);
        await other.locator('[data-process-edit]').click();
        await other.locator('[name$=".ReturnToPosition"]').fill('7');
        await page.keyboard.press('Escape');
        assert.ok(await other.locator('[data-process-client-conflict]').innerText().then(t => t.includes('etapa anterior') || t.includes('tarea anterior')));
        assert.ok(await other.evaluate(e => e.classList.contains('process-stage--conflict')));
        await other.locator('[data-process-edit]').click();
        await other.locator('[name$=".ReturnToPosition"]').fill('');
        await page.keyboard.press('Escape');
        assert.equal(await other.evaluate(e => e.classList.contains('process-stage--conflict')), false);
        await page.close();
    }
    await browser.close();
    console.log('PASS real process editor: 320/1280, themes, conflicts, overflow, keyboard dialog and focus');
})().catch(e => { console.error(e); process.exit(1); });
