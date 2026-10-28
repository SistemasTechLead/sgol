import { build } from 'esbuild';
import { readFile, writeFile } from 'node:fs/promises';
await build({
  entryPoints: ['avatars.jsx'], bundle: true, minify: true, format: 'iife',
  outfile: '../../src/Sgol.Web/wwwroot/js/person-avatars.js',
  alias: { 'react/jsx-runtime': 'preact/jsx-runtime', react: 'preact/compat' }, jsxFactory: 'h', jsxFragment: 'Fragment',
  legalComments: 'linked',
});
await writeFile('../../src/Sgol.Web/wwwroot/js/person-avatars.NOTICES.txt',
  await readFile('node_modules/boring-avatars/LICENSE', 'utf8') + '\n\n' +
  await readFile('node_modules/preact/LICENSE', 'utf8'));
