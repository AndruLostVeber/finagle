import {existsSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {createProvider} from '../src/llm.mjs';
const envPath=fileURLToPath(new URL('../.env',import.meta.url));
if(existsSync(envPath))process.loadEnvFile(envPath);
if(!process.env.GIGACHAT_AUTH_KEY){console.error('GIGACHAT_AUTH_KEY is not configured');process.exit(1);}
try{
  const response=await fetch('https://ngw.devices.sberbank.ru:9443/api/v2/oauth',{method:'POST',headers:{Authorization:'Basic '+process.env.GIGACHAT_AUTH_KEY,RqUID:crypto.randomUUID(),'Content-Type':'application/x-www-form-urlencoded'},body:new URLSearchParams({scope:process.env.GIGACHAT_SCOPE||'GIGACHAT_API_PERS'}),signal:AbortSignal.timeout(15000),redirect:'error'});
  if(!response.ok)throw Error('OAuth HTTP '+response.status);
  const token=await response.json();
  const models=await fetch('https://api.giga.chat/v1/models',{headers:{Authorization:'Bearer '+token.access_token},signal:AbortSignal.timeout(15000),redirect:'error'});
  if(!models.ok)throw Error('Models HTTP '+models.status);
  const names=(await models.json()).data?.map(m=>m.id)||[];
  console.log('Available models:',names.join(', '));
  if(!names.includes('GigaChat-3-Ultra'))throw Error('Requested model unavailable for this account');
  const provider=createProvider();
  const result=await provider.answerAssistant({company:{name:'Тестовая компания',cash:2500000,profit:0,forecastProfit:0,debt:0,payables:0},round:{number:1,title:'Начало квартала',documents:[{id:'capital',title:'Уставный капитал',text:'Владелец внес 2500000 рублей на счет компании. Вклад не является выручкой.'}],hints:[],actions:[],tasks:[]},decisions:[],history:[],previousForecastProfit:0},'Почему вклад владельца не считается прибылью компании?',2,process.env);
  console.log('Strict assistant schema:',result.source==='llm'?'PASS':'FAILED (fallback)');
  if(result.source!=='llm')process.exitCode=1;
}catch(e){console.error('GigaChat check failed:',e.cause?.code||e.code||e.message);process.exitCode=1;}
