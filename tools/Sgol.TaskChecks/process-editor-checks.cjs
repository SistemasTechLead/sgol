const {chromium}=require('playwright');
const fs=require('fs');
(async()=>{
 const browser=await chromium.launch({headless:true}); const page=await browser.newPage();
 const stage=i=>`<fieldset data-process-stage><legend>Tarea ${i}</legend><input name="Input.Steps[${i-1}].TaskId" value="task-${i}"><label for="Input_Steps_${i-1}__Position">Posición</label><input id="Input_Steps_${i-1}__Position" name="Input.Steps[${i-1}].Position" value="${i}" type="number"><select name="Input.Steps[${i-1}].Action"><option>ENTREGAR</option><option>REVISAR</option><option>AUTORIZAR</option></select><select name="Input.Steps[${i-1}].ReleaseCondition"><option>INICIO</option><option>ENTREGA</option><option>APROBACION</option></select><input name="Input.Steps[${i-1}].ReturnToPosition" value="${i===6?'2':''}"><textarea name="Input.Steps[${i-1}].Instructions">nota-${i}</textarea></fieldset>`;
 await page.setContent(`<form data-process-rules><select data-process-start-kind><option>OCASIONAL</option><option>SEMESTRAL</option></select><div data-process-calendar><input></div><section>${Array.from({length:7},(_,i)=>stage(i+1)).join('')}<p data-process-order-status></p><button>Guardar</button></section></form>`);
 await page.addScriptTag({content:fs.readFileSync('src/Sgol.Web/wwwroot/js/process-rules.js','utf8')});
 const read=()=>page.evaluate(()=>Array.from(document.querySelectorAll('[data-process-stage]')).map(row=>Object.fromEntries(Array.from(row.querySelectorAll('[name]')).map(c=>[c.name.split('.').at(-1),c.value]))));
 const check=(ok,label)=>{if(!ok)throw Error(label);console.log('PASS '+label)};
 await page.locator('[name="Input.Steps[5].Position"]').fill('3'); await page.locator('[name="Input.Steps[5].Position"]').dispatchEvent('change');
 let rows=await read(); check(rows.map(r=>r.TaskId).join()==='task-1,task-2,task-6,task-3,task-4,task-5,task-7','Move 6 to 3 and shift all intervening tasks');
 check(rows.map(r=>r.Position).join()==='1,2,3,4,5,6,7','Consecutive positions after move'); check(rows[2].ReturnToPosition==='2'&&rows[2].Instructions==='nota-6','Return destination and instructions preserved');
 await page.locator('[name="Input.Steps[1].Action"]').selectOption('REVISAR'); rows=await read(); check(rows[2].ReleaseCondition==='APROBACION','Changing action updates next entry');
 await page.locator('[name="Input.Steps[1].Position"]').fill('4');await page.locator('[name="Input.Steps[1].Position"]').dispatchEvent('change');rows=await read();
 check(rows[1].TaskId==='task-6'&&rows[1].ReturnToPosition==='4','Return follows original task identity when target moves');
 check(await page.locator('[name="Input.Steps[1].ReturnToPosition"]').evaluate(el=>!el.checkValidity()),'Return to a later task blocks saving with a specific message');
 check(await page.locator('label').evaluateAll(labels=>labels.every(l=>document.getElementById(l.htmlFor)?.closest('fieldset')===l.closest('fieldset'))),'Labels match reindexed controls');
 await page.locator('[name="Input.Steps[1].ReturnToPosition"]').fill('1'); await page.locator('[name="Input.Steps[1].ReturnToPosition"]').dispatchEvent('input');
 check(await page.locator('[name="Input.Steps[1].ReturnToPosition"]').evaluate(el=>el.checkValidity()),'Corrected return clears validation');
 await page.locator('[name="Input.Steps[0].Position"]').fill('7');await page.locator('[name="Input.Steps[0].Position"]').dispatchEvent('change'); rows=await read();check(rows[6].TaskId==='task-1'&&rows[0].ReleaseCondition==='INICIO','Move first to last updates first entry');
 for (const choice of ['ENTREGA','ACEPTADA_CON_DIFERENCIAS','DEVOLUCION']) {
  await page.setContent(`<dialog data-assigned-task-detail="test"><button data-dialog-close>Cerrar</button><form data-assigned-task-submit><button type="submit" name="Decision" value="ENTREGA">Entregar</button><button type="submit" name="Decision" value="ACEPTADA_CON_DIFERENCIAS">Aceptar</button><button type="submit" name="Decision" value="DEVOLUCION">Devolver</button><p data-assigned-task-busy hidden></p></form></dialog>`);
  await page.addScriptTag({content:fs.readFileSync('src/Sgol.Web/wwwroot/js/assigned-tasks.js','utf8')});
  await page.evaluate(()=>document.querySelector('form').addEventListener('submit',event=>{event.preventDefault();window.postedDecision=new FormData(event.target).get('Decision')}));
  await page.locator(`button[value="${choice}"]`).click();
  check(await page.evaluate(()=>window.postedDecision)===choice,`Submission retains selected decision ${choice} while controls are disabled`);
 }
 await browser.close();
})().catch(e=>{console.error(e);process.exitCode=1});