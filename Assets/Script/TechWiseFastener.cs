using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.XR.Content.Interaction;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Individual keyed screw: manual release inserts; held driver contact tightens or loosens.</summary>
public sealed class TechWiseFastener : MonoBehaviour
{
    public enum TighteningState { Loose, PartiallyTightened, FullyTightened }
    public float secondsToTighten = 2.5f, contactTolerance = .018f, angleTolerance = 18f;
    public float Progress { get; private set; }
    public bool Inserted => Hole != null && grab != null && Hole.IsSelecting(grab) && !TechWiseSimulationRuntime.IsHeld(grab);
    public bool Complete => Inserted && Progress >= 1;
    public bool Removed => !Inserted && Progress == 0 && !TechWiseSimulationRuntime.IsHeld(grab) && Vector3.Distance(transform.position, Hole.transform.position) > .06f;
    public string Id { get; private set; }
    public string ComponentId { get; private set; }
    public XRLockSocketInteractor Hole { get; private set; }
    public XRGrabInteractable Grab => grab;
    public TighteningState State => Progress >= 1 ? TighteningState.FullyTightened : Progress > 0 ? TighteningState.PartiallyTightened : TighteningState.Loose;
    public Func<bool> permitted;
    internal static readonly List<TechWiseFastener> All = new();
    XRGrabInteractable grab;
    Rigidbody body;
    Transform seated;
    Key key;
    Mesh extendedShaft;
    Pose home;
    bool released, armed, initialized;
    TechWiseAssemblyHistory.Snapshot actionBefore, drivingBefore;
    LineRenderer marker;
    TMP_Text screwTag,holeTag;
    internal string Marking => ComponentId switch { "CPUCooler"=>"C", "M2"=>"M", "Motherboard"=>"B", "FanRear"=>"R", "FanFront1"=>"U", "FanFront2"=>"L", "GPU"=>"G", "Storage"=>"S", "PSU"=>"P", _=>"?" } + Id.Substring(Id.LastIndexOf('/')+1);

    internal void Initialize(string component, string id, Transform holeParent, Pose mount, Pose inventory)
    {
        ComponentId = component; Id = id; home = inventory;
        var holeObject = new GameObject(id + " hole"); holeObject.SetActive(false);
        holeObject.transform.SetParent(holeParent, true); holeObject.transform.SetPositionAndRotation(mount.position, mount.rotation);
        var scale = holeObject.transform.lossyScale; holeObject.transform.localScale = new Vector3(holeObject.transform.localScale.x / scale.x, holeObject.transform.localScale.y / scale.y, holeObject.transform.localScale.z / scale.z);
        var volume = holeObject.AddComponent<SphereCollider>(); volume.isTrigger = true; volume.radius = .018f;
        holeObject.AddComponent<TechWiseScrewHole>().screw = this;
        Hole = holeObject.AddComponent<XRLockSocketInteractor>(); Hole.interactionLayers = ~0; Hole.socketScaleMode = SocketScaleMode.None;
        Hole.hoverSocketSnapping = false; Hole.showInteractableHoverMeshes = false;
        seated = new GameObject("Screw seated pose").transform; seated.SetParent(holeObject.transform, false); Hole.attachTransform = seated;
        key = ScriptableObject.CreateInstance<Key>(); key.name = id; Hole.keychainLock.requiredKeys.Add(key);
        transform.SetParent(null, true); transform.SetPositionAndRotation(home.position, home.rotation);
        gameObject.SetActive(false);
        body = gameObject.AddComponent<Rigidbody>(); body.useGravity = true; body.isKinematic = true; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var collider = gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(0,0,.006f); collider.size = new Vector3(.012f,.012f,.025f);
        grab = gameObject.AddComponent<XRGrabInteractable>(); grab.interactionLayers = ~0; grab.selectMode = InteractableSelectMode.Single;
        grab.colliders.Clear(); grab.colliders.Add(collider); grab.throwOnDetach = false; grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.useDynamicAttach = true; gameObject.AddComponent<Keychain>().AddKey(key);
        grab.selectFilters.Add(new XRSelectFilterDelegate((hand, part) =>
        {
            if (TechWiseAssemblyHistory.Restoring || TechWiseDisassemblyRuntime.IsPreparing) return hand == (IXRSelectInteractor)Hole;
            if (hand is XRSocketInteractor) return hand == (IXRSelectInteractor)Hole && (Hole.IsSelecting(part) || released && armed && CanUse && PlacementProblem(Hole) == null);
            return CanUse && Progress <= 0 && (!Inserted || TechWiseSimulationModeManager.IsDisassembly);
        }));
        grab.selectEntered.AddListener(Selected); grab.selectExited.AddListener(Released);
        Hole.selectExited.AddListener(args =>
        {
            if(args.isCanceled || !TechWiseSimulationModeManager.IsDisassembly || TechWiseAssemblyHistory.Restoring) return;
            actionBefore=TechWiseAssemblyHistory.Capture();
            var saved=actionBefore?.screws.Find(s=>s.screw==this); if(saved!=null) saved.inserted=true;
        });
        Hole.selectEntered.AddListener(_ => { released = false; body.isKinematic = true; TechWiseAssemblyHistory.Commit(actionBefore); actionBefore=null; });
        marker = holeObject.AddComponent<LineRenderer>(); marker.sharedMaterial = TechWiseDetailedAssemblyRuntime.Instance.MarkerMaterial;
        marker.useWorldSpace = false; marker.widthMultiplier = .0015f; marker.positionCount = 25;
        for (int i=0;i<25;i++) { float a=i*Mathf.PI*2/24; marker.SetPosition(i,new Vector3(Mathf.Cos(a)*.009f,Mathf.Sin(a)*.009f,-.007f)); }
        marker.enabled = false;
        screwTag=Tag(transform,"Screw ID"); holeTag=Tag(holeObject.transform,"Hole ID");
        initialized = true; All.Add(this); holeObject.SetActive(true); gameObject.SetActive(true);
    }
    TMP_Text Tag(Transform parent,string name)
    {
        var root=new GameObject(name,typeof(TextMeshPro)); root.transform.SetParent(parent,false);
        var tag=root.GetComponent<TextMeshPro>();tag.text=Marking;tag.fontSize=.065f;tag.color=new Color(.15f,.85f,1);tag.alignment=TextAlignmentOptions.Center;tag.rectTransform.sizeDelta=new Vector2(.026f,.014f);
        return tag;
    }
    void PositionTags()
    {
        var camera=Camera.main;if(camera==null)return;
        screwTag.gameObject.SetActive(!Inserted&&!TechWiseSimulationRuntime.IsHeld(grab));
        holeTag.gameObject.SetActive(!Complete);
        if(screwTag.gameObject.activeSelf) { screwTag.transform.position=transform.position+Vector3.up*.02f; screwTag.transform.rotation=Quaternion.LookRotation(screwTag.transform.position-camera.transform.position); }
        if(holeTag.gameObject.activeSelf) { holeTag.transform.position=Hole.transform.position-Hole.transform.forward*.015f+Hole.transform.right*.014f;holeTag.transform.rotation=Quaternion.LookRotation(holeTag.transform.position-camera.transform.position); }
    }
    bool CanUse => !TechWisePauseSession.Active && !TechWiseTutorialRuntime.ControlsPending && TechWiseSimulationRuntime.Instance != null && !TechWiseSimulationRuntime.Instance.ManipulationLocked && (permitted == null || permitted());
    void Selected(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;
        armed = true; released = false; actionBefore ??= TechWiseAssemblyHistory.Capture();
    }
    void Released(SelectExitEventArgs args)
    {
        if (args.isCanceled || args.interactorObject is XRSocketInteractor || TechWiseAssemblyHistory.Restoring || TechWisePauseSession.Active) return;
        released = true;
    }
    void LateUpdate()
    {
        if (!initialized) return;
        PositionTags();
        if (released && !TechWiseSimulationRuntime.IsHeld(grab))
        {
            if (TechWiseSimulationModeManager.IsDisassembly)
            {
                if (Removed) TechWiseAssemblyHistory.Commit(actionBefore);
            }
            else
            {
                var nearest = All.Where(s => s != null && s.Hole != null).OrderBy(s => Vector3.Distance(transform.position,s.Hole.transform.position)).FirstOrDefault();
                if (nearest != null && Vector3.Distance(transform.position,nearest.Hole.transform.position) < .045f)
                {
                    string problem = PlacementProblem(nearest.Hole);
                    if (nearest == this && armed && CanUse && problem == null) { Seat(0); TechWiseAssemblyHistory.Commit(actionBefore); }
                    else TechWiseSimulationRuntime.Instance.RecordMistake(nearest == this ? "incorrect_orientation" : "wrong_screw_hole",ComponentId,
                        problem ?? "This hole is not ready for insertion.",nearest == this ? "Seat the component first; align the screw shaft with the hole and release." : "Use the matching numbered hole for " + Id + ".");
                }
            }
            released = false; actionBefore = null;
            if (!Inserted) { body.isKinematic = false; body.useGravity = true; }
        }
        if (Inserted) ApplyVisual();
        else if (!grab.isSelected && (transform.position.y < home.position.y-.7f || Vector3.Distance(transform.position,home.position)>3))
        { transform.SetPositionAndRotation(home.position,home.rotation); body.linearVelocity=body.angularVelocity=Vector3.zero; body.isKinematic=true; }
    }
    internal string PlacementProblem(XRLockSocketInteractor target)
    {
        if (target != Hole) return "This screw does not belong in this hole.";
        if (target.hasSelection && !target.IsSelecting(grab)) return "The hole is already occupied.";
        if (Vector3.Distance(transform.position,Hole.transform.position)>.02f) return "Move the screw tip closer to its matching hole.";
        if (Vector3.Angle(transform.forward,Hole.transform.forward)>20) return "Align the screw shaft with the hole; keep the head facing outward.";
        return null;
    }
    internal void Seat(float progress)
    {
        foreach(var owner in grab.interactorsSelecting.ToArray()) grab.interactionManager.SelectCancel(owner,grab);
        transform.SetPositionAndRotation(Hole.transform.position,Hole.transform.rotation); Progress=progress; body.isKinematic=true;
        Hole.interactionManager.SelectEnter((IXRSelectInteractor)Hole,grab); ApplyVisual();
    }
    internal void Restore(bool inserted,float progress,Pose pose)
    {
        foreach(var owner in grab.interactorsSelecting.ToArray()) grab.interactionManager.SelectCancel(owner,grab);
        released=armed=false; actionBefore=drivingBefore=null; Progress=progress;
        if(inserted) Seat(progress); else { transform.SetPositionAndRotation(pose.position,pose.rotation); body.isKinematic=true; }
    }
    internal void ShowMarker(bool show) { if(marker!=null) marker.enabled=show; }
    internal Vector3 HeadPosition => transform.TransformPoint(new Vector3(0,0,-.006f));
    void Update()
    {
        if(!initialized || !Inserted || !CanUse) return;
        foreach(var tool in TechWiseAssemblyTool.Tools)
        {
            if(tool==null || tool.kind!=TechWiseAssemblyTool.ToolKind.Screwdriver || tool.tip==null) continue;
            float before=Progress;
            if(Advance(tool.tip.position,tool.tip.forward,tool.TriggerPressed,Time.deltaTime))
            { tool.RotateWhileDriving((Progress-before)*720f); TechWiseScrewDetail.Instance?.Driving(this,tool); break; }
        }
    }
    internal bool Advance(Vector3 tip,Vector3 direction,bool heldTrigger,float elapsed)
    {
        if(!Inserted || !CanUse || !heldTrigger || Vector3.Distance(tip,HeadPosition)>contactTolerance || Vector3.Angle(direction,transform.forward)>angleTolerance) return false;
        bool removing=TechWiseSimulationModeManager.IsDisassembly;
        if(removing ? Progress<=0 : Progress>=1) return false;
        drivingBefore ??= TechWiseAssemblyHistory.Capture();
        Progress=Mathf.Clamp01(Progress+(removing?-1:1)*Mathf.Clamp(elapsed,0,.1f)/secondsToTighten); ApplyVisual();
        if(removing ? Progress<=0 : Progress>=1) { TechWiseAssemblyHistory.Commit(drivingBefore); drivingBefore=null; }
        return true;
    }
    internal void ResetProgress() { Progress=0; ApplyVisual(); }
    void ApplyVisual()
    {
        if(!Inserted) return;
        seated.localPosition=Vector3.back*(.003f*(1-Progress)); seated.localRotation=Quaternion.AngleAxis(Progress*720f,Vector3.forward);
        var property=new MaterialPropertyBlock(); property.SetColor("_BaseColor",Progress>=1?new Color(.36f,.48f,.4f):new Color(.63f,.65f,.68f));
        foreach(var renderer in GetComponentsInChildren<Renderer>()) if(renderer.name!="Phillips recess") renderer.SetPropertyBlock(property);
    }
    internal void ExtendShaftTo(Vector3 boardContact)
    {
        var filter=transform.Find("Steel head and threads").GetComponent<MeshFilter>(); extendedShaft=Instantiate(filter.sharedMesh); extendedShaft.name="Screw reaching mounting surface";
        var points=extendedShaft.vertices; float end=Mathf.Max(.006f,filter.transform.InverseTransformPoint(boardContact).z);
        for(int i=0;i<points.Length;i++) if(points[i].z>-.003f) points[i].z=Mathf.Lerp(-.003f,end,Mathf.InverseLerp(-.003f,.006f,points[i].z));
        extendedShaft.vertices=points; extendedShaft.RecalculateNormals(); extendedShaft.RecalculateBounds(); filter.sharedMesh=extendedShaft;
    }
    void OnDestroy()
    {
        All.Remove(this); if(Hole!=null) Destroy(Hole.gameObject); if(key!=null) Destroy(key); if(extendedShaft!=null) Destroy(extendedShaft);
    }
}
public sealed class TechWiseScrewHole : MonoBehaviour { public TechWiseFastener screw; }
