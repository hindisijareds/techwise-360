using System;
using System.Collections;
using System.Reflection;
using Unity.Netcode;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DefaultExecutionOrder(-10000)]
public sealed class TechWisePracticeModeRuntime : MonoBehaviour
{
    const string MainMenuSceneName = "MainMenu";
    const string SingleplayerSceneName = "Singleplayer";
    const string PracticeSceneName = "Multiplayer";
    const float BoundsMinHeight = -2.5f;
    const float BoundsMaxHorizontalDistance = 40f;
    static readonly Vector3 FallbackSpawn = new(0f, 0f, 0f);

    static readonly string[] OnlineUiObjectNames =
    {
        "Network Manager VR Mutliplayer",
        "Offline Menu UI",
        "OfflinePanel",
        "Player Menu UI",
        "Player_Menu_UI",
        "Player_Appearance_UI",
        "Appearance Panel",
        "Appearance (Toggle)",
        "Lobby UI",
        "Room Info UI",
        "Room Info",
        "RoomsMenu",
        "Room Panels",
        "RoomOptionsPanel (Host)",
        "RoomOptionsPanel (Client)",
        "LeaveRoom Prompt",
        "LeaveRoom Button",
        "Public Rooms (Toggle)",
        "Current Room (Toggle)",
        "CreateRoom Button",
        "New Room Button",
        "RoomPrivacy Toggle",
        "RoomName Button",
        "QuickJoinLobby",
        "Creating Room",
        "Voice Chat Status",
        "Voice Input (Slider)",
        "Voice Output (Slider)",
    };

    static readonly string[] DormantOnlineComponentTypeNames =
    {
        "AuthenticationManager",
        "GreetingBoardUI",
        "LobbyManager",
        "LobbyUI",
        "LobbyListSlotUI",
        "NetworkManagerVRMultiplayer",
        "OfflineMenu",
        "PlayerOptions",
        "VoiceChatManager",
        "XRINetworkGameManager",
    };

    static readonly string[] TeleportMarkerObjectNames =
    {
        "Affordance Callout Teleport",
        "Teleport Anchor",
        "Teleport Indicator Visuals",
        "TeleportIndicatorVisual",
    };

    static readonly string[] OnboardingClipNameFragments =
    {
        "Intro AI",
        "After Create name",
        "Congratss",
        "spring-birds",
        "Chasing Comets",
        "Lofi",
        "Creating Room",
    };

    static readonly string[] OnboardingAudioParentNames =
    {
        "Lobby UI",
        "Offline Menu UI",
        "Player Menu UI",
        "Player_Menu_UI",
        "Room Info UI",
        "Intro AI",
        "Welcome Menu",
        "BGM",
    };

    float lastBoundsResetTime = -10f;
    Vector3 lastSafePlayerPosition = FallbackSpawn;
    Quaternion lastSafePlayerRotation = Quaternion.identity;
    bool hasLastSafePlayerPose;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (FindAnyObjectByType<TechWisePracticeModeRuntime>() != null)
            return;

        var runtimeObject = new GameObject("TechWise Practice Mode Runtime");
        DontDestroyOnLoad(runtimeObject);
        runtimeObject.AddComponent<TechWisePracticeModeRuntime>();
    }

    public static bool OnlineFlowDisabledForScene(string sceneName)
    {
        return IsLocalScene(sceneName);
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene());
        StartCoroutine(ApplyAfterDelay(SceneManager.GetActiveScene()));
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply(scene);
        StartCoroutine(ApplyAfterDelay(scene));
    }

    void Update()
    {
        ResetPlayerIfOutOfBounds(SceneManager.GetActiveScene());
    }

    IEnumerator ApplyAfterDelay(Scene scene)
    {
        yield return null;
        Apply(scene);
        yield return new WaitForSeconds(0.75f);
        Apply(scene);
    }

    static void Apply(Scene scene)
    {
        DisableNonessentialUiInteractors();
        ClearTeleportReticles();
        DisableTeleportMarkers();

        if (!OnlineFlowDisabledForScene(scene.name))
            return;

        DisableOnlineComponents();
        DisableNamedOnlineUi();
        DisableOnboardingAudio();
    }

    static bool IsLocalScene(string sceneName)
    {
        return sceneName == MainMenuSceneName || sceneName == SingleplayerSceneName || sceneName == PracticeSceneName;
    }

    static void DisableNonessentialUiInteractors()
    {
        foreach (var pokeInteractor in FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pokeInteractor != null)
                pokeInteractor.enableUIInteraction = false;
        }

        foreach (var gazeInteractor in FindObjectsByType<XRGazeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (gazeInteractor != null)
                gazeInteractor.enableUIInteraction = false;
        }
    }

    static void DisableOnlineComponents()
    {
        foreach (var networkObject in FindObjectsByType<NetworkObject>(FindObjectsInactive.Include))
            if (!networkObject.IsSpawned) networkObject.AutoObjectParentSync = false;

        foreach (var networkManager in FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (networkManager == null)
                continue;

            if (networkManager.IsListening)
                networkManager.Shutdown();

            networkManager.gameObject.SetActive(false);
        }

        foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null)
                continue;

            var typeName = behaviour.GetType().Name;
            if (Array.IndexOf(DormantOnlineComponentTypeNames, typeName) >= 0)
                behaviour.enabled = false;
        }
    }

    static void DisableNamedOnlineUi()
    {
        foreach (var obj in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (obj == null)
                continue;

            if (Array.IndexOf(OnlineUiObjectNames, obj.name) >= 0)
                obj.SetActive(false);
        }
    }

    static void DisableTeleportMarkers()
    {
        foreach (var obj in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (obj == null)
                continue;

            foreach (var markerName in TeleportMarkerObjectNames)
            {
                if (obj.name.Equals(markerName, StringComparison.Ordinal) ||
                    obj.name.StartsWith(markerName + " (", StringComparison.Ordinal))
                {
                    obj.SetActive(false);
                    break;
                }
            }
        }
    }

    static void ClearTeleportReticles()
    {
        foreach (var rayInteractor in FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rayInteractor == null)
                continue;

            ClearObjectReference(rayInteractor, "m_Reticle");
            ClearObjectReference(rayInteractor, "m_BlockedReticle");
            ClearObjectReference(rayInteractor, "reticle");
            ClearObjectReference(rayInteractor, "blockedReticle");
        }
    }

    static void ClearObjectReference(object target, string memberName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = target.GetType();

        var field = type.GetField(memberName, flags);
        if (field != null && typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
        {
            field.SetValue(target, null);
            return;
        }

        var property = type.GetProperty(memberName, flags);
        if (property != null && property.CanWrite && typeof(UnityEngine.Object).IsAssignableFrom(property.PropertyType))
            property.SetValue(target, null);
    }

    static void DisableOnboardingAudio()
    {
        foreach (var audioSource in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (audioSource == null)
                continue;

            if (!IsOnboardingAudio(audioSource))
                continue;

            audioSource.Stop();
            audioSource.playOnAwake = false;
            audioSource.mute = true;
            audioSource.enabled = false;
        }
    }

    static bool IsOnboardingAudio(AudioSource audioSource)
    {
        if (audioSource.clip != null)
        {
            foreach (var fragment in OnboardingClipNameFragments)
            {
                if (audioSource.clip.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        var current = audioSource.transform;
        while (current != null)
        {
            foreach (var parentName in OnboardingAudioParentNames)
            {
                if (current.name.Equals(parentName, StringComparison.Ordinal) ||
                    current.name.StartsWith(parentName + " (", StringComparison.Ordinal))
                    return true;
            }

            current = current.parent;
        }

        return false;
    }

    void ResetPlayerIfOutOfBounds(Scene scene)
    {
        if (!IsLocalScene(scene.name) || scene.name == MainMenuSceneName || Time.unscaledTime < lastBoundsResetTime + 0.5f)
            return;

        var xrOrigin = FindAnyObjectByType<XROrigin>();
        var playerTransform = xrOrigin != null ? xrOrigin.transform : Camera.main != null ? Camera.main.transform.root : null;
        if (playerTransform == null)
            return;

        var position = playerTransform.position;
        if (position.y >= BoundsMinHeight &&
            Mathf.Abs(position.x) <= BoundsMaxHorizontalDistance &&
            Mathf.Abs(position.z) <= BoundsMaxHorizontalDistance)
        {
            CacheLastSafePlayerPose(playerTransform);
            return;
        }

        lastBoundsResetTime = Time.unscaledTime;
        ResetPlayerTransform(
            playerTransform,
            hasLastSafePlayerPose ? lastSafePlayerPosition : FindSafeSpawn(),
            hasLastSafePlayerPose ? lastSafePlayerRotation : Quaternion.identity);
    }

    void CacheLastSafePlayerPose(Transform playerTransform)
    {
        var characterController = playerTransform.GetComponent<CharacterController>();
        if (characterController != null && !characterController.isGrounded)
            return;

        if (playerTransform.position.y < -0.05f || playerTransform.position.y > 2.5f)
            return;

        lastSafePlayerPosition = playerTransform.position;
        lastSafePlayerRotation = playerTransform.rotation;
        hasLastSafePlayerPose = true;
    }

    static Vector3 FindSafeSpawn()
    {
        foreach (var transformInScene in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (transformInScene != null && transformInScene.name == "ResetPosition")
                return new Vector3(transformInScene.position.x, 0f, transformInScene.position.z);
        }

        return FallbackSpawn;
    }

    static void ResetPlayerTransform(Transform playerTransform, Vector3 destination, Quaternion rotation)
    {
        var characterController = playerTransform.GetComponent<CharacterController>();
        var wasCharacterControllerEnabled = characterController != null && characterController.enabled;
        if (characterController != null)
            characterController.enabled = false;

        playerTransform.SetPositionAndRotation(destination, rotation);

        if (characterController != null)
            characterController.enabled = wasCharacterControllerEnabled;
    }
}
