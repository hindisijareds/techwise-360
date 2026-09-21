using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public sealed class TechWiseCasePanel : MonoBehaviour
{
    internal bool Installed { get; private set; }=true;
    XRGrabInteractable grab;
    Rigidbody body;
    Transform home;
    TechWiseDetailedAssemblyRuntime runtime;
    bool pending, releasedAway;
    internal bool AccessOpen => !Installed && !grab.isSelected && releasedAway;
    Pose releasedPose;
    TechWiseAssemblyHistory.Snapshot before;
    internal void Initialize(TechWiseDetailedAssemblyRuntime owner,XRGrabInteractable interaction)
    {
        runtime=owner; grab=interaction; body=GetComponent<Rigidbody>();
        home=new GameObject("Side panel original attachment").transform; home.SetParent(transform.parent,true); home.SetPositionAndRotation(transform.position,transform.rotation);
        grab.selectEntered.AddListener(Selected); grab.selectExited.AddListener(Released);
    }
    void Selected(SelectEnterEventArgs args) { before=TechWiseAssemblyHistory.Capture(); Installed=false; }
    void Released(SelectExitEventArgs args)
    {
        if(args.isCanceled || TechWisePauseSession.Active || TechWiseAssemblyHistory.Restoring) return;
        releasedPose=new Pose(transform.position,transform.rotation); pending=true;
    }
    void LateUpdate()
    {
        if(!pending || grab.isSelected) return; pending=false;
        releasedAway=Vector3.Distance(releasedPose.position,home.position)>.2f;
        bool near=Vector3.Distance(releasedPose.position,home.position)<.045f && Quaternion.Angle(releasedPose.rotation,home.rotation)<15;
        bool canClose=TechWiseSimulationModeManager.IsAssembly && runtime.CasePrepared && runtime.CurrentPhase==TechWiseDetailedAssemblyRuntime.Phase.CloseCase;
        if(near && canClose) { Installed=true; transform.SetPositionAndRotation(home.position,home.rotation); body.isKinematic=true; }
        else { Installed=false; body.isKinematic=false; body.useGravity=true; body.detectCollisions=true; foreach(var c in grab.colliders) c.isTrigger=false; }
        TechWiseAssemblyHistory.Commit(before); before=null;
    }
    internal void Restore(bool installed,Pose pose)
    {
        pending=false; releasedAway=!installed && Vector3.Distance(pose.position,home.position)>.2f; Installed=installed; transform.SetPositionAndRotation(installed?home.position:pose.position,installed?home.rotation:pose.rotation);
        body.isKinematic=installed; body.useGravity=true;
    }
    void OnDestroy() { if(home!=null) Destroy(home.gameObject); if(grab!=null) { grab.selectEntered.RemoveListener(Selected); grab.selectExited.RemoveListener(Released); } }
}
