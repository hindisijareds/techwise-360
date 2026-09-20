using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Content.Interaction;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using XRMultiplayer;

[DefaultExecutionOrder(-9500)]
public sealed class TechWiseComponentRecovery : MonoBehaviour
{
    const string SingleplayerSceneName = "Singleplayer";
    const string PracticeSceneName = "Multiplayer";
    const float ComponentMinHeight = -2.5f;
    const float ComponentMaxHorizontalDistance = 40f;

    static TechWiseComponentRecovery instance;
    public static event System.Action<string> ComponentRecovered;
    public static void RefreshTracking() => instance?.RefreshSceneRecords();
    internal static void TrackTutorialPart(XRGrabInteractable part)
    {
        if (TechWiseTutorialRuntime.InTutorial) instance?.TrackInteractable(part);
    }

    readonly Dictionary<XRGrabInteractable, ComponentRecord> recordsByInteractable = new();
    readonly Dictionary<Transform, ComponentRecord> recordsByTransform = new();
    readonly List<ComponentRecord> records = new();
    readonly List<XRLockSocketInteractor> sockets = new();

    Material socketHoverMaterial;
    ComponentRecord heldRecord;
    ComponentRecord lastGrabbedRecord;

    sealed class ComponentRecord
    {
        public XRGrabInteractable interactable;
        public NetworkPhysicsInteractable networkPhysicsInteractable;
        public Transform transform;
        public GameObject gameObject;
        public Rigidbody rigidbody;
        public Pose originalPose;
        public Vector3 originalScale;
        public bool hadEnabledRendererAtStart;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (instance != null || FindAnyObjectByType<TechWiseComponentRecovery>() != null)
            return;

        var runtimeObject = new GameObject("TechWise Component Recovery");
        DontDestroyOnLoad(runtimeObject);
        instance = runtimeObject.AddComponent<TechWiseComponentRecovery>();
    }

    public static bool ResetHeldRotation(Transform selectedTransform)
    {
        return ResetHeldRotation(selectedTransform, out _);
    }

    public static bool ResetHeldRotation(Transform selectedTransform, out Quaternion rotation)
    {
        rotation = Quaternion.identity;
        return instance != null && instance.TryResetRotation(selectedTransform, out rotation);
    }

    public static bool ResetHeldOrLast(Transform selectedTransform)
    {
        return instance != null && instance.TryResetHeldOrLast(selectedTransform);
    }

    void Awake()
    {
        instance = this;
        socketHoverMaterial = CreateSocketHoverMaterial();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshSceneRecords();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        ClearRecords();

        if (instance == this)
            instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSceneRecords();
    }

    void Update()
    {
        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
            return;

        MonitorRecords();
    }

    static bool IsGameplayScene(string sceneName)
    {
        return sceneName == SingleplayerSceneName || sceneName == PracticeSceneName;
    }

    void RefreshSceneRecords()
    {
        ClearRecords();

        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
            return;

        foreach (var interactable in FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Exclude))
            TrackInteractable(interactable);

        sockets.Clear();
        foreach (var socket in FindObjectsByType<XRLockSocketInteractor>(FindObjectsInactive.Exclude))
        {
            if (socket == null)
                continue;

            sockets.Add(socket);
            ConfigureSocketHover(socket);
        }
    }

    void TrackInteractable(XRGrabInteractable interactable)
    {
            if (!ShouldTrack(interactable) || recordsByInteractable.ContainsKey(interactable)) return;

            var record = new ComponentRecord
            {
                interactable = interactable,
                networkPhysicsInteractable = interactable.GetComponent<NetworkPhysicsInteractable>() ?? interactable.GetComponentInParent<NetworkPhysicsInteractable>(),
                transform = interactable.transform,
                gameObject = interactable.gameObject,
                rigidbody = interactable.GetComponent<Rigidbody>() ?? interactable.GetComponentInParent<Rigidbody>(),
                originalPose = new Pose(interactable.transform.position, interactable.transform.rotation),
                originalScale = interactable.transform.localScale,
                hadEnabledRendererAtStart = HasEnabledRenderer(interactable.transform),
            };

            records.Add(record);
            recordsByInteractable[interactable] = record;
            recordsByTransform[interactable.transform] = record;
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
    }

    static bool ShouldTrack(XRGrabInteractable interactable)
    {
        if (interactable == null || interactable.GetComponent<TechWiseFastener>() != null)
            return false;

        return interactable.GetComponent<IKeychain>() != null ||
            interactable.GetComponentInParent<IKeychain>() != null ||
            interactable.GetComponent<NetworkPhysicsInteractable>() != null ||
            interactable.GetComponentInParent<NetworkPhysicsInteractable>() != null;
    }

    void ClearRecords()
    {
        foreach (var record in records)
        {
            if (record.interactable == null)
                continue;

            record.interactable.selectEntered.RemoveListener(OnSelectEntered);
            record.interactable.selectExited.RemoveListener(OnSelectExited);
        }

        foreach (var socket in sockets)
        {
            if (socket == null)
                continue;

            socket.selectEntered.RemoveListener(OnSocketSelectEntered);
            socket.selectExited.RemoveListener(OnSocketSelectExited);
        }

        records.Clear();
        recordsByInteractable.Clear();
        recordsByTransform.Clear();
        sockets.Clear();
        heldRecord = null;
        lastGrabbedRecord = null;
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args == null || args.interactableObject == null)
            return;

        if (args.interactorObject is XRSocketInteractor)
            return;

        if (!recordsByTransform.TryGetValue(args.interactableObject.transform, out var record))
            return;

        heldRecord = record;
        lastGrabbedRecord = record;
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (args == null || args.interactableObject == null)
            return;

        if (!recordsByTransform.TryGetValue(args.interactableObject.transform, out var record))
            return;

        if (heldRecord == record && !IsHeldByNonSocketInteractor(record))
            heldRecord = null;

        if (TechWiseSimulationModeManager.IsDisassembly && args.interactorObject is not XRSocketInteractor)
            ReleaseDisassemblyPhysics(record);
    }

    void MonitorRecords()
    {
        if (TechWiseSimulationRuntime.Instance != null && TechWiseSimulationRuntime.Instance.ManipulationLocked) return;
        foreach (var record in records)
        {
            if (record == null || record.transform == null)
                continue;

            if (TechWiseDetailedAssemblyRuntime.Active && (TechWiseDetailedAssemblyRuntime.Instance.IsRetiredPaste(record.transform) || TechWiseDetailedAssemblyRuntime.Instance.IsConditionallyHidden(record.transform)))
                continue;

            if (IsSocketed(record))
                continue;

            if (NeedsRecovery(record))
                ResetRecord(record);
        }
    }

    static bool NeedsRecovery(ComponentRecord record)
    {
        if (record.gameObject != null && !record.gameObject.activeInHierarchy)
            return true;

        var position = record.transform.position;
        if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
            return true;

        return position.y < ComponentMinHeight ||
            Mathf.Abs(position.x) > ComponentMaxHorizontalDistance ||
            Mathf.Abs(position.z) > ComponentMaxHorizontalDistance ||
            record.hadEnabledRendererAtStart && !HasEnabledRenderer(record.transform);
    }

    static bool HasEnabledRenderer(Transform root)
    {
        if (root == null)
            return false;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(false))
        {
            if (renderer != null && renderer.enabled)
                return true;
        }

        return false;
    }

    bool TryResetRotation(Transform selectedTransform, out Quaternion rotation)
    {
        rotation = Quaternion.identity;
        var record = ResolveRecord(selectedTransform) ?? heldRecord;
        if (record == null || record.transform == null || IsSocketed(record))
            return false;

        rotation = record.originalPose.rotation;
        SetRotation(record, rotation);
        return true;
    }

    bool TryResetHeldOrLast(Transform selectedTransform)
    {
        if (TechWiseSimulationRuntime.Instance != null && TechWiseSimulationRuntime.Instance.ManipulationLocked) return false;
        var record = ResolveRecord(selectedTransform) ?? heldRecord ?? lastGrabbedRecord;
        if (record == null || IsSocketed(record))
            return false;

        ResetRecord(record);
        return true;
    }

    ComponentRecord ResolveRecord(Transform selectedTransform)
    {
        if (selectedTransform == null)
            return null;

        if (recordsByTransform.TryGetValue(selectedTransform, out var record))
            return record;

        var interactable = selectedTransform.GetComponent<XRGrabInteractable>() ?? selectedTransform.GetComponentInParent<XRGrabInteractable>();
        return interactable != null && recordsByInteractable.TryGetValue(interactable, out record) ? record : null;
    }

    void ResetRecord(ComponentRecord record)
    {
        if (record == null || record.transform == null)
            return;

        CancelSelection(record);

        if (record.gameObject != null && !record.gameObject.activeSelf)
            record.gameObject.SetActive(true);

        record.transform.SetPositionAndRotation(record.originalPose.position, record.originalPose.rotation);
        record.transform.localScale = record.originalScale;
        ResetPhysics(record);

        if (heldRecord == record)
            heldRecord = null;

        ComponentRecovered?.Invoke(TechWiseSimulationModeManager.ResolveStepId(record.transform));
    }

    static void SetRotation(ComponentRecord record, Quaternion rotation)
    {
        record.transform.rotation = rotation;

        if (record.rigidbody != null)
        {
            record.rigidbody.rotation = rotation;
            record.rigidbody.angularVelocity = Vector3.zero;
        }
    }

    static void ResetPhysics(ComponentRecord record)
    {
        if (record.rigidbody != null)
        {
            record.rigidbody.linearVelocity = Vector3.zero;
            record.rigidbody.angularVelocity = Vector3.zero;
            record.rigidbody.Sleep();
        }

        if (record.networkPhysicsInteractable != null && record.networkPhysicsInteractable.isActiveAndEnabled)
            record.networkPhysicsInteractable.ResetObjectPhysics();
    }

    static void ReleaseDisassemblyPhysics(ComponentRecord record)
    {
        if (record?.rigidbody == null || IsSocketed(record))
            return;

        record.rigidbody.isKinematic = false;
        record.rigidbody.useGravity = true;
        record.rigidbody.linearVelocity = Vector3.zero;
        record.rigidbody.angularVelocity = Vector3.zero;
        record.rigidbody.WakeUp();
    }

    static void CancelSelection(ComponentRecord record)
    {
        if (record.interactable == null || !record.interactable.isSelected || record.interactable.interactionManager == null)
            return;

        record.interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)record.interactable);
    }

    static bool IsSocketed(ComponentRecord record)
    {
        if (record.interactable == null || !record.interactable.isSelected)
            return false;

        foreach (var interactor in record.interactable.interactorsSelecting)
        {
            if (interactor is XRSocketInteractor)
                return true;
        }

        return false;
    }

    static bool IsHeldByNonSocketInteractor(ComponentRecord record)
    {
        if (record.interactable == null || !record.interactable.isSelected)
            return false;

        foreach (var interactor in record.interactable.interactorsSelecting)
        {
            if (interactor is not XRSocketInteractor)
                return true;
        }

        return false;
    }

    void ConfigureSocketHover(XRLockSocketInteractor socket)
    {
        socket.selectEntered.RemoveListener(OnSocketSelectEntered);
        socket.selectExited.RemoveListener(OnSocketSelectExited);
        socket.selectEntered.AddListener(OnSocketSelectEntered);
        socket.selectExited.AddListener(OnSocketSelectExited);

        socket.interactableHoverMeshMaterial = socketHoverMaterial;
        socket.interactableCantHoverMeshMaterial = null;
        socket.interactableHoverScale = 1.035f;
        SetSocketHoverVisible(socket, socket.interactablesSelected.Count == 0);
    }

    void OnSocketSelectEntered(SelectEnterEventArgs args)
    {
        if (args?.interactorObject is XRLockSocketInteractor socket)
            SetSocketHoverVisible(socket, false);
    }

    void OnSocketSelectExited(SelectExitEventArgs args)
    {
        if (args?.interactorObject is XRLockSocketInteractor socket)
            SetSocketHoverVisible(socket, true);
    }

    static void SetSocketHoverVisible(XRLockSocketInteractor socket, bool visible)
    {
        if (socket == null) return;
        visible = visible && TechWiseSimulationModeManager.IsPracticeMode &&
            !TechWiseSimulationModeManager.IsKnowledgeDisplay(socket.transform);
        if (socket != null && socket.showInteractableHoverMeshes != visible)
            socket.showInteractableHoverMeshes = visible;
    }

    static Material CreateSocketHoverMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color") ??
            Shader.Find("Sprites/Default") ??
            Shader.Find("Standard");

        var material = new Material(shader)
        {
            name = "TechWise Socket Hover Green",
            color = new Color(0.05f, 1f, 0.2f, 0.55f),
            renderQueue = 3000,
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", material.color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", material.color);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        return material;
    }
}
