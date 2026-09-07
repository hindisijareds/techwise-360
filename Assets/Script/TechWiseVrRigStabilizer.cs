using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;
using CommonUsages = UnityEngine.XR.CommonUsages;
using InputDevice = UnityEngine.XR.InputDevice;

[DefaultExecutionOrder(-10000)]
public sealed class TechWiseVrRigStabilizer : MonoBehaviour
{
    public static TechWiseVrRigStabilizer Instance { get; private set; }

    sealed class StabilizerTriggerReader : IXRInputButtonReader
    {
        readonly XRInputButtonReader actionReader;
        readonly XRNode node;
        int sampledFrame = -1;
        bool hasSample;
        bool previousPressed;
        bool currentPressed;
        float currentValue;

        public StabilizerTriggerReader(XRInputButtonReader actionReader, XRNode node)
        {
            this.actionReader = actionReader;
            this.node = node;
        }

        public bool ReadIsPerformed()
        {
            SampleDevice();
            return (actionReader != null && actionReader.ReadIsPerformed()) || currentPressed;
        }

        public bool ReadWasPerformedThisFrame()
        {
            SampleDevice();
            return (actionReader != null && actionReader.ReadWasPerformedThisFrame()) || (currentPressed && !previousPressed);
        }

        public bool ReadWasCompletedThisFrame()
        {
            SampleDevice();
            return (actionReader != null && actionReader.ReadWasCompletedThisFrame()) || (!currentPressed && previousPressed);
        }

        public float ReadValue()
        {
            SampleDevice();
            var actionValue = actionReader != null ? actionReader.ReadValue() : 0f;
            return Mathf.Max(actionValue, currentValue);
        }

        public bool TryReadValue(out float value)
        {
            SampleDevice();
            var actionValue = 0f;
            var readAction = actionReader != null && actionReader.TryReadValue(out actionValue);
            value = Mathf.Max(actionValue, currentValue);
            return readAction || currentValue > 0f;
        }

        void SampleDevice()
        {
            if (Time.frameCount == sampledFrame)
                return;

            sampledFrame = Time.frameCount;
            var device = InputDevices.GetDeviceAtXRNode(node);
            var pressed = false;
            var val = 0f;

            if (device.isValid)
            {
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out var triggerButton) && triggerButton)
                    pressed = true;

                if (device.TryGetFeatureValue(CommonUsages.trigger, out var triggerVal) && triggerVal > 0.45f)
                {
                    pressed = true;
                    val = Mathf.Max(val, triggerVal);
                }

                if (device.TryGetFeatureValue(CommonUsages.primaryButton, out var primary) && primary)
                    pressed = true;

                if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondary) && secondary)
                    pressed = true;

                if (device.TryGetFeatureValue(CommonUsages.gripButton, out var grip) && grip)
                    pressed = true;
            }

            var inputDevice = node == XRNode.LeftHand
                ? (UnityEngine.InputSystem.InputDevice)UnityEngine.InputSystem.XR.XRController.leftHand
                : UnityEngine.InputSystem.XR.XRController.rightHand;

            if (inputDevice != null)
            {
                if (inputDevice.allControls.Count > 0)
                {
                    var triggerControl = inputDevice.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("triggerPressed") ??
                                         inputDevice.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("triggerButton") ??
                                         inputDevice.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryAction");
                    if (triggerControl != null && triggerControl.isPressed)
                        pressed = true;

                    var primaryControl = inputDevice.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton");
                    if (primaryControl != null && primaryControl.isPressed)
                        pressed = true;
                }
            }

            if (!hasSample)
            {
                hasSample = true;
                previousPressed = pressed;
            }
            else
            {
                previousPressed = currentPressed;
            }

            currentPressed = pressed;
            currentValue = pressed ? Mathf.Max(val, 1f) : val;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (Instance != null)
            return;

        var obj = new GameObject("TechWise VR Rig Stabilizer");
        DontDestroyOnLoad(obj);
        Instance = obj.AddComponent<TechWiseVrRigStabilizer>();
    }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnforceVrEnvironment();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnforceVrEnvironment();
        StabilizeScene(scene);
    }

    void LateUpdate()
    {
        EnforceVrEnvironment();
        EnsureHeadTracking();
        MaintainInteractors();
    }

    static void EnforceVrEnvironment()
    {
        var isAndroid = Application.platform == RuntimePlatform.Android;
#if UNITY_ANDROID && !UNITY_EDITOR
        isAndroid = true;
#endif
        if (isAndroid)
        {
            PlayerPrefs.SetString(MainMenu.ControlModeKey, MainMenu.VrModeValue);
            PlayerPrefs.SetInt("TechWise360.ControlModeExplicit", 1);

            foreach (var desktop in FindObjectsByType<TechWiseDesktopController>(FindObjectsInactive.Include))
            {
                if (desktop != null)
                    desktop.enabled = false;
            }
        }
    }

    static void StabilizeScene(Scene scene)
    {
        foreach (var origin in FindObjectsByType<XROrigin>(FindObjectsInactive.Include))
        {
            if (origin == null)
                continue;

            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 1.36144f;

            if (origin.CameraFloorOffsetObject != null)
            {
                origin.CameraFloorOffsetObject.transform.localScale = Vector3.one;
            }

            // In Multiplayer scene (Practice / Competition), ensure spawn position is in front of the workbench
            if (scene.name == "Multiplayer")
            {
                if (origin.transform.position.z < -5f)
                {
                    origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                }

                foreach (var resetter in origin.GetComponentsInChildren<XRMultiplayer.CharacterResetter>(true))
                {
                    var offPosField = typeof(XRMultiplayer.CharacterResetter).GetField("offlinePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                    var onPosField = typeof(XRMultiplayer.CharacterResetter).GetField("onlinePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                    offPosField?.SetValue(resetter, Vector3.zero);
                    onPosField?.SetValue(resetter, Vector3.zero);
                }
            }
            else if (scene.name == "Singleplayer")
            {
                foreach (var resetter in origin.GetComponentsInChildren<XRMultiplayer.CharacterResetter>(true))
                {
                    var offPosField = typeof(XRMultiplayer.CharacterResetter).GetField("offlinePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                    var onPosField = typeof(XRMultiplayer.CharacterResetter).GetField("onlinePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                    var spPos = new Vector3(0f, 0f, -1.47f);
                    offPosField?.SetValue(resetter, spPos);
                    onPosField?.SetValue(resetter, spPos);
                }
            }
        }

        EnsureHeadTracking();
        MaintainInteractors();
    }

    static void EnsureHeadTracking()
    {
        foreach (var iam in FindObjectsByType<InputActionManager>(FindObjectsInactive.Include))
        {
            if (iam != null && !iam.enabled)
                iam.enabled = true;
        }

        var mainCam = Camera.main;
        if (mainCam != null)
        {
            var tpd = mainCam.GetComponent<TrackedPoseDriver>() ?? mainCam.gameObject.AddComponent<TrackedPoseDriver>();
            tpd.enabled = true;
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        }
    }

    static void MaintainInteractors()
    {
        var xrModule = FindAnyObjectByType<XRUIInputModule>();
        if (xrModule != null)
        {
            xrModule.enableXRInput = true;
            xrModule.enabled = true;
        }

        foreach (var interactor in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include))
        {
            if (interactor == null)
                continue;

            interactor.enableFarCasting = true;
            interactor.enableUIInteraction = true;

            if (xrModule != null)
                xrModule.RegisterInteractor(interactor);

            if (interactor.transform.parent != null &&
                interactor.transform.parent.name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var isLeftController = interactor.transform.parent.name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0;
                var triggerReader = interactor.uiPressInput;
                if (triggerReader != null && triggerReader.bypass is not StabilizerTriggerReader)
                    triggerReader.bypass = new StabilizerTriggerReader(
                        triggerReader,
                        isLeftController ? XRNode.LeftHand : XRNode.RightHand);

                var visual = interactor.GetComponentInChildren<CurveVisualController>(true);
                if (visual != null)
                {
                    visual.gameObject.SetActive(true);
                    visual.enabled = true;
                    visual.extendLineToEmptyHit = true;
                    visual.maxVisualCurveDistance = 10f;
                    visual.restingVisualLineLength = 0.5f;
                    var lr = visual.GetComponentInChildren<LineRenderer>(true);
                    if (lr != null)
                    {
                        lr.enabled = true;
                        lr.gameObject.SetActive(true);
                    }
                }
            }
        }

        foreach (var ray in FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include))
        {
            if (ray == null)
                continue;

            ray.enableUIInteraction = true;
            if (xrModule != null)
                xrModule.RegisterInteractor(ray);
        }

        foreach (var poke in FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include))
        {
            if (poke == null)
                continue;

            if (xrModule != null)
                xrModule.RegisterInteractor(poke);
        }
    }
}
