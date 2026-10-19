const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = name => fs.readFileSync(path.join(__dirname, '../../src/Sgol.Web/wwwroot/js', name), 'utf8');
const board = source('live-board.js');
const boardColors = board.slice(board.indexOf('    function colors()'), board.indexOf('    function message('));
const work = source('my-work-timing.js');
let checks = 0;
function verify(start, due, now, expected, delivered = false) {
    const label = { textContent: '' };
    const classes = new Set();
    const row = {
        dataset: { start: String(start), due: String(due), delivered: String(delivered), taskStatus: 'pending' },
        classList: { toggle: (name, enabled) => enabled ? classes.add(name) : classes.delete(name) },
        querySelectorAll: () => [label],
        querySelector: () => label
    };
    const root = {
        dataset: { workNow: String(now), workDayStart: '0', workDayEnd: String(864000000) },
        querySelectorAll: selector => selector === '[data-work-task]' || selector === '.board-card' ? [row] : [],
        querySelector: () => null
    };
    let tick;
    let elapsed = 0;
    vm.runInNewContext(work, {
        document: { querySelector: () => root }, performance: { now: () => elapsed },
        setInterval: fn => { tick = fn; }
    });
    assert(classes.has(`my-work-color--${expected}`), `My Work at ${now}: ${expected}`);
    vm.runInNewContext(boardColors + '\ncolors();', { root, stopped: false, now: () => now });
    assert.equal(row.className, `board-card board-card--${expected}`);
    const boardText = label.textContent;
    // Verify the actual one-second callback as time moves across both thresholds.
    for (const [instant, state] of [[start, 'active'], [due - (due-start)*0.3, 'warning'], [due - (due-start)*0.1, 'urgent'], [due, 'urgent']]) {
        if (delivered) break;
        elapsed = instant - now;
        tick();
        assert(classes.has(`my-work-color--${state}`), `My Work timer: ${state}`);
    }
    if (expected === 'urgent' && now >= due && !delivered) assert.equal(boardText, 'Vencida');
    checks++;
}
for (const duration of [3600000, 7200000, 259200000]) {
    const start = 1000000, due = start + duration;
    for (const [now, expected] of [
        [start - 1, 'scheduled'], [start, 'active'],
        [due - duration * 0.3 - 1, 'active'], [due - duration * 0.3, 'warning'],
        [due - duration * 0.1 - 1, 'warning'], [due - duration * 0.1, 'urgent'],
        [due, 'urgent'], [due + 86400000, 'urgent']
    ]) verify(start, due, now, expected);
    verify(start, due, start - 1, 'delivered', true);
    verify(start, due, due + 1, 'delivered', true);
}
console.log(`PASS ${checks} browser-script cases across Board and My Work, including timer transitions`);
