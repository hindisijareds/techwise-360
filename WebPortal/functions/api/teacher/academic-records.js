import { ApiError, optionsResponse, requireEnv, requireTeacher, selectRows } from '../_utils.js';
import { uuid, vrFail, vrJson } from '../_vr.js';
export const onRequestOptions=()=>optionsResponse();
export async function onRequestGet({request,env}) {
  try{
    requireEnv(env);await requireTeacher(request,env);const p=new URL(request.url).searchParams;
    const q=new URLSearchParams({select:'*,profiles(full_name),lessons(title)',order:'updated_at.desc,id.desc',limit:'200'});
    for(const key of ['academic_year_id','quarter_id','student_id','section_id'])if(p.get(key))q.set(key,'eq.'+uuid(p.get(key)));
    if(p.get('grade_level')){if(!['Grade 9','Grade 10'].includes(p.get('grade_level')))throw new ApiError('Invalid grade.',400);q.set('grade_level','eq.'+p.get('grade_level'));}
    const offset=Number(p.get('offset')||0);if(!Number.isSafeInteger(offset)||offset<0)throw new ApiError('Invalid page.',400);q.set('offset',offset);
    const records=await selectRows(env,'lesson_progress',q.toString());return vrJson({records,next_offset:records.length===200?offset+200:null});
  }catch(e){return vrFail(e);}
}
