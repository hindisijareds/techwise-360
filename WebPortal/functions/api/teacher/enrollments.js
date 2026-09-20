import { ApiError, optionsResponse, requireEnv, requireTeacher, selectRows } from '../_utils.js';
import { limitedJson, uuid, vrFail, vrJson } from '../_vr.js';
import { academicRpc } from '../_academic.js';
export const onRequestOptions=()=>optionsResponse();
export async function onRequestGet({request,env}) {
  try{
    requireEnv(env);await requireTeacher(request,env);const p=new URL(request.url).searchParams;
    const q=new URLSearchParams({select:'*',order:'created_at.desc,id.desc',limit:'200'});
    for(const k of ['academic_year_id','quarter_id','student_id','section_id'])if(p.get(k))q.set(k,'eq.'+uuid(p.get(k)));
    const offset=Number(p.get('offset')||0);if(!Number.isSafeInteger(offset)||offset<0)throw new ApiError('Invalid page.',400);q.set('offset',offset);
    const enrollments=await selectRows(env,'student_enrollments',q.toString());
    const [years,terms,sections,students]=await Promise.all([selectRows(env,'academic_years','select=*&order=name.desc'),selectRows(env,'quarters','select=*&order=school_year.desc,name.asc'),selectRows(env,'class_sections','select=*&order=school_year.desc,name.asc'),selectRows(env,'profiles','select=id,full_name,grade_level,status&role=eq.student&order=full_name.asc')]);
    return vrJson({enrollments,years,terms,sections,students,next_offset:enrollments.length===200?offset+200:null});
  }catch(e){return vrFail(e);}
}
export async function onRequestPost({request,env}) {
  try{
    requireEnv(env);const {profile}=await requireTeacher(request,env);const b=await limitedJson(request);
    if(!['Grade 9','Grade 10'].includes(b.grade_level))throw new ApiError('Select Grade 9 or Grade 10.',400);
    return vrJson({enrollment:await academicRpc(env,'enroll_student',{p_student:uuid(b.student_id),p_year:uuid(b.academic_year_id),p_term:uuid(b.quarter_id),p_section:uuid(b.section_id),p_grade:b.grade_level,p_teacher:profile.id})});
  }catch(e){return vrFail(e);}
}
