import {createEconomy,requiredPayments,spendable,available,setBudget,decide,settleRound,openRound,updateForecast,accountingBalance,finalize,EconomyError} from './economy.mjs';
import {roundDefinition,publicRound} from './curriculum.mjs';
export {EconomyError};
export class TeamError extends Error {constructor(message,status=400){super(message);this.status=status;}}
const clean=(value,max,label)=>{if(typeof value!=='string'||!value.trim()||value.trim().length>max)throw new TeamError('Укажите '+label+' (до '+max+' символов)');return value.trim();};
export function leagueName(value='main'){const name=clean(value,32,'соревнование');if(!/^[a-zA-Z0-9а-яА-ЯёЁ_-]+$/.test(name))throw new TeamError('Неверное название соревнования');return name;}
export class Competition {
 constructor(saved){this.game=saved?.game||null;this.teams=saved?.teams||[];this.people=saved?.people||[];}
 snapshot(){return {game:this.game,teams:this.teams,people:this.people};}
 create(body,code){if(this.game){if(this.game.requestId!==body.requestId)throw new TeamError('Код занят. Повторите создание.',409);}else this.game={code,title:clean(body.title,80,'название игры'),status:'lobby',economyVersion:3,round:1,totalRounds:12,secret:crypto.randomUUID(),requestId:body.requestId,created:Date.now()};return {code:this.game.code,secret:this.game.secret};}
 exists(){if(!this.game)throw new TeamError('Игра с таким кодом не найдена. Получите код у преподавателя.',404);}
 teacher(body){this.exists();if(body.secret!==this.game.secret)throw new TeamError('Нет доступа к панели преподавателя',403);}
 manage(body){
  this.teacher(body);
  if(body.action==='team'){const name=clean(body.name,40,'название команды');if(this.game.status==='closed')throw new TeamError('Игра завершена',409);if(this.teams.some(t=>t.name.toLocaleLowerCase()===name.toLocaleLowerCase()))throw new TeamError('Команда уже существует',409);if(this.teams.length>=100)throw new TeamError('Не более 100 команд',409);this.teams.push({id:crypto.randomUUID(),name});}
  else if(body.action==='start'){if(!this.teams.length)throw new TeamError('Сначала добавьте команды');if(this.game.status==='closed')throw new TeamError('Игра уже завершена',409);this.game.status='active';}
  else if(body.action==='advance'){
   this.active();if(body.round!==this.game.round)throw new TeamError('Неделя уже изменилась',409);
   if(this.people.some(p=>p.economy&&!p.economy.submitted)&&body.skipUnfinished!==true)throw new TeamError('Не все компании завершили неделю. Подтвердите пропуск незавершенных компаний.',409);
   if(this.game.round===12)this.close();else{this.game.round++;for(const p of this.people)if(p.economy){openRound(p.economy,this.game.round);updateForecast(p.economy);}}
  }else if(body.action==='close')this.close();else if(body.action!=='view')throw new TeamError('Неизвестное действие');
  return this.view('teacher');
 }
 close(){if(this.game.status==='closed')return;for(const p of this.people)if(p.economy)finalize(p.economy);this.game.status='closed';this.game.closed=Date.now();this.game.earlyClose=this.people.some(p=>p.economy&&p.economy.history.length<12);}
 join(body,league){
  this.exists();if(this.game.status==='closed')throw new TeamError('Прием участников завершен',409);
  const name=clean(body.name,40,'имя'),company=clean(body.company,48,'компанию'),team=this.teams.find(t=>t.id===body.teamId);if(!team)throw new TeamError('Выберите команду, созданную преподавателем',404);
  const requestId=clean(body.requestId,100,'идентификатор входа');let person=this.people.find(p=>p.requestId===requestId);
  if(!person){if(this.people.length>=500)throw new TeamError('Соревнование заполнено',409);person={id:crypto.randomUUID(),secret:crypto.randomUUID(),teamId:team.id,name,company,practice:false,events:[],state:null,requestId};if(this.game.economyVersion===3){person.economy=createEconomy(this.game.round);updateForecast(person.economy);person.requests={};person.evaluations={};}this.people.push(person);}
  return {member:{id:person.id,secret:person.secret,teamId:person.teamId,league},team:this.teams.find(t=>t.id===person.teamId)};
 }
 member(body){this.exists();const p=this.people.find(p=>p.id===body.id&&p.secret===body.secret);if(!p)throw new TeamError('Нет доступа к компании',403);return p;}
 active(){this.exists();if(this.game.status!=='active')throw new TeamError(this.game.status==='lobby'?'Преподаватель еще не открыл игру':'Игра завершена',409);if(this.game.economyVersion!==3)throw new TeamError('Сохранение старой версии доступно только через прежний протокол',409);}
 state(body,testFinal=false){const p=this.member(body);if(!p.economy)return {...this.view(p.teamId),legacy:true,state:p.state,company:null,round:null,features:{testFinal:false}};const e=p.economy;const {journal,protection,maintained,saving,...company}=structuredClone(e);company.decisions=company.decisions.map(({effect,...d})=>d);company.history=company.history.map(h=>({...h,decisions:h.decisions.map(({effect,...d})=>d)}));return {...this.view(p.teamId),company:{id:p.id,name:p.name,company:p.company,...company,requiredPayments:requiredPayments(e),spendable:spendable(e),availableFunds:available(e),balance:accountingBalance(e)},round:publicRound(e.round),features:{testFinal}};}
 mutation(body,kind,operation){
  const p=this.member(body),requestId=clean(body.requestId,100,'идентификатор запроса');if(!Number.isInteger(body.round)||body.round<1||body.round>12)throw new TeamError('Неверная неделя');
  if(!p.economy)throw new TeamError('Нужна игра новой версии',409);
  const key=body.round+':'+requestId,prior=p.requests[key];if(prior){if(prior.kind!==kind)throw new TeamError('Идентификатор уже использован другим запросом',409);return {...this.state(body),...prior.extra};}
  this.active();if(body.round!==this.game.round||p.economy.round!==body.round)throw new TeamError('Неделя уже изменилась. Обновите состояние.',409);if(p.economy.submitted||p.economy.finished)throw new TeamError('Неделя уже завершена',409);
  // Apply to a copy so a failed check cannot leave a partial financial mutation.
  const next=structuredClone(p.economy),extra=operation(next,p)||{};p.economy=next;p.updated=Date.now();p.requests[key]={kind,extra};return {...this.state(body),...extra};
 }
 budget(body){return this.mutation(body,'budget',e=>{setBudget(e,body.amount);});}
 decision(body){return this.mutation(body,'decision',e=>{const before={cash:e.cash,profit:e.profit,forecastProfit:e.forecastProfit},reason=decide(e,body);updateForecast(e);return {change:{cash:e.cash-before.cash,profit:e.profit-before.profit,forecastProfit:e.forecastProfit-before.forecastProfit,reason}};});}
 answer(body){return this.mutation(body,'answer',e=>{const task=roundDefinition(e.round).tasks.find(t=>t.id===body.taskId);if(!task||!task.options.some(o=>o.id===body.optionId))throw new TeamError('Неизвестный бухгалтерский ответ');if(e.accounting.some(a=>a.round===e.round&&a.taskId===task.id))throw new TeamError('Первый ответ уже принят',409);const answer={correct:body.optionId===task.correct,explanation:task.explanation};e.accounting.push({round:e.round,taskId:task.id,optionId:body.optionId,...answer});return {answer};});}
 finish(body){return this.mutation(body,'finish',(e,p)=>{const previousForecastProfit=e.forecastProfit;settleRound(e);updateForecast(e);if(e.round===12)finalize(e);p.evaluations[e.round]={context:this.context(p,e,previousForecastProfit),result:null,pending:null};});}
 context(p,e,previousForecastProfit=e.forecastProfit,taskId){const round={...publicRound(e.round),hints:roundDefinition(e.round).hints};if(taskId){const t=roundDefinition(e.round).tasks.find(t=>t.id===taskId);if(!t)throw new TeamError('Неизвестная задача');round.hints=[`Обратите внимание на вопрос: ${t.prompt}`,round.documents[0].text,t.explanation];}return {company:{name:p.company,cash:e.cash,profit:e.profit,forecastProfit:e.forecastProfit,debt:e.debt,payables:e.payables},round,decisions:e.decisions.map(({effect,...d})=>d),history:e.history.map(h=>({...h,decisions:h.decisions.map(({effect,...d})=>d)})),previousForecastProfit,...(taskId?{taskId}:{})};}
 rate(p,kind,now=Date.now()){p.rates??={};const times=(p.rates[kind]||[]).filter(t=>now-t<60000);if(times.length>=10)throw new TeamError('Слишком много запросов. Попробуйте через минуту.',429);p.rates[kind]=[...times,now];}
 prepareAssistant(body,now=Date.now()){const p=this.member(body);if(!p.economy)throw new TeamError('Нужна игра новой версии',409);const question=clean(body.question,1200,'вопрос');if(![1,2,3].includes(body.level))throw new TeamError('Выберите уровень подсказки 1, 2 или 3');const context=this.context(p,p.economy,p.economy.forecastProfit,body.taskId);this.rate(p,'assistant',now);return {context,question,level:body.level};}
 prepareEvaluation(body,now=Date.now()){const p=this.member(body);const slot=p.evaluations?.[body.round];if(!Number.isInteger(body.round)||!slot)throw new TeamError('Сначала завершите эту неделю',409);if(slot.result)return {evaluation:slot.result};if(slot.pending&&now-slot.pending.started<20000)return {evaluation:{status:'pending'}};this.rate(p,'evaluation',now);const token=crypto.randomUUID();slot.pending={token,started:now};return {token,context:structuredClone(slot.context)};}
 completeEvaluation(body,token,result){const p=this.member(body),slot=p.evaluations?.[body.round];if(!slot)throw new TeamError('Неделя не найдена',404);if(slot.result)return slot.result;if(!result){if(slot.pending?.token===token)slot.pending=null;return {status:'pending'};}const context=slot.context;if(!['llm','fallback'].includes(result.source)||!Number.isFinite(result.success)||result.success<0||result.success>100||typeof result.explanation!=='string'||!Array.isArray(result.risks)||!Array.isArray(result.hints))throw new TeamError('Некорректный анализ',502);slot.result={...result,forecastProfit:context.company.forecastProfit,forecastDelta:context.company.forecastProfit-context.previousForecastProfit};slot.pending=null;return slot.result;}
 testFinal(body,enabled){this.member(body);if(!enabled)throw new TeamError('Серверный тестовый режим выключен',403);if(this.game.economyVersion!==3)throw new TeamError('Нужна игра новой версии',409);const clone=new Competition(structuredClone(this.snapshot()));clone.close();return {...clone.state(body,true),preview:true};}
  progress(body){
    this.member(body);if(this.game.economyVersion===3)throw new TeamError("Финансы новой игры рассчитывает сервер",409);
    this.exists();if(this.game.status!=='active')throw new TeamError(this.game.status==='lobby'?'Преподаватель еще не открыл игру':'Игра завершена',409);
    const p=this.member(body);
    if(!Array.isArray(body.events)||body.events.length>300)throw new TeamError('Неверный список решений');
    const events=new Map(p.events.map(e=>[e.id,e]));
    for(const e of body.events){
      if(!e||typeof e.id!=='string'||!/^op:e[a-z0-9]+:\d{1,2}$|^business:(site|money|raw|equip)$/.test(e.id))throw new TeamError('Неверное решение');
      if(!Number.isFinite(e.delta)||typeof e.title!=='string'||e.title.length>300||typeof e.reason!=='string'||e.reason.length>200)throw new TeamError('Неверный результат');
      if(e.id.startsWith('op:')&&![25,-35].includes(e.delta))throw new TeamError('Неверная оценка операции');
      const allowed={site:[-80,-25,45],money:[-15,20,5],raw:[15,-60,35]};
      const kind=e.id.split(':')[1];
      if(allowed[kind]&&!allowed[kind].includes(e.delta))throw new TeamError('Неверный эффект решения');
      if(kind==='equip'&&(e.delta<-20||e.delta>30))throw new TeamError('Неверный эффект оборудования');
      // First confirmed decision wins; retries never award profit twice.
      if(!events.has(e.id))events.set(e.id,{id:e.id,delta:e.delta,title:e.title,reason:e.reason,day:e.day});
    }
    if(body.state&&JSON.stringify(body.state).length>350000)throw new TeamError('Слишком большое сохранение',413);
    p.events=[...events.values()];p.updated=Date.now();
    if(body.state){p.state=body.state;p.state.finance={events:p.events};p.state.member={id:p.id,secret:p.secret,teamId:p.teamId,league:body.league};}
    return {ok:true,profit:this.profit(p),decisions:p.events.length};
  }
 profit(p){return p.economy?p.economy.profit:Math.round((-250+p.events.reduce((sum,e)=>sum+e.delta,0))*10)/10;}
 view(audience){this.exists();const game={code:this.game.code,title:this.game.title,status:this.game.status,economyVersion:this.game.economyVersion||1,round:this.game.round||1,totalRounds:12,...(this.game.status==='closed'?{earlyClose:!!this.game.earlyClose}:{})};const teams=this.teams.map(t=>{const members=this.people.filter(p=>p.teamId===t.id&&!p.practice).map(p=>({id:p.id,name:p.name,company:p.company,profit:this.profit(p),forecastProfit:p.economy?.forecastProfit??this.profit(p),cash:p.economy?.cash??null,decisions:p.economy?p.economy.history.reduce((s,h)=>s+h.decisions.length,0)+(p.economy.submitted?0:p.economy.decisionCount):p.events.length,round:p.economy?.round??null,submitted:p.economy?.submitted??false,updated:p.updated||null}));return {...t,memberCount:members.length,profit:members.length?Math.round(members.reduce((s,p)=>s+p.profit,0)/members.length):0,forecastProfit:members.length?Math.round(members.reduce((s,p)=>s+p.forecastProfit,0)/members.length):0,decisions:members.reduce((s,p)=>s+p.decisions,0),...((audience==='teacher'||audience===t.id)?{members}:{})};});const metric=this.game.status==='closed'?'profit':'forecastProfit';teams.sort((a,b)=>b[metric]-a[metric]||a.name.localeCompare(b.name,'ru'));let rank=0,prior;for(let i=0;i<teams.length;i++){if(teams[i][metric]!==prior)rank=i+1;teams[i].rank=rank;prior=teams[i][metric];}return {game,teams};}
}
