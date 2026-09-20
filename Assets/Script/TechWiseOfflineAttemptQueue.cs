using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class TechWiseQueuedAttempt
{
    public string local_attempt_id;
    public TechWiseVrAttemptPayload payload;
    public int retry_count;
    public string last_error;
    public string queued_at;
    public string student_id, portal_origin;
}

[Serializable]
public sealed class TechWiseAttemptQueueFile
{
    public List<TechWiseQueuedAttempt> attempts = new();
    public List<string> acknowledged = new();
}

[DefaultExecutionOrder(-11900)]
public sealed class TechWiseOfflineAttemptQueue : MonoBehaviour
{
    public static TechWiseOfflineAttemptQueue Instance { get; private set; }
    public static event Action<int> QueueChanged;

    TechWiseAttemptQueueFile queue = new();
    bool isSyncing;
    bool loadFailed;
    public static string PersistenceError { get; private set; }

#if UNITY_EDITOR
    public static string VerificationQueuePath;
#endif
    static string QueuePath
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(VerificationQueuePath)) return VerificationQueuePath;
#endif
            return Path.Combine(Application.persistentDataPath, "techwise-vr-attempt-queue.json");
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        EnsureInstance();
    }

    public static TechWiseOfflineAttemptQueue EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<TechWiseOfflineAttemptQueue>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        var queueObject = new GameObject("TechWise Offline Attempt Queue");
        if (Application.isPlaying) DontDestroyOnLoad(queueObject);
        Instance = queueObject.AddComponent<TechWiseOfflineAttemptQueue>();
        return Instance;
    }

    public static void Enqueue(TechWiseVrAttemptPayload payload, string studentId = null, string portalOrigin = null)
    {
        EnsureInstance().Add(payload, studentId, portalOrigin);
    }

    public static bool HasSynced(string id) => EnsureInstance().queue.acknowledged.Contains(id);

    public static void SyncNow()
    {
        var instance = EnsureInstance();
        if (instance.isActiveAndEnabled)
            instance.StartCoroutine(instance.SyncCoroutine());
    }

    public static int PendingCount => EnsureInstance().queue.attempts.Count;

    public static bool HasPending(string localAttemptId)
    {
        return EnsureInstance().Contains(localAttemptId);
    }

    public static string GetLastError(string localAttemptId)
    {
        return EnsureInstance().FindLastError(localAttemptId);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        Load();
    }

    void OnEnable()
    {
        StartCoroutine(PeriodicSyncCoroutine());
    }

    void Add(TechWiseVrAttemptPayload payload, string studentId, string portalOrigin)
    {
        if (payload == null)
            return;

        if (payload.metadata == null)
            payload.metadata = new TechWiseVrAttemptMetadata();

        if (string.IsNullOrWhiteSpace(payload.metadata.local_attempt_id))
            payload.metadata.local_attempt_id = Guid.NewGuid().ToString("N");

        // Repeated local completion/retry calls keep one immutable payload for this ID.
        if (Contains(payload.metadata.local_attempt_id) || HasSynced(payload.metadata.local_attempt_id)) return;
        payload = JsonUtility.FromJson<TechWiseVrAttemptPayload>(JsonUtility.ToJson(payload));

        if (string.IsNullOrEmpty(studentId))
            studentId = TechWiseSessionStore.GetProfile()?.id;
        if (string.IsNullOrEmpty(portalOrigin))
            portalOrigin = TechWisePortalClient.PortalBaseUrl;

        payload.metadata.offline_queued = true;
        queue.attempts.Add(new TechWiseQueuedAttempt
        {
            local_attempt_id = payload.metadata.local_attempt_id,
            payload = payload,
            queued_at = DateTime.UtcNow.ToString("o"),
            student_id = studentId,
            portal_origin = portalOrigin,
        });

        Save();
        QueueChanged?.Invoke(queue.attempts.Count);
        SyncNow();
    }

    IEnumerator PeriodicSyncCoroutine()
    {
        yield return new WaitForSecondsRealtime(2f);

        while (true)
        {
            if (!string.IsNullOrEmpty(PersistenceError)) Save();
            yield return SyncCoroutine();
            yield return new WaitForSecondsRealtime(30f);
        }
    }

    IEnumerator SyncCoroutine()
    {
        if (isSyncing || queue.attempts.Count == 0)
            yield break;

        if (!TechWiseSessionStore.HasSession)
        {
            var station = MainMenu.CurrentStationId;
            yield return TechWisePortalClient.EnsureInstance().CheckStationStatusCoroutine(station, (ok, error, resp) => { });
        }

        if (!TechWiseSessionStore.HasSession)
            yield break;
#if UNITY_EDITOR
        if (TechWisePortalClient.VerificationTransport == null && Application.internetReachability == NetworkReachability.NotReachable) yield break;
#endif

        if (!Save()) yield break;
        isSyncing = true;
        try
        {
        var client = TechWisePortalClient.EnsureInstance();
        var index = 0;

        while (index < queue.attempts.Count)
        {
            var queuedAttempt = queue.attempts[index];
            if (string.IsNullOrEmpty(queuedAttempt.student_id))
            {
                var profile = TechWiseSessionStore.GetProfile();
                if (profile != null && !string.IsNullOrEmpty(profile.id))
                {
                    queuedAttempt.student_id = profile.id;
                    queuedAttempt.portal_origin = TechWisePortalClient.PortalBaseUrl;
                }
            }
            if (string.IsNullOrEmpty(queuedAttempt.student_id) || queuedAttempt.student_id != TechWiseSessionStore.GetProfile()?.id || queuedAttempt.portal_origin != TechWisePortalClient.PortalBaseUrl)
            {
                queuedAttempt.last_error = "Reconnect the original student and portal. Legacy unowned results require teacher review.";
                index++;
                continue;
            }
            var synced = false;
            var message = string.Empty;

            yield return client.PostAttemptCoroutine(queuedAttempt.payload, (ok, error) =>
            {
                synced = ok;
                message = error ?? string.Empty;
            }, queuedAttempt.student_id, queuedAttempt.portal_origin);

            if (synced)
            {
                if (!queue.acknowledged.Contains(queuedAttempt.local_attempt_id)) queue.acknowledged.Add(queuedAttempt.local_attempt_id);
                queue.attempts.RemoveAt(index);
                Save();
                QueueChanged?.Invoke(queue.attempts.Count);
                continue;
            }

            queuedAttempt.retry_count++;
            queuedAttempt.last_error = message;
            Save();
            QueueChanged?.Invoke(queue.attempts.Count);

            if (message.IndexOf("log in", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("session", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                break;
            }

            index++;
        }

        }
        finally { isSyncing = false; }
    }

    void Load()
    {
        loadFailed = false;
        PersistenceError = null;
        if (!File.Exists(QueuePath))
        {
            queue = new TechWiseAttemptQueueFile();
            return;
        }

        try
        {
            var json = File.ReadAllText(QueuePath);
            queue = string.IsNullOrWhiteSpace(json)
                ? new TechWiseAttemptQueueFile()
                : JsonUtility.FromJson<TechWiseAttemptQueueFile>(json) ?? new TechWiseAttemptQueueFile();

            if (queue.attempts == null)
                queue.attempts = new List<TechWiseQueuedAttempt>();
            queue.acknowledged ??= new List<string>();
        }
        catch (Exception error)
        {
            Debug.LogWarning($"Unable to load TechWise attempt queue: {error.Message}");
            queue = new TechWiseAttemptQueueFile();
            loadFailed = true;
            PersistenceError = "The existing queue could not be read. Keep the app open and contact support; its file has been preserved.";
        }
    }

    bool Contains(string localAttemptId)
    {
        if (string.IsNullOrWhiteSpace(localAttemptId))
            return false;

        foreach (var attempt in queue.attempts)
        {
            if (attempt != null && attempt.local_attempt_id == localAttemptId)
                return true;
        }

        return false;
    }

    string FindLastError(string localAttemptId)
    {
        if (string.IsNullOrWhiteSpace(localAttemptId))
            return string.Empty;

        foreach (var attempt in queue.attempts)
        {
            if (attempt != null && attempt.local_attempt_id == localAttemptId)
                return attempt.last_error ?? string.Empty;
        }

        return string.Empty;
    }

    bool Save()
    {
        if (loadFailed) return false;
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            var temporary = QueuePath + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(queue, true));
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(QueuePath)) File.Replace(temporary, QueuePath, QueuePath + ".bak");
            else File.Move(temporary, QueuePath);
            PersistenceError = null;
            return true;
        }
        catch (Exception error)
        {
            Debug.LogWarning($"Unable to save TechWise attempt queue: {error.Message}");
            PersistenceError = "Local save failed. Keep the app open, free storage, then retry sync.";
            return false;
        }
    }
}
