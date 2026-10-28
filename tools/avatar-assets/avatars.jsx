import { h, render } from 'preact';
import Avatar from 'boring-avatars';

const variants = ['marble', 'beam', 'pixel', 'sunset', 'ring', 'bauhaus'];
const palettes = [
  ['#92A1C6', '#146A7C', '#F0AB3D', '#C271B4', '#C20D90'],
  ['#E8A399', '#F7DDD5', '#78528C', '#BA6C84', '#58436F'],
  ['#D4ECED', '#146A7C', '#429EA6', '#294C60', '#87C7B7'],
  ['#F4DBB0', '#C47C4D', '#785440', '#8C9566', '#E8B66B'],
];
const sources = new Map();
function draw(element, variant, palette, seed) {
  if (!variants.includes(variant)) return;
  const name = `${element.dataset.personAvatar}:${seed}`;
  const key = `${name}:${variant}:${palette}`;
  let src = sources.get(key);
  if (!src) {
    const host = document.createElement('div');
    render(h(Avatar, { name, variant, colors: palettes[palette] || palettes[0], size: 80 }), host);
    // Each SVG image has its own ID scope: repeated masks/gradients cannot collide.
    src = `data:image/svg+xml,${encodeURIComponent(new XMLSerializer().serializeToString(host.firstChild))}`;
    render(null, host);
    if (sources.size > 2048) sources.clear();
    sources.set(key, src);
  }
  const image = new Image(); image.alt = ''; image.width = 80; image.height = 80; image.src = src;
  element.replaceChildren(image);
}
const seen = new WeakSet();
function initialize(root) {
  const elements = [...(root.matches?.('[data-person-avatar]') ? [root] : []), ...root.querySelectorAll('[data-person-avatar]')];
  for (const element of elements) {
    if (seen.has(element)) continue;
    seen.add(element);
    draw(element, element.dataset.avatarVariant, Number(element.dataset.avatarPalette), Number(element.dataset.avatarSeed));
  }
}
initialize(document);
new MutationObserver(records => {
  for (const record of records) for (const node of record.addedNodes)
    if (node.nodeType === 1) initialize(node);
}).observe(document.body, { childList: true, subtree: true });

const editor = document.querySelector('[data-avatar-editor]');
if (editor) {
  const preview = editor.querySelector('[data-avatar-preview] [data-person-avatar]');
  const initial = editor.querySelector('input[name="AvatarVariant"][value=""]').closest('label').querySelector('.person-avatar').textContent;
  const palette = editor.querySelector('[name="AvatarPalette"]');
  const seed = editor.querySelector('[name="AvatarSeed"]');
  const shuffle = editor.querySelector('[data-avatar-shuffle]');
  const status = editor.querySelector('[data-avatar-status]');
  function update() {
    const variant = editor.querySelector('[name="AvatarVariant"]:checked')?.value || '';
    editor.querySelector('[data-avatar-customize]').hidden = !variant;
    palette.disabled = seed.disabled = shuffle.disabled = !variant;
    if (variant) draw(preview, variant, Number(palette.value), Number(seed.value));
    else preview.textContent = initial;
    for (const option of editor.querySelectorAll('[data-avatar-option]'))
      draw(option, option.dataset.avatarOption, Number(palette.value), Number(seed.value));
    status.textContent = 'Vista previa actualizada. Guarda para aplicar los cambios.';
  }
  editor.addEventListener('change', update);
  shuffle.addEventListener('click', () => {
    const value = new Uint32Array(1); crypto.getRandomValues(value);
    seed.value = value[0] % 1000000; update();
  });
}
