import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';

// Source-level simulation in an isolated mock. This is not browser or layout QA.
const html = fs.readFileSync('outputs/secuencias/prototipo/index.html', 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)?.[1];
assert.ok(script);
const elements = new Map();
function element(id) {
  if (!elements.has(id)) elements.set(id, {
    value: '', checked: false, textContent: '', innerHTML: '', open: false,
    classList: { toggle() {} }, setAttribute() {}, showModal() { this.open = true; }, close() { this.open = false; },
  });
  return elements.get(id);
}
const context = vm.createContext({ Date, document: {
  getElementById: element,
  querySelectorAll: () => [element('difference-1'), element('difference-2')],
} });
vm.runInContext(script, context);
let checks = 0;
function check(expression, label) {
  assert.equal(vm.runInContext(expression, context), true, label);
  checks++;
  console.log('PASS: ' + label);
}
const run = expression => vm.runInContext(expression, context);
function approve(comment = 'Revisión documentada de la hoja y sus diferencias') {
  element('verified').checked = true;
  element('comment').value = comment;
  element('approve').onclick();
}
check('state.active===1 && state.version===2 && state.round===2', 'Example begins with a corrected inventory awaiting Subcoordination');
element('approve').onclick();
check('state.active===1', 'Review cannot advance without declaration and conclusion');
element('viewer').value = '2'; element('viewer').onchange({ target: element('viewer') }); approve();
check('state.active===1', 'Future Administration view cannot advance another role stage');
element('viewer').value = '1'; element('viewer').onchange({ target: element('viewer') });
approve(); check('state.active===2 && state.documentByRole[2]===2', 'Administration receives exactly the reviewed input version');
element('return').onclick(); element('confirm-return').onclick();
check('state.active===2 && state.round===2', 'Return requires a reason');
element('reason').value = 'Hay un movimiento pendiente de revisar'; element('confirm-return').onclick();
check('state.active===1 && state.round===3 && state.documentByRole[2]===null', 'Return creates a new round and invalidates the later stage result');
element('return').onclick(); element('reason').value = 'Buscar faltantes y presentar sobrantes'; element('confirm-return').onclick();
check('state.active===0 && state.round===4', 'Subcoordination can return to Floor without skipping a role');
approve('Se completó la búsqueda');
check('state.active===0', 'Floor cannot declare delivery without its PDF');
element('example-pdf').onclick(); approve('Inventario completo y búsqueda documentados');
check('state.active===1 && state.version===3 && state.documentByRole[0]===3', 'Corrected Floor PDF becomes a new identifiable version');
element('example-pdf').onclick(); approve('Hoja marcada con diferencias pendientes');
check('state.active===2 && state.version===4 && state.documentByRole[0]===3 && state.documentByRole[2]===4', 'Updated review PDF does not change the earlier Floor document version');
approve('Chequeo administrativo terminado');
check('state.active===3 && state.version===4', 'Direction receives the latest reviewed document');
check("$('detail').innerHTML.includes('mar') && instant(state.received+168*60).getUTCDate()===1 && instant(state.received+168*60).getUTCMonth()===2", 'Direction effective deadline is one elapsed week in the example');
element('outcome').value = 'resolved'; approve('Intento de cierre');
check('!state.closed', 'Closing without differences requires explicit resolution declaration');
element('outcome').value = 'differences'; approve('Acepto esta versión y las diferencias pendientes documentadas');
check("state.closed && state.outcome==='Inventario aceptado con diferencias pendientes' && state.version===4", 'Direction can accept the exact version with documented unresolved differences');
check("state.events[0].title.includes('V4') && $('summary').innerHTML.includes('V4')", 'History and final acceptance identify the authorized version');
check("document.querySelectorAll()[0].textContent==='Pendiente de aclarar'", 'Acceptance does not silently mark differences resolved');
element('return').onclick();
check("!$('return-dialog').open && state.active===3", 'A concluded process cannot be returned');
run('reset()');
approve(); approve();
element('outcome').value = 'resolved'; element('all-resolved').checked = true; approve('Diferencias aclaradas y documentadas');
check("state.closed && state.outcome==='Inventario concluido sin diferencias'", 'Documented resolution supports the alternative satisfactory closure');
check("document.querySelectorAll()[0].textContent==='Resuelta y documentada'", 'Resolved closure shows consistent difference status');
console.log(JSON.stringify({ sourceSimulationChecks: checks, browserOrLayoutVerified: false }));
