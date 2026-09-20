import { deleteRows, optionsResponse, patchRows, requireEnv } from '../_utils.js';
import { hashToken, limitedJson, vrFail, vrJson } from '../_vr.js';
import { normalizeStationId } from './station-pair.js';

export const onRequestOptions = () => optionsResponse();

export async function onRequestPost({ request, env }) {
  try {
    requireEnv(env);
    const body = await limitedJson(request);
    const stationId = normalizeStationId(body.station_id);
    const codeHash = await hashToken(`STATION:${stationId.toUpperCase()}`);

    try {
      await deleteRows(
        env,
        'vr_launch_codes',
        `code_hash=eq.${codeHash}`,
        'Unable to delete station launch code.'
      );
    } catch {
      try {
        await patchRows(
          env,
          'vr_launch_codes',
          `code_hash=eq.${codeHash}`,
          { expires_at: new Date(0).toISOString(), consumed_at: new Date().toISOString() },
          'Unable to expire station launch code.',
          false
        );
      } catch {
        // Ignore if no active launch code
      }
    }

    return vrJson({
      ok: true,
      station_id: stationId,
      message: 'Station unpairing completed.'
    });
  } catch (err) {
    return vrFail(err);
  }
}
