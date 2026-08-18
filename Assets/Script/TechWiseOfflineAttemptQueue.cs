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
}

[Serializable]
public sealed class TechWiseAttemptQueueFile
{
    public List<TechWiseQueuedAttempt> attempts = new();
}

[DefaultExecutionOrder(-11900)]
public sealed class TechWiseOfflineAttemptQueue : MonoBehaviour
{
    public static TechWiseOfflineAttemptQueue Instance { get; private set; }
    public static event Action<int> QueueChanged;

    TechWiseAttemptQueueFile queue = new();
    bool isSyncing;

    static string QueuePath => Path.Combine(Application.persistentDataPath, "techwise-vr-attempt-queue.json");

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
        DontDestroyOnLoad(queueObject);
        Instance = queueObject.AddComponent<TechWiseOfflineAttemptQueue>();
        return Instance;
    }

    public static void Enqueue(TechWiseVrAttemptPayload payload)
    {
        EnsureInstance().Add(payload);
    }

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
        DontDestroyOnLoad(gameObject);
        Load();
    }

    void OnEnable()
    {
        StartCoroutine(PeriodicSyncCoroutine());
    }

    void Add(TechWiseVrAttemptPayload payload)
    {
        if (payload == null)
            return;

        if (payload.metadata == null)
            payload.metadata = new TechWiseVrAttemptMetadata();

        if (string.IsNullOrWhiteSpace(payload.metadata.local_attempt_id))
            payload.metadata.local_attempt_id = Guid.NewGuid().ToString("N");

        payload.metadata.offline_queued = true;
        queue.attempts.Add(new TechWiseQueuedAttempt
        {
            local_attempt_id = payload.metadata.local_attempt_id,
            payload = payload,
            queued_at = DateTime.UtcNow.ToString("o"),
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
            yield return SyncCoroutine();
            yield return new WaitForSecondsRealtime(30f);
        }
    }

    IEnumerator SyncCoroutine()
    {
        if (isSyncing || queue.attempts.Count == 0)
            yield break;

        if (!TechWiseSessionStore.HasSession || Application.internetReachability == NetworkReachability.NotReachable)
            yield break;

        isSyncing = true;
        var client = TechWisePortalClient.EnsureInstance();
        var index = 0;

        while (index < queue.attempts.Count)
        {
            var queuedAttempt = queue.attempts[index];
            var synced = false;
            var message = string.Empty;

            yield return client.PostAttemptCoroutine(queuedAttempt.payload, (ok, error) =>
            {
                synced = ok;
                message = error ?? string.Empty;
            });

            if (synced)
            {
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

        isSyncing = false;
    }

    void Load()
    {
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
        }
        catch (Exception error)
        {
            Debug.LogWarning($"Unable to load TechWise attempt queue: {error.Message}");
            queue = new TechWiseAttemptQueueFile();
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

    void Save()
    {
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(QueuePath, JsonUtility.ToJson(queue, true));
        }
        catch (Exception error)
        {
            Debug.LogWarning($"Unable to save TechWise attempt queue: {error.Message}");
        }
    }
}
