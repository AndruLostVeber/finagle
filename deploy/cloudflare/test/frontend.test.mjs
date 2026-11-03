import test from 'node:test';
import assert from 'node:assert/strict';
import {existsSync} from 'node:fs';
const source=new URL('../../../demo-next/quarter.mjs',import.meta.url);
async function ui(){assert.ok(existsSync(source),'quarter client must exist');return import(source)}
const storage=()=>{const map=new Map();return {getItem:k=>map.get(k)||null,setItem:(k,v)=>map.set(k,v),removeItem:k=>map.delete(k)}};
const auth={league:'G12345678',id:'person',secret:'secret',teamId:'team'};
test('uncertain purchase survives reload and retries its original identity and selection',async()=>{
 const {createSession}=await ui(),store=storage();let accepted;const s=createSession({storage:store,request:async(path,body)=>{accepted={path,body};throw Error('offline')}});s.setMember(auth);
 await assert.rejects(s.write('decision',{round:1,actionId:'equipment',itemIds:['press']}),/offline/);
 const id=accepted.body.requestId;assert.ok(id);let retry;
 const resumed=createSession({storage:store,request:async(path,body)=>{retry={path,body};return {company:{cash:2200000}}}});
 assert.equal(resumed.member.id,'person');assert.equal(resumed.pending.body.requestId,id);
 const result=await resumed.retry();assert.equal(result.company.cash,2200000);assert.deepEqual(retry,accepted);assert.equal(resumed.pending,null);
});
test('a second write cannot overwrite an uncertain decision; a server rejection unlocks it',async()=>{
 const {createSession}=await ui(),s=createSession({storage:storage(),request:async()=>{throw Error('offline')}});s.setMember(auth);
 await assert.rejects(s.write('decision',{round:1,actionId:'a',optionId:'yes'}));
 await assert.rejects(s.write('decision',{round:1,actionId:'b',optionId:'no'}),/подтверждение/);
 const bad=Error('budget');bad.status=422;const r=createSession({storage:storage(),request:async()=>{throw bad}});r.setMember(auth);
 await assert.rejects(r.write('budget',{round:1,amount:999}));assert.equal(r.pending,null);
});
test('order is one payload with only explicitly enabled items and no financial outcomes',async()=>{
 const {decisionPayload,orderCost}=await ui();const action={id:'equipment',kind:'purchase',items:[{id:'a',cost:100},{id:'b',cost:250}]};
 assert.deepEqual(decisionPayload(action,{items:{a:true,b:false},cash:999999}),{actionId:'equipment',itemIds:['a']});
 assert.equal(orderCost(action,{items:{a:true,b:false}}),100);
 assert.deepEqual(decisionPayload(action,{items:{}}),{actionId:'equipment',itemIds:[]});
 assert.throws(()=>decisionPayload({id:'loan',kind:'loan',min:100,max:1000,step:100},{amount:-10}));
});
test('budget reserves obligations and separates quarter forecast from spendable cash',async()=>{
 const {financeView}=await ui();assert.deepEqual(financeView({cash:1000000,forecastProfit:9000000,profit:10000,budget:500000,budgetSpent:125000,requiredPayments:300000,spendable:700000,payables:0}),{cash:1000000,forecast:9000000,profit:10000,remaining:375000,available:700000,blocked:false});
 assert.equal(financeView({cash:100,budget:100,budgetSpent:0,spendable:100,payables:1}).blocked,true);
});
test('summary distinguishes early actuals, forecast, debt and isolated preview',async()=>{
 const {summaryHTML,rankingHTML}=await ui();const state={game:{status:'closed',round:5,totalRounds:12},preview:true,company:{company:'<script>',cash:500000,profit:20000,forecastProfit:70000,debt:100000,payables:5000,summary:{earlyClose:true,roundsCompleted:5}},teams:[]};
 const html=summaryHTML(state);assert.match(html,/Досрочное/);assert.match(html,/Тестовый/);assert.match(html,/20\s000/);assert.match(html,/100\s000/);assert.doesNotMatch(html,/<script>/);
 const ranks=rankingHTML([{id:'a',name:'A',rank:1,memberCount:2,profit:10,forecastProfit:20},{id:'b',name:'B',rank:1,memberCount:1,profit:10,forecastProfit:20}],'a',false);assert.equal((ranks.match(/data-rank="1"/g)||[]).length,2);
});
