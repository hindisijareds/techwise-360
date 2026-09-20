using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

[DisallowMultipleComponent]
public class TechWiseDesktopController : MonoBehaviour
{
    const string DesktopRayName = "TechWise Desktop Ray Interactor";
    const string DesktopAttachName = "TechWise Desktop Attach";
    const string CrosshairCanvasName = "TechWise Desktop Crosshair";
    const string CrosshairHorizontalName = "Crosshair Horizontal";
    const string CrosshairVerticalName = "Crosshair Vertical";
    const string XRDeviceSimulatorName = "XR Device Simulator";
    const string XRInteractionSimulatorName = "XR Interaction Simulator";
    const float RotationResetSettleDuration = 0.35f;
    const float DesktopSafeMinY = -0.05f;
    const float DesktopSafeMaxY = 2.5f;
    const float DesktopRestoreMinY = -0.35f;
    const float DesktopMaxSingleFrameDrop = 0.6f;

    [SerializeField] float moveSpeed = 2.4f;
    [SerializeField] float mouseSensitivity = 0.12f;
    [SerializeField] float gravity = -9.81f;
    [SerializeField] float maxRayDistance = 6f;
    [SerializeField] float holdDistance = 2f;
    [SerializeField] float minHoldDistance = 0.6f;
    [SerializeField] float maxHoldDistance = 6f;
    [SerializeField] float holdDistanceScrollSensitivity = 0.35f;
    [SerializeField] float rotateSensitivity = 0.3f;
    [SerializeField] float scrollRotateSensitivity = 12f;
    [SerializeField] Key cycleRotateAxisKey = Key.R;
    [SerializeField] Key distanceModifierKey = Key.LeftShift;
    [SerializeField] Key resetHeldRotationKey = Key.F;
    [SerializeField] Key resetHeldComponentKey = Key.Backspace;
    [SerializeField] bool desktopModeOverridesVrWhenHeadsetPresent;
    [SerializeField] bool showCrosshair = true;
    [SerializeField] float crosshairSize = 18f;
    [SerializeField] Color crosshairColor = new(1f, 1f, 1f, 0.88f);
    [SerializeField] bool disablePokeUiInteractionForRegistrationHeadroom = true;

    XROrigin xrOrigin;
    Camera xrCamera;
    CharacterController characterController;
    XRRayInteractor rayInteractor;
    GameObject rayObject;
    Transform attachTransform;
    readonly Dictionary<Behaviour, bool> isolatedBehaviourStates = new();
    readonly List<Behaviour> isolatedBehaviours = new();
    readonly Dictionary<GameObject, bool> simulatorObjectStates = new();
    Canvas crosshairCanvas;
    Image crosshairHorizontal;
    Image crosshairVertical;

    float pitch;
    float verticalVelocity;
    float simulatorRefreshTimer;
    float currentHoldDistance;
    float heldRotationLockUntil;
    int rotateAxisIndex;
    Quaternion heldRotation;
    Transform selectedManipulationTarget;
    Transform heldRotationLockTarget;
    XRGrabInteractable selectedGrabInteractable;
    bool selectedGrabTrackRotation;
    bool selectedGrabTrackRotationCached;
    Vector3 lastSafeDesktopPosition;
    Quaternion lastSafeDesktopRotation = Quaternion.identity;
    bool hasLastSafeDesktopPose;
    bool desktopActive;
    bool cursorLocked;
    bool selectHeld;

    void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        enabled = false;
        SetDesktopActive(false);
        return;
#endif
        ResolveReferences();
        currentHoldDistance = Mathf.Clamp(holdDistance, minHoldDistance, maxHoldDistance);
        LimitVrUiRegistration();
        EnsureRayInteractor();
        EnsureCrosshair();
        SetDesktopActive(false);
        ApplyActiveState();
    }

    void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        ApplyActiveState();
    }

    void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SetDesktopActive(false);
    }

    void Update()
    {
        ApplyActiveState();
        RefreshRuntimeModeObjects();

        if (!desktopActive)
            return;

        if (TechWisePauseSession.Active)
        {
            selectHeld = false;
            QueueSelectState(false);
            UnlockCursor();
            return;
        }

        HandleCursorLock();
        HandleLook();
        HandleMove();
        QueueSelectInput();
        HandleHeldObjectManipulation();
    }

    void LateUpdate()
    {
        if (!desktopActive)
            return;

        if (selectedManipulationTarget != null &&
            selectedGrabTrackRotationCached &&
            rayInteractor != null &&
            rayInteractor.interactablesSelected.Count > 0)
        {
            ForceSelectedRotation(selectedManipulationTarget, heldRotation);
            return;
        }

        if (heldRotationLockTarget == null || Time.unscaledTime > heldRotationLockUntil)
            return;

        if (rayInteractor == null || rayInteractor.interactablesSelected.Count == 0)
        {
            heldRotationLockTarget = null;
            return;
        }

        ForceSelectedRotation(heldRotationLockTarget, heldRotation);
    }

    void OnActiveSceneChanged(Scene previous, Scene next)
    {
        ApplyActiveState();
    }

    void ResolveReferences()
    {
        if (xrOrigin == null)
            xrOrigin = GetComponent<XROrigin>() ?? GetComponentInParent<XROrigin>() ?? FindAnyObjectByType<XROrigin>();

        if (xrCamera == null)
            xrCamera = xrOrigin != null && xrOrigin.Camera != null ? xrOrigin.Camera : Camera.main;

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>() ?? GetComponentInParent<CharacterController>();
            if (characterController == null && xrOrigin != null)
                characterController = xrOrigin.GetComponent<CharacterController>();
            if (characterController == null)
                characterController = FindAnyObjectByType<CharacterController>();
        }
    }

    void LimitVrUiRegistration()
    {
        if (!disablePokeUiInteractionForRegistrationHeadroom || xrOrigin == null)
            return;

        foreach (var pokeInteractor in xrOrigin.GetComponentsInChildren<XRPokeInteractor>(true))
        {
            if (pokeInteractor != null)
                pokeInteractor.enableUIInteraction = false;
        }
    }

    void EnsureRayInteractor()
    {
        if (xrCamera == null)
            return;

        var existing = xrCamera.transform.Find(DesktopRayName);
        rayObject = existing != null ? existing.gameObject : new GameObject(DesktopRayName);
        rayObject.SetActive(false);
        rayObject.transform.SetParent(xrCamera.transform, false);
        rayObject.transform.localPosition = Vector3.zero;
        rayObject.transform.localRotation = Quaternion.identity;

        rayInteractor = rayObject.GetComponent<XRRayInteractor>();
        if (rayInteractor == null)
            rayInteractor = rayObject.AddComponent<XRRayInteractor>();

        rayInteractor.lineType = XRRayInteractor.LineType.StraightLine;
        rayInteractor.interactionLayers = ~0;
        rayInteractor.maxRaycastDistance = maxRayDistance;
        rayInteractor.hitDetectionType = XRRayInteractor.HitDetectionType.Raycast;
        rayInteractor.enableUIInteraction = false;
        rayInteractor.keepSelectedTargetValid = true;
        rayInteractor.selectActionTrigger = XRBaseInputInteractor.InputTriggerType.State;
        rayInteractor.selectInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
        rayInteractor.rayOriginTransform = rayObject.transform;

        attachTransform = rayObject.transform.Find(DesktopAttachName);
        if (attachTransform == null)
        {
            var attachObject = new GameObject(DesktopAttachName);
            attachObject.transform.SetParent(rayObject.transform, false);
            attachTransform = attachObject.transform;
        }

        UpdateAttachDistance();
        attachTransform.localRotation = Quaternion.identity;
        rayInteractor.attachTransform = attachTransform;

        var lineVisual = rayObject.GetComponent<XRInteractorLineVisual>();
        if (lineVisual != null)
            lineVisual.enabled = false;

        rayInteractor.enabled = false;
    }

    void EnsureCrosshair()
    {
        if (crosshairCanvas != null)
            return;

        var canvasObject = new GameObject(CrosshairCanvasName, typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        crosshairCanvas = canvasObject.GetComponent<Canvas>();
        crosshairCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        crosshairCanvas.sortingOrder = 1000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        crosshairHorizontal = CreateCrosshairLine(CrosshairHorizontalName, canvasObject.transform);
        crosshairVertical = CreateCrosshairLine(CrosshairVerticalName, canvasObject.transform);
        SetCrosshairVisible(false);
    }

    Image CreateCrosshairLine(string lineName, Transform parent)
    {
        var lineObject = new GameObject(lineName, typeof(RectTransform), typeof(Image));
        lineObject.transform.SetParent(parent, false);

        var rectTransform = lineObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = lineName == CrosshairHorizontalName
            ? new Vector2(crosshairSize, 2f)
            : new Vector2(2f, crosshairSize);

        var image = lineObject.GetComponent<Image>();
        image.color = crosshairColor;
        image.raycastTarget = false;
        return image;
    }

    void ApplyActiveState()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (desktopActive)
            SetDesktopActive(false);
        enabled = false;
        return;
#endif
        ResolveReferences();
        var shouldBeActive = false; // Enforce VR mode only for Meta Quest 2
        if (shouldBeActive == desktopActive)
            return;

        SetDesktopActive(shouldBeActive);
    }

    void SetDesktopActive(bool active)
    {
        active = false;
        if (active)
            ResolveReferences();

        desktopActive = active;
        selectHeld = false;

        QueueSelectState(false);

        if (rayInteractor != null)
            rayInteractor.enabled = active;

        if (rayObject != null)
            rayObject.SetActive(active);

        SetCrosshairVisible(active && showCrosshair);

        SetVrInputIsolation(active);
        SetSimulatorObjectsActive(!active);

        if (active)
        {
            LockCursor();
            SyncPitchFromCamera();
        }
        else
        {
            UnlockCursor();
            RestoreSelectedGrabTrackRotation();
            selectedManipulationTarget = null;
            heldRotationLockTarget = null;
        }
    }

    static bool IsGameplayScene(string sceneName)
    {
        return sceneName == "Singleplayer" || sceneName == "Multiplayer";
    }

    void HandleCursorLock()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            selectHeld = false;
            QueueSelectState(false);
            UnlockCursor();
            return;
        }

        if (!cursorLocked && mouse != null && mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi())
            LockCursor();
    }

    void HandleLook()
    {
        if (!cursorLocked || xrCamera == null || Mouse.current == null)
            return;

        if (selectedManipulationTarget != null && Mouse.current.rightButton.isPressed) return;

        var delta = Mouse.current.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(Vector3.up, delta.x, Space.World);

        pitch = Mathf.Clamp(pitch - delta.y, -80f, 80f);
        xrCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMove()
    {
        if (!cursorLocked || characterController == null || Keyboard.current == null)
            return;

        CacheDesktopSafePose();

        var keyboard = Keyboard.current;
        var input = Vector2.zero;

        if (keyboard.wKey.isPressed)
            input.y += 1f;
        if (keyboard.sKey.isPressed)
            input.y -= 1f;
        if (keyboard.dKey.isPressed)
            input.x += 1f;
        if (keyboard.aKey.isPressed)
            input.x -= 1f;

        input = Vector2.ClampMagnitude(input, 1f);

        var forward = Vector3.ProjectOnPlane(xrCamera != null ? xrCamera.transform.forward : transform.forward, Vector3.up).normalized;
        var right = Vector3.ProjectOnPlane(xrCamera != null ? xrCamera.transform.right : transform.right, Vector3.up).normalized;
        var move = (forward * input.y + right * input.x) * moveSpeed;

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -1f;
        else
            verticalVelocity += gravity * Time.deltaTime;

        var delta = move * Time.deltaTime;
        var horizontalDelta = ResolveDesktopHorizontalMove(new Vector3(delta.x, 0f, delta.z));
        var verticalDelta = Vector3.up * (verticalVelocity * Time.deltaTime);
        var beforeMove = transform.position;

        characterController.Move(horizontalDelta + verticalDelta);

        if (ShouldRestoreDesktopPosition(beforeMove))
            RestoreDesktopSafePose();
        else
            CacheDesktopSafePose();
    }

    void QueueSelectInput()
    {
        if (Mouse.current == null)
        {
            selectHeld = false;
            QueueSelectState(false);
            return;
        }

        if (cursorLocked &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            TechWiseCrosshairUiBridge.TryPressCenteredButton(xrCamera))
        {
            selectHeld = false;
            QueueSelectState(false);
            return;
        }

        if (IsPointerOverUi())
        {
            selectHeld = false;
            QueueSelectState(false);
            return;
        }

        selectHeld = cursorLocked && Mouse.current.leftButton.isPressed;
        QueueSelectState(selectHeld);
    }

    void QueueSelectState(bool performed)
    {
        if (rayInteractor == null)
            return;

        rayInteractor.selectInput.QueueManualState(performed, performed ? 1f : 0f);
    }

    Vector3 ResolveDesktopHorizontalMove(Vector3 horizontalDelta)
    {
        if (horizontalDelta.sqrMagnitude <= 0.000001f || !TryGetCharacterCapsule(out var top, out var bottom, out var radius))
            return horizontalDelta;

        var direction = horizontalDelta.normalized;
        var distance = horizontalDelta.magnitude + 0.04f;
        var hits = Physics.CapsuleCastAll(
            top,
            bottom,
            radius,
            direction,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        RaycastHit? nearestHit = null;
        foreach (var hit in hits)
        {
            if (hit.collider == null || ShouldIgnoreDesktopMovementHit(hit.collider))
                continue;

            if (!nearestHit.HasValue || hit.distance < nearestHit.Value.distance)
                nearestHit = hit;
        }

        if (!nearestHit.HasValue)
            return horizontalDelta;

        var projected = Vector3.ProjectOnPlane(horizontalDelta, nearestHit.Value.normal);
        return Vector3.Dot(projected, horizontalDelta) > 0f ? projected : Vector3.zero;
    }

    bool TryGetCharacterCapsule(out Vector3 top, out Vector3 bottom, out float radius)
    {
        top = Vector3.zero;
        bottom = Vector3.zero;
        radius = 0f;

        if (characterController == null)
            return false;

        var controllerTransform = characterController.transform;
        var scaledRadius = characterController.radius * Mathf.Max(
            Mathf.Abs(controllerTransform.lossyScale.x),
            Mathf.Abs(controllerTransform.lossyScale.z));
        var scaledHeight = Mathf.Max(
            characterController.height * Mathf.Abs(controllerTransform.lossyScale.y),
            scaledRadius * 2f);
        var worldCenter = controllerTransform.TransformPoint(characterController.center);
        var halfSegment = Mathf.Max(0f, scaledHeight * 0.5f - scaledRadius);
        var up = controllerTransform.up;

        top = worldCenter + up * halfSegment;
        bottom = worldCenter - up * halfSegment;
        radius = Mathf.Max(0.05f, scaledRadius - characterController.skinWidth);
        return true;
    }

    bool ShouldIgnoreDesktopMovementHit(Collider hitCollider)
    {
        if (hitCollider == null || hitCollider.isTrigger)
            return true;

        if (characterController != null && hitCollider == characterController)
            return true;

        if (selectedManipulationTarget == null)
            return false;

        return hitCollider.transform.IsChildOf(selectedManipulationTarget) ||
            selectedManipulationTarget.IsChildOf(hitCollider.transform);
    }

    void CacheDesktopSafePose()
    {
        if (characterController == null || !characterController.enabled || !characterController.isGrounded)
            return;

        var position = transform.position;
        if (position.y < DesktopSafeMinY || position.y > DesktopSafeMaxY)
            return;

        lastSafeDesktopPosition = position;
        lastSafeDesktopRotation = transform.rotation;
        hasLastSafeDesktopPose = true;
    }

    bool ShouldRestoreDesktopPosition(Vector3 beforeMove)
    {
        if (!hasLastSafeDesktopPose)
            return false;

        var position = transform.position;
        return position.y < DesktopRestoreMinY || beforeMove.y - position.y > DesktopMaxSingleFrameDrop;
    }

    void RestoreDesktopSafePose()
    {
        if (!hasLastSafeDesktopPose || characterController == null)
            return;

        var wasEnabled = characterController.enabled;
        characterController.enabled = false;
        transform.SetPositionAndRotation(lastSafeDesktopPosition, lastSafeDesktopRotation);
        characterController.enabled = wasEnabled;
        verticalVelocity = -1f;
    }

    void HandleHeldObjectManipulation()
    {
        if (TechWiseSimulationRuntime.Instance != null && TechWiseSimulationRuntime.Instance.ManipulationLocked)
        {
            selectHeld = false;
            QueueSelectState(false);
            RestoreSelectedGrabTrackRotation();
            selectedManipulationTarget = null;
            heldRotationLockTarget = null;
            return;
        }
        if (Keyboard.current != null &&
            WasKeyPressedThisFrame(Keyboard.current, resetHeldComponentKey) &&
            (rayInteractor == null || rayInteractor.interactablesSelected.Count == 0))
        {
            TechWiseComponentRecovery.ResetHeldOrLast(null);
            return;
        }

        if (rayInteractor == null || !selectHeld || Mouse.current == null || rayInteractor.interactablesSelected.Count == 0)
        {
            RestoreSelectedGrabTrackRotation();
            selectedManipulationTarget = null;
            return;
        }

        var selectedTransform = rayInteractor.interactablesSelected[0].transform;
        if (selectedTransform == null)
        {
            RestoreSelectedGrabTrackRotation();
            selectedManipulationTarget = null;
            return;
        }

        if (selectedManipulationTarget != selectedTransform)
        {
            RestoreSelectedGrabTrackRotation();
            selectedManipulationTarget = selectedTransform;
            heldRotation = selectedTransform.rotation;
            heldRotationLockTarget = null;
            if (attachTransform != null)
                attachTransform.rotation = selectedTransform.rotation;
        }

        BeginDesktopRotationControl(selectedTransform);

        if (Keyboard.current != null &&
            WasKeyPressedThisFrame(Keyboard.current, resetHeldComponentKey) &&
            TechWiseComponentRecovery.ResetHeldOrLast(selectedTransform))
        {
            selectHeld = false;
            QueueSelectState(false);
            RestoreSelectedGrabTrackRotation();
            selectedManipulationTarget = null;
            return;
        }

        if (Keyboard.current != null &&
            WasKeyPressedThisFrame(Keyboard.current, resetHeldRotationKey) &&
            TechWiseComponentRecovery.ResetHeldRotation(selectedTransform, out var resetRotation))
        {
            heldRotation = resetRotation;
            if (attachTransform != null)
                attachTransform.rotation = resetRotation;

            ForceSelectedRotation(selectedTransform, resetRotation);
            heldRotationLockTarget = selectedTransform;
            heldRotationLockUntil = Time.unscaledTime + RotationResetSettleDuration;
        }

        if (Keyboard.current != null && WasKeyPressedThisFrame(Keyboard.current, cycleRotateAxisKey))
            rotateAxisIndex = (rotateAxisIndex + 1) % 3;

        if (Mouse.current.rightButton.isPressed)
        {
            var delta = Mouse.current.delta.ReadValue();
            ApplyHeldRotation(selectedTransform, Vector3.up, -delta.x * rotateSensitivity);
            ApplyHeldRotation(selectedTransform, xrCamera != null ? xrCamera.transform.right : transform.right, delta.y * rotateSensitivity);
        }

        var scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) <= 0.01f)
            return;

        var scrollSteps = NormalizeScrollSteps(scroll);

        if (Keyboard.current != null && IsKeyPressed(Keyboard.current, distanceModifierKey))
        {
            currentHoldDistance = Mathf.Clamp(
                currentHoldDistance + scrollSteps * holdDistanceScrollSensitivity,
                minHoldDistance,
                maxHoldDistance);
            UpdateAttachDistance();
            MoveSelectedToAttach(selectedTransform);
            return;
        }

        ApplyHeldRotation(selectedTransform, GetCurrentRotationAxis(), scrollSteps * scrollRotateSensitivity);
    }

    static float NormalizeScrollSteps(float scrollValue)
    {
        return Mathf.Abs(scrollValue) > 10f ? scrollValue / 120f : scrollValue;
    }

    void BeginDesktopRotationControl(Transform selectedTransform)
    {
        var grabInteractable = selectedTransform != null
            ? selectedTransform.GetComponent<XRGrabInteractable>() ?? selectedTransform.GetComponentInParent<XRGrabInteractable>()
            : null;

        if (selectedGrabInteractable == grabInteractable && selectedGrabTrackRotationCached)
            return;

        RestoreSelectedGrabTrackRotation();

        if (grabInteractable == null)
            return;

        selectedGrabInteractable = grabInteractable;
        selectedGrabTrackRotation = grabInteractable.trackRotation;
        selectedGrabTrackRotationCached = true;
        grabInteractable.trackRotation = false;
    }

    void RestoreSelectedGrabTrackRotation()
    {
        if (!selectedGrabTrackRotationCached)
            return;

        if (selectedGrabInteractable != null)
            selectedGrabInteractable.trackRotation = selectedGrabTrackRotation;

        selectedGrabInteractable = null;
        selectedGrabTrackRotation = false;
        selectedGrabTrackRotationCached = false;
    }

    void ApplyHeldRotation(Transform selectedTransform, Vector3 axis, float angle)
    {
        if (selectedTransform == null || Mathf.Abs(angle) <= 0.001f)
            return;

        var deltaRotation = Quaternion.AngleAxis(angle, axis.normalized);

        heldRotation = deltaRotation * heldRotation;
        if (attachTransform != null)
            attachTransform.rotation = heldRotation;

        ForceSelectedRotation(selectedTransform, heldRotation);
    }

    static void ForceSelectedRotation(Transform selectedTransform, Quaternion rotation)
    {
        var rigidbody = selectedTransform.GetComponent<Rigidbody>() ?? selectedTransform.GetComponentInParent<Rigidbody>();
        if (rigidbody != null)
        {
            rigidbody.rotation = rotation;
            rigidbody.angularVelocity = Vector3.zero;
        }

        selectedTransform.rotation = rotation;
    }

    void UpdateAttachDistance()
    {
        if (attachTransform != null)
            attachTransform.localPosition = new Vector3(0f, 0f, currentHoldDistance);
    }

    void MoveSelectedToAttach(Transform selectedTransform)
    {
        if (attachTransform == null || selectedTransform == null)
            return;

        var rigidbody = selectedTransform.GetComponent<Rigidbody>() ?? selectedTransform.GetComponentInParent<Rigidbody>();
        if (rigidbody != null && !rigidbody.isKinematic)
        {
            rigidbody.MovePosition(attachTransform.position);
            return;
        }

        selectedTransform.position = attachTransform.position;
    }

    Vector3 GetCurrentRotationAxis()
    {
        return rotateAxisIndex switch
        {
            1 => Vector3.right,
            2 => Vector3.forward,
            _ => Vector3.up,
        };
    }

    static bool IsKeyPressed(Keyboard keyboard, Key key)
    {
        return key != Key.None && keyboard[key] != null && keyboard[key].isPressed;
    }

    static bool WasKeyPressedThisFrame(Keyboard keyboard, Key key)
    {
        return key != Key.None && keyboard[key] != null && keyboard[key].wasPressedThisFrame;
    }

    void SetVrInputIsolation(bool isolate)
    {
        if (xrOrigin == null)
            return;

        if (isolate)
        {
            CacheVrInputBehaviours();
            foreach (var behaviour in isolatedBehaviours)
            {
                if (behaviour != null)
                    behaviour.enabled = false;
            }
        }
        else
        {
            foreach (var pair in isolatedBehaviourStates)
            {
                if (pair.Key != null)
                    pair.Key.enabled = pair.Value;
            }

            isolatedBehaviourStates.Clear();
            isolatedBehaviours.Clear();
        }
    }

    void CacheVrInputBehaviours()
    {
        if (isolatedBehaviours.Count > 0)
            return;

        AddBehavioursToIsolation(xrOrigin.GetComponentsInChildren<TrackedPoseDriver>(true));
        AddBehavioursToIsolation(xrOrigin.GetComponentsInChildren<InputActionManager>(true));
    }

    void AddBehavioursToIsolation<T>(T[] behaviours) where T : Behaviour
    {
        foreach (var behaviour in behaviours)
        {
            if (behaviour == null || behaviour == this || behaviour == rayInteractor)
                continue;

            if (isolatedBehaviourStates.ContainsKey(behaviour))
                continue;

            isolatedBehaviourStates.Add(behaviour, behaviour.enabled);
            isolatedBehaviours.Add(behaviour);
        }
    }

    void RefreshRuntimeModeObjects()
    {
        simulatorRefreshTimer -= Time.unscaledDeltaTime;
        if (simulatorRefreshTimer > 0f)
            return;

        simulatorRefreshTimer = 0.5f;
        SetSimulatorObjectsActive(!desktopActive);
    }

    void SetSimulatorObjectsActive(bool active)
    {
        foreach (var transformInScene in FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            var simulatorObject = transformInScene.gameObject;
            if (!IsSimulatorObject(simulatorObject))
                continue;

            if (!simulatorObjectStates.ContainsKey(simulatorObject))
                simulatorObjectStates.Add(simulatorObject, simulatorObject.activeSelf);

            simulatorObject.SetActive(active ? simulatorObjectStates[simulatorObject] : false);
        }

        if (!active)
            return;

        var missingObjects = new List<GameObject>();
        foreach (var pair in simulatorObjectStates)
        {
            if (pair.Key == null)
                missingObjects.Add(pair.Key);
        }

        foreach (var missingObject in missingObjects)
            simulatorObjectStates.Remove(missingObject);
    }

    static bool IsSimulatorObject(GameObject candidate)
    {
        if (candidate == null)
            return false;

        return candidate.name.StartsWith(XRDeviceSimulatorName, System.StringComparison.Ordinal) ||
            candidate.name.StartsWith(XRInteractionSimulatorName, System.StringComparison.Ordinal);
    }

    void SetCrosshairVisible(bool visible)
    {
        if (crosshairCanvas != null)
            crosshairCanvas.gameObject.SetActive(visible);

        if (crosshairHorizontal != null)
        {
            crosshairHorizontal.color = crosshairColor;
            crosshairHorizontal.rectTransform.sizeDelta = new Vector2(crosshairSize, 2f);
        }

        if (crosshairVertical != null)
        {
            crosshairVertical.color = crosshairColor;
            crosshairVertical.rectTransform.sizeDelta = new Vector2(2f, crosshairSize);
        }
    }

    void LockCursor()
    {
        cursorLocked = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UnlockCursor()
    {
        cursorLocked = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void SyncPitchFromCamera()
    {
        if (xrCamera == null)
            return;

        var euler = xrCamera.transform.localEulerAngles;
        pitch = euler.x > 180f ? euler.x - 360f : euler.x;
    }

    static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
