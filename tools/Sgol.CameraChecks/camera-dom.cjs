// Exercises the production camera controller in an isolated DOM, without accessing a real camera.
const fs = require('fs');
const { JSDOM } = require('../../.local/camera-dom/node_modules/jsdom');
const controller = fs.readFileSync('src/Sgol.Web/wwwroot/js/camera-capture.js', 'utf8');
const assert = require('node:assert/strict');
const flush = async () => { for (let i = 0; i < 6; i++) await new Promise(resolve => setImmediate(resolve)); };
function fixture({ maximum = 5, denial = false, delayed = false, insecure = false, unsupportedFacing = false, ignoreFacing = false,
  devices = [{ kind: 'videoinput', deviceId: 'front', label: 'Front Camera' }, { kind: 'videoinput', deviceId: 'rear', label: 'Back Camera' }] } = {}) {
  const dom = new JSDOM(`<div data-camera-capture data-camera-min="1" data-camera-max="${maximum}" data-camera-bytes="31457280">
    <button data-camera-open></button><div data-camera-gallery></div><p data-camera-status></p><p data-camera-error hidden></p>
    <dialog data-camera-dialog><video data-camera-video></video><img data-camera-preview hidden><p data-camera-dialog-status></p>
    <label data-camera-selector hidden><select data-camera-device></select></label><button data-camera-shutter disabled></button><button data-camera-switch></button><button data-camera-retry hidden></button><button data-camera-use hidden></button><button data-camera-close></button></dialog></div>`,
    { url: 'https://localhost:7240', runScripts: 'outside-only', pretendToBeVisual: true });
  const w = dom.window; let stopped = 0, requested = [], release, canvasSize, active = 0;
  Object.defineProperty(w, 'isSecureContext', { value: !insecure });
  w.URL.createObjectURL = () => 'blob:test-' + Math.random(); w.URL.revokeObjectURL = () => {};
  w.HTMLDialogElement.prototype.showModal = function () { this.open = true; };
  w.HTMLDialogElement.prototype.close = function () { this.open = false; this.dispatchEvent(new w.Event('close')); };
  w.HTMLMediaElement.prototype.play = async () => {};
  const video = w.document.querySelector('video'); Object.defineProperties(video, { videoWidth: { value: 4000 }, videoHeight: { value: 2000 } });
  w.HTMLCanvasElement.prototype.getContext = () => ({ drawImage() {} });
  w.HTMLCanvasElement.prototype.toBlob = function (callback, type) { canvasSize = [this.width, this.height]; callback(new w.Blob(['camera-pixels'], { type })); };
  Object.defineProperty(w.navigator, 'mediaDevices', { value: {
    getUserMedia: async options => {
      requested.push(options);
      if (denial) { const error = new Error(); error.name = 'NotAllowedError'; throw error; }
      if (unsupportedFacing && options.video.facingMode?.exact) { const error = new Error(); error.name = 'OverconstrainedError'; throw error; }
      assert.equal(active, 0, 'The previous camera must be stopped before opening another');
      if (delayed) await new Promise(resolve => { release = resolve; });
      const id = options.video.deviceId?.exact || (ignoreFacing ? 'front' : options.video.facingMode?.exact === 'user' ? 'front' : 'rear');
      active++;
      let ended = false;
      const track = { stop: () => { if (!ended) { stopped++; active--; ended = true; } }, getSettings: () => ({ deviceId: id, facingMode: id === 'front' ? 'user' : 'environment' }) };
      return { getTracks: () => [track], getVideoTracks: () => [track] };
    }, enumerateDevices: async () => devices, getSupportedConstraints: () => ({ facingMode: devices.length > 1 })
  } });
  w.eval(controller);
  const root = w.document.querySelector('[data-camera-capture]');
  const camera = w.SGOLCamera(root);
  const click = name => root.querySelector(`[data-camera-${name}]`).click();
  return { dom, w, camera, root, click, requested, stopped: () => stopped, release: () => release(), canvasSize: () => canvasSize };
}
(async () => {
  const f = fixture(); f.click('open'); await flush();
  assert.equal(f.requested[0].audio, false); assert.equal(f.requested[0].video.facingMode.exact, 'environment');
  f.click('shutter'); await flush(); assert.equal(f.camera.photos.length, 0); assert.deepEqual(f.canvasSize(), [2560, 1280]);
  assert.equal(f.stopped(), 1);
  f.click('retry'); await flush(); f.click('shutter'); await flush(); f.click('use'); await flush();
  assert.equal(f.camera.photos.length, 1); assert.equal(f.camera.photos[0].file.type, 'image/jpeg');
  assert.equal(f.root.querySelector('input[type=file]'), null);
  console.log('PASS: Camera permission requests no audio; capture is reviewed, retry discards draft, dimensions are bounded and gallery files cannot be selected');
  for (let i=1;i<5;i++) { f.click('open'); await flush(); f.click('shutter'); await flush(); f.click('use'); await flush(); }
  assert.equal(f.camera.photos.length, 5); assert.equal(f.root.querySelector('[data-camera-open]').disabled, true);
  f.root.querySelector('[data-camera-gallery] button').click(); await flush(); assert.equal(f.camera.photos.length, 4);
  f.camera.dispose(); assert.equal(f.camera.photos.length, 0);
  console.log('PASS: Multiple captures enforce the maximum, allow removal and release temporary photos');
  const late = fixture({ delayed: true }); late.click('open'); await flush(); late.click('close'); late.release(); await flush();
  assert.equal(late.stopped(), 1); assert.equal(late.root.querySelector('video').srcObject, null);
  console.log('PASS: A camera permission resolved after Cancel cannot leave the camera running');
  const twenty = fixture({ maximum: 20 });
  for (let i = 0; i < 20; i++) { twenty.click('open'); await flush(); twenty.click('shutter'); await flush(); twenty.click('use'); await flush(); }
  assert.equal(twenty.camera.photos.length, 20);
  assert.equal(twenty.root.querySelector('[data-camera-open]').disabled, true);
  assert.match(twenty.root.querySelector('[data-camera-status]').textContent, /20 de 20/);
  console.log('PASS: Camera accepts twenty photographs and disables further captures at the configured limit');
  const denied = fixture({ denial: true }); denied.click('open'); await flush();
  assert.match(denied.root.querySelector('[data-camera-dialog-status]').textContent, /Permite/); assert.equal(denied.camera.photos.length, 0);
  const insecure = fixture({ insecure: true }); insecure.click('open'); await flush();
  assert.equal(insecure.requested.length, 0); assert.match(insecure.root.querySelector('[data-camera-dialog-status]').textContent, /HTTPS/);
  console.log('PASS: Denied permissions and insecure origins give actionable messages without a file-picker fallback');
  const switching = fixture({ unsupportedFacing: true, ignoreFacing: true }); switching.click('open'); await flush();
  assert.equal(switching.requested[0].video.facingMode.exact, 'environment');
  assert.equal(switching.requested[1].video.facingMode.ideal, 'environment');
  assert.equal(switching.requested[2].video.deviceId.exact, 'rear');
  switching.click('switch'); await flush(); assert.equal(switching.requested.at(-1).video.deviceId.exact, 'front');
  switching.click('switch'); await flush(); assert.equal(switching.requested.at(-1).video.deviceId.exact, 'rear');
  const selector = switching.root.querySelector('[data-camera-device]'); selector.value='front'; selector.dispatchEvent(new switching.w.Event('change')); await flush();
  assert.equal(switching.requested.at(-1).video.deviceId.exact, 'front');
  switching.camera.dispose();
  console.log('PASS: Browsers that ignore facing preferences select the rear device, switch actual device IDs, and allow manual camera selection');
  for (const x of [f,late,denied,insecure,switching]) { x.camera.dispose(); x.dom.window.close(); }
})().catch(error => { console.error(error); process.exitCode=1; });
