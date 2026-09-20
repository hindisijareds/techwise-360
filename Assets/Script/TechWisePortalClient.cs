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

[Serializable] public sealed class TechWiseConnectionRequest { public string code; }
[Serializable] public sealed class TechWiseStationPairRequest { public string station_id; public bool claim = true; }
[Serializable]
public sealed class TechWiseStationStatusResponse
{
    public string status;
    public string station_id;
    public string message;
    public TechWisePortalProfile profile;
    public TechWisePortalProfile student;
    public TechWisePortalSession session;
    public TechWiseVrCompetition[] competitions;
}
[Serializable] public sealed class TechWiseStartRequest { public string attempt_id, simulation_type, competition_id; }
[Serializable] public sealed class TechWiseServerAttempt
{
    public string id, student_id, competition_id, simulation_type, academic_year_id, quarter_id, enrollment_id, section_id;
    public string grade_level, section_name, student_name, started_at, status;
    public TechWiseAssessmentScoringSettings scoring_configuration;
}
[Serializable] public sealed class TechWiseStartResponse { public TechWiseServerAttempt attempt; }
[Serializable] public sealed class TechWiseResultAcknowledgement
{
    public string status, attempt_id;
    public TechWiseServerAttempt attempt;
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
    public int schema_version;
    public double elapsed_seconds;
    public TechWiseAssessmentScoringSettings scoring_configuration;
    public TechWiseAssessmentScore scoring_breakdown;
    public TechWiseVrComponentResult[] component_results;
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
    public TechWiseVrMistakeDetail[] mistake_details;
    public string game_version;
    public string control_mode;
    public bool offline_queued;
    public string station_id;
    public string device_id;
    public bool headset_present_continuous;
    public int headset_unmount_count;
}

[Serializable]
public sealed class TechWiseVrMistakeDetail
{
    public string category;
    public string severity;
    public int score_penalty;
    public int order;
    public double elapsed_seconds;
    public string component_id;
    public string kind;
    public string attempted_step;
    public string expected_step;
    public string attempted_destination;
    public string expected_destination;
    public string explanation;
    public string correction;
    public int occurred_at_seconds;
}

[Serializable]
public sealed class TechWiseVrComponentResult
{
    public string component_id;
    public string component;
    public string step;
    public bool complete;
    public string issue;
    public string explanation;
    public string correction;
}

[DefaultExecutionOrder(-12000)]
public sealed class TechWisePortalClient : MonoBehaviour
{
    public const string PortalBaseUrlKey = "TechWise360.PortalBaseUrl";
    const string DefaultPortalBaseUrl = "https://techwise360-web-portal.pages.dev";

    public static TechWisePortalClient Instance { get; private set; }
    bool refreshing;
#if UNITY_EDITOR
    public static Func<string, string, string, Action<bool, string, string>, IEnumerator> VerificationTransport;
#endif
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
        if (Application.isPlaying) DontDestroyOnLoad(clientObject);
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
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
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

            var parsed = ParseResponse<TechWiseLoginResponse>(response);
            if (parsed?.session == null || parsed.profile == null || parsed.profile.role != "student" || parsed.profile.status != "approved")
            {
                callback?.Invoke(false, "Please log in with an approved student account.");
                return;
            }

            TechWiseSessionStore.Save(parsed.session, parsed.profile);
            callback?.Invoke(true, $"Logged in as {parsed.profile.full_name}");
            TechWiseOfflineAttemptQueue.SyncNow();
        });
    }

    public IEnumerator ExchangeConnectionCodeCoroutine(string code, Action<bool, string> callback)
    {
        yield return SendJson("/api/vr/exchange", new TechWiseConnectionRequest { code = code.Trim() }, "", (ok, message, response) =>
        {
            if (!ok) { callback?.Invoke(false, message); return; }
            var parsed = ParseResponse<TechWiseLoginResponse>(response);
            if (parsed?.session == null || parsed.profile?.role != "student" || parsed.profile.status != "approved")
            { callback?.Invoke(false, "Unable to connect this student account."); return; }
            TechWiseSessionStore.Save(parsed.session, parsed.profile);
            callback?.Invoke(true, $"Connected as {parsed.profile.full_name}");
            TechWiseOfflineAttemptQueue.SyncNow();
        });
    }

    public IEnumerator CheckStationStatusCoroutine(string stationId, Action<bool, string, TechWiseStationStatusResponse> callback)
    {
        var body = new TechWiseStationPairRequest
        {
            station_id = string.IsNullOrWhiteSpace(stationId) ? "station-01" : stationId.Trim(),
            claim = true
        };

        yield return SendJson("/api/vr/station-status", body, string.Empty, (ok, message, response) =>
        {
            if (!ok)
            {
                callback?.Invoke(false, message, null);
                return;
            }

            var parsed = ParseResponse<TechWiseStationStatusResponse>(response);
            if (parsed == null)
            {
                callback?.Invoke(false, "Invalid station status response.", null);
                return;
            }

            var student = parsed.student ?? parsed.profile;
            if (parsed.status == "paired" && parsed.session != null && student != null)
            {
                TechWiseSessionStore.Save(parsed.session, student);
                TechWiseOfflineAttemptQueue.SyncNow();
            }

            callback?.Invoke(true, string.Empty, parsed);
        });
    }

    public IEnumerator UnpairStationCoroutine(string stationId, Action<bool, string> callback)
    {
        var body = new TechWiseStationPairRequest
        {
            station_id = string.IsNullOrWhiteSpace(stationId) ? "station-01" : stationId.Trim()
        };

        yield return SendJson("/api/vr/station-unpair", body, string.Empty, (ok, message, response) =>
        {
            callback?.Invoke(ok, message);
        });
    }

    public IEnumerator StartAssessmentCoroutine(TechWiseStartRequest body, Action<bool, string, TechWiseServerAttempt> callback)
    {
        var owner = TechWiseSessionStore.GetProfile()?.id;
        var origin = PortalBaseUrl;
        bool fresh = false; string error = "";
        yield return EnsureFreshSessionCoroutine((ok, message) => { fresh = ok; error = message; });
        if (!fresh || owner != TechWiseSessionStore.GetProfile()?.id || origin != PortalBaseUrl)
        { callback(false, fresh ? "Account changed. Please start again." : error, null); yield break; }
        yield return SendJson("/api/vr/start", body, TechWiseSessionStore.GetSession().access_token, (ok, message, response) =>
        {
            if (!ok) { callback(false, message, null); return; }
            var attempt = ParseResponse<TechWiseStartResponse>(response)?.attempt;
            if (attempt == null || attempt.student_id != owner || !SameAttempt(attempt.id, body.attempt_id) || attempt.status != "in_progress")
            { callback(false, "The server did not confirm a new assessment.", null); return; }
            callback(true, "", attempt);
        });
    }

    public static bool SameAttempt(string a, string b) => Guid.TryParse(a, out var first) && Guid.TryParse(b, out var second) && first == second;

    public IEnumerator EnsureFreshSessionCoroutine(Action<bool, string> callback)
    {
        while (refreshing) yield return null;
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
        refreshing = true;
        try
        {
        yield return SendJson("/api/auth/refresh", body, string.Empty, (ok, message, response) =>
        {
            if (!ok)
            {
                callback?.Invoke(false, message);
                return;
            }

            var parsed = ParseResponse<TechWiseLoginResponse>(response);
            if (parsed?.session == null || parsed.profile?.role != "student" || parsed.profile.status != "approved" || TechWiseSessionStore.GetSession() != session)
            {
                callback?.Invoke(false, "Unable to refresh session.");
                return;
            }

            TechWiseSessionStore.Save(parsed.session, parsed.profile);
            callback?.Invoke(true, string.Empty);
        });
        }
        finally { refreshing = false; }
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
        yield return SendGet("/api/vr/context", session.access_token, (ok, message, response) =>
        {
            if (!ok)
            {
                callback?.Invoke(false, message, null);
                return;
            }

            var parsed = ParseResponse<TechWiseStudentLeaderboardResponse>(response);
            var context = ParseResponse<TechWiseLoginResponse>(response);
            if (parsed == null || context?.profile == null) { callback?.Invoke(false, "Student context could not be read.", null); return; }
            if (context?.profile != null) TechWiseSessionStore.Save(session, context.profile);
            callback?.Invoke(true, string.Empty, parsed);
        });
    }

    public IEnumerator PostAttemptCoroutine(TechWiseVrAttemptPayload payload, Action<bool, string> callback, string expectedStudent = null, string expectedOrigin = null)
    {
        var refreshOk = false;
        var refreshMessage = string.Empty;
        yield return EnsureFreshSessionCoroutine((ok, message) =>
        {
            refreshOk = ok;
            refreshMessage = message;
        });

        if (!refreshOk || string.IsNullOrEmpty(expectedStudent) || expectedStudent != TechWiseSessionStore.GetProfile()?.id || expectedOrigin != PortalBaseUrl)
        {
            callback?.Invoke(false, refreshOk ? "Reconnect the student account that started this attempt." : refreshMessage);
            yield break;
        }

        var session = TechWiseSessionStore.GetSession();
        yield return SendJson("/api/student/vr-attempts", payload, session.access_token, (ok, message, response) =>
        {
            if (!ok) { callback?.Invoke(false, message); return; }
            var ack = ParseResponse<TechWiseResultAcknowledgement>(response);
            var confirmed = ack?.status == "synced" && SameAttempt(ack.attempt_id, payload.metadata.local_attempt_id) && ack.attempt?.student_id == expectedStudent;
            callback?.Invoke(confirmed, confirmed ? "" : "Server confirmation was missing. Result remains pending.");
        });
    }

    IEnumerator SendGet(string path, string bearerToken, Action<bool, string, string> callback)
    {
        var origin = PortalBaseUrl;
        using (var request = UnityWebRequest.Get(origin + path))
        {
            ApplyHeaders(request, bearerToken);
            yield return request.SendWebRequest();
            if (origin != PortalBaseUrl) callback(false, "Portal changed. Reconnect your account.", "");
            else CompleteRequest(request, callback);
        }
    }

    IEnumerator SendJson<T>(string path, T body, string bearerToken, Action<bool, string, string> callback)
    {
        var json = JsonUtility.ToJson(body);
#if UNITY_EDITOR
        if (VerificationTransport != null) { yield return VerificationTransport(path, json, bearerToken, callback); yield break; }
#endif
        var origin = PortalBaseUrl;
        using (var request = new UnityWebRequest(origin + path, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            ApplyHeaders(request, bearerToken);
            yield return request.SendWebRequest();
            if (origin != PortalBaseUrl) callback(false, "Portal changed. Reconnect your account.", "");
            else CompleteRequest(request, callback);
        }
    }

    static void ApplyHeaders(UnityWebRequest request, string bearerToken)
    {
        request.timeout = 20;
        request.redirectLimit = 0;
        request.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrWhiteSpace(bearerToken))
            request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
    }

    static T ParseResponse<T>(string json) where T : class
    {
        try { return JsonUtility.FromJson<T>(json); }
        catch (ArgumentException) { return null; }
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
                message = "The server returned an unreadable response. Please retry.";
            }
        }

        callback?.Invoke(false, string.IsNullOrWhiteSpace(message) ? "Network request failed." : message, response);
    }

    static string NormalizeBaseUrl(string value)
    {
        var url = string.IsNullOrWhiteSpace(value) ? DefaultPortalBaseUrl : value.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            return DefaultPortalBaseUrl;
        return url.EndsWith("/") ? url.TrimEnd('/') : url;
    }
}
