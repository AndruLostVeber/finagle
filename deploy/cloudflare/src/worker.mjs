import {DurableObject} from 'cloudflare:workers';
import {Competition,TeamError,leagueName} from './teams.mjs';
const json=(data,status=200)=>Response.json(data,{status,headers:{'Cache-Control':'no-store'}});
export default {
  async fetch(request,env){
    const url=new URL(request.url);
    if(url.pathname==='/health')return json({ok:true,app:'kuro-production',mode:'teams'});
    if(url.pathname.startsWith('/api/teams')){
      try{
        let body=null;
        if(request.method==='POST'){
          if(request.headers.get('Origin')&&request.headers.get('Origin')!==url.origin)return json({error:'Недопустимый источник'},403);
          const raw=await request.text();if(raw.length>400000)return json({error:'Слишком большой запрос'},413);
          try{body=JSON.parse(raw);}catch{throw new TeamError('Неверный JSON');}
          if(!body||typeof body!=='object'||Array.isArray(body))throw new TeamError('Неверный запрос');
        }else if(request.method!=='GET')return json({error:'Метод не поддерживается'},405);
        let league;
        if(url.pathname==='/api/teams/create'&&request.method==='POST'){
          if(typeof body.requestId!=='string'||!/^[a-f0-9-]{36}$/.test(body.requestId))throw new TeamError('Неверный запрос создания');
          const hash=await crypto.subtle.digest('SHA-256',new TextEncoder().encode(body.requestId));
          league='G'+Array.from(new Uint8Array(hash)).slice(0,4).map(b=>b.toString(16).padStart(2,'0')).join('').toUpperCase();
        }else league=leagueName(body?.league||url.searchParams.get('league')||'').toUpperCase();
        // The old binding name remains for migration compatibility; no legacy endpoints are served.
        return env.CLASSROOMS.getByName('teams-v2:'+league).fetch(new Request('https://internal'+url.pathname+'?league='+encodeURIComponent(league),{method:request.method,headers:{'Content-Type':'application/json'},body:body?JSON.stringify(body):undefined}));
      }catch(e){return json({error:e instanceof TeamError?e.message:'Ошибка сервера'},e.status||500);}
    }
    if(/^\/(api|ws|s|p|phone|projector|join|qr.svg|export.csv)(\/|$)/.test(url.pathname))return new Response('Not found',{status:404});
    return env.ASSETS.fetch(request);
  }
};
// Preserve the deployed class name so existing migrations remain valid.
export class Classrooms extends DurableObject {
  async fetch(request){
    try{
      const url=new URL(request.url),body=request.method==='POST'?await request.json():null;
      return await this.ctx.blockConcurrencyWhile(async()=>{
        try{
        const competition=new Competition(await this.ctx.storage.get('competition'));
        let result;
        if(request.method==='POST'&&url.pathname==='/api/teams/create')result=competition.create(body,url.searchParams.get('league'));
        else if(request.method==='POST'&&url.pathname==='/api/teams/teacher')result=competition.manage(body);
        else if(request.method==='GET'&&url.pathname==='/api/teams')result=competition.view();
        else if(request.method==='POST'&&url.pathname==='/api/teams/join')result=competition.join(body,url.searchParams.get('league'));
        else if(request.method==='POST'&&url.pathname==='/api/teams/progress')result=competition.progress(body);
        else if(request.method==='POST'&&url.pathname==='/api/teams/resume')result={state:competition.member(body).state};
        else return json({error:'Not found'},404);
        if(request.method==='POST'&&url.pathname!=='/api/teams/resume'&&!(url.pathname==='/api/teams/teacher'&&body.action==='view'))await this.ctx.storage.put('competition',competition.snapshot());
        return json(result);
        }catch(e){return json({error:e instanceof TeamError?e.message:'Ошибка сервера'},e.status||500);}
      });
    }catch(e){return json({error:e instanceof TeamError?e.message:'Ошибка сервера'},e.status||500);}
  }
}
