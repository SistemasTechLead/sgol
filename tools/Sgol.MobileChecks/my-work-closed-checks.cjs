// Regression for the actual live timer; DOM stub only, not a visual/browser test.
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const now = Date.parse('2026-11-03T18:00:00Z');
function row(id, status, delivered, start, due, color) {
    const classes = new Set([`my-work-color--${color}`]);
    const label = { textContent: status === 'rejected' ? 'Cerrada · No satisfecha' : '' };
    return { dataset: { workTask: id, taskStatus: status, delivered: String(delivered), start, due }, label,
        classList: { contains: c => classes.has(c), toggle: (c, on) => on ? classes.add(c) : classes.delete(c) },
        querySelectorAll: () => [label] };
}
const rejected = row('closed', 'rejected', false, now - 90000, now - 60000, 'closed');
const urgent = row('urgent', 'pending', false, now - 90000, now - 60000, 'urgent');
const delivered = row('delivered', 'review', true, now - 90000, now - 60000, 'delivered');
const correction = row('correction', 'pending', false, now - 30000, now + 60000, 'active');
let rows = [rejected, delivered, correction, urgent];
const list = { querySelectorAll: () => rows, append: (...ordered) => { rows = ordered; } };
const root = { dataset: { workNow: now }, querySelectorAll: selector => selector === '[data-work-task]' ? rows : [list] };
let tick, elapsed = 0;
vm.runInNewContext(fs.readFileSync('src/Sgol.Web/wwwroot/js/my-work-timing.js', 'utf8'), {
    document: { querySelector: () => root }, performance: { now: () => elapsed }, setInterval: callback => { tick = callback; }
});
for (const time of [0, 70000, 86400000]) {
    elapsed = time; tick();
    assert.equal(rows.at(-1), rejected);
    assert.equal(rejected.label.textContent, 'Cerrada · No satisfecha');
    assert(rejected.classList.contains('my-work-color--closed'));
    assert(!rejected.classList.contains('my-work-color--urgent'));
    assert(rows.indexOf(correction) < rows.indexOf(delivered));
}
console.log('PASS live timer: rejected stays last and neutral; pending correction remains actionable across deadline/day changes.');
