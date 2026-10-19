// Regression: the user's capture + conclusion flow must verify photos before submitting.
const fs = require('fs');
const { JSDOM } = require('../../.local/camera-dom/node_modules/jsdom');
const assert = require('node:assert/strict');
const code = fs.readFileSync('src/Sgol.Web/wwwroot/js/assigned-tasks.js', 'utf8');
const flush = async () => { for (let i=0;i<8;i++) await new Promise(resolve => setTimeout(resolve, 5)); };
function fixture({ failure = false, minimum = 1, deferBegin = false } = {}) {
  const dom = new JSDOM(`<dialog data-assigned-task-detail="task-id"><button data-dialog-close type="button"></button>
    <form data-assigned-task-submit><input name="__RequestVerificationToken" value="csrf"><input name="captureSessionId"><input name="photographIds">
    <div data-camera-capture data-camera-min="${minimum}" data-camera-max="5"><p data-camera-status></p></div>
    <button data-camera-upload type="button"></button><button data-camera-cancel-upload type="button"></button>
    <textarea name="conclusion" maxlength="4000"></textarea><button type="submit"></button><p data-assigned-task-busy hidden></p></form></dialog>`,
    { url: 'https://localhost:7240/mi-trabajo', runScripts: 'outside-only' });
  const w=dom.window, form=w.document.querySelector('form'), button=form.querySelector('button[type=submit]');
  let callbacks, photos=[], calls=[], submitted=[], release, errors=[];
  w.HTMLDialogElement.prototype.showModal = function () { this.open=true; };
  w.HTMLFormElement.prototype.requestSubmit = function () {
    if (!this.dispatchEvent(new w.Event('submit', { bubbles:true, cancelable:true }))) return;
    submitted.push({ session: this.querySelector('[name=captureSessionId]').value,
      photos: JSON.parse(this.querySelector('[name=photographIds]').value), clean: photos.every(p=>p.clean) });
  };
  w.fetch = async (url, options) => {
    const handler = new URL(url, w.location.origin).searchParams.get('handler'); calls.push(handler);
    if (handler==='BeginCamera') {
      if (deferBegin) await new Promise(resolve=>release=resolve);
      return { ok:true, headers:{get:()=> 'application/json'}, json:async()=>({captureSessionId:'session-id',expiresAt:new Date(Date.now()+1800000).toISOString()}) };
    }
    assert.equal(options.body.get('captureSessionId'), 'session-id');
    if (failure) return { ok:false,status:422,headers:{get:()=> 'application/json'},json:async()=>({message:'Fotografía rechazada'}) };
    return { ok:true,headers:{get:()=> 'application/json'},json:async()=>({photographId:options.body.get('photographId'),status:'LIMPIO'}) };
  };
  const root=form.querySelector('[data-camera-capture]');
  w.SGOLCamera = (_, options) => {
    callbacks=options; options.onchange(photos);
    return { photos,valid:()=>photos.length>=minimum && photos.length<=5,render:()=>options.onchange(photos),
      setBusy:value=>{root.setAttribute('aria-busy',String(value));options.onchange(photos);},error:text=>errors.push(text),dispose:()=>{} };
  };
  w.eval(code);
  const conclusion=value=>{form.querySelector('textarea').value=value;form.querySelector('textarea').dispatchEvent(new w.Event('input'));};
  const take=()=>{photos.push({id:w.crypto.randomUUID(),file:new w.File(['photo'],'photo.jpg',{type:'image/jpeg'}),clean:false});callbacks.onchange(photos);};
  return { dom,w,form,button,photos,calls,submitted,errors,conclusion,take,begin:()=>callbacks.onopen(),release:()=>release() };
}
(async()=>{
  const f=fixture();assert.equal(f.button.disabled,true);await f.begin();f.take();assert.equal(f.button.disabled,false);
  f.form.dispatchEvent(new f.w.Event('submit',{cancelable:true}));await flush();
  assert.deepEqual(f.calls,['BeginCamera','UploadCamera']);assert.equal(f.submitted.length,1);assert.equal(f.submitted[0].clean,true);
  assert.equal(f.submitted[0].session,'session-id');assert.equal(f.submitted[0].photos.length,1);
  console.log('PASS: Photo without conclusion enables Send and uploads before exactly one native submission');
  const rejected=fixture({failure:true});await rejected.begin();rejected.take();rejected.conclusion('Conservar conclusión');
  rejected.form.dispatchEvent(new rejected.w.Event('submit',{cancelable:true}));await flush();
  assert.equal(rejected.submitted.length,0);assert.equal(rejected.form.querySelector('textarea').value,'Conservar conclusión');
  assert.equal(rejected.button.disabled,false);assert.ok(rejected.errors.includes('Fotografía rechazada'));
  console.log('PASS: Rejected photos prevent submission and preserve the conclusion for retry');
  const limits=fixture({minimum:2});await limits.begin();limits.take();limits.conclusion('  ');assert.equal(limits.button.disabled,true);
  limits.take();assert.equal(limits.button.disabled,false);
  console.log('PASS: Required photo count is enforced with a blank optional conclusion');
  const race=fixture({deferBegin:true});const pending=race.begin();await flush();race.take();race.conclusion('Terminada');assert.equal(race.button.disabled,true);
  race.release();await pending;assert.equal(race.button.disabled,false);assert.equal(race.form.querySelector('[name=captureSessionId]').value,'session-id');
  console.log('PASS: A capture finishing before session authorization still submits the correct session context');
  for(const x of [f,rejected,limits,race])x.dom.window.close();
})().catch(error=>{console.error(error);process.exitCode=1;});
