using UnityEngine;

/// <summary>Optional per-socket authoring. Screw input can set fasteningComplete after adviser approval.</summary>
public sealed class TechWisePlacementRule : MonoBehaviour
{
    [Range(1f, 180f)] public float orientationTolerance = 12f;
    public bool exactOrientation = true;
    [Min(.001f)] public float distanceTolerance = .035f;
    public bool fastenAfterPlacement;
    public string requiredInstalledStep;
    public bool requiresManualFastening;
    public bool fasteningComplete;
    public bool FasteningReady => !requiresManualFastening || fasteningComplete;
}
