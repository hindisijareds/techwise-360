export async function academicRpc(env,name,body) {
  const r=await fetch(`${env.SUPABASE_URL}/rest/v1/rpc/${name}`,{method:'POST',headers:{apikey:env.SUPABASE_SERVICE_ROLE_KEY,Authorization:`Bearer ${env.SUPABASE_SERVICE_ROLE_KEY}`,'Content-Type':'application/json'},body:JSON.stringify(body)});
  const data=await r.json().catch(()=>null);
  if(!r.ok) {
    const detail = data?.message || data?.details || data?.hint || '';
    const msg = data?.code === '23505'
      ? 'That academic year, term or enrollment already exists.'
      : data?.code === 'P0001'
        ? data.message
        : data?.code?.startsWith('22')
          ? 'Check the year, date and identifier values.'
          : detail ? `Unable to save academic context: ${detail}` : 'Unable to save academic context. Check the Batch 4 migration.';
    throw Object.assign(new Error(msg), { status: data?.code === 'P0001' || data?.code === '23505' ? 409 : 400 });
  }
  return data;
}
