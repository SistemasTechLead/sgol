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
    vm.runInNewContext(boardColors + '\ncolors();', { root, stopped: false, now: () => now, pace: () => {} });
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

// Filter the live cards on each clock tick, including transitions into an empty result.
{
    let instant = 1000;
    let stateFilter = 'scheduled';
    const empty = { hidden: false };
    const card = { dataset: { start: '2000', due: '12000', delivered: 'false' },
        querySelector: () => ({ textContent: '' }) };
    const root = {
        querySelectorAll: () => [card],
        querySelector: selector => selector === '[data-state]' ? { dataset: { state: stateFilter } } :
            selector === '[data-board-empty]' ? empty :
            selector === '.board-card:not([hidden])' ? card.hidden ? null : card : null
    };
    const update = () => vm.runInNewContext(boardColors + '\ncolors();',
        { root, stopped: false, now: () => instant, pace: () => {} });
    for (const [time, state] of [[1000, 'scheduled'], [2000, 'active'], [9000, 'warning'], [11000, 'urgent'], [12000, 'overdue']]) {
        instant = time; stateFilter = state; update();
        assert.equal(card.hidden, false, `${state} is visible at ${time}`);
        assert.equal(empty.hidden, true);
        stateFilter = 'delivered'; update();
        assert.equal(card.hidden, true);
        assert.equal(empty.hidden, false);
    }
    card.dataset.delivered = 'true'; update();
    assert.equal(card.hidden, false);
    stateFilter = 'scheduled'; instant = 1000; card.dataset.delivered = 'false'; update();
    assert.equal(card.hidden, false);
    instant = 2000; update();
    assert.equal(card.hidden, true, 'The scheduled card leaves the filter exactly at activation');
    stateFilter = ''; update();
    assert.equal(card.hidden, false, 'All tasks restores every card');
    console.log('PASS all six state filters, empty results and live activation transitions');
}

// A single list mixes days and plans, and reorders as scheduled work activates.
{
    let elapsed = 0, tick;
    const now = 10 * 86400000;
    const row = (id, start, due, delivered = false) => ({
        dataset: { workTask: id, start: String(start), due: String(due), delivered: String(delivered), taskStatus: 'pending' },
        classList: { toggle() {} }, querySelectorAll: () => []
    });
    let rows = [row('blue-old', now-86400000, now-80000000, true),
        row('gray', now+1000, now+10000), row('yellow', now-8000, now+2000),
        row('green', now-1000, now+10000), row('red-today', now-10000, now-1),
        row('red-old', now-3*86400000, now-2*86400000)];
    const list = { querySelectorAll: () => rows, append: (...ordered) => { rows = ordered; } };
    const root = { dataset: { workNow: String(now) }, querySelectorAll: selector =>
        selector === '[data-work-task]' ? rows : selector === '[data-work-task-list]' ? [list] : [] };
    vm.runInNewContext(work, { document: { querySelector: () => root }, performance: { now: () => elapsed },
        setInterval: fn => { tick = fn; } });
    assert.deepEqual(rows.map(r => r.dataset.workTask), ['red-old','red-today','green','yellow','gray','blue-old']);
    elapsed = 1000; tick();
    assert.deepEqual(rows.map(r => r.dataset.workTask), ['red-old','red-today','yellow','gray','green','blue-old']);
    console.log('PASS My Work globally orders mixed dates and reorders after timer transitions');
}
