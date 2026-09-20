using System;
using UnityEngine;

/// <summary>Authoring data extracted from the project's assembled PC model, in model-local coordinates.</summary>
public sealed class TechWisePhaseOneAssets : ScriptableObject
{
    [Serializable] public struct ModelPose
    {
        public string id;
        public Vector3 position, scale;
        public Quaternion rotation;
    }
    public ModelPose[] boardParts;
    public ModelPose boardInCase;
    public Vector3[] motherboardHoles;
    public Vector3[] coolerScrews;
    public Vector3 m2Standoff;
    public Vector3 m2Connector;
    public Vector3 coverHinge;
    public float boardThickness;
    public GameObject screwdriver, screw, pasteApplicator, fan, officeShell;
    public Mesh pasteBlob, standoffMesh;
    public Material metal, paste, accent;
    public string sourceModel;
}
