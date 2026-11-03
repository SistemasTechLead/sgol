const {chromium,webkit}=require('playwright');
const assert=require('assert/strict');
const path=require('path');
(async()=>{let count=0;for(const [name,engine] of Object.entries({chromium,webkit})){
const browser=await engine.launch(name==='chromium'?{channel:'msedge'}:{});
const page=await browser.newPage();
for(const width of [320,768,1440])for(const theme of ['light','dark','special']){
await page.setViewportSize({width,height:1000});
const block=(title,text)=>`<div class="postponement-blocks"><div class="postponement-obstacle"><strong>${title}</strong><div class="postponement-obstacle__content"><p>${text}</p><small>Revisa la propuesta y el alcance de supervisión.</small></div></div></div>`;
await page.setContent(`<html data-theme="${theme}"><body><section class="postponement-item"><div class="postponement-diagnostics">${block('Cruce de horario','Coincide con Dar seguimiento a pedidos, de 09:30 a 10:15.')}${block('Sin personas para reasignar','No hay otra persona activa elegible con la especialidad Administración dentro de tu alcance de supervisión.')}</div></section></body></html>`);
for(const css of ['tokens.css','postponement-review.css'])await page.addStyleTag({path:path.resolve('src/Sgol.Web/wwwroot/css',css)});
const boxes=await page.locator('.postponement-blocks').evaluateAll(es=>es.map(e=>{const r=e.getBoundingClientRect();return {x:r.x,y:r.y,w:r.width,h:r.height}}));
if(width===1440){assert(Math.abs(boxes[0].y-boxes[1].y)<1);assert(boxes[1].x>boxes[0].x+boxes[0].w);}else assert(boxes[1].y>=boxes[0].y+boxes[0].h);
assert(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));count++;
}await browser.close();}console.log('PASS '+count+' component layout cases: side by side at 1440; stacked at 320/768; no horizontal overflow.');})().catch(e=>{console.error(e);process.exitCode=1});
