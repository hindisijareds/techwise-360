using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public sealed class TechWiseApiError
{
    public string error;
}

[Serializable]
public sealed class TechWiseLoginRequest
{
    public string identifier;
    public string password;
}

[Serializable]
public sealed class TechWiseRefreshRequest
{
    public string refresh_token;
}

[Serializable]
public sealed class TechWiseLoginResponse
{
    public TechWisePortalSession session;
    public TechWisePortalProfile profile;
}

[Serializable]
public sealed class TechWiseVrCompetition
{
    public string id;
    public string title;
    public string description;
    public string simulation_type;
    public string grade_level;
    public string section;
    public string start_at;
    public string end_at;
    public int attempts_allowed;
    public string status;
}

[Serializable]
public sealed class TechWiseStudentLeaderboardResponse
{
    public TechWiseVrCompetition[] competitions;
}

[Serializable]
public sealed class TechWiseVrAttemptPayload
{
    public string simulation_type;
    public string competition_id;
    public float score_percent;
    public int duration_seconds;
    public int mistakes;
    public string started_at;
    public string completed_at;
    public TechWiseVrAttemptMetadata metadata;
}

[Serializable]
public sealed class TechWiseVrAttemptMetadata
{
    public string local_attempt_id;
    public string mode;
    public string[] expected_order;
    public string[] completed_order;
    public string[] skipped_steps;
    public int wrong_order_count;
    public int wrong_part_count;
    public int reset_count;
    public int time_penalty;
    public int mistake_penalty;
    public string game_version;
    public string control_mode;
    public bool offline_queued;
}

[DefaultExecutionOrder(-12000)]
public sealed class TechWisePortalClient : MonoBehaviour
{
    public const string PortalBaseUrlKey = "TechWise360.PortalBaseUrl";
    const string DefaultPortalBaseUrl = "https://techwise360-web-portal.pages.dev";

    public static TechWisePortalClient Instance { get; private set; }
    public static string PortalBaseUrl
    {
        get => NormalizeBaseUrl(PlayerPrefs.GetString(PortalBaseUrlKey, DefaultPortalBaseUrl));
        set
        {
            PlayerPrefs.SetString(PortalBaseUrlKey, NormalizeBaseUrl(value));
            PlayerPrefs.Save();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        EnsureInstance();
    }

    public static TechWisePortalClient EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<TechWisePortalClient>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        var clientObject = new GameObject("TechWise Portal Client");
        DontDestroyOnLoad(clientObject);
        Instance = clientObject.AddComponent<TechWisePortalClient>();
        return Instance;
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
    }

    public void Login(string identifier, string password, Action<bool, string> callback)
    {
        StartCoroutine(LoginCoroutine(identifier, password, callback));
    }

    public IEnumerator LoginCoroutine(string identifier, string password, Action<bool, string> callback)
    {
        var body = new TechWiseLoginRequest
        {
            identifier = identifier ?? string.Empty,
            password = password ?? string.Empty,
        };

        yield return SendJson("/api/login", body, string.Empty, (ok, message, response) =>
        {
            if (!ok)
            {
                callback?.Invoke(false, message);
                return;
            }

            var parsed = JsonUtility.FromJson<TechWiseLoginResponse>(response);
            if (parsed?.session == null || parsed.profile == null || parsed.profile.role != "student")
            {
                callback?.Invoke(false, "Please log in with an approved student account.");
                return;
            }

            TechWiseSessionStore.Save(parsed.session, parsed.profile);
            callback?.Invoke(true, $"Logged in as {parsed.profile.full_name}");
            TechWiseOfflineAttemptQueue.SyncNow();
        });
    }

    public IEnumerator EnsureFreshSessionCoroutine(Action<bool, string> callback)
    {
        if (!TechWiseSessionStore.HasSession)
        {
            callback?.Invoke(false, "Please log in first.");
            yield break;
        }

        if (!TechWiseSessionStore.SessionNeedsRefresh())
        {
            callback?.Invoke(true, string.Empty);
            yield break;
        }

        var session = TechWiseSessionStore.GetSession();
        if (session == null || string.IsNullOrWhiteSpace(session.refresh_token))
        {
            callback?.Invoke(false, "Session expired. Please log in again.");
            yield break;
        }

        var body = new TechWiseRefreshRequest { refresh_token = session.refresh_token };
        yield return SendJson("/api/auth/refresh", body, string.Empty, (ok, message, response) =>
        {
            if (!ok)
            {
                callback?.Invoke(false, message);
                return;
            }

            var parsed = JsonUtility.FromJson<TechWiseLoginResponse>(response);
            if (parsed?.session == null || parsed.profile == null)
            {
                callback?.Invoke(false, "Unable to refresh session.");
                return;
            }

            TechWiseSessionStore.Save(parsed.session, parsed.profile);
            callback?.Invoke(true, string.Empty);
        });
    }

    public IEnumerator LoadStudentLeaderboardCoroutine(Action<bool, string, TechWiseStudentLeaderboardResponse> callback)
    {
        var refreshOk = false;
        var refreshMessage = string.Empty;
        yield return EnsureFreshSessionCoroutine((ok, message) =>
        {
            refreshOk = ok;
            refreshMessage = message;
        });

        if (!refreshOk)
        {
            callback?.Invoke(false, refreshMessage, null);
            yield break;
        }

        var session = TechWiseSessionStore.GetSession();
        yield return SendGet("/api/student/leaderboard", session.access_token, (ok, message, response) =>
        {
            if (!ok)
            {
                callback?.Invoke(false, message, null);
                return;
            }

            var parsed = JsonUtility.FromJson<TechWiseStudentLeaderboardResponse>(response);
            callback?.Invoke(true, string.Empty, parsed);
        });
    }

    public IEnumerator PostAttemptCoroutine(TechWiseVrAttemptPayload payload, Action<bool, string> callback)
    {
        var refreshOk = false;
        var refreshMessage = string.Empty;
        yield return EnsureFreshSessionCoroutine((ok, message) =>
        {
            refreshOk = ok;
            refreshMessage = message;
        });

        if (!refreshOk)
        {
            callback?.Invoke(false, refreshMessage);
            yield break;
        }

        var session = TechWiseSessionStore.GetSession();
        yield return SendJson("/api/student/vr-attempts", payload, session.access_token, (ok, message, _) =>
        {
            callback?.Invoke(ok, message);
        });
    }

    IEnumerator SendGet(string path, string bearerToken, Action<bool, string, string> callback)
    {
        using (var request = UnityWebRequest.Get(PortalBaseUrl + path))
        {
            ApplyHeaders(request, bearerToken);
            yield return request.SendWebRequest();
            CompleteRequest(request, callback);
        }
    }

    IEnumerator SendJson<T>(string path, T body, string bearerToken, Action<bool, string, string> callback)
    {
        var json = JsonUtility.ToJson(body);
        using (var request = new UnityWebRequest(PortalBaseUrl + path, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            ApplyHeaders(request, bearerToken);
            yield return request.SendWebRequest();
            CompleteRequest(request, callback);
        }
    }

    static void ApplyHeaders(UnityWebRequest request, string bearerToken)
    {
        request.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
    }

    static void CompleteRequest(UnityWebRequest request, Action<bool, string, string> callback)
    {
        var response = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        var ok = request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300;
        if (ok)
        {
            callback?.Invoke(true, string.Empty, response);
            return;
        }

        var message = request.error;
        if (!string.IsNullOrWhiteSpace(response))
        {
            try
            {
                var apiError = JsonUtility.FromJson<TechWiseApiError>(response);
                if (!string.IsNullOrWhiteSpace(apiError?.error))
                    message = apiError.error;
            }
            catch
            {
                message = response;
            }
        }

        callback?.Invoke(false, string.IsNullOrWhiteSpace(message) ? "Network request failed." : message, response);
    }

    static string NormalizeBaseUrl(string value)
    {
        var url = string.IsNullOrWhiteSpace(value) ? DefaultPortalBaseUrl : value.Trim();
        return url.EndsWith("/") ? url.TrimEnd('/') : url;
    }
}
