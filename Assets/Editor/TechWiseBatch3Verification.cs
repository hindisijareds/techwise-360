using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Isolated queue/API-contract checks. Never sends a real credential or modifies the user's queue.</summary>
public static class TechWiseBatch3Verification
{
    static int checks, requests;
    static string responseMode;
    static readonly string Student = Guid.NewGuid().ToString();
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception(label);
        checks++; Debug.Log("BATCH3_PASS " + label);
    }
    static object Invoke(object instance, string method) => instance.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, null);
    static void RunEnumerator(IEnumerator routine)
    {
        int iterations = 0;
        while (routine.MoveNext())
        {
            if (++iterations > 100) throw new Exception("Unexpected asynchronous wait in isolated test");
            if (routine.Current is IEnumerator nested) RunEnumerator(nested);
        }
    }
    static void SignIn(string id) => TechWiseSessionStore.Save(
        new TechWisePortalSession { access_token = "isolated-test-token", expires_at = DateTime.UtcNow.AddHours(1).ToString("o") },
        new TechWisePortalProfile { id = id, role = "student", status = "approved" });
    static IEnumerator Transport(string path, string body, string token, Action<bool,string,string> callback)
    {
        requests++;
        if (responseMode == "offline") callback(false, "Simulated connection loss", "");
        else if (responseMode == "invalid") callback(true, "", "not json");
        else
        {
            var payload = JsonUtility.FromJson<TechWiseVrAttemptPayload>(body);
            callback(true, "", JsonUtility.ToJson(new TechWiseResultAcknowledgement
            {
                status = "synced", attempt_id = responseMode == "wrong-id" ? Guid.NewGuid().ToString() : payload.metadata.local_attempt_id,
                attempt = new TechWiseServerAttempt { student_id = Student }
            }));
        }
        yield break;
    }
    public static void Run()
    {
        Directory.CreateDirectory("Logs/Batch3");
        var directory = Path.GetFullPath(Path.Combine("Logs/Batch3", "queue-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "attempts.json");
        TechWiseOfflineAttemptQueue.VerificationQueuePath = path;
        var originalSession = TechWiseSessionStore.GetSession();
        var originalProfile = TechWiseSessionStore.GetProfile();
        try
        {
            var queue = TechWiseOfflineAttemptQueue.EnsureInstance();
            queue.StopAllCoroutines(); queue.enabled = false;
            Invoke(queue, "Load");
            TechWisePortalClient.VerificationTransport = Transport;
            SignIn(Student);
            var id = Guid.NewGuid().ToString("N");
            var clock = new TechWiseAssessmentClock();
            Check(clock.Start(10, DateTime.UtcNow, id) && clock.AttemptId == id, "Registered server identity is retained by the Batch 2 clock");
            var payload = new TechWiseVrAttemptPayload { score_percent = 100, metadata = new TechWiseVrAttemptMetadata { local_attempt_id = id } };
            TechWiseOfflineAttemptQueue.Enqueue(payload, Student, TechWisePortalClient.PortalBaseUrl);
            Check(File.Exists(path) && TechWiseOfflineAttemptQueue.HasPending(id), "DONE payload is persisted before transmission");
            payload.score_percent = 0;
            Check(File.ReadAllText(path).Contains("100"), "Pending payload is an immutable copy");
            responseMode = "offline";
            RunEnumerator((IEnumerator)Invoke(queue, "SyncCoroutine"));
            Check(TechWiseOfflineAttemptQueue.HasPending(id) && !TechWiseOfflineAttemptQueue.HasSynced(id), "Failed request remains pending, never synced");
            Invoke(queue, "Load");
            Check(TechWiseOfflineAttemptQueue.HasPending(id), "Pending result survives queue reload");
            SignIn(Guid.NewGuid().ToString());
            int before = requests;
            RunEnumerator((IEnumerator)Invoke(queue, "SyncCoroutine"));
            Check(requests == before && TechWiseOfflineAttemptQueue.HasPending(id), "Another student cannot upload the original student's result");
            SignIn(Student);
            responseMode = "invalid";
            RunEnumerator((IEnumerator)Invoke(queue, "SyncCoroutine"));
            Check(TechWiseOfflineAttemptQueue.HasPending(id), "HTTP success with unreadable JSON is not an acknowledgement");
            responseMode = "wrong-id";
            RunEnumerator((IEnumerator)Invoke(queue, "SyncCoroutine"));
            Check(TechWiseOfflineAttemptQueue.HasPending(id), "Receipt for a different attempt is rejected");
            responseMode = "success";
            RunEnumerator((IEnumerator)Invoke(queue, "SyncCoroutine"));
            Check(!TechWiseOfflineAttemptQueue.HasPending(id) && TechWiseOfflineAttemptQueue.HasSynced(id), "Matching server receipt marks result synced");
            Invoke(queue, "Load");
            Check(TechWiseOfflineAttemptQueue.HasSynced(id), "Explicit sync receipt survives restart");
            TechWiseOfflineAttemptQueue.Enqueue(payload, Student, TechWisePortalClient.PortalBaseUrl);
            Check(!TechWiseOfflineAttemptQueue.HasPending(id), "Repeated DONE does not requeue acknowledged result");
            var legacyId = Guid.NewGuid().ToString("N");
            payload.metadata.local_attempt_id = legacyId;
            TechWiseOfflineAttemptQueue.Enqueue(payload);
            before = requests;
            RunEnumerator((IEnumerator)Invoke(queue, "SyncCoroutine"));
            Check(requests == before && TechWiseOfflineAttemptQueue.HasPending(legacyId), "Unowned legacy result is retained without claiming the current account");
            File.WriteAllText(path, "corrupted-json");
            Invoke(queue, "Load");
            payload.metadata.local_attempt_id = Guid.NewGuid().ToString("N");
            TechWiseOfflineAttemptQueue.Enqueue(payload, Student, TechWisePortalClient.PortalBaseUrl);
            Check(File.ReadAllText(path) == "corrupted-json" && !string.IsNullOrEmpty(TechWiseOfflineAttemptQueue.PersistenceError), "Unreadable queue is preserved and save failure is visible");
            TechWiseSessionStore.Save(new TechWisePortalSession { access_token="test", expires_at="invalid-date" }, new TechWisePortalProfile {id=Student});
            Check(TechWiseSessionStore.SessionNeedsRefresh(), "Malformed token expiry fails closed");
            Check(!PlayerPrefs.HasKey("TechWise360.PortalSession"), "Credentials are not persisted in PlayerPrefs");
            File.WriteAllText("Logs/Batch3/unity-results.txt", $"PASS: {checks} isolated Unity queue/API assertions");
        }
        catch (Exception error)
        {
            File.WriteAllText("Logs/Batch3/unity-results.txt", $"FAIL after {checks}: {error}");
            throw;
        }
        finally
        {
            TechWisePortalClient.VerificationTransport = null;
            TechWiseOfflineAttemptQueue.VerificationQueuePath = null;
            TechWiseSessionStore.Clear();
            if (originalSession != null) TechWiseSessionStore.Save(originalSession, originalProfile);
        }
    }
}
