export const esc=value=>String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
export const money=value=>Math.round(Number(value)||0).toLocaleString('ru-RU')+' ₽';
export const signed=value=>(Number(value)>0?'+':'')+money(value);
const read=(storage,key)=>{try{return JSON.parse(storage.getItem(key)||'null')}catch{return null}};
export function createSession({storage,request}){
 const key='kuro-quarter-session-v3';let saved=read(storage,key)||{},member=saved.member||null,pending=saved.pending||null;
 function persist(){storage.setItem(key,JSON.stringify({member,pending}));}
 async function retry(){
  if(!pending)return null;
  const current=pending;
  try{const data=await request(current.path,current.body);pending=null;persist();return data;}
  catch(error){if(error.status>=400&&error.status<500&&error.status!==408&&error.status!==429){pending=null;persist();}throw error;}
 }
 return {get member(){return member},get pending(){return pending},setMember(value){member=value;persist()},
  read:(path='state',body={})=>request(path,{...member,...body}),retry,
  async write(path,body){if(pending)throw Error('Сначала восстановите подтверждение предыдущего запроса.');pending={path,body:{...member,...body,requestId:crypto.randomUUID()}};persist();return retry();}
 };
}
export function decisionPayload(action,draft={}){
 if(action.kind==='purchase')return {actionId:action.id,itemIds:(action.items||[]).filter(item=>draft.items?.[item.id]===true).map(item=>item.id)};
 if(action.kind==='choice'){
  if(!action.options?.some(option=>option.id===draft.optionId))throw Error('Выберите один вариант.');
  return {actionId:action.id,optionId:draft.optionId};
 }
 if(action.kind==='loan'||action.kind==='repay'){
  const amount=Number(draft.amount);if(!Number.isSafeInteger(amount)||amount<Math.max(1,action.min||0)||amount>(action.max??Infinity)||(action.step&&amount%action.step!==0))throw Error('Укажите допустимую сумму в рублях.');
  return {actionId:action.id,amount};
 }
 throw Error('Неизвестный вид решения. Обновите страницу.');
}
export function orderCost(action,draft={}){
 if(action.kind==='purchase')return (action.items||[]).reduce((sum,item)=>sum+(draft.items?.[item.id]===true?item.cost:0),0);
 if(action.kind==='choice')return action.options?.find(option=>option.id===draft.optionId)?.cost||0;
 return action.kind==='repay'?Number(draft.amount)||0:0;
}
export function financeView(company){return {cash:company.cash,forecast:company.forecastProfit,profit:company.profit,remaining:Math.max(0,(company.budget||0)-(company.budgetSpent||0)),available:Math.max(0,company.spendable||0),blocked:company.payables>0};}
export function rankingHTML(teams,ownId,closed){
 return `<p class="muted">Место определяется ${closed?'средней фактической прибылью':'средним прогнозом прибыли за весь квартал'}. При равенстве — общее место.</p><div class="table-scroll" tabindex="0" aria-label="Результаты команд"><table><thead><tr><th>Место / команда</th><th>Компаний</th><th>Факт</th><th>Прогноз квартала</th></tr></thead><tbody>${teams.map(team=>`<tr data-rank="${Number(team.rank)||'—'}" class="${team.id===ownId?'own':''}"><th scope="row">${team.rank??'—'} · ${esc(team.name)}${team.id===ownId?'<small>Ваша команда</small>':''}</th><td>${team.memberCount??team.members?.length??0}</td><td>${money(team.profit)}</td><td>${money(team.forecastProfit)}</td></tr>`).join('')||'<tr><td colspan="4">Команд пока нет.</td></tr>'}</tbody></table></div>`;
}
export function summaryHTML(state){
 const c=state.company,s=c.summary||{},early=s.earlyClose??s.early??state.game.round<12;
 return `${state.preview?'<p class="notice">Тестовый просмотр на отдельной копии. Результаты компании и рейтинг не изменены; пропущенные недели не создают операций.</p>':''}<h2>${early?'Досрочное завершение':'Квартал завершён'}</h2><p>${esc(c.company||c.name)} · закрыто недель: ${s.roundsCompleted??s.completedRounds??c.history?.filter(h=>h.type==='settlement').length??state.game.round} из 12.</p><dl class="ledger"><div><dt>Фактическая прибыль${early?' на текущую дату':''}</dt><dd>${money(s.profit??c.profit)}</dd></div><div><dt>Деньги на счёте</dt><dd>${money(s.cash??c.cash)}</dd></div><div><dt>Прогноз прибыли за весь квартал</dt><dd>${money(s.forecastProfit??c.forecastProfit)}</dd></div><div><dt>Непогашенный кредит</dt><dd>${money(s.debt??c.debt)}</dd></div><div><dt>Неоплаченные обязательства</dt><dd>${money(s.payables??c.payables)}</dd></div></dl><p>Прогноз не является деньгами на счёте. Капитал и кредит не входят в выручку; долг сохраняется после завершения.</p>${early?'<p class="notice">Показан фактический результат только проведённых недель. Будущие доходы и расходы не начислялись.</p>':''}`;
}
