using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public sealed partial class TechWiseDetailedAssemblyRuntime
{
    internal readonly List<TechWiseFastener> ExtraScrews = new();
    public IEnumerable<TechWiseFastener> Fasteners => CoolerScrews.Concat(M2Screws).Concat(BoardScrews).Concat(ExtraScrews);
    internal Material MarkerMaterial { get { if (hardwareMarker == null) hardwareMarker = new Material(Resources.Load<Shader>("TechWisePlacementMarker")) { color = new Color(.2f,1,.6f) }; return hardwareMarker; } }
    Material hardwareMarker;
    TechWiseCasePanel casePanel;
    readonly Dictionary<GameObject, bool> conditionalObjects = new();
    public bool PanelInstalled => casePanel != null && casePanel.Installed;
    internal XRGrabInteractable SidePanel => panel;
    internal bool IsConditionallyHidden(Transform item) => conditionalObjects.TryGetValue(item.gameObject,out var hidden) && hidden;
    internal bool BuildComplete => Ready && TechWiseBuildDefinition.Components.All(c => Seated(c.id) && StepFastened(c.id)) && PanelInstalled;
    internal bool RemovalComplete => Ready && !PanelInstalled && TechWiseBuildDefinition.Components.All(c => state.IsStepComplete(c.id)) && Fasteners.All(f => f.Removed) && !Cover.Closed && !Lever.Closed;
    internal string DisassemblyStep => !Ready ? null : TechWiseBuildDefinition.DisassemblyOrder.FirstOrDefault(id => !state.IsStepComplete(id));
    internal TechWiseFastener CurrentFastener
    {
        get
        {
            if (!Ready || CurrentPartStep == null || !Seated(CurrentPartStep) || TechWiseSimulationModeManager.IsDisassembly && PanelInstalled) return null;
            if (CurrentPartStep == "M2" && !M2Lowered && TechWiseSimulationModeManager.IsAssembly) return null;
            return Fasteners.FirstOrDefault(f => f.ComponentId == CurrentPartStep &&
                (TechWiseSimulationModeManager.IsDisassembly ? !f.Removed : !f.Complete));
        }
    }
    void ClearCompleteBuild()
    {
        ExtraScrews.Clear(); conditionalObjects.Clear(); casePanel = null;
        if (hardwareMarker != null) Destroy(hardwareMarker); hardwareMarker = null;
        TechWiseAssemblyHistory.Clear();
    }
    void CreateAdditionalFasteners()
    {
        foreach (var id in new[]{"FanRear","FanFront1","FanFront2"})
        {
            var part = FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include).First(p => p.GetComponent<TechWiseAssemblyPartId>()?.step == id);
            // The retained 120 mm fan has a four-corner mounting pattern, 105 mm pitch.
            float sign = id == "FanRear" ? -1 : 1;
            var bounds = TechWiseComponentGeometry.BoundsOf(part.transform,out var b) ? b : new Bounds(Vector3.zero,Vector3.one*.12f);
            float z = sign < 0 ? bounds.min.z : bounds.max.z;
            foreach (int x in new[]{-1,1}) foreach(int y in new[]{-1,1})
                ExtraScrews.Add(Screw(part.transform,part.transform.TransformPoint(new Vector3(x*.0525f,y*.0525f,z)),part.transform.forward*sign,id+" mounting screw",()=>Seated(id)));
        }
        foreach (var id in new[]{"GPU","Storage","PSU"})
        {
            var part = state.FindPart(id); if (part == null) throw new InvalidOperationException("Missing required component: "+id);
            if (!TechWiseComponentGeometry.BoundsOf(part.transform,out var bounds,true)) throw new InvalidOperationException("Missing component mesh: "+id);
            int axis = id == "Storage" ? (bounds.size.x < bounds.size.y ? (bounds.size.x < bounds.size.z ? 0:2) : (bounds.size.y < bounds.size.z ? 1:2)) : 0;
            int u=(axis+1)%3,v=(axis+2)%3;
            int count=TechWiseBuildDefinition.Find(id).screws;
            for(int i=0;i<count;i++)
            {
                var p=bounds.center; p[axis]=bounds.max[axis];
                p[u]+=bounds.extents[u]*.78f*(i%2==0?-1:1);
                p[v]+=bounds.extents[v]*(count==2?.8f:(i<2?-.78f:.78f));
                var normal=Vector3.zero; normal[axis]=1;
                ExtraScrews.Add(Screw(part.transform,part.transform.TransformPoint(p),part.transform.TransformDirection(normal),id+" mounting screw",()=>Seated(id)));
            }
        }
    }
    void InitializeManualScrews()
    {
        var groups = new Dictionary<string,int>();
        var inventory = board.position + Vector3.right*.65f + Vector3.back*.35f;
        inventory.y = TechWiseWorkbenchSurface.Height(inventory,board.position.y)+.025f;
        int index=0;
        foreach(var screw in Fasteners)
        {
            string component = CoolerScrews.Contains(screw)?"CPUCooler":M2Screws.Contains(screw)?"M2":BoardScrews.Contains(screw)?"Motherboard":screw.name.Split(' ')[0];
            int ordinal=groups.TryGetValue(component,out var n)?n:0; groups[component]=ordinal+1;
            var parent=screw.transform.parent; var pose=new Pose(screw.transform.position,screw.transform.rotation);
            var home=new Pose(inventory+new Vector3((index%9-4)*.043f,0,(index/9-1.5f)*.045f),Quaternion.Euler(90,0,0));
            screw.Initialize(component,TechWiseBuildDefinition.HoleId(component,ordinal),parent,pose,home); index++;
            Label(screw.transform,TechWiseSimulationRuntime.Label(component)+" screw "+(ordinal+1),.02f,
                ()=>highlightedFastener==screw && !TechWiseSimulationRuntime.IsHeld(screw.Grab) && !screw.Inserted && screw.gameObject.activeInHierarchy);
        }
        // A shallow physical tray keeps the individually grabbable hardware separated.
        var tray=Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); tray.name="Organized screw tray";
        tray.transform.position=inventory+Vector3.down*.02f; tray.transform.localScale=new Vector3(.43f,.012f,.22f);
        tray.GetComponent<Renderer>().sharedMaterial=assets.metal;
        foreach(var definition in TechWiseBuildDefinition.Components)
            if(Fasteners.Count(f=>f.ComponentId==definition.id)!=definition.screws)
                throw new InvalidOperationException("Screw count mismatch: "+definition.id);
    }
    void InitializePanelAndWorkspace()
    {
        casePanel=panel.gameObject.AddComponent<TechWiseCasePanel>();
        casePanel.Initialize(this,panel);
        TechWiseAssemblyHistory.Clear();
        UpdateAvailability();
    }
    void UpdateCompleteBuild()
    {
        UpdateAvailability();
        if(TechWiseSimulationModeManager.IsDisassembly && casePanel!=null && !casePanel.Installed) CasePrepared=true;
    }
    void UpdateAvailability()
    {
        if(!Ready) return;
        foreach(var id in new[]{"FanRear","FanFront1","FanFront2"})
        {
            var part=state.FindPart(id); if(part==null) continue;
            bool show=!TechWiseTutorialRuntime.InTutorial || TechWiseSimulationModeManager.IsDisassembly ||
                !TechWiseTutorialRuntime.ControlsPending && (CurrentPartStep==id || Seated(id));
            conditionalObjects[part.gameObject]=!show;
            if(part.gameObject.activeSelf!=show) part.gameObject.SetActive(show);
        }
        foreach(var screw in Fasteners)
        {
            bool show=!TechWiseTutorialRuntime.InTutorial || TechWiseSimulationModeManager.IsDisassembly ||
                !TechWiseTutorialRuntime.ControlsPending && (screw.Inserted || CurrentPartStep==screw.ComponentId && Seated(screw.ComponentId));
            conditionalObjects[screw.gameObject]=!show;
            if(screw.gameObject.activeSelf!=show) screw.gameObject.SetActive(show);
        }
    }
    internal bool CanRemove(string id)
    {
        if(!Ready || PanelInstalled || id!=DisassemblyStep) return false;
        if(Fasteners.Any(f=>f.ComponentId==id && !f.Removed)) return false;
        if(id=="M2" && M2Lowered) return false;
        if(id=="CPU" && (Cover.Closed || Lever.Closed)) return false;
        return true;
    }
    internal void PrepareDisassemblyHardware()
    {
        if(!m2HandleMade) CreateM2Handle();
        RestoreMechanics(true,true,true,0,0,0);
        foreach(var screw in Fasteners) screw.Seat(1);
        casePanel.Restore(true,new Pose(panel.transform.position,panel.transform.rotation));
        TechWiseAssemblyHistory.Clear();
    }
    internal void RestoreMechanics(bool paste,bool prepared,bool lowered,float cover,float lever,float m2)
    {
        PasteApplied=paste; pasteProgress=paste?1:0; CasePrepared=prepared; M2Lowered=lowered;
        Cover.RestoreAngle(cover); Lever.RestoreAngle(lever); SetM2Angle(m2);
        if(M2Hinge!=null) M2Hinge.RestoreAngle(m2);
        if(paste && blob==null)
        {
            var cpu=state.FindPart("CPU"); var mesh=cpu.GetComponentsInChildren<Renderer>().First();
            blob=Own(new GameObject("Visible thermal paste on CPU",typeof(MeshFilter),typeof(MeshRenderer)));
            blob.transform.SetParent(cpu.transform,true); blob.transform.position=mesh.bounds.center+board.forward*.006f;
            blob.transform.rotation=Quaternion.FromToRotation(Vector3.up,board.forward); SetWorldScale(blob.transform,Vector3.one);
            blob.GetComponent<MeshFilter>().sharedMesh=assets.pasteBlob; blob.GetComponent<MeshRenderer>().sharedMaterial=assets.paste;
        }
        if(blob!=null) blob.SetActive(paste);
    }
    internal void RestorePanel(bool installed,Pose pose) => casePanel.Restore(installed,pose);
    internal string BuildHeading => TechWiseSimulationModeManager.IsDisassembly
        ? RemovalComplete?"PC disassembly complete":"PC disassembly | "+(Array.IndexOf(TechWiseBuildDefinition.DisassemblyOrder,DisassemblyStep)+1)+" / "+TechWiseBuildDefinition.Components.Length
        : BuildComplete?"PC assembly complete":"PC assembly | "+GuideStep+" / 18";
    public string Instruction => TechWiseBuildLesson.Describe(this,state);
}

internal static class TechWiseWorkbenchSurface
{
    internal static float Height(Vector3 point,float fallback)
    {
        var hits=Physics.RaycastAll(point+Vector3.up*2,Vector3.down,5);
        foreach(var hit in hits.OrderBy(h=>h.distance))
            for(var t=hit.collider.transform;t!=null;t=t.parent)
                if(t.name=="Table Work Station") return hit.point.y;
        var table=GameObject.Find("Table Work Station");
        if(table!=null)
        {
            var surfaces=table.GetComponentsInChildren<Renderer>().Where(r=>r.bounds.size.x>.3f && r.bounds.size.z>.3f).ToArray();
            if(surfaces.Length>0) return surfaces.Max(r=>r.bounds.max.y);
        }
        return fallback-.04f;
    }
}
