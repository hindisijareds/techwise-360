import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { PGlite } from '@electric-sql/pglite';
import { randomUUID } from 'node:crypto';
import { onRequestPost as connect } from '../functions/api/vr/connect.js';
import { onRequestPost as exchange } from '../functions/api/vr/exchange.js';
import { onRequestGet as context } from '../functions/api/vr/context.js';
import { onRequestPost as start } from '../functions/api/vr/start.js';
import { onRequestPost as submit } from '../functions/api/student/vr-attempts.js';
import { onRequestGet as report } from '../functions/api/teacher/vr-results.js';
import { SCORING, validateResult, limitedJson } from '../functions/api/_vr.js';

// Actual PostgreSQL migration/RPCs, with a local Supabase Auth/PostgREST adapter.
// No production credentials, network calls, or live records are used.
const db = new PGlite();
const student = randomUUID(), other = randomUUID(), teacher = randomUUID(), term = randomUUID(), section = randomUUID(), competition = randomUUID();
await db.exec(`
create role anon; create role authenticated; create role service_role;
create table profiles(id uuid primary key,role text,status text,full_name text,grade_level text,section text,section_id uuid);
create table quarters(id uuid primary key,name text,title text,school_year text,is_active boolean,status text);
create table class_sections(id uuid primary key,name text,school_year text,grade_level text,status text);
create table vr_competitions(id uuid primary key,title text,simulation_type text,grade_level text,section text,quarter_id uuid,start_at timestamptz,end_at timestamptz,attempts_allowed int,status text);
create table vr_simulation_attempts(id uuid primary key default gen_random_uuid(),student_id uuid references profiles(id),competition_id uuid references vr_competitions(id),simulation_type text,score_percent numeric(5,2),duration_seconds int,mistakes int,status text,metadata jsonb,started_at timestamptz,completed_at timestamptz,created_at timestamptz default now());
`);
await db.query(`insert into class_sections values($1,'A','2026-2027','Grade 9','active')`,[section]);
await db.query(`insert into quarters values($1,'T1','Term 1','2026-2027',true,'active')`,[term]);
for (const [id,role,name] of [[student,'student','Test Student'],[other,'student','Other Student'],[teacher,'teacher','Test Teacher']])
  await db.query(`insert into profiles values($1,$2,'approved',$3,'Grade 9','A',$4)`,[id,role,name,section]);
await db.query(`insert into vr_competitions values($1,'PC Test','both','Grade 9','A',$2,null,null,2,'active')`,[competition,term]);
const migration = await readFile(new URL('../supabase/migrations/20260906_vr_integration.sql',import.meta.url),'utf8');
await db.exec(migration);
await db.exec(migration); // repeatable additive migration
let checks = 0;
function check(value, message) { assert.ok(value,message); checks++; console.log('PASS',message); }
const env = { SUPABASE_URL:'https://local.invalid', SUPABASE_ANON_KEY:'test-anon', SUPABASE_SERVICE_ROLE_KEY:'test-service' };
const tokens = { student, other, teacher };
let failResult = false;
const originalFetch = globalThis.fetch;
globalThis.fetch = async (url, options = {}) => {
  const u = new URL(url), authorization = new Headers(options.headers).get('Authorization');
  if (u.pathname === '/auth/v1/user') {
    const id = tokens[authorization?.replace('Bearer ','')];
    return Response.json(id ? {id} : {message:'Expired authentication'}, {status:id?200:401});
  }
  assert.equal(authorization,'Bearer test-service');
  try {
    if (u.pathname.includes('/rpc/')) {
      const name = u.pathname.split('/').at(-1), args = JSON.parse(options.body);
      if (failResult && name === 'complete_vr_assessment') throw new Error('Simulated unavailable database');
      const keys = Object.keys(args);
      assert.match(name,/^[a-z_]+$/); keys.forEach(key => assert.match(key,/^[a-z_]+$/));
      const sql = `select to_jsonb(${name}(${keys.map((key,i)=>`${key} => $${i+1}`).join(',')})) as result`;
      const row = (await db.query(sql,Object.values(args))).rows[0];
      return Response.json(row.result);
    }
    const table = u.pathname.split('/').at(-1); assert.match(table,/^[a-z_]+$/);
    const args = [], where = []; let order = '', limit = '';
    const selected = u.searchParams.get('select') || '*'; assert.match(selected,/^[a-z_,*]+$/);
    for (const [key,value] of u.searchParams) {
      if (key === 'select') continue;
      if (key === 'order') { order = ' order by ' + value.split(',').map(v=>{ assert.match(v,/^[a-z_]+\.(asc|desc)$/); return v.replace('.',' '); }).join(','); continue; }
      if (key === 'limit' || key === 'offset') { assert.match(value,/^\d+$/); limit += ` ${key} ${value}`; continue; }
      assert.match(key,/^[a-z_]+$/);
      const dot = value.indexOf('.'), op = {eq:'=',gt:'>',gte:'>=',lte:'<='}[value.slice(0,dot)]; assert.ok(op);
      args.push(value.slice(dot+1)); where.push(`${key} ${op} $${args.length}`);
    }
    const rows = (await db.query(`select ${selected} from ${table}${where.length?' where '+where.join(' and '):''}${order}${limit}`,args)).rows;
    return Response.json(rows);
  } catch(error) { return Response.json({message:error.message},{status:400}); }
};
async function call(handler, token, body, query = '') {
  const request = new Request('https://portal.invalid/api/test'+query,{method:body===undefined?'GET':'POST',headers:token?{Authorization:`Bearer ${token}`}:{},...(body===undefined?{}:{body:JSON.stringify(body)})});
  const response = await handler({request,env}); return {status:response.status,data:await response.json(),headers:response.headers};
}
try {
  check((await call(connect,'teacher',{})).status===403,'Teacher cannot obtain a student VR credential');
  check((await call(connect,null,{})).status===401,'Missing authentication rejected');
  const issued = await call(connect,'student',{});
  check(issued.status===200 && issued.data.code.length===64 && issued.headers.get('Cache-Control')==='no-store','Approved website session issues a private short-lived code');
  const stored = (await db.query('select * from vr_launch_codes')).rows[0];
  check(stored.code_hash!==issued.data.code && stored.code_hash.length===64,'Database stores code hash, never the launch credential');
  const linked = await call(exchange,null,{code:issued.data.code});
  assert.equal(linked.status,200,JSON.stringify(linked.data));
  const token = linked.data.session.access_token;
  check(linked.data.profile.id===student && linked.data.profile.quarter_id===term && !!linked.data.profile.academic_year_id,'Website identity and academic context reach VR');
  check((await call(exchange,null,{code:issued.data.code})).status===401,'Connection code cannot be replayed');
  check((await call(context,token)).data.profile.id===student,'Scoped VR token retrieves the correct student');
  const expired = await call(connect,'other',{});
  await db.query(`update vr_launch_codes set expires_at=now()-interval '1 second' where student_id=$1`,[other]);
  check((await call(exchange,null,{code:expired.data.code})).status===401,'Expired launch code rejected');
  const attemptId = randomUUID(), request = {attempt_id:attemptId.replaceAll('-',''),simulation_type:'assembly',competition_id:competition};
  check((await call(start,token,{...request,simulation_type:null})).status===400,'Missing activity is rejected before database registration');
  const registered = await call(start,token,request);
  assert.equal(registered.status,200,JSON.stringify(registered.data));
  const session = registered.data.attempt;
  check(session.id===attemptId && session.student_id===student && !!session.enrollment_id,'START creates one formal assessment with server-owned enrollment');
  check((await call(start,token,request)).data.attempt.id===attemptId,'Repeated START uses the same attempt');
  check((await call(start,'other',request)).status===409,'Another student cannot claim the attempt ID');
  const components = Object.entries({CPU:1,RAM:4,M2:1,CPUCooler:1,Motherboard:1,GPU:1,Storage:1,PSU:1}).flatMap(([step,count])=>Array.from({length:count},(_,i)=>({component_id:`${step}:${i+1}`,component:step,step,complete:true,issue:'',explanation:'',correction:''})));
  const body = {student_id:other,simulation_type:'assembly',competition_id:competition,score_percent:100,duration_seconds:0,mistakes:0,completed_at:new Date().toISOString(),metadata:{schema_version:2,local_attempt_id:request.attempt_id,mode:'competition',elapsed_seconds:0,component_results:components,mistake_details:[],game_version:'test',control_mode:'Desktop'}};
  check((await call(submit,'other',body)).status===404,'A student cannot submit another student’s assessment');
  check((await call(submit,token,{...body,score_percent:99})).status===409,'Tampered score rejected before storage');
  failResult = true;
  check((await call(submit,token,body)).status===503,'Storage failure never returns a synced receipt');
  check((await db.query('select count(*)::int n from vr_simulation_attempts')).rows[0].n===0,'Failed submission creates no result');
  failResult = false;
  const firstRetries = await Promise.all(Array.from({length:4},()=>call(submit,token,body)));
  const saved = firstRetries[0];
  assert.equal(saved.status,200,JSON.stringify(saved.data));
  check(saved.data.status==='synced' && saved.data.attempt.student_id===student,'Retry returns a server receipt; spoofed body student ID is ignored');
  check(firstRetries.every(r=>r.data.attempt.id===saved.data.attempt.id),'Concurrent first submissions converge on one locked database result');
  const duplicates = await Promise.all(Array.from({length:4},()=>call(submit,token,body)));
  check(duplicates.every(r=>r.data.attempt.id===saved.data.attempt.id) && (await db.query('select count(*)::int n from vr_simulation_attempts')).rows[0].n===1,'Concurrent retries return the single original result');
  check((await call(submit,token,{...body,score_percent:0})).data.attempt.score_percent==='100.00','Completed result is immutable on retry');
  await db.query(`update profiles set full_name='Changed Name',grade_level='Grade 10' where id=$1`,[student]);
  const reportResult = await call(report,'teacher',undefined,`?academic_year_id=${session.academic_year_id}&quarter_id=${term}&student_id=${student}&competition_id=${competition}&section_id=${section}&simulation_type=assembly&grade_level=Grade+9&options=true`);
  assert.equal(reportResult.status,200,JSON.stringify(reportResult.data));
  check(reportResult.data.results.length===1 && reportResult.data.results[0].student_name==='Test Student','Existing teacher report filters use historical enrollment and name snapshots');
  check((await call(report,'student')).status===403,'Student cannot read teacher records');
  check((await call(report,'teacher',undefined,'?student_id=invalid')).status===400,'Malformed report identifiers rejected');
  check((await call(report,'teacher',undefined,'?simulation_type=disassembly')).data.results.length===0,'Activity filter excludes unmatched results');
  await db.query(`update profiles set grade_level='Grade 9' where id=$1`,[student]);
  check((await call(start,token,{...request,attempt_id:randomUUID(),simulation_type:'disassembly'})).status===200,'Disassembly registration shares the same secure flow');
  check((await call(start,token,{...request,attempt_id:randomUUID()})).status===409,'Attempt limit counts registered starts');
  await db.query(`update vr_device_sessions set expires_at=now()-interval '1 second'`);
  check((await call(context,token)).status===401,'Expired device session rejected');
  const incomplete = structuredClone(body); incomplete.metadata.component_results[0].complete=false; incomplete.metadata.component_results[0].issue='missing_component';
  assert.throws(()=>validateResult(incomplete,{...session,scoring_configuration:SCORING}),/Missing final mistake/); checks++;
  incomplete.metadata.mistake_details=[{kind:'missing_component',component_id:'CPU:1',elapsed_seconds:0,explanation:'CPU is missing',correction:'Install CPU'}]; incomplete.mistakes=1; incomplete.score_percent=88;
  const validated = validateResult(incomplete,{...session,scoring_configuration:SCORING});
  check(validated.score_percent===88 && validated.metadata.mistake_details[0].score_penalty===8,'Server derives completion cap and structured mistake penalties');
  const privilege = (await db.query(`select has_function_privilege('authenticated','public.complete_vr_assessment(uuid,uuid,jsonb)','execute') allowed`)).rows[0];
  check(!privilege.allowed,'Client database role cannot execute result-writing RPC');
  for (const activity of ['assembly','disassembly']) {
    const fixture = JSON.parse(await readFile(new URL(`fixtures/vr/${activity}-result.json`,import.meta.url),'utf8'));
    const validatedFixture = validateResult(fixture,{id:fixture.metadata.local_attempt_id,simulation_type:activity,competition_id:null,started_at:fixture.started_at,scoring_configuration:SCORING},Date.parse(fixture.completed_at));
    check(validatedFixture.score_percent===fixture.score_percent,`Actual Batch 2 Unity ${activity} payload passes the server contract`);
  }
  await assert.rejects(limitedJson(new Request('https://local.invalid',{method:'POST',body:JSON.stringify({data:'x'.repeat(131072)})})),error=>error.status===413); checks++;
  const legacy = randomUUID();
  await db.query(`insert into vr_simulation_attempts(id,student_id,competition_id,simulation_type,status,score_percent) values($1,$2,$3,'assembly','completed',75)`,[legacy,other,competition]);
  await db.exec(migration);
  check((await db.query('select score_percent,assessment_session_id from vr_simulation_attempts where id=$1',[legacy])).rows[0].assessment_session_id===null,'Repeated migration preserves legacy results without inventing attempt context');
  check((await call(start,'other',{...request,attempt_id:randomUUID()})).status===200,'A legacy result permits only the remaining assigned attempt');
  check((await call(start,'other',{...request,attempt_id:randomUUID()})).status===409,'Legacy results count toward competition attempt limits');
  console.log(`PASS: ${checks} Batch 3 database/API assertions`);
} finally { globalThis.fetch = originalFetch; await db.close(); }
