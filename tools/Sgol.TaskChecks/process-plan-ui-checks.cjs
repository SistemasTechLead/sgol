const {chromium}=require('playwright');const fs=require('fs');const path=require('path');
const check=(ok,text)=>{if(!ok)throw Error(text);console.log('PASS '+text)};
(async()=>{
 const browser=await chromium.launch({headless:true});const page=await browser.newPage({viewport:{width:1366,height:900}});
 const source=fs.readFileSync('src/Sgol.Web/Pages/Planning/Processes.cshtml','utf8');
 const start=source.indexOf('<article class="process-stage process-plan-card">',source.indexOf('data-process-plan-editor'));
 const template=source.slice(start,source.indexOf('</article>',start)+10);
 const cards=Array.from({length:7},(_,i)=>template.replaceAll('@step.Position',String(i+1)).replaceAll('@task.Name',`Tarea ${i+1}: revisar información del reporte`).replaceAll('@IndexModel.RoleLabel(task.AssignedRole)','Administración').replaceAll('@task.LoadMinutes',String(15+i*5)).replaceAll('@IndexModel.Label(task.Requirement)','Comentario').replace(/@\(Model.Preview != null[^<]*?<\/span>/,'</span>').replace(/<option value="@Guid.Empty"[\s\S]*?<\/option>/,'<option value="00000000-0000-0000-0000-000000000000">Aleatorio · según disponibilidad y carga</option>').replace(/@foreach\(var worker[\s\S]*?\}/,'<option value="a" selected>Alejandra · disponible</option><option value="b">Mariana · disponible</option> ').replace(/@\(string.IsNullOrWhiteSpace\(step.Instructions\)[\s\S]*?<\/p>/,'Revisa los datos y adjunta el resultado.</p>')).join('');
 await page.setContent(`<div style="container-type:inline-size;container-name:sgol-viewport"><section class="process-page" data-process-plan-page><h1>Plan de proceso</h1><div class="process-toolbar"><div><strong>Reporte de incidencias</strong><small data-process-plan-status></small></div><div class="process-actions"><button class="boton boton--secundario" form="process-plan-editor">Ver fechas y capacidad</button><button class="boton boton--primario" form="process-plan-prepare" data-process-plan-save>Guardar plan preparado</button></div><p data-process-plan-next>Guardar reserva las etapas. Activar es el siguiente paso.</p></div><form id="process-plan-editor" data-process-plan-editor data-has-preview="true"><details class="panel-seccion process-plan-settings"><summary>Nombre y fechas del plan</summary><label class="campo">Nombre<input name="Input.Name" value="Plan de prueba" required></label></details><section class="panel-seccion"><div class="process-section-heading"><h2>Responsables por tarea</h2><button type="button" class="boton boton--secundario" data-process-plan-auto>Todos en Aleatorio</button></div><div class="process-flow process-plan-flow" data-process-plan-flow>${cards}</div></section></form><section class="panel-seccion" data-process-plan-preview><h2>Revisar plan</h2><form id="process-plan-prepare"><input type="hidden" name="Input.Fingerprint" value="reviewed"></form></section></section></div>`);
 for(const css of ['tokens.css','components.css','mobile.css','processes.css'])await page.addStyleTag({path:path.join('src/Sgol.Web/wwwroot/css',css)});
 await page.addStyleTag({content:'body{margin:16px;font-family:Arial;background:#f7f5f2}'});
 await page.addScriptTag({path:'src/Sgol.Web/wwwroot/js/process-plan.js'});
 const save=page.locator('[data-process-plan-save]');
 check(await save.isEnabled(),'Reviewed plan can be saved');
 await page.locator('[data-process-plan-auto]').click();
 check(await page.locator('[data-process-plan-person]').evaluateAll(selects=>selects.every(s=>s.value==='00000000-0000-0000-0000-000000000000')),'Bulk automatic selection marks every individual stage');
 check(await save.isDisabled() && (await page.locator('[data-process-plan-next]').textContent()).includes('Vuelve a revisar'),'Changing recipients blocks saving a stale preview with explanation');
 for(const select of await page.locator('[data-process-plan-person]').all())await select.selectOption('a');
 check(await save.isEnabled(),'Reverting recipients restores reviewed plan');
 await page.locator('[data-process-plan-detail-open]').first().click();
 check(await page.locator('dialog[open]').isVisible(),'Instructions open without expanding square cards');
 await page.locator('dialog[open] [data-process-plan-detail-close]').click();
 check(await page.locator('dialog[open]').count()===0,'Instructions close and retain recipients');
 await page.locator('.process-plan-settings summary').click();await page.locator('[name="Input.Name"]').fill('Otro nombre');
 check(await save.isDisabled(),'Changing plan settings invalidates preview');
 await page.locator('[name="Input.Name"]').fill('Plan de prueba');await page.locator('.process-plan-settings summary').click();
 fs.mkdirSync('artifacts/process-plan-ui',{recursive:true});
 for(const width of [1366,768,390,320]){
  await page.setViewportSize({width,height:900});await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
  const geometry=await page.locator('[data-process-plan-flow]').evaluate(flow=>Array.from(flow.children).map(c=>({w:c.clientWidth,h:c.clientHeight,content:Math.max(...Array.from(c.children).filter(el=>!el.matches("dialog,.process-flow-arrow")).map(el=>el.getBoundingClientRect().bottom-c.getBoundingClientRect().top)),x:c.offsetLeft,y:c.offsetTop})));
  check(geometry.every(c=>Math.abs(c.w-c.h)<2),`Cards stay square at ${width}px`);
  check(await page.locator('.process-plan-card').evaluateAll(cards=>cards.every(card=>{
   const box=card.getBoundingClientRect(),title=card.querySelector('.process-plan-task-name').getBoundingClientRect(),select=card.querySelector('select').getBoundingClientRect(),button=card.querySelector('[data-process-plan-detail-open]').getBoundingClientRect();
   return title.width>box.width*.7 && title.height>12 && select.width>box.width*.7 && title.bottom<=select.top && select.bottom<=button.top+1 && button.bottom<=box.bottom;
  })),`Title, recipient and instructions occupy separate full-width rows at ${width}px`);
  check(geometry.every(c=>c.content<=c.h+1),`Card contents fit at ${width}px`);
  check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),`No horizontal overflow at ${width}px`);
  check(await page.locator('[data-process-plan-person]').first().isVisible(),`Responsible selector is visible on card at ${width}px`);
  check(await page.locator('[data-process-plan-flow]>:last-child [data-process-flow-arrow]').isHidden(),'Last stage has no outgoing arrow');
  await page.evaluate(()=>window.scrollTo(0,0));await page.screenshot({path:`artifacts/process-plan-ui/${width}.png`,fullPage:true});
 }
 await page.evaluate(()=>document.documentElement.dataset.theme='dark'); await page.addStyleTag({content:'body{background:var(--color-fondo);color:var(--color-texto-primario)}'}); await page.screenshot({path:'artifacts/process-plan-ui/dark-320.png',fullPage:true});
 await page.setViewportSize({width:414,height:900}); await page.locator('.process-plan-card').first().screenshot({path:'artifacts/process-plan-ui/dark-card.png'});
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
