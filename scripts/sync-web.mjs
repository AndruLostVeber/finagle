import {readFile,writeFile,mkdir,copyFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {resolve,join} from 'node:path';
export const root=resolve(fileURLToPath(new URL('..',import.meta.url)));
export async function syncWeb(build=join(root,'unity/Builds/WebGL-next')){
  const source=await readFile(join(root,'demo-next/index.html'),'utf8');
  const end='\n})();\n</script>';
  if(!source.includes(end))throw Error('Не найден основной скрипт игры');
  const stamp=Date.now();
  let html=source.replace(end,'\nwindow.kuroDemo={get S(){return S;},DAYS:DAYS,openTasks:openTasks};'+end).replace('</body>',`<script src="kuro-embed.js?v=${stamp}"></script>\n</body>`);
  if(!source.includes('</body>'))html+=`\n<script src="kuro-embed.js?v=${stamp}"></script>\n`;
  await mkdir(join(build,'demo'),{recursive:true});
  await writeFile(join(build,'demo/index.html'),html);
  await copyFile(join(root,'unity/Assets/WebGLTemplates/KuroNext/demo/kuro-embed.js'),join(build,'demo/kuro-embed.js'));
  let index=await readFile(join(build,'index.html'),'utf8');
  index=index.replace(/src="demo\/index.html(?:\?v=\d+)?"/,`src="demo/index.html?v=${stamp}"`);
  await writeFile(join(build,'index.html'),index);
}
if(process.argv[1]===fileURLToPath(import.meta.url)){await syncWeb();console.log('Интерфейс 3D-игры обновлен');}
