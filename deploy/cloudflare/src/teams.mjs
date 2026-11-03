// Each competition is an independent Durable Object. Companies never share game state.
export class TeamError extends Error {
  constructor(message,status=400){super(message);this.status=status;}
}
const clean=(value,max,label)=>{
  if(typeof value!=='string'||!value.trim()||value.trim().length>max)throw new TeamError('Укажите '+label+' (до '+max+' символов)');
  return value.trim();
};
export function leagueName(value='main'){
  const name=clean(value,32,'соревнование');
  if(!/^[a-zA-Z0-9а-яА-ЯёЁ_-]+$/.test(name))throw new TeamError('Неверное название соревнования');
  return name;
}
export class Competition {
  constructor(saved){this.game=saved?.game||null;this.teams=saved?.teams||[];this.people=saved?.people||[];}
  snapshot(){return {game:this.game,teams:this.teams,people:this.people};}
  create(body,code){
    if(this.game){if(this.game.requestId!==body.requestId)throw new TeamError('Код занят. Повторите создание.',409);}
    else this.game={code,title:clean(body.title,80,'название игры'),status:'lobby',secret:crypto.randomUUID(),requestId:body.requestId,created:Date.now()};
    return {code:this.game.code,secret:this.game.secret};
  }
  exists(){if(!this.game)throw new TeamError('Игра с таким кодом не найдена. Получите код у преподавателя.',404);}
  teacher(body){this.exists();if(body.secret!==this.game.secret)throw new TeamError('Нет доступа к панели преподавателя',403);}
  manage(body){
    this.teacher(body);
    if(body.action==='team'){
      const name=clean(body.name,40,'название команды');
      if(this.teams.some(t=>t.name.toLocaleLowerCase()===name.toLocaleLowerCase()))throw new TeamError('Команда уже существует',409);
      if(this.teams.length>=100)throw new TeamError('Не более 100 команд',409);
      this.teams.push({id:crypto.randomUUID(),name});
    }else if(body.action==='start'){
      if(!this.teams.length)throw new TeamError('Сначала добавьте команды');
      if(this.game.status==='closed')throw new TeamError('Игра уже завершена',409);
      this.game.status='active';
    }else if(body.action==='close')this.game.status='closed';
    else if(body.action!=='view')throw new TeamError('Неизвестное действие');
    return this.view();
  }
  join(body,league){
    this.exists();if(this.game.status==='closed')throw new TeamError('Прием участников завершен',409);
    const name=clean(body.name,40,'имя'),company=clean(body.company,48,'компанию');
    const practice=false;
    const team=this.teams.find(t=>t.id===body.teamId);
    if(!team)throw new TeamError('Выберите команду, созданную преподавателем',404);
    if(this.people.length>=500)throw new TeamError('Соревнование заполнено',409);
    // Request identity makes an interrupted join safe to retry.
    let person=body.requestId&&this.people.find(p=>p.requestId===body.requestId);
    if(!person){person={id:crypto.randomUUID(),secret:crypto.randomUUID(),teamId:team.id,name,company,practice,events:[],state:null,requestId:body.requestId};this.people.push(person);}
    return {member:{id:person.id,secret:person.secret,teamId:person.teamId,league},team:this.teams.find(t=>t.id===person.teamId)};
  }
  member(body){const p=this.people.find(p=>p.id===body.id&&p.secret===body.secret);if(!p)throw new TeamError('Нет доступа к компании',403);return p;}
  progress(body){
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
  profit(p){return Math.round((-250+p.events.reduce((sum,e)=>sum+e.delta,0))*10)/10;}
  view(){this.exists();return {game:{code:this.game.code,title:this.game.title,status:this.game.status},teams:this.teams.map(t=>{
    const members=this.people.filter(p=>p.teamId===t.id&&!p.practice).map(p=>({id:p.id,name:p.name,company:p.company,profit:this.profit(p),decisions:p.events.length,updated:p.updated||null}));
    return {...t,members,profit:members.length?Math.round(members.reduce((sum,p)=>sum+p.profit,0)/members.length*10)/10:-250,decisions:members.reduce((sum,p)=>sum+p.decisions,0)};
  }).sort((a,b)=>b.profit-a.profit||b.decisions-a.decisions||a.name.localeCompare(b.name,'ru'))};}
}
