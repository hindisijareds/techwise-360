// Uses the portal's existing Supabase session; never puts credentials in a URL.
const SESSION_KEY = 'techwise360.session';
export async function request(path, body, retry = true) {
  const storedSession = localStorage.getItem(SESSION_KEY);
  const session = JSON.parse(storedSession || 'null');
  if (!session?.access_token) throw new Error('Please sign in to TechWise360 first.');
  const response = await fetch(path, { method: body ? 'POST' : 'GET', cache: 'no-store', headers: { Authorization: `Bearer ${session.access_token}`, 'Content-Type': 'application/json' }, ...(body ? { body: JSON.stringify(body) } : {}) });
  if (response.status === 401 && retry && session.refresh_token) {
    const refresh = await fetch('/api/auth/refresh', { method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify({refresh_token:session.refresh_token}) });
    if (refresh.ok) {
      const updated = await refresh.json();
      // Do not resurrect a session after logout or overwrite a different student's login.
      if (localStorage.getItem(SESSION_KEY) !== storedSession) throw new Error('Your account changed. Reload this page.');
      localStorage.setItem(SESSION_KEY, JSON.stringify({...updated.session, profile:updated.profile}));
      return request(path, body, false);
    }
  }
  const data = await response.json().catch(() => ({}));
  if (localStorage.getItem(SESSION_KEY) !== storedSession) throw new Error('Your account changed. Reload this page.');
  if (!response.ok) throw new Error(data.error || 'Unable to load VR records. Please retry.');
  return data;
}
const generate = document.getElementById('generateVrCode');
if (generate) {
  const input = document.getElementById('vrConnectionCode'), status = document.getElementById('vrConnectStatus'), copy = document.getElementById('copyVrCode');
  let expires = 0;
  generate.onclick = async () => {
    generate.disabled = true; input.value = ''; copy.disabled = true; expires = 0;
    try { const data = await request('/api/vr/connect', {}); input.value = data.code; expires = Date.now() + data.expires_in * 1000; copy.disabled = false; status.textContent = 'Code ready. Paste it into the simulation.'; }
    catch (error) { status.textContent = error.message; }
    finally { generate.disabled = false; }
  };
  copy.onclick = async () => { try { await navigator.clipboard.writeText(input.value); status.textContent = 'Code copied.'; } catch { input.select(); status.textContent = 'Select the code and copy it manually.'; } };
  setInterval(() => { if (expires && Date.now() >= expires) { input.value = ''; copy.disabled = true; expires = 0; status.textContent = 'Code expired. Generate a new one.'; } }, 1000);
  window.addEventListener('pagehide', () => { input.value = ''; });
}

const record = document.getElementById('teacherVrClassRecord');
if (record) {
  const filters = document.createElement('div'); filters.className = 'reports-toolbar';
  const selects = {};
  for (const [key, title] of [['academic_year_id','Academic year'],['quarter_id','Term'],['section_id','Section enrollment'],['student_id','Student'],['competition_id','Assessment'],['simulation_type','VR activity']]) {
    const label = document.createElement('label'); label.textContent = title;
    const select = document.createElement('select'); select.add(new Option('All', '')); label.append(select); filters.append(label); selects[key] = select;
    select.onchange = () => load();
  }
  selects.simulation_type.add(new Option('PC Assembly','assembly')); selects.simulation_type.add(new Option('PC Disassembly','disassembly'));
  const refresh = document.createElement('button'); refresh.textContent = 'Refresh VR results'; refresh.onclick = () => load();
  const download = document.createElement('button'); download.textContent = 'Download VR results CSV';
  refresh.className = download.className = 'small-button';
  const status = document.createElement('p'); status.setAttribute('role','status');
  const table = document.createElement('table'); table.className = 'data-table';
  const scroller = document.createElement('div'); scroller.style.overflowX = 'auto'; scroller.append(table);
  const next = document.createElement('button'); next.textContent = 'Next results'; next.hidden = true;
  next.className = 'small-button';
  record.append(filters, refresh, download, status, scroller, next);
  let generation = 0, nextOffset = null, loadedOptions = false;
  const columns = ['student_name','simulation_type','score_percent','accuracy_percent','duration_seconds','mistakes','completed_at','academic_year','term','grade_level','section_name','assessment','assessment_session_id','student_id'];
  const labels = ['Student','Activity','Score %','Accuracy %','Seconds','Mistakes','Completed','Academic year','Term','Grade','Section','Assessment','Attempt ID','Student ID'];
  let lookup = {};
  function params() {
    const p = new URLSearchParams();
    for (const [key, select] of Object.entries(selects)) if (select.value) p.set(key, select.value);
    for (const [id,key] of [['reportGradeFilter','grade_level'],['reportSectionFilter','section_name'],['reportStartDate','from'],['reportEndDate','to']]) {
      const value = document.getElementById(id)?.value; if (value) p.set(key,value);
    }
    return p;
  }
  function decorate(row) { return {...row, student_name:row.student_name || lookup.students?.[row.student_id] || row.student_id, academic_year:lookup.years?.[row.academic_year_id] || 'Legacy / unassigned', term:lookup.terms?.[row.quarter_id] || 'Legacy / unassigned', assessment:lookup.assessments?.[row.competition_id] || 'Unassigned'}; }
  async function load(offset = 0) {
    const current = ++generation; status.textContent = 'Loading VR results…';
    const p = params(); p.set('offset',offset); if (!loadedOptions) p.set('options','true');
    try {
      const data = await request('/api/teacher/vr-results?' + p);
      if (current !== generation) return;
      if (data.options) {
        for (const [key, source] of [['academic_year_id','years'],['quarter_id','terms'],['section_id','sections'],['student_id','students'],['competition_id','assessments']]) {
          lookup[source] = {};
          for (const item of data.options[source]) {
            const name = item.full_name || item.title || item.name;
            lookup[source][item.id] = name;
            selects[key].add(new Option([item.school_year,item.grade_level,name].filter(Boolean).join(' · '), item.id));
          }
        }
        loadedOptions = true;
      }
      table.replaceChildren();
      const head = table.createTHead().insertRow();
      for (const title of labels.slice(0,12)) { const cell = document.createElement('th'); cell.textContent = title; head.append(cell); }
      const tbody = table.createTBody();
      for (const raw of data.results) {
        const row = decorate(raw), tr = tbody.insertRow();
        for (const key of columns.slice(0,12)) tr.insertCell().textContent = String(row[key] ?? '—');
        const details = document.createElement('details'), summary = document.createElement('summary'); summary.textContent = 'Mistake details'; details.append(summary);
        for (const mistake of raw.metadata?.mistake_details || []) { const p = document.createElement('p'); p.textContent = `${mistake.category}: ${mistake.explanation} ${mistake.correction || ''}`; details.append(p); }
        tr.cells[5].append(details);
      }
      nextOffset = data.next_offset; next.hidden = nextOffset == null;
      status.textContent = data.results.length ? `Showing ${offset + 1}–${offset + data.results.length}. Confirmed server results.` : 'No VR results match these filters.';
    } catch (error) { if (current === generation) { table.replaceChildren(); next.hidden = true; status.textContent = error.message; } }
  }
  next.onclick = () => load(nextOffset);
  download.onclick = async () => {
    download.disabled = true;
    try {
      const p = params(), rows = []; let offset = 0;
      do { p.set('offset',offset); const data = await request('/api/teacher/vr-results?' + p); rows.push(...data.results.map(decorate)); offset = data.next_offset; } while (offset != null);
      const cell = value => '"' + String(value ?? '').replace(/^\s*[=+@-]/,"'$&").replaceAll('"','""') + '"';
      const csv = [labels.map(cell).join(','), ...rows.map(row => columns.map(key => cell(row[key])).join(','))].join('\r\n');
      const url = URL.createObjectURL(new Blob(['\uFEFF' + csv], {type:'text/csv;charset=utf-8'}));
      const a = document.createElement('a'); a.href = url; a.download = 'techwise-vr-class-record.csv'; a.click(); setTimeout(() => URL.revokeObjectURL(url),1000);
      status.textContent = `Exported ${rows.length} VR results using the selected report filters.`;
    } catch (error) { status.textContent = error.message; }
    finally { download.disabled = false; }
  };
  for (const id of ['reportGradeFilter','reportSectionFilter','reportStartDate','reportEndDate']) document.getElementById(id)?.addEventListener('change', () => load());
  document.addEventListener('techwise:report-filters', () => load());
  load();
}
