import { cp, mkdir, readFile, writeFile, rm, readdir, stat } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';
import { gzipSync } from 'node:zlib';
import { syncWeb } from '../../scripts/sync-web.mjs';

const here = fileURLToPath(new URL('.', import.meta.url));
const root = resolve(here, '../..');
const out = join(here, 'public');
const build = join(root, 'unity/Builds/WebGL-next');
if (!(await readFile(join(build, 'build-result.txt'), 'utf8')).startsWith('Succeeded')) {
  throw new Error('Сначала соберите Unity WebGL-next без ошибок.');
}
await syncWeb(build);
await rm(out, { recursive: true, force: true });
await cp(build, out, { recursive: true });
await rm(join(out, 'build-result.txt'), {force:true});
let html = await readFile(join(out, 'index.html'), 'utf8');
const compressedAssets = [];
let headers = '/*\n  X-Content-Type-Options: nosniff\n  Referrer-Policy: same-origin\n\n/\n  Cache-Control: no-cache\n\n/demo/*\n  Cache-Control: no-cache\n';
for (const [name, type] of [['WebGL-next.wasm', 'application/wasm'], ['WebGL-next.data', 'application/octet-stream']]) {
  const path = join(out, 'Build', name);
  const input = await readFile(path);
  const compressed = gzipSync(input, {level: 9});
  await writeFile(path + '.gz', compressed);
  await rm(path);
  const index = compressedAssets.length;
  compressedAssets.push({url: 'Build/' + name + '.gz', type});
  html = html.replace('"Build/' + name + '"', `files[${index}]`);
  headers += `\n/Build/${name}.gz\n  Content-Type: application/gzip\n  Cache-Control: public, max-age=3600\n`;
  console.log(`${name}: ${(input.length / 1048576).toFixed(2)} → ${(compressed.length / 1048576).toFixed(2)} MiB`);
}
// Decode at the application layer: static asset hosts may apply an additional
// transport encoding when Content-Encoding is set through _headers.
const bootstrap = `var unityAssetURLs = [];
      async function loadUnityAsset(asset) {
        var response = await fetch(asset.url);
        if (!response.ok) throw new Error('Не удалось загрузить ' + asset.url);
        var stream = response.body.pipeThrough(new DecompressionStream('gzip'));
        var bytes = await new Response(stream).arrayBuffer();
        var url = URL.createObjectURL(new Blob([bytes], {type: asset.type}));
        unityAssetURLs.push(url);
        return url;
      }
      Promise.all(${JSON.stringify(compressedAssets)}.map(loadUnityAsset)).then(function(files) {
      return createUnityInstance(canvas, {`;
if (!html.includes('createUnityInstance(canvas, {')) throw new Error('Unity initialization not found');
html = html.replace('createUnityInstance(canvas, {', bootstrap);
html = html.replace('}).then(function (instance) {', '}); }).then(function (instance) {\n        unityAssetURLs.forEach(URL.revokeObjectURL);');
await writeFile(join(out, 'index.html'), html);
await mkdir(join(out,'teacher'),{recursive:true});
await cp(join(root,'teacher/index.html'),join(out,'teacher/index.html'));
await writeFile(join(out, '_headers'), headers);
await writeFile(join(out, '404.html'), '<!doctype html><html lang="ru"><meta charset="utf-8"><title>Страница не найдена</title><p>Страница не найдена. <a href="/">Открыть игру</a></p></html>');
async function check(dir) {
  for (const name of await readdir(dir)) {
    const p = join(dir, name), s = await stat(p);
    if (s.isDirectory()) await check(p);
    else if (s.size > 25 * 1048576) throw new Error(`Файл превышает лимит Cloudflare: ${p}`);
  }
}
await check(out);
console.log('Cloudflare assets ready:', out);
