import test from 'node:test';
import assert from 'node:assert/strict';
import {Competition,leagueName} from '../src/teams.mjs';
const join=(c,teamName='Баланс',other={})=>{
 if(!c.game)c.create({title:'Тест',requestId:'test'},'G12345678');
 if(!c.teams.some(t=>t.name===teamName))c.manage({secret:c.game.secret,action:'team',name:teamName});
 const result=c.join({teamId:c.teams.find(t=>t.name===teamName).id,name:'Анна',company:'Химия',...other},'G12345678');
 c.manage({secret:c.game.secret,action:'start'});return result;
};
const event=(id='op:e1:0',delta=25)=>({id,delta,title:'Операция',reason:'Результат',day:0});
test('separate companies, average ranking and private credentials',()=>{
 const c=new Competition(),a=join(c);join(c);join(c,'Рост');
 c.progress({...a.member,events:[event()],state:{screen:'office'}});
 assert.equal(c.people.length,3);assert.equal(c.teams.length,2);
 const view=c.view();assert.equal(view.teams[0].name,'Баланс');assert.equal(view.teams[0].profit,-237.5);
 assert.equal(view.teams[0].members[1].profit,-250);assert.ok(!JSON.stringify(view).includes(a.member.secret));
 assert.equal(c.people[0].state.screen,'office');assert.equal(c.people[1].state,null);
});
test('authenticated updates, idempotency, first decision wins and invalid profit',()=>{
 const c=new Competition(),a=join(c);
 assert.throws(()=>c.progress({...a.member,secret:'bad',events:[]}),/доступ/);
 c.progress({...a.member,events:[event()]});c.progress({...a.member,events:[event(),event()]});
 assert.equal(c.people[0].events.length,1);assert.equal(c.profit(c.people[0]),-225);
 c.progress({...a.member,events:[event('op:e1:0',-35)]});assert.equal(c.profit(c.people[0]),-225);
 assert.throws(()=>c.progress({...a.member,events:[event('op:e1:1',999)]}));
 assert.equal(c.people[0].events.length,1);
});
test('restoration, practice exclusion and retried joins',()=>{
 const c=new Competition(),a=join(c,'Рост',{requestId:'join-once'});join(c,'Рост',{requestId:'join-once'});
 assert.equal(c.people.length,1);
 assert.throws(()=>c.join({teamName:'Новая',name:'A',company:'A'},'G12345678'));assert.equal(c.view().teams.length,1);
 const restored=new Competition(JSON.parse(JSON.stringify(c.snapshot())));
 assert.equal(restored.member(a.member).name,'Анна');
 assert.throws(()=>leagueName('../a'));assert.equal(leagueName('Группа_1'),'Группа_1');
});

test('only teacher creates games and teams; lobby and closed games reject decisions',()=>{
 const c=new Competition();assert.throws(()=>c.join({name:'A',company:'A',teamName:'B'},'missing'),/не найдена/);
 assert.throws(()=>c.view(),/не найдена/);
 const owner=c.create({title:'Урок',requestId:'owner'},'G12345678');
 assert.throws(()=>c.manage({action:'team',name:'Баланс',secret:'pupil'}),/доступ/);
 c.manage({...owner,action:'team',name:'Баланс'});
 const p=c.join({teamId:c.teams[0].id,name:'A',company:'A',practice:true},owner.code);
 assert.equal(c.people[0].practice,false);
 assert.throws(()=>c.progress({...p.member,events:[event()]}),/не открыл/);
 c.manage({...owner,action:'start'});c.progress({...p.member,events:[event()]});
 c.manage({...owner,action:'close'});
 assert.throws(()=>c.progress({...p.member,events:[event('op:e1:1')]}),/завершена/);
 assert.throws(()=>c.join({teamId:c.teams[0].id,name:'B',company:'B'},owner.code),/завершен/);
 const view=JSON.stringify(c.view());assert.ok(!view.includes(owner.secret));assert.ok(!view.includes('owner'));
 const restored=new Competition(JSON.parse(JSON.stringify(c.snapshot())));assert.equal(restored.manage({...owner,action:'view'}).game.status,'closed');
});
