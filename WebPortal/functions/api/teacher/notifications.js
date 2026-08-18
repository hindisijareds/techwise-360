import {
  fail,
  getTeacherNotifications,
  json,
  markAllTeacherNotificationsRead,
  markTeacherNotificationRead,
  optionsResponse,
  publicTeacherNotification,
  readJson,
  requireEnv,
  requireTeacher
} from "../_utils.js";

export const onRequestOptions = () => optionsResponse();

export async function onRequestGet({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const notifications = await getTeacherNotifications(env, teacher.id);

    return json({
      notifications: notifications.map(publicTeacherNotification),
      unread_count: notifications.filter((notification) => !notification.read_at).length
    });
  } catch (error) {
    return fail(error.message || "Unable to load notifications.", error.status || 500);
  }
}

export async function onRequestPatch({ request, env }) {
  try {
    requireEnv(env);
    const { profile: teacher } = await requireTeacher(request, env);
    const body = await readJson(request);
    const action = String(body.action || "read").trim().toLowerCase();

    if (action === "read_all") {
      await markAllTeacherNotificationsRead(env, teacher.id);
      const notifications = await getTeacherNotifications(env, teacher.id);
      return json({
        notifications: notifications.map(publicTeacherNotification),
        unread_count: notifications.filter((notification) => !notification.read_at).length
      });
    }

    const notificationId = String(body.id || "").trim();
    if (!notificationId) {
      return fail("Notification id is required.", 400);
    }

    const notification = await markTeacherNotificationRead(env, teacher.id, notificationId);
    const notifications = await getTeacherNotifications(env, teacher.id);
    return json({
      notification: publicTeacherNotification(notification),
      notifications: notifications.map(publicTeacherNotification),
      unread_count: notifications.filter((item) => !item.read_at).length
    });
  } catch (error) {
    return fail(error.message || "Unable to update notification.", error.status || 500);
  }
}
