import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
function game(){
 const html=readFileSync(new URL('../../../demo-next/index.html',import.meta.url),'utf8');
 let source=html.match(/<script>([\s\S]*?)<\/script>/)[1];
 source=source.slice(0,source.indexOf('const hotApi='))+`window.test={get S(){return S},set S(s){S=s},FRESH,T,DAYS,EQUIP,dayTasks,taskOps,ensureAnswer,testOf,scoreOp,confirmTask,recordDecision,companyProfit,businessImpact,rTitle,rCreate,rPC,rMeet,rEquip,rTalk,rNight,resultsBody,TEAM};})();`;
 const events={},nodes=new Map();
 const node=id=>{if(!nodes.has(id))nodes.set(id,{innerHTML:'',style:{},classList:{toggle(){}},querySelector(){return null},querySelectorAll(){return []},addEventListener(type,fn){events[id+':'+type]=fn},insertAdjacentHTML(){}});return nodes.get(id)};
 const document={querySelector(id){return ['#scr','#ov','#stage','#toastc','#taskpg'].includes(id)?node(id):null},getElementById(id){return node('#'+id)},addEventListener(){}};
 const local=new Map(),window={addEventListener(){}};
 vm.runInNewContext(source,{document,window,localStorage:{getItem:k=>local.get(k),setItem:(k,v)=>local.set(k,v)},setInterval(){},setTimeout(){},clearTimeout(){},clearInterval(){},console,location:{protocol:'http:',hostname:'localhost',port:'8790'}});
 return {api:window.test,events};
}
test('student entry accepts a code and existing team only',()=>{
 const {api:g}=game();const title=g.rTitle();assert.equal((title.match(/<button/g)||[]).length,1);assert.match(title,/Присоединиться к игре/);assert.doesNotMatch(title,/Вступление|Продолжить|Настройки|Об игре|Рейтинг команд|Деловая игра|ноябрь/);assert.match(g.rCreate(),/Команда/);assert.doesNotMatch(g.rCreate(),/Создать команду|Новая команда|QR/);assert.match(g.rCreate(),/Код игры/);
 g.S.screen='office';assert.match(g.rPC(),/Результаты команд/);assert.match(g.rPC(),/Моя компания/);
});
test('all January paths render, answers change profit once, companies are isolated',()=>{
 const {api:g}=game();
 for(const site of ['A','B','V'])for(const money of ['A','B','V'])for(const raw of ['A','B','V']){
  g.S=g.FRESH();const s=g.S;s.screen='office';s.setup.company='Компания';s.setup.team='Тест';s.choices={site,money,raw,equip:Array(10).fill(1)};
  for(let day=0;day<g.DAYS.length;day++){
   s.day=day;
   for(const id of g.dayTasks(day)){
    s.task=id;const ans=g.ensureAnswer(id);
    for(const [i,op] of g.taskOps(id).entries()){
     const test=g.testOf(id,i);assert.ok(test.opts.length>=3);
     const rows=op.ref||[];ans.ops[i]={pick:0,none:rows.length===0,rows:rows.map(r=>({dt:r[0],kt:r[1],amt:String(r[2])}))};
     const result=g.scoreOp(op,ans.ops[i]);assert.equal(result.got,result.max);
     g.recordDecision('op:'+id+':'+i,25,op.t,'Верно');g.recordDecision('op:'+id+':'+i,25,op.t,'Верно');
    }
    ans.done=true;
   }
   assert.doesNotThrow(()=>g.rNight());
  }
  assert.equal(g.companyProfit(),-250+s.finance.events.length*25);
  assert.match(g.resultsBody('company'),/История решений/);
 }
 const before=g.S;g.S=g.FRESH();assert.equal(g.companyProfit(),-250);assert.ok(before.finance.events.length>0);
});
test('actual answer button applies correct and incorrect financial outcomes',()=>{
 const {api:g,events}=game(),s=g.S;s.screen='office';s.task='e1';const ans=g.ensureAnswer('e1');
 const opts=g.testOf('e1',0).opts,op=g.taskOps('e1')[0];
 const correct=opts.findIndex(rows=>JSON.stringify(rows)===JSON.stringify(op.ref));
 assert.ok(correct>=0);ans.sel=correct;
 const click=()=>events['#stage:click']({target:{closest:()=>({dataset:{act:'tok'}})}});
 click();assert.equal(g.companyProfit(),-225);
 const next=g.testOf('e1',1);ans.sel=next.opts.findIndex(rows=>JSON.stringify(rows)!==JSON.stringify(g.taskOps('e1')[1].ref));
 click();assert.equal(g.companyProfit(),-260);
});
