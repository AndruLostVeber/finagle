import {roundDefinition} from './curriculum.mjs';
export class EconomyError extends Error {constructor(message,status=409){super(message);this.status=status;}}
const requireValue=(ok,message)=>{if(!ok)throw new EconomyError(message);};
export function rub(value,label='сумму'){requireValue(Number.isSafeInteger(value)&&value>=0&&value<=1000000000,`Укажите корректную ${label} в целых рублях`);return value;}
export function createEconomy(round=1){return {cash:2500000,capital:2500000,profit:0,forecastProfit:0,debt:0,payables:0,receivables:0,stock:0,stockValue:0,equipment:[],round,budget:0,budgetSpent:0,decisionCount:0,decisions:[],history:[],accounting:[],journal:[],submitted:false,finished:false,summary:null,protection:false,maintained:false,saving:0};}
function effects(e){return e.decisions.reduce((out,d)=>{for(const [k,v] of Object.entries(d.effect||{})){if(['baseDemand','price','collectNow','protection','maintained'].includes(k))out[k]=v;else out[k]=(out[k]||0)+v;}return out;},{});}
function overhead(e){const f=effects(e);return Math.max(0,160000-e.saving-e.equipment.reduce((s,x)=>s+(x.effect.saving||0),0)) + Math.max(0,roundDefinition(e.round).mandatory-(f.mandatoryReduction||0));}
export function requiredPayments(e){return e.submitted||e.finished?0:overhead(e)+Math.round(e.debt*0.0025);}
export function available(e){return Math.max(0,e.cash-e.payables-requiredPayments(e));}
export function spendable(e){return e.payables?0:Math.max(0,Math.min(available(e),e.budget-e.budgetSpent));}
function entry(e,type,amount,description){e.journal.push({round:e.round,type,amount,description});}
function settlePayables(e){const amount=Math.min(e.cash,e.payables);e.cash-=amount;e.payables-=amount;if(amount)entry(e,'payment',amount,'Погашение начисленных обязательств');}
function expense(e,amount,description){e.profit-=amount;e.payables+=amount;entry(e,'expense',amount,description);settlePayables(e);}
function optional(e,amount){requireValue(e.payables===0,'Сначала погасите просроченные обязательства');requireValue(amount<=available(e),'Недостаточно свободных денег после обязательных платежей');requireValue(amount<=e.budget-e.budgetSpent,'Недостаточно остатка недельного бюджета');e.cash-=amount;e.budgetSpent+=amount;}
export function setBudget(e,amount){rub(amount,'сумму бюджета');requireValue(amount>=e.budgetSpent&&amount<=e.budgetSpent+available(e),'Недельный бюджет должен покрывать уже потраченное и не превышать свободные средства');e.budget=amount;}
export function decide(e,body){
 requireValue(e.decisionCount<5,'Не более пяти решений за неделю');
 const action=roundDefinition(e.round).actions.find(a=>a.id===body.actionId);requireValue(action,'Неизвестное решение');
 requireValue(!e.decisions.some(d=>d.actionId===action.id),'Это решение уже подтверждено');
 let cost=0,effect={},label=action.title;
 if(action.kind==='loan'){
  const amount=rub(body.amount);requireValue(amount>0&&amount+e.debt<=1500000,'Кредитный лимит: не более 1 500 000 ₽ непогашенного долга');
  e.cash+=amount;e.debt+=amount;cost=-amount;entry(e,'loan',amount,'Получение кредита — не выручка');settlePayables(e);
 }else if(action.kind==='repay'){
  const amount=rub(body.amount);requireValue(amount>0&&amount<=e.debt,'Погашение не может превышать долг');optional(e,amount);e.debt-=amount;cost=amount;entry(e,'repay',amount,'Возврат основного долга — не расход');
 }else if(action.kind==='purchase'){
  requireValue(Array.isArray(body.itemIds)&&body.itemIds.length>0&&body.itemIds.length<=action.items.length&&new Set(body.itemIds).size===body.itemIds.length,'Выберите разные позиции оборудования');
  const items=body.itemIds.map(id=>action.items.find(i=>i.id===id));requireValue(items.every(Boolean),'Неизвестное оборудование');requireValue(items.every(i=>!e.equipment.some(x=>x.id===i.id)),'Это оборудование уже куплено');
  cost=items.reduce((s,i)=>s+i.cost,0);optional(e,cost);for(const i of items)e.equipment.push({id:i.id,label:i.label,cost:i.cost,bookValue:i.cost,weeklyDepreciation:Math.round(i.cost/48),effect:i.effect});label=items.map(i=>i.label).join(', ');entry(e,'equipment',cost,'Капитализация: '+label);
 }else{
  const choice=action.options.find(o=>o.id===body.optionId);requireValue(choice,'Неизвестный вариант');cost=choice.cost;effect=choice.effect;label=choice.label;optional(e,cost);
  if(effect.stock){e.stock+=effect.stock;e.stockValue+=cost;entry(e,'materials',cost,label);}
  else if(cost){e.profit-=cost;entry(e,'expense',cost,label);}
  if(effect.protection)e.protection=true;if(effect.maintained)e.maintained=true;if(effect.saving)e.saving+=effect.saving;
 }
 const decision={actionId:action.id,title:action.title,label,cost,effect,...(body.optionId?{optionId:body.optionId}:{}),...(body.itemIds?{itemIds:[...body.itemIds]}:{}),...(['loan','repay'].includes(action.kind)?{amount:body.amount}:{})};
 e.decisions.push(decision);e.decisionCount++;return label;
}
function removeStock(e,count){const amount=count===e.stock?e.stockValue:Math.round(e.stockValue*count/e.stock);e.stock-=count;e.stockValue-=amount;return amount;}
export function settleRound(e){
 requireValue(!e.submitted&&!e.finished,'Неделя уже завершена');const r=roundDefinition(e.round),f=effects(e),openingCash=e.cash,openingProfit=e.profit;
 if(e.receivables){entry(e,'collection',e.receivables,'Оплата ранее признанной выручки');e.cash+=e.receivables;e.receivables=0;}
 let spoilage=0;if(e.round===5&&!e.protection){const lost=Math.floor(e.stock*0.2);if(lost){spoilage=removeStock(e,lost);e.profit-=spoilage;entry(e,'writeoff',spoilage,`Порча ${lost} комплектов`);}}
 const capacity=Math.max(0,100+e.equipment.reduce((s,x)=>s+(x.effect.capacity||0),0)+(f.capacity||0)-(e.round===7&&!e.maintained?30:0));
 const demand=Math.max(0,(f.baseDemand??100)+r.demand+(f.demand||0));
 const units=Math.min(e.stock,capacity,demand),unitPrice=(f.price??4000)+e.equipment.reduce((s,x)=>s+(x.effect.price||0),0),revenue=units*unitPrice,costOfSales=units?removeStock(e,units):0;
 e.profit+=revenue-costOfSales;e.receivables=e.round===6&&!f.collectNow?Math.round(revenue/2):0;e.cash+=revenue-e.receivables;
 entry(e,'revenue',revenue,`Продажа ${units} ламп × ${unitPrice} ₽`);entry(e,'costOfSales',costOfSales,'Себестоимость проданных ламп');
 const operatingExpenses=overhead(e),interest=Math.round(e.debt*0.0025);expense(e,operatingExpenses,'Зарплата, аренда, коммунальные и обязательные расходы');if(interest)expense(e,interest,'Проценты 0,25% на остаток кредита');
 let depreciation=0;for(const item of e.equipment){const amount=Math.min(item.bookValue,item.weeklyDepreciation);item.bookValue-=amount;depreciation+=amount;}e.profit-=depreciation;if(depreciation)entry(e,'depreciation',depreciation,'Амортизация оборудования');settlePayables(e);
 const weekEntries=e.journal.filter(j=>j.round===e.round),weekExpenses=weekEntries.filter(j=>['expense','writeoff','costOfSales','depreciation'].includes(j.type)).reduce((s,j)=>s+j.amount,0);
 e.history.push({round:e.round,title:r.title,monthClose:e.round%4===0,units,revenue,costOfSales,operatingExpenses,interest,depreciation,spoilage,expenses:weekExpenses,profit:revenue-weekExpenses,cash:e.cash,closingProfit:e.profit,settlementCashDelta:e.cash-openingCash,settlementProfitDelta:e.profit-openingProfit,decisions:structuredClone(e.decisions),accounting:structuredClone(e.accounting.filter(a=>a.round===e.round))});e.submitted=true;
}
export function openRound(e,round){e.round=round;e.submitted=false;e.budget=0;e.budgetSpent=0;e.decisionCount=0;e.decisions=[];}
export function updateForecast(e){
 if(e.finished)return e.forecastProfit;
 const sim=structuredClone(e);sim.journal=[];sim.history=[];
 if(!sim.submitted)settleRound(sim);
 for(let round=e.round+1;round<=12;round++){
  openRound(sim,round);if(!sim.payables&&available(sim)>=150000){sim.cash-=150000;sim.stock+=100;sim.stockValue+=150000;}settleRound(sim);
 }
 e.forecastProfit=sim.profit;return e.forecastProfit;
}
export function accountingBalance(e){const equipmentValue=e.equipment.reduce((s,i)=>s+i.bookValue,0);const assets=e.cash+e.stockValue+equipmentValue+e.receivables,liabilities=e.debt+e.payables;return {cash:e.cash,stock:e.stockValue,equipment:equipmentValue,receivables:e.receivables,assets,debt:e.debt,payables:e.payables,liabilities,capital:e.capital,retainedProfit:e.profit,equity:e.capital+e.profit,balanced:assets===liabilities+e.capital+e.profit};}
export function finalize(e){updateForecast(e);e.finished=true;e.summary={earlyClose:e.history.length<12,completedRounds:e.history.length,totalRounds:12,actualProfit:e.profit,forecastProfit:e.forecastProfit,forecastDelta:e.profit-e.forecastProfit,cash:e.cash,debt:e.debt,payables:e.payables,revenue:e.history.reduce((s,h)=>s+h.revenue,0),expenses:e.journal.filter(j=>['expense','writeoff','costOfSales','depreciation'].includes(j.type)).reduce((s,j)=>s+j.amount,0),balance:accountingBalance(e),decisions:e.history.flatMap(h=>h.decisions).length+(e.submitted?0:e.decisionCount),accountingCorrect:e.accounting.filter(a=>a.correct).length,accountingTotal:e.accounting.length,explanation:e.history.length<12?'Досрочное закрытие: учтены только совершенные операции. Непройденные недели не создавали доходы или расходы.':'Квартал завершен. Прибыль рассчитана по фактическим доходам и расходам. Деньги, запасы и долги раскрыты отдельно.'};return e.summary;}
