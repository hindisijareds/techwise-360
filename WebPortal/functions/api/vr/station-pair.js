import { ApiError, deleteRows, insertRow, optionsResponse, requireEnv, requireProfile } from '../_utils.js';
import { hashToken, limitedJson, vrFail, vrJson } from '../_vr.js';

export const onRequestOptions = () => optionsResponse();

export function normalizeStationId(raw) {
  const s = String(raw || '').trim().toLowerCase().replace(/[^a-z0-9-_]/g, '');
  if (!s) throw new ApiError('Lab station identifier is required.', 400);
  return s;
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireProfile(request, env);
    if (profile.role !== 'student' || profile.status !== 'approved') {
      throw new ApiError('Approved student access is required.', 403);
    }

    const body = await limitedJson(request);
    const stationId = normalizeStationId(body.station_id);
    const codeHash = await hashToken(`STATION:${stationId.toUpperCase()}`);

    // Clean up any stale or previous pending claim for this station
    try {
      await deleteRows(env, 'vr_launch_codes', `code_hash=eq.${codeHash}`, 'Unable to clear previous station pairing.');
    } catch {
      // Ignore if table doesn't have the record
    }

    // Insert new pairing claim with a 15-minute pairing window
    const expiresAt = new Date(Date.now() + 15 * 60 * 1000).toISOString();
    await insertRow(
      env,
      'vr_launch_codes',
      {
        code_hash: codeHash,
        student_id: profile.id,
        expires_at: expiresAt
      },
      'Failed to register station pairing.'
    );

    return vrJson({
      ok: true,
      station_id: stationId,
      student_id: profile.id,
      student_name: profile.full_name,
      expires_at: expiresAt
    });
  } catch (err) {
    return vrFail(err);
  }
}
