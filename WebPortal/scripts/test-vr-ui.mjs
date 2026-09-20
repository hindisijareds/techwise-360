import { chromium } from 'playwright';
import { createServer } from 'node:http';
import { readFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import assert from 'node:assert/strict';
const root = fileURLToPath(new URL('../',import.meta.url));
const server = createServer(async (req,res) => {
  const file = path.resolve(root,'.'+new URL(req.url,'http://localhost').pathname);
  if (!file.startsWith(root)) { res.writeHead(403).end(); return; }
  try { const bytes = await readFile(file); res.setHeader('Content-Type',file.endsWith('.js')?'text/javascript':file.endsWith('.css')?'text/css':'text/html'); res.end(bytes); }
  catch { res.writeHead(404).end(); }
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
const origin = `http://127.0.0.1:${server.address().port}`;
const browser = await chromium.launch({headless:true,...(process.env.TECHWISE_BROWSER_CHANNEL ? {channel:process.env.TECHWISE_BROWSER_CHANNEL} : {})});
let checks = 0;
function check(value,label) { assert.ok(value,label); checks++; console.log('PASS',label); }
try {
  const page = await browser.newPage({viewport:{width:1440,height:1000}});
  const errors = []; page.on('pageerror',error=>errors.push(error.message));
  await page.route('**/*', async route => {
    if (!route.request().url().startsWith(origin)) return route.abort();
    if (route.request().url().endsWith('/app.js')) return route.fulfill({contentType:'text/javascript',body:'// Existing dashboard boot is excluded from this focused integration test.'});
    return route.continue();
  });
  await page.addInitScript(()=>localStorage.setItem('techwise360.session',JSON.stringify({access_token:'isolated-test-token',profile:{role:'teacher'}})));
  await page.route('**/api/vr/connect',route=>route.fulfill({json:{code:'a'.repeat(64),expires_in:120}}));
  await page.clock.install();
  await page.goto(origin+'/vr-connect.html');
  await page.locator('#generateVrCode').click();
  await page.waitForFunction(()=>document.querySelector('#vrConnectionCode').value.length===64);
  check(await page.locator('#copyVrCode').isEnabled(),'Signed-in website flow displays a copyable code');
  await page.clock.fastForward(121000);
  check(await page.locator('#vrConnectionCode').inputValue()==='' && await page.locator('#copyVrCode').isDisabled(),'Expired code is cleared from the page');
  await page.evaluate(()=>localStorage.removeItem('techwise360.session'));
  await page.locator('#generateVrCode').click();
  await page.waitForFunction(()=>document.querySelector('#vrConnectStatus').textContent.includes('sign in'));
  check(true,'Signed-out page requests the existing portal login');
  let queries = [];
  const row = {student_id:'student-id',student_name:'\t=2+3',simulation_type:'assembly',score_percent:92,accuracy_percent:92,duration_seconds:123,mistakes:1,completed_at:'2026-09-06T12:00:00Z',academic_year_id:'year-id',quarter_id:'term-id',grade_level:'Grade 9',section_name:'A',competition_id:'assessment-id',assessment_session_id:'attempt-id',metadata:{mistake_details:[{category:'Incorrect Component',explanation:'<script>bad()</script>',correction:'Use the CPU socket.'}]}};
  await page.route('**/api/teacher/vr-results?*',route=>{
    const p=new URL(route.request().url()).searchParams; queries.push(p);
    return route.fulfill({json:{results:[{...row,assessment_session_id:p.get('offset')==='200'?'second-id':'attempt-id'}],next_offset:p.get('offset')==='200'?null:200,...(p.get('options')==='true'?{options:{years:[{id:'year-id',name:'2026-2027'}],terms:[{id:'term-id',title:'Term 1',school_year:'2026-2027'}],sections:[{id:'section-id',name:'A',school_year:'2026-2027'}],students:[{id:'student-id',full_name:'=2+3'}],assessments:[{id:'assessment-id',title:'PC Test'}]}}:{})}});
  });
  await page.goto(origin+'/teacher-dashboard.html');
  // Match the existing report navigation state without booting unrelated dashboard APIs.
  await page.locator('[data-view="reports"]').evaluate(node=>{node.style.display='block';});
  await page.waitForFunction(()=>document.querySelector('#teacherVrClassRecord tbody')?.rows.length===1);
  check((await page.locator('#teacherVrClassRecord').innerText()).includes('2026-2027'),'VR results render inside the existing teacher Reports page');
  await page.locator('#teacherVrClassRecord select').nth(0).selectOption('year-id');
  await page.waitForFunction(()=>document.querySelector('#teacherVrClassRecord [role="status"]').textContent.includes('Showing'));
  await page.waitForTimeout(100);
  check(queries.some(q=>q.get('academic_year_id')==='year-id'),'Academic-year selection reaches the results API');
  await page.locator('#reportGradeFilter').evaluate(select=>{select.add(new Option('Grade 9','Grade 9'));select.value='Grade 9';document.dispatchEvent(new Event('techwise:report-filters'));});
  await page.waitForTimeout(100);
  check(queries.some(q=>q.get('grade_level')==='Grade 9'),'Existing report filter updates also reload VR records');
  await page.locator('#teacherVrClassRecord summary').click();
  check(await page.locator('#teacherVrClassRecord details script').count()===0 && (await page.locator('#teacherVrClassRecord details').innerText()).includes('<script>'),'Mistake descriptions are rendered as text');
  const downloadEvent = page.waitForEvent('download');
  await page.getByRole('button',{name:'Download VR results CSV'}).click();
  const download = await downloadEvent;
  const stream = await download.createReadStream(); let csv=''; for await (const chunk of stream) csv+=chunk;
  check(csv.includes('attempt-id') && csv.includes('second-id') && csv.includes("'\t=2+3"),'CSV export reads every filtered page and escapes whitespace-prefixed spreadsheet formulas');
  await mkdir(new URL('../../Logs/Batch3/',import.meta.url),{recursive:true});
  await page.locator('#teacherVrClassRecord').screenshot({path:fileURLToPath(new URL('../../Logs/Batch3/teacher-vr-record.png',import.meta.url))});
  check(errors.length===0,'Focused connection/report UI produced no JavaScript errors');
  console.log(`PASS: ${checks} focused browser assertions (API responses mocked)`);
} finally { await browser.close(); server.close(); }
