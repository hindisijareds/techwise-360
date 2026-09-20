import { ApiError, optionsResponse, requireEnv, requireTeacher, selectRows } from '../_utils.js';
import { uuid, vrFail, vrJson } from '../_vr.js';
export const onRequestOptions = () => optionsResponse();
export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env); await requireTeacher(request, env);
    const params = new URL(request.url).searchParams;
    const query = new URLSearchParams({ select: '*', order: 'completed_at.desc,id.desc', limit: '200' });
    const offset = Number(params.get('offset') || 0);
    if (!Number.isSafeInteger(offset) || offset < 0) throw new ApiError('Invalid page.', 400);
    query.set('offset', String(offset));
    for (const key of ['academic_year_id','quarter_id','section_id','student_id','competition_id'])
      if (params.get(key)) query.set(key, `eq.${uuid(params.get(key))}`);
    for (const key of ['grade_level','section_name']) {
      if (params.get(key)) {
        if (params.get(key).length > 100) throw new ApiError('Invalid filter.', 400);
        query.set(key, `eq.${params.get(key)}`);
      }
    }
    if (params.get('simulation_type')) {
      if (!['assembly','disassembly'].includes(params.get('simulation_type'))) throw new ApiError('Invalid activity.', 400);
      query.set('simulation_type', `eq.${params.get('simulation_type')}`);
    }
    for (const [key, operator] of [['from','gte'],['to','lte']]) {
      if (params.get(key)) {
        if (!/^\d{4}-\d{2}-\d{2}$/.test(params.get(key)) || !Number.isFinite(Date.parse(params.get(key)))) throw new ApiError('Invalid date.', 400);
        query.append('completed_at', `${operator}.${params.get(key)}T${key === 'from' ? '00:00:00' : '23:59:59.999'}Z`);
      }
    }
    const results = await selectRows(env, 'vr_simulation_attempts', query.toString());
    let options;
    if (params.get('options') === 'true') {
      const [years, terms, sections, students, assessments] = await Promise.all([
        selectRows(env,'academic_years','select=id,name&order=name.desc'),
        selectRows(env,'quarters','select=id,title,school_year,academic_year_id&order=school_year.desc'),
        selectRows(env,'class_sections','select=id,name,school_year,grade_level&order=name.asc'),
        selectRows(env,'profiles','select=id,full_name&role=eq.student&order=full_name.asc'),
        selectRows(env,'vr_competitions','select=id,title&order=title.asc')
      ]);
      options = { years, terms, sections, students, assessments };
    }
    return vrJson({ results, next_offset: results.length === 200 ? offset + 200 : null, options });
  } catch (error) { return vrFail(error); }
}
