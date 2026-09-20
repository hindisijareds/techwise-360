using System;
using UnityEngine;

[Serializable]
public sealed class TechWisePortalSession
{
    public string access_token;
    public string refresh_token;
    public int expires_in;
    public string token_type;
    public string expires_at;
}

[Serializable]
public sealed class TechWisePortalProfile
{
    public string id;
    public string role;
    public string status;
    public string username;
    public string email;
    public string full_name;
    public string first_name;
    public string last_name;
    public string grade_level;
    public string section;
    public string section_id, academic_year_id, academic_year, quarter_id, term, enrollment_id;
    public string name => !string.IsNullOrEmpty(full_name) ? full_name : username;
}

public static class TechWiseSessionStore
{
    const string SessionKey = "TechWise360.PortalSession";
    const string ProfileKey = "TechWise360.PortalProfile";
    static TechWisePortalSession currentSession;
    static TechWisePortalProfile currentProfile;
    static string sessionOrigin;

    static TechWiseSessionStore()
    {
        // Credentials from older builds must not remain in plaintext preferences.
        PlayerPrefs.DeleteKey(SessionKey);
        PlayerPrefs.DeleteKey(ProfileKey);
        PlayerPrefs.Save();
    }

    public static bool HasSession => !string.IsNullOrWhiteSpace(GetSession()?.access_token);
    public static bool HasRefreshToken => !string.IsNullOrWhiteSpace(GetSession()?.refresh_token);

    public static TechWisePortalSession GetSession()
    {
        return sessionOrigin == TechWisePortalClient.PortalBaseUrl ? currentSession : null;
    }

    public static TechWisePortalProfile GetProfile()
    {
        return sessionOrigin == TechWisePortalClient.PortalBaseUrl ? currentProfile : null;
    }

    public static void Save(TechWisePortalSession session, TechWisePortalProfile profile)
    {
        if (session != null)
        {
            if (string.IsNullOrWhiteSpace(session.expires_at))
            {
                var seconds = Mathf.Max(60, session.expires_in > 0 ? session.expires_in : 3600);
                session.expires_at = DateTime.UtcNow.AddSeconds(seconds - 30).ToString("o");
            }

            currentSession = session;
            sessionOrigin = TechWisePortalClient.PortalBaseUrl;
        }

        if (profile != null)
            currentProfile = profile;

        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        currentSession = null;
        currentProfile = null;
        PlayerPrefs.DeleteKey(SessionKey);
        PlayerPrefs.DeleteKey(ProfileKey);
        PlayerPrefs.Save();
    }

    public static bool SessionNeedsRefresh()
    {
        var session = GetSession();
        if (session == null || string.IsNullOrWhiteSpace(session.access_token))
            return false;

        if (string.IsNullOrWhiteSpace(session.expires_at))
            return true;

        return !DateTime.TryParse(session.expires_at, out var expiresAt) ||
            DateTime.UtcNow >= expiresAt.ToUniversalTime().AddSeconds(-45);
    }
}
