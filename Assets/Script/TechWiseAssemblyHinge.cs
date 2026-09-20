using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>A constrained model hinge driven by the selecting controller's wrist, then committed on release.</summary>
public sealed class TechWiseAssemblyHinge : MonoBehaviour
{
    public Transform movingPart;
    public Vector3 localAxis = Vector3.right;
    public float maximumAngle = 100;
    public bool useLoweringGesture;
    public float closeTolerance = 8;
    public float Angle { get; private set; }
    public bool Closed { get; private set; }
    public System.Func<bool> permitted;
    public System.Action<float> moved;
    public System.Action closed;
    public System.Action opened;
    TechWiseAssemblyHistory.Snapshot before;
    XRSimpleInteractable interaction;
    Transform hand;
    Quaternion handStart, closedRotation;
    Vector3 handStartPosition;
    float startAngle;
    public void Initialize(float initialAngle)
    {
        interaction = GetComponent<XRSimpleInteractable>();
        closedRotation = movingPart.localRotation; Angle = initialAngle; Apply();
        interaction.selectEntered.AddListener(Selected); interaction.selectExited.AddListener(Released);
    }
    void Selected(SelectEnterEventArgs args)
    {
        if (Closed && !TechWiseSimulationModeManager.IsDisassembly || permitted != null && !permitted()) return;
        before = TechWiseAssemblyHistory.Capture();
        hand = args.interactorObject.transform; handStart = hand.rotation; startAngle = Angle;
        handStartPosition = hand.position;
    }
    void Update()
    {
        if (hand == null || permitted != null && !permitted()) return;
        ObserveHandMotion(hand.position, hand.rotation);
    }
    internal void ObserveHandMotion(Vector3 position, Quaternion rotation)
    {
        if (useLoweringGesture)
        {
            var normal = movingPart.parent != null ? movingPart.parent.forward : Vector3.up;
            float travel = Vector3.Dot(position - handStartPosition, normal);
            if (Mathf.Abs(travel) > .01f) { Observe(startAngle + travel / .09f * maximumAngle); return; }
        }
        var delta = rotation * Quaternion.Inverse(handStart); delta.ToAngleAxis(out var angle, out var axis);
        if (angle > 180) angle -= 360;
        var worldAxis = movingPart.parent != null ? movingPart.parent.TransformDirection(localAxis) : localAxis;
        Observe(startAngle + angle * Vector3.Dot(axis, worldAxis));
    }
    internal void Observe(float angle)
    {
        if (Closed && !TechWiseSimulationModeManager.IsDisassembly || permitted != null && !permitted()) return;
        Angle = Mathf.Clamp(angle, 0, maximumAngle); Apply();
    }
    void Apply() { movingPart.localRotation = closedRotation * Quaternion.AngleAxis(Angle, localAxis); moved?.Invoke(Angle); }
    void Released(SelectExitEventArgs args) { hand = null; if (!args.isCanceled) Commit(); }
    internal void RestoreAngle(float angle) { hand = null; Angle = angle; Closed = angle <= closeTolerance; Apply(); }
    internal void Commit()
    {
        if (permitted != null && !permitted()) return;
        if (TechWiseSimulationModeManager.IsDisassembly)
        {
            if (Angle < maximumAngle - closeTolerance) return;
            Closed = false; Angle = maximumAngle; Apply(); opened?.Invoke();
        }
        else
        {
            if (Closed || Angle > closeTolerance) return;
            Closed = true; Angle = 0; Apply(); closed?.Invoke();
        }
        TechWiseAssemblyHistory.Commit(before); before = null;
    }
}
