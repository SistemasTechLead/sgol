const {chromium}=require('playwright');
const fs=require('fs');
const assert=(value,label)=>{if(!value)throw Error(label);console.log('PASS actual checklist browser: '+label);};
(async()=>{
 const browser=await chromium.launch({headless:true});
 try {
  const page=await browser.newPage({ignoreHTTPSErrors:true,viewport:{width:1366,height:900}});
  const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto('https://localhost:7259/tareas?edit='+process.argv[2]);
  const editor=page.locator('[data-checklist-editor]');
  assert(await editor.locator('textarea').count()===6,'saved title and two checkboxes reopen with continuation and next section');
  assert(await editor.locator('select').count()===0,'actual editor has no type selector');
  await page.getByRole('textbox',{name:'Título del apartado 2 (opcional)',exact:true}).fill('Seguridad y servicio');
  await page.getByRole('textbox',{name:'Agregar casilla al apartado 2',exact:true}).fill('Verificar puertas y accesos');
  await page.getByRole('button',{name:'Guardar tarea',exact:true}).click();
  await page.waitForURL(url=>!url.searchParams.has('edit'));
  await page.goto('https://localhost:7259/tareas?edit='+process.argv[2]);
  let data=JSON.parse(await page.locator('[data-checklist-definition]').inputValue());
  assert(data.length===5 && data[3].IsTitle && data[4].Text==='Verificar puertas y accesos','real form saves added section and checkbox');
  fs.mkdirSync('artifacts/checklist-ui',{recursive:true});
  await editor.scrollIntoViewIfNeeded();await page.screenshot({path:'artifacts/checklist-ui/actual-editor-desktop.png',fullPage:true});
  await editor.screenshot({path:'artifacts/checklist-ui/actual-editor-detail.png'});
  await page.setViewportSize({width:320,height:850});await editor.scrollIntoViewIfNeeded();
  assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'actual editor fits 320px');
  await page.screenshot({path:'artifacts/checklist-ui/actual-editor-320.png',fullPage:true});
  await page.evaluate(()=>document.documentElement.dataset.theme='dark');await page.screenshot({path:'artifacts/checklist-ui/actual-editor-dark-320.png',fullPage:true});
  // Remove both title rows and save: headings are optional even after previously using them.
  await page.getByRole('button',{name:'Eliminar título del apartado 2',exact:true}).click();
  await page.getByRole('button',{name:'Eliminar título del apartado 1',exact:true}).click();
  await page.getByRole('button',{name:'Guardar tarea',exact:true}).click();await page.waitForURL(url=>!url.searchParams.has('edit'));
  await page.goto('https://localhost:7259/tareas?edit='+process.argv[2]);
  data=JSON.parse(await page.locator('[data-checklist-definition]').inputValue());
  assert(data.length===3 && data.every(p=>!p.IsTitle),'real form saves and reopens a checklist without titles');
  await page.goto('https://localhost:7259/mi-trabajo?assignedTask='+process.argv[3]);
  const evidence=page.locator('[data-checklist-evidence]');await evidence.scrollIntoViewIfNeeded();
  assert(await evidence.locator('.checklist-title').count()===1 && await evidence.locator('[data-check-id]').count()===2,'submitted history retains its original title and two checkboxes after catalogue edits');
  assert(await evidence.locator('[data-checklist-count]').textContent()==='2 de 2 completados','title is excluded from the real completion count');
  assert(await evidence.locator('[data-check-id]:disabled:checked').count()===2,'submitted checkbox evidence stays checked and read-only');
  await page.screenshot({path:'artifacts/checklist-ui/actual-evidence-320.png',fullPage:true});
  await evidence.screenshot({path:'artifacts/checklist-ui/actual-evidence-detail.png'});
  await page.setViewportSize({width:1366,height:900});await page.screenshot({path:'artifacts/checklist-ui/actual-evidence-desktop.png',fullPage:true});
  assert(errors.length===0,'actual pages have no JavaScript errors');
 } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exit(1);});
