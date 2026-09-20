import { ApiError, getProfileById, insertRow, optionsResponse, patchRows, requireEnv, selectRows } from '../_utils.js';
import { hashToken, limitedJson, randomToken, studentContext, vrFail, vrJson } from '../_vr.js';
import { normalizeStationId } from './station-pair.js';

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const url = new URL(request.url);
    const stationId = normalizeStationId(url.searchParams.get('station_id'));
    const claim = url.searchParams.get('claim') !== 'false'; // Default to true (headset claim mode)
    return await resolveStationStatus(env, stationId, claim);
  } catch (err) {
    return vrFail(err);
  }
}

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const body = await limitedJson(request);
    const stationId = normalizeStationId(body.station_id);
    const claim = body.claim !== false;
    return await resolveStationStatus(env, stationId, claim);
  } catch (err) {
    return vrFail(err);
  }
}

async function resolveStationStatus(env, stationId, shouldClaim) {
  const codeHash = await hashToken(`STATION:${stationId.toUpperCase()}`);
  const nowIso = new Date().toISOString();

  const filter = shouldClaim
    ? `select=*&code_hash=eq.${codeHash}&consumed_at=is.null&expires_at=gt.${encodeURIComponent(nowIso)}&order=created_at.desc&limit=1`
    : `select=*&code_hash=eq.${codeHash}&expires_at=gt.${encodeURIComponent(nowIso)}&order=created_at.desc&limit=1`;

  const rows = await selectRows(env, 'vr_launch_codes', filter);

  if (!rows || rows.length === 0) {
    return vrJson({
      status: 'idle',
      station_id: stationId,
      message: 'Station is waiting for student pairing from the web dashboard.'
    });
  }

  const claimRow = rows[0];
  const profile = await getProfileById(env, claimRow.student_id);
  if (!profile || profile.role !== 'student' || profile.status !== 'approved') {
    return vrJson({
      status: 'idle',
      station_id: stationId,
      message: 'Station claim has invalid student account.'
    });
  }

  if (!shouldClaim) {
    // Read-only peek for web dashboard
    return vrJson({
      status: 'paired',
      station_id: stationId,
      student_id: profile.id,
      student_name: profile.full_name,
      expires_at: claimRow.expires_at
    });
  }

  // Headset claiming: consume the launch code and generate an 8-hour device session
  await patchRows(
    env,
    'vr_launch_codes',
    `code_hash=eq.${codeHash}&consumed_at=is.null`,
    { consumed_at: new Date().toISOString() },
    'Unable to consume station pairing code.',
    false
  );

  const token = 'twvr_' + randomToken();
  const tokenHash = await hashToken(token);
  const sessionExpiresAt = new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString();

  await insertRow(
    env,
    'vr_device_sessions',
    {
      token_hash: tokenHash,
      student_id: profile.id,
      expires_at: sessionExpiresAt
    },
    'Unable to create VR device session.'
  );

  const context = await studentContext(env, profile);

  return vrJson({
    status: 'paired',
    station_id: stationId,
    profile: context.profile,
    student: context.profile,
    session: {
      access_token: token,
      token_type: 'Bearer',
      expires_in: 28800,
      expires_at: sessionExpiresAt
    },
    competitions: context.competitions
  });
}
