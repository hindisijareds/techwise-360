using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Uses the rig's existing grip selection and trigger action; does not introduce a second grab system.</summary>
public sealed class TechWiseAssemblyTool : MonoBehaviour
{
    public enum ToolKind { Screwdriver, ThermalPaste }
    public ToolKind kind;
    public Transform tip;
    public System.Func<bool> permitted;
    internal static readonly List<TechWiseAssemblyTool> Tools = new();
    XRGrabInteractable grab;
    InputAction trigger;
    bool left;
    Vector3 home;
    Quaternion homeRotation;
    Transform rotatingModel;
    internal float RotationDegrees { get; private set; }
    internal void RotateWhileDriving(float degrees)
    {
        if (rotatingModel == null)
        {
            rotatingModel = new GameObject("Rotating screwdriver model").transform; rotatingModel.SetParent(transform, false);
            foreach (var filter in GetComponentsInChildren<MeshFilter>()) filter.transform.SetParent(rotatingModel, true);
        }
        RotationDegrees += degrees;
        rotatingModel.localRotation = Quaternion.AngleAxis(RotationDegrees, Vector3.forward);
    }
    void Awake() { grab = GetComponent<XRGrabInteractable>(); home = transform.position; homeRotation = transform.rotation; }
    void OnEnable() { Tools.Add(this); grab.selectEntered.AddListener(Selected); }
    void OnDisable() { Tools.Remove(this); if (grab != null) grab.selectEntered.RemoveListener(Selected); }
    void Selected(SelectEnterEventArgs args)
    {
        left = TechWiseTutorialRuntime.IsLeft(args.interactorObject.transform);
        string mapName = "XRI " + (left ? "Left" : "Right") + " Interaction";
        trigger = null;
        foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
        {
            var action = asset.FindActionMap(mapName, false)?.FindAction("Activate", false);
            if (action != null && action.enabled) trigger = action;
        }
    }
    internal bool Held => grab != null && TechWiseSimulationRuntime.IsHeld(grab);
    internal bool TriggerPressed
    {
        get
        {
            if (!Held || permitted != null && !permitted()) return false;
            if (trigger != null && trigger.enabled) return trigger.IsPressed();
            return InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand).TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out var pressed) && pressed;
        }
    }
    void Update()
    {
        if (!Held && (transform.position.y < home.y - .7f || Vector3.Distance(transform.position, home) > 3f))
        {
            transform.SetPositionAndRotation(home, homeRotation);
            var body = GetComponent<Rigidbody>(); body.linearVelocity = body.angularVelocity = Vector3.zero;
        }
    }
}
