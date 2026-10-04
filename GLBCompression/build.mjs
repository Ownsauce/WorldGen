import { build } from './node_modules/esbuild/lib/main.js';
const dependencies = new URL('./node_modules/', import.meta.url).pathname;
await build({
  entryPoints: [new URL('./src.js', import.meta.url).pathname],
  bundle: true,
  format: 'iife',
  target: 'es2022',
  outfile: new URL('./app.js', import.meta.url).pathname,
  nodePaths: [dependencies]
});
