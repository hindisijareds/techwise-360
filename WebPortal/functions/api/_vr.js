import { ApiError, getProfileById, json, requireProfile, selectRows } from './_utils.js';
export const SCORING = Object.freeze({ version:'legacy-mistakes-time-v1',minorPenalty:4,standardPenalty:8,criticalPenalty:16,maximumTimePenalty:20,overtimeIntervalSeconds:30,penaltyPerInterval:2,assemblyTargetSeconds:600,disassemblyTargetSeconds:420 });
const COUNTS={CPU:1,RAM:4,M2:1,CPUCooler:1,Motherboard:1,GPU:1,Storage:1,PSU:1};
const CATEGORIES={wrong_part:'Incorrect Component',invalid_target:'Invalid Target',incorrect_orientation:'Incorrect Orientation',wrong_order:'Incorrect Sequence',missing_component:'Missing Component',missing_connection:'Missing Connection',missing_dependency:'Critical Assembly Error',missing_fastening:'Incomplete Fastening',incomplete_removal:'Incomplete Disassembly',invalid_placement:'Invalid Placement'};
export function vrJson(data,status=200) { const r=json(data,status); r.headers.set('Cache-Control','no-store'); return r; }
export function vrFail(e) { console.error('VR Error:', e?.message || e); return vrJson({error: e?.message || 'VR service unavailable. Please retry later.'}, e?.status || 500); }
export async function limitedJson(request) {
  const reader=request.body?.getReader(), decoder=new TextDecoder(); let raw='',bytes=0;
  if(reader) {
    try {
      while(true) {
        const {done,value}=await reader.read(); if(done) break;
        bytes+=value.byteLength;
        if(bytes>131072) { await reader.cancel(); throw new ApiError('Result is too large.',413); }
        raw+=decoder.decode(value,{stream:true});
      }
      raw+=decoder.decode();
    } finally { reader.releaseLock(); }
  }
  try { const b=JSON.parse(raw); if(!b||Array.isArray(b)||typeof b!=='object') throw new Error(); return b; } catch { throw new ApiError('Invalid JSON body.',400); }
}
export function uuid(value) {
  let s=String(value||'').toLowerCase(); if(/^[a-f0-9]{32}$/.test(s)) s=s.replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/,'$1-$2-$3-$4-$5');
  if(!/^[a-f0-9]{8}(-[a-f0-9]{4}){3}-[a-f0-9]{12}$/.test(s)) throw new ApiError('Invalid identifier.',400); return s;
}
export function randomToken() { return Array.from(crypto.getRandomValues(new Uint8Array(32)),b=>b.toString(16).padStart(2,'0')).join(''); }
export async function hashToken(s) { return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(s))),b=>b.toString(16).padStart(2,'0')).join(''); }
export async function rpc(env,name,body) {
  const r=await fetch(`${env.SUPABASE_URL}/rest/v1/rpc/${name}`,{method:'POST',headers:{apikey:env.SUPABASE_SERVICE_ROLE_KEY,Authorization:`Bearer ${env.SUPABASE_SERVICE_ROLE_KEY}`,'Content-Type':'application/json'},body:JSON.stringify(body)});
  const data=await r.json().catch(()=>null);
  if(!r.ok) {
    const messages={VR_STUDENT_REQUIRED:['Approved student access is required.',403],VR_RATE_LIMIT:['Too many connection codes. Wait five minutes.',429],VR_CODE_EXPIRED:['Connection code expired or already used.',401],VR_ATTEMPT_CONFLICT:['Attempt belongs to a different context.',409],VR_CONTEXT_REQUIRED:['An active term and matching section enrollment are required. Contact your teacher.',409],VR_COMPETITION_UNAVAILABLE:['Assessment is not available for this student or time.',403],VR_ATTEMPT_LIMIT:['Assessment attempt limit reached.',409],VR_ATTEMPT_NOT_FOUND:['Assessment attempt was not found for this student.',404],VR_INVALID_RESULT:['Invalid result.',400]};
    const m=messages[data?.message]; throw new ApiError(m?.[0]||'VR database operation failed.',m?.[1]||503);
  } return data;
}
export async function requireVrStudent(request,env) {
  const token=request.headers.get('Authorization')?.replace(/^Bearer\s+/i,'')||''; let profile;
  if(token.startsWith('twvr_')) {
    if(!/^twvr_[a-f0-9]{64}$/.test(token)) throw new ApiError('Invalid VR session.',401);
    const rows=await selectRows(env,'vr_device_sessions',`select=student_id&token_hash=eq.${await hashToken(token)}&expires_at=gt.${encodeURIComponent(new Date().toISOString())}`);
    if(!rows[0]) throw new ApiError('VR session expired. Reconnect from the website.',401);
    profile=await getProfileById(env,rows[0].student_id);
  } else ({profile}=await requireProfile(request,env));
  if(!profile||profile.role!=='student'||profile.status!=='approved') throw new ApiError('Approved student access is required.',403); return profile;
}
export async function studentContext(env,profile) {
  const q=(await selectRows(env,'quarters','select=*&is_active=eq.true&status=eq.active'))[0];
  const s=profile.section_id?(await selectRows(env,'class_sections',`select=*&id=eq.${uuid(profile.section_id)}`))[0]:null;
  const enrollment=q&&s?(await selectRows(env,'student_enrollments',`select=*&student_id=eq.${uuid(profile.id)}&quarter_id=eq.${q.id}&section_id=eq.${s.id}`))[0]:null;
  const competitions=await selectRows(env,'vr_competitions','select=*&status=eq.active'); const now=Date.now();
  return {profile:{id:profile.id,full_name:profile.full_name,grade_level:profile.grade_level,section:s?.name||profile.section,section_id:s?.id||'',role:profile.role,status:profile.status,academic_year_id:q?.academic_year_id||'',academic_year:q?.school_year||'',quarter_id:q?.id||'',term:q?.title||'',enrollment_id:enrollment?.id||''},competitions:competitions.filter(c=>(!c.grade_level||c.grade_level===profile.grade_level)&&(!c.section||c.section===s?.name)&&(!c.quarter_id||c.quarter_id===q?.id)&&(!c.start_at||Date.parse(c.start_at)<=now)&&(!c.end_at||Date.parse(c.end_at)>=now))};
}
function text(s,max=600) { if(typeof s!=='string'||s.length>max) throw new ApiError('Invalid mistake text.',400); return s; }
export function validateResult(body,session,now=Date.now()) {
  const m=body.metadata||{};
  if(body.simulation_type!==session.simulation_type||(body.competition_id||'')!==(session.competition_id||'')) throw new ApiError('Assessment context does not match.',409);
  const duration=body.duration_seconds,completed=Date.parse(body.completed_at),started=Date.parse(session.started_at);
  if(!Number.isInteger(duration)||duration<0||duration>86400||!Number.isFinite(completed)) throw new ApiError('Invalid assessment timing.',400);

  // If schema_version 2 with detailed component tracking
  if (m.schema_version === 2 && Array.isArray(m.component_results) && m.component_results.length === 11) {
    const elapsed=m.elapsed_seconds;
    const seen=new Set(),done={};
    const components=m.component_results.map(c=>{
      if(!c||!Object.hasOwn(COUNTS,c.step)||!new RegExp(`^${c.step}:[1-${COUNTS[c.step]}]$`).test(c.component_id)||seen.has(c.component_id)||typeof c.complete!=='boolean') throw new ApiError('Invalid component snapshot.',400);
      seen.add(c.component_id); if(c.complete) done[c.step]=(done[c.step]||0)+1;
      if(!c.complete&&(!Object.hasOwn(CATEGORIES,c.issue)||(session.simulation_type==='disassembly'&&c.issue!=='incomplete_removal'))) throw new ApiError('Invalid incomplete component.',400);
      return {component_id:c.component_id,step:c.step,component:text(c.component,80),complete:c.complete,issue:c.complete?'':c.issue,explanation:text(c.explanation||''),correction:text(c.correction||'')};
    });
    const settings=session.scoring_configuration;
    const details=(m.mistake_details||[]).map((d,i)=>{
      if(!d||!Object.hasOwn(CATEGORIES,d.kind)||!Number.isFinite(d.elapsed_seconds)||d.elapsed_seconds<0||d.elapsed_seconds>elapsed+1) throw new ApiError('Invalid mistake detail.',400);
      const severity=d.kind==='missing_dependency'?'critical':d.kind==='incorrect_orientation'?'minor':'standard',cid=d.component_id||'';
      if(cid&&!seen.has(cid)) throw new ApiError('Unknown mistake component.',400);
      return {kind:d.kind,category:CATEGORIES[d.kind],severity,score_penalty:severity==='critical'?settings.criticalPenalty:severity==='minor'?settings.minorPenalty:settings.standardPenalty,order:i+1,elapsed_seconds:d.elapsed_seconds,occurred_at_seconds:Math.floor(d.elapsed_seconds),component_id:cid,attempted_step:text(d.attempted_step||'',80),expected_step:text(d.expected_step||'',80),explanation:text(d.explanation),correction:text(d.correction||'')};
    });
    for(const c of components) if(!c.complete&&!details.some(d=>d.component_id===c.component_id&&d.kind===c.issue)) throw new ApiError('Missing final mistake explanation.',400);
    const completion=Math.round(100*Object.keys(COUNTS).filter(k=>done[k]===COUNTS[k]).length/8),penalty=details.reduce((n,d)=>n+d.score_penalty,0);
    const target=session.simulation_type==='disassembly'?settings.disassemblyTargetSeconds:settings.assemblyTargetSeconds;
    const timePenalty=Math.min(settings.maximumTimePenalty,Math.floor(Math.max(0,duration-target)/settings.overtimeIntervalSeconds)*settings.penaltyPerInterval);
    const score=Math.min(completion,Math.max(0,100-penalty-timePenalty)),accuracy=Math.min(completion,Math.max(0,100-penalty));
    if (Number.isFinite(body.score_percent) && Math.abs(Number(body.score_percent) - score) > 0.01) throw new ApiError('Tampered score rejected before storage.', 409);
    return {score_percent:score,accuracy_percent:accuracy,duration_seconds:duration,mistakes:details.length,completed_at:new Date(completed).toISOString(),metadata:{schema_version:2,local_attempt_id:session.id.replaceAll('-',''),mode:'competition',elapsed_seconds:elapsed,scoring_configuration:settings,component_results:components,mistake_details:details,scoring_breakdown:{final_score:score,accuracy_percent:accuracy,completion_percent:completion,mistake_penalty:penalty,time_penalty:timePenalty},game_version:text(m.game_version||'',40),control_mode:text(m.control_mode||'',30),server_validated:true}};
  }

  // Standard VR Headset Attempt
  const mistakes = Number.isInteger(body.mistakes) && body.mistakes >= 0 ? body.mistakes : 0;
  let score = Number.isFinite(body.score_percent) ? Math.max(0, Math.min(100, Math.round(body.score_percent))) : 0;

  if (Array.isArray(m.expected_order) && m.expected_order.length > 0) {
    const totalSteps = m.expected_order.length;
    const completedSteps = Array.isArray(m.completed_order) ? m.completed_order.length : 0;
    const completionRatio = Math.min(1, Math.max(0, completedSteps / totalSteps));
    if (completionRatio < 1) {
      score = Math.round(score * completionRatio);
    }
  }

  const accuracy = Math.max(0, Math.min(100, 100 - (mistakes * 5)));
  return {
    score_percent: score,
    accuracy_percent: accuracy,
    duration_seconds: duration,
    mistakes: mistakes,
    completed_at: new Date(completed).toISOString(),
    metadata: {
      ...m,
      schema_version: m.schema_version || 1,
      local_attempt_id: session.id.replaceAll('-', ''),
      mode: m.mode || 'competition',
      game_version: text(m.game_version || '', 40),
      control_mode: text(m.control_mode || '', 30),
      server_validated: true
    }
  };
}
