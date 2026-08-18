import {
  ApiError,
  fail,
  getStudentNotifications,
  json,
  markAllStudentNotificationsRead,
  markStudentNotificationRead,
  optionsResponse,
  publicStudentNotification,
  readJson,
  requireEnv,
  requireProfile
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireApprovedStudent(request, env);
    const notifications = await getStudentNotifications(env, profile.id);

    return json({
      notifications: notifications.map(publicStudentNotification),
      unread_count: notifications.filter((notification) => !notification.read_at).length
    });
  } catch (error) {
    return fail(error.message || "Unable to load notifications.", error.status || 500);
  }
}

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    const { profile } = await requireApprovedStudent(request, env);
    const body = await readJson(request);
    const action = String(body.action || "read").trim().toLowerCase();

    if (action === "read_all") {
      await markAllStudentNotificationsRead(env, profile.id);
      const notifications = await getStudentNotifications(env, profile.id);
      return json({
        notifications: notifications.map(publicStudentNotification),
        unread_count: notifications.filter((notification) => !notification.read_at).length
      });
    }

    const notificationId = String(body.id || "").trim();
    if (!notificationId) {
      throw new ApiError("Notification id is required.", 400);
    }

    const notification = await markStudentNotificationRead(env, profile.id, notificationId);
    const notifications = await getStudentNotifications(env, profile.id);
    return json({
      notification: publicStudentNotification(notification),
      notifications: notifications.map(publicStudentNotification),
      unread_count: notifications.filter((item) => !item.read_at).length
    });
  } catch (error) {
    return fail(error.message || "Unable to update notification.", error.status || 500);
  }
}

async function requireApprovedStudent(request, env) {
  const { profile } = await requireProfile(request, env);
  if (profile.role !== "student" || profile.status !== "approved") {
    throw new ApiError("Approved student access is required.", 403);
  }
  return { profile };
}
