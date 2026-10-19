const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '../../src/Sgol.Web/wwwroot/js');
let onChange;
const input = {
    dataset: { min: '2', max: '20', maxBytes: '31457280' }, files: [],
    addEventListener(event, handler) { if (event === 'change') onChange = handler; },
    setCustomValidity(message) { this.error = message; }
};
const status = {};
const form = {
    querySelector(selector) {
        if (selector === '[data-screenshots]') return input;
        if (selector === '[data-screenshots-status]') return status;
        return null;
    },
    addEventListener() {}
};
const dialog = {
    showModal() {}, addEventListener() {},
    querySelector(selector) { return selector === '[data-assigned-task-submit]' ? form : {}; }
};
vm.runInNewContext(fs.readFileSync(path.join(root, 'assigned-tasks.js'), 'utf8'), {
    document: { querySelector: () => dialog }
});
const image = (size = 100, type = 'image/png') => ({ size, type });
function select(files, valid) {
    input.files = files; onChange();
    assert.equal(!input.error, valid, input.error);
}
select([], false);
select([image()], false);
select([image(), image()], true);
select(Array.from({ length: 20 }, () => image()), true);
select(Array.from({ length: 21 }, () => image()), false);
select([image(15728640), image(15728640, 'image/jpeg')], true);
select([image(15728641), image()], false);
select([image(11000000), image(11000000), image(11000000)], false);
select([image(), image(100, 'application/pdf')], false);
select([image(), image(0)], false);
select([image(), image()], true);
assert.match(status.textContent, /2 capturas seleccionadas/);
console.log('PASS: Screenshot browser validation enforces count, file type, per-file size and aggregate size; correcting selection clears errors.');
