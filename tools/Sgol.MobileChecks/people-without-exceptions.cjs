const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
(async () => {
    const [url, out] = process.argv.slice(2);
    fs.mkdirSync(out, { recursive: true });
    const browser = await chromium.launch();
    for (const width of [320, 768, 1440]) for (const theme of ['light', 'dark']) {
        const page = await browser.newPage({ viewport: { width, height: 900 }, colorScheme: theme });
        await page.goto(url);
        await page.evaluate(theme => document.documentElement.dataset.theme = theme, theme);
        assert.equal(await page.getByRole('heading', { name: 'Horario laboral', exact: true }).count(), 1);
        assert.equal(await page.getByText('Excepciones por fecha', { exact: true }).count(), 0);
        assert.equal(await page.locator('[name^="Exception."], [name="exceptionDay"]').count(), 0);
        const mode = page.locator('[name="Schedule.Days[1].Mode"]');
        await mode.focus();
        assert(await mode.evaluate(el => el === document.activeElement));
        await page.keyboard.press('Tab');
        assert(await page.evaluate(() => document.activeElement !== document.body));
        await page.getByRole('heading', { name: 'Horario laboral', exact: true }).scrollIntoViewIfNeeded();
        await page.screenshot({ path: `${out}/${width}-${theme}.png` });
        await page.close();
        console.log(`PASS People actual Razor page ${width}px ${theme}, weekly schedule and keyboard`);
    }
    await browser.close();
})().catch(error => { console.error(error); process.exit(1); });
