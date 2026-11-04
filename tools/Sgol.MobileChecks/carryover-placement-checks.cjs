// Structural regression: the Razor partial must occupy mutually exclusive positions.
// This does not replace an authenticated browser check.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const source = fs.readFileSync(path.join(__dirname, '../../src/Sgol.Web/Pages/Validations/Index.cshtml'), 'utf8');
const partial = '<partial name="_Carryovers" model="Model" />';
const upper = '@if (Model.PendingAuthorizedCarryoverCount > 0) { ' + partial + ' }';
const lower = '@if (group.Key == "pending" && Model.PendingAuthorizedCarryoverCount == 0) { ' + partial + ' }';
assert.equal(source.split(partial).length - 1, 2, 'Only the two conditional placements may render carryovers');
assert(source.includes(upper), 'Upper placement requires pending requests');
assert(source.includes(lower), 'Empty placement belongs after pending reviews');
assert(source.indexOf(upper) < source.indexOf('@foreach (var group in Model.Sections)'));
assert(source.indexOf(lower) > source.indexOf('</section>'));
console.log('PASS: mutually exclusive carryover placements in Razor (structural check)');
