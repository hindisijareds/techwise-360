using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Builds additive authoring assets from the existing assembled model. Never edits the source FBX or scenes.</summary>
public static class TechWisePhaseOneAuthoring
{
    [Serializable] class MeshDump { public Vector3[] vertices; public int[] triangles; }
    const string Root = "Assets/PhaseOne";
    const string Source = "Assets/Prefab/PC/dream-computer-setup/source/maya2sketchfab.fbx";
    public static void Generate()
    {
        Directory.CreateDirectory(Root + "/Generated"); Directory.CreateDirectory(Root + "/Models"); Directory.CreateDirectory("Assets/Resources");
        File.Copy("new_assets/Bolt.fbx", Root + "/Models/Bolt.fbx", true);
        File.Copy("new_assets/screw_driver_export.fbx", Root + "/Models/Screwdriver.fbx", true);
        AssetDatabase.Refresh();
        foreach (var path in new[] { Root + "/Models/Bolt.fbx", Root + "/Models/Screwdriver.fbx" })
        { var importer = (ModelImporter)AssetImporter.GetAtPath(path); importer.isReadable = true; importer.SaveAndReimport(); }
        var data = ScriptableObject.CreateInstance<TechWisePhaseOneAssets>(); data.sourceModel = Source;
        data.metal = Material("Office steel", new Color(.23f, .25f, .27f));
        data.paste = Material("Thermal compound", new Color(.66f, .69f, .71f));
        data.accent = Material("Tool handle", new Color(.13f, .2f, .25f));
        data.screwdriver = Normalize(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/Screwdriver.fbx").transform, "Phase screwdriver", .23f, true, data.accent);
        data.screw = BuildScrew(data.metal);
        var pasteModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Thermal Paste/Tooth_Paste.prefab");
        data.pasteApplicator = Normalize(pasteModel.transform, "Thermal paste applicator", .16f, true, data.accent);
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source));
        try
        {
            var board = model.transform.Find("MotherBoard"); var casing = model.transform.Find("Case/Case_Box");
            var parts = new List<TechWisePhaseOneAssets.ModelPose>();
            foreach (var id in new[] { "CPU", "RAM", "RAM1", "RAM2", "RAM3", "M2" }) parts.Add(PoseOf(id, board, model.transform.Find(id)));
            parts.Add(PoseOf("CPUCooler", board, model.transform.Find("WaterCooling/WaterBlock")));
            data.boardParts = parts.ToArray(); data.boardInCase = PoseOf("Motherboard", casing, board);
            data.coverHinge = board.InverseTransformPoint(board.Find("pCube478").GetComponent<Renderer>().bounds.center);
            data.coolerScrews = Enumerable.Range(181, 4).Select(i => CoolerMountSurface(model.transform.Find("WaterCooling/WaterBlock"), board, i)).ToArray();
            var pcb = board.Find("pCube511").GetComponent<MeshFilter>();
            data.boardThickness = BoundsIn(pcb.transform, board).size.z;
            var holes = FindPlanarHoles(pcb, board);
            // Cooler holes lie around the CPU; the other circular PCB openings are mounting holes.
            var cpu = parts.First(p => p.id == "CPU").position;
            var cooler = holes.Where(p => data.coolerScrews.Any(s => Vector2.Distance(p, s) < .002f)).ToArray();
            data.motherboardHoles = holes.Except(cooler).ToArray();
            if (data.motherboardHoles.Length < 6) throw new Exception("PCB mounting-hole extraction needs review; found " + holes.Count + " circles, " + data.motherboardHoles.Length + " mounting holes.");
            var m2 = model.transform.Find("M2");
            var m2Bounds = BoundsIn(m2, board);
            var mounts = board.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.StartsWith("pCylinder") && r.sharedMaterials.Any(m => m != null && m.name == "Mertal1M"));
            // Match the actual SSD free-end opening to the board's existing support, not the old socket object's centre.
            var ends = new[] { new Vector3(m2Bounds.min.x, m2Bounds.center.y, m2Bounds.center.z), new Vector3(m2Bounds.max.x, m2Bounds.center.y, m2Bounds.center.z) };
            var standoff = mounts.OrderBy(r => ends.Min(p => Vector2.Distance(p, board.InverseTransformPoint(r.bounds.center)))).First();
            var support = BoundsIn(standoff.transform, board);
            data.m2Standoff = new Vector3(support.center.x,support.center.y,support.max.z);
            var ssdPCB = BoundsIn(m2.Find("pCube842"), board);
            float seatOffset = support.max.z - ssdPCB.min.z;
            int ssdIndex = Array.FindIndex(data.boardParts, p => p.id == "M2");
            data.boardParts[ssdIndex].position += Vector3.forward * seatOffset;
            data.m2Connector = ends.OrderByDescending(p => Vector2.Distance(p, data.m2Standoff)).First() + Vector3.forward * seatOffset;
            data.fan = Normalize(model.transform.Find("Corsair_Fan"), "Office fan", .12f, false, data.metal);
            data.officeShell = BuildShell(casing, board, data);
            data.pasteBlob = Blob(); AssetDatabase.CreateAsset(data.pasteBlob, Root + "/Generated/Visible thermal paste.asset");
            data.standoffMesh = Standoff(); AssetDatabase.CreateAsset(data.standoffMesh, Root + "/Generated/Hollow hex motherboard standoff.asset");
            AssetDatabase.CreateAsset(data, "Assets/Resources/TechWisePhaseOneAssets.asset");
            Directory.CreateDirectory("Logs/PhaseOne");
            File.WriteAllText("Logs/PhaseOne/authoring.json", JsonUtility.ToJson(data, true));
            AssetDatabase.SaveAssets();
        }
        finally { UnityEngine.Object.DestroyImmediate(model); }
    }
    static TechWisePhaseOneAssets.ModelPose PoseOf(string id, Transform parent, Transform child)
    {
        var matrix = parent.worldToLocalMatrix * child.localToWorldMatrix;
        return new TechWisePhaseOneAssets.ModelPose { id = id, position = matrix.GetColumn(3), rotation = matrix.rotation, scale = matrix.lossyScale };
    }
    static Material Material(string name, Color color)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.name = name; mat.color = color;
        mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", .25f); mat.SetFloat("_Metallic", .35f);
        AssetDatabase.CreateAsset(mat, Root + "/Generated/" + name + ".mat"); return mat;
    }
    static Bounds BoundsIn(Transform root, Transform space)
    {
        var points = root.GetComponentsInChildren<MeshFilter>(true).SelectMany(m => m.sharedMesh.vertices.Select(v => space.InverseTransformPoint(m.transform.TransformPoint(v)))).ToArray();
        var bounds = new Bounds(points[0], Vector3.zero); foreach (var p in points) bounds.Encapsulate(p); return bounds;
    }
    static GameObject BuildScrew(Material metal)
    {
        // Millimetre-scale hardware: broad Phillips head, shoulder and threaded shaft.
        // -Z is the accessible head; +Z enters the mounting hole.
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        { int n = vertices.Count; vertices.AddRange(new[] { a,b,c,d }); triangles.AddRange(new[] { n,n+1,n+2,n,n+2,n+3 }); }
        var profile = new List<Vector2> { new(.0037f,-.006f), new(.004f,-.0056f), new(.004f,-.0035f), new(.0016f,-.003f) };
        for (int thread = 0; thread < 9; thread++)
        { float z = -.003f + thread * .001f; profile.Add(new Vector2(.0016f,z)); profile.Add(new Vector2(.002f,z+.0003f)); profile.Add(new Vector2(.0016f,z+.0007f)); }
        profile.Add(new Vector2(.0012f,.006f));
        Vector3 Ring(Vector2 point, int segment) => new Vector3(Mathf.Cos(segment*Mathf.PI/12)*point.x, Mathf.Sin(segment*Mathf.PI/12)*point.x, point.y);
        for (int i=0;i<profile.Count-1;i++) for(int segment=0;segment<24;segment++)
            Quad(Ring(profile[i],segment),Ring(profile[i],segment+1),Ring(profile[i+1],segment+1),Ring(profile[i+1],segment));
        for(int segment=0;segment<24;segment++)
        {
            int n=vertices.Count; vertices.AddRange(new[]{new Vector3(0,0,-.006f),Ring(profile[0],segment+1),Ring(profile[0],segment)}); triangles.AddRange(new[]{n,n+1,n+2});
        }
        var mesh = new Mesh { name = "Phillips screw head and threaded shaft" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, Root + "/Generated/Phillips screw hardware.asset");
        var root = new GameObject("Phase screw");
        var body = new GameObject("Steel head and threads",typeof(MeshFilter),typeof(MeshRenderer)); body.transform.SetParent(root.transform,false);
        body.GetComponent<MeshFilter>().sharedMesh=mesh; body.GetComponent<MeshRenderer>().sharedMaterial=metal;
        vertices.Clear(); triangles.Clear();
        Quad(new Vector3(-.0025f,-.0005f,-.00602f),new Vector3(-.0025f,.0005f,-.00602f),new Vector3(.0025f,.0005f,-.00602f),new Vector3(.0025f,-.0005f,-.00602f));
        Quad(new Vector3(-.0005f,-.0025f,-.00603f),new Vector3(-.0005f,.0025f,-.00603f),new Vector3(.0005f,.0025f,-.00603f),new Vector3(.0005f,-.0025f,-.00603f));
        var recessMesh = new Mesh { name="Phillips drive recess" }; recessMesh.SetVertices(vertices); recessMesh.SetTriangles(triangles,0); recessMesh.RecalculateNormals(); recessMesh.RecalculateBounds();
        AssetDatabase.CreateAsset(recessMesh,Root+"/Generated/Phillips drive recess.asset");
        var recess=new GameObject("Phillips recess",typeof(MeshFilter),typeof(MeshRenderer)); recess.transform.SetParent(root.transform,false);
        recess.GetComponent<MeshFilter>().sharedMesh=recessMesh; recess.GetComponent<MeshRenderer>().sharedMaterial=Material("Screw drive recess",new Color(.035f,.04f,.045f));
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Generated/Phase screw.prefab"); UnityEngine.Object.DestroyImmediate(root); return prefab;
    }
    static GameObject Normalize(Transform source, string name, float length, bool lengthwise, Material material)
    {
        var filters = source.GetComponentsInChildren<MeshFilter>(true).Where(m => name != "Thermal paste applicator" || m.name != "Cap").ToArray();
        var vertices = filters.SelectMany(m => m.sharedMesh.vertices.Select(v => source.InverseTransformPoint(m.transform.TransformPoint(v)))).ToArray();
        var bounds = new Bounds(vertices[0],Vector3.zero); foreach(var point in vertices) bounds.Encapsulate(point); var size = bounds.size;
        int axis = lengthwise ? (size.x > size.y ? (size.x > size.z ? 0 : 2) : (size.y > size.z ? 1 : 2)) : (size.x < size.y ? (size.x < size.z ? 0 : 2) : (size.y < size.z ? 1 : 2));
        var direction = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        var rotation = Quaternion.FromToRotation(direction, Vector3.forward);
        float scale = length / (lengthwise ? size[axis] : Mathf.Max(size.x, size.y, size.z));
        // Put the narrowest endpoint at +Z so the screwdriver/paste tip follows its real mesh.
        if (lengthwise)
        {
            float Radius(bool positive) => vertices.Where(v => positive ? v[axis] > bounds.max[axis] - size[axis] * .06f : v[axis] < bounds.min[axis] + size[axis] * .06f)
                .Select(v => Vector3.ProjectOnPlane(v - bounds.center, direction).sqrMagnitude).DefaultIfEmpty(0).Average();
            if (Radius(true) > Radius(false)) rotation = Quaternion.FromToRotation(-direction, Vector3.forward);
        }
        var root = new GameObject(name);
        int index = 0;
        var driverMaterials = name == "Phase screwdriver" ? new[] { Material("Driver grey",new Color(.55f,.58f,.62f)), Material("Driver yellow",new Color(1,.72f,.045f)) } : null;
        foreach (var filter in filters)
        {
            if (filter.sharedMesh == null) continue;
            var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh); mesh.name = name + " mesh " + index;
            mesh.vertices = mesh.vertices.Select(v => rotation * (source.InverseTransformPoint(filter.transform.TransformPoint(v)) - bounds.center) * scale).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Root + "/Generated/" + name + "-" + index++ + ".asset");
            var child = new GameObject(filter.name, typeof(MeshFilter), typeof(MeshRenderer)); child.transform.SetParent(root.transform, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh; child.GetComponent<MeshRenderer>().sharedMaterials = Enumerable.Repeat(material, mesh.subMeshCount).ToArray();
            if (name == "Phase screwdriver")
            {
                var points = mesh.vertices; var faces = mesh.triangles; var steel = new List<int>(); var yellow = new List<int>();
                for (int triangle = 0; triangle < faces.Length; triangle += 3)
                {
                    var centre = (points[faces[triangle]] + points[faces[triangle+1]] + points[faces[triangle+2]]) / 3;
                    bool metal = centre.z > .005f || Mathf.Sin(Mathf.Atan2(centre.y, centre.x) * 6) > .55f;
                    (metal ? steel : yellow).AddRange(new[]{faces[triangle], faces[triangle+1], faces[triangle+2]});
                }
                mesh.subMeshCount = 2; mesh.SetTriangles(steel, 0); mesh.SetTriangles(yellow, 1);
                child.GetComponent<MeshRenderer>().sharedMaterials = driverMaterials;
            }
        }
        var result = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Generated/" + name + ".prefab"); UnityEngine.Object.DestroyImmediate(root); return result;
    }
    internal static List<Vector3> FindPlanarHoles(MeshFilter filter, Transform space)
    {
        var points = filter.sharedMesh.vertices.Select(v => space.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
        Directory.CreateDirectory("Logs/PhaseOne");
        File.WriteAllText("Logs/PhaseOne/pcb-mesh.json", JsonUtility.ToJson(new MeshDump { vertices = points, triangles = filter.sharedMesh.triangles }));
        var ids = new Dictionary<Vector3Int, int>(); var unique = new List<Vector3>(); var map = new int[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            var key = Vector3Int.RoundToInt(points[i] * 1000000);
            if (!ids.TryGetValue(key, out var id)) { id = unique.Count; ids[key] = id; unique.Add(points[i]); } map[i] = id;
        }
        var edges = new Dictionary<(int, int), int>();
        void Edge(int a, int b) { var key = a < b ? (a, b) : (b, a); edges[key] = edges.TryGetValue(key, out var n) ? n + 1 : 1; }
        var triangles = filter.sharedMesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = map[triangles[i]], b = map[triangles[i + 1]], c = map[triangles[i + 2]];
            var normal = Vector3.Cross(unique[b] - unique[a], unique[c] - unique[a]);
            if (normal.magnitude < 1e-14f) continue;
            normal /= normal.magnitude; // Source FBX is small: Vector3.normalized would discard these faces.
            if (Mathf.Abs(normal.z) < .99f) continue;
            Edge(a, b); Edge(b, c); Edge(c, a);
        }
        var graph = new Dictionary<int, List<int>>();
        foreach (var edge in edges.Where(p => p.Value == 1).Select(p => p.Key))
        { if (!graph.ContainsKey(edge.Item1)) graph[edge.Item1] = new(); if (!graph.ContainsKey(edge.Item2)) graph[edge.Item2] = new(); graph[edge.Item1].Add(edge.Item2); graph[edge.Item2].Add(edge.Item1); }
        var seen = new HashSet<int>(); var circles = new List<Vector3>();
        foreach (var id in graph.Keys)
        {
            if (!seen.Add(id)) continue; var group = new List<int>(); var todo = new Stack<int>(); todo.Push(id);
            while (todo.Count > 0) { int node = todo.Pop(); group.Add(node); foreach (var next in graph[node]) if (seen.Add(next)) todo.Push(next); }
            if (group.Count < 6 || group.Any(i => graph[i].Count != 2)) continue;
            var center = group.Select(i => unique[i]).Aggregate(Vector3.zero, (sum, p) => sum + p) / group.Count;
            var radii = group.Select(i => Vector3.Distance(unique[i], center)).ToArray();
            if (radii.Max() > .002f || radii.Min() < .0003f || radii.Max() / radii.Min() > 1.35f) continue;
            if (!circles.Any(p => Vector2.Distance(p, center) < .0002f)) circles.Add(center);
        }
        return circles;
    }
    // A folded sheet-metal envelope adds office-case access panels around the original tray/brackets.
    // Intersect the actual cooler bracket beneath each old bolt; its old head top is not a mounting surface.
    static Vector3 CoolerMountSurface(Transform cooler, Transform board, int bolt)
    {
        var old = BoundsIn(cooler.Find("pCylinder" + bolt), board);
        var point = new Vector3(old.center.x, old.center.y, old.max.z);
        float surface = float.NegativeInfinity;
        foreach (var filter in cooler.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.name.StartsWith("pCylinder18")) continue;
            var vertices = filter.sharedMesh.vertices.Select(v => board.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
            var triangles = filter.sharedMesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]]; var b = vertices[triangles[i+1]]; var c = vertices[triangles[i+2]];
                float denominator = (b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);
                if (Mathf.Abs(denominator) < 1e-12f) continue;
                float u = ((b.y-c.y)*(point.x-c.x)+(c.x-b.x)*(point.y-c.y))/denominator;
                float v = ((c.y-a.y)*(point.x-c.x)+(a.x-c.x)*(point.y-c.y))/denominator;
                if (u < -.0001f || v < -.0001f || u+v > 1.0001f) continue;
                float z = u*a.z+v*b.z+(1-u-v)*c.z;
                if (z <= old.max.z + .0001f) surface = Mathf.Max(surface, z);
            }
        }
        if (float.IsNegativeInfinity(surface)) throw new Exception("No cooler bracket beneath bolt " + bolt);
        point.z = surface; return point;
    }
    static GameObject BuildShell(Transform casing, Transform board, TechWisePhaseOneAssets data)
    {
        var bounds = BoundsIn(casing, casing);
        // Original feet stay outside the sheet-metal envelope; retain the authored tray's top/bottom.
        var tray = casing.Find("pCube884"); var trayBounds = BoundsIn(tray, casing);
        var min = bounds.min; var max = bounds.max; min.y = trayBounds.min.y; max.y = trayBounds.max.y;
        var sourceRoot = casing.root;
        foreach (var name in new[] { "RTX2080ti", "PSU", "MotherBoard" })
        { var b = BoundsIn(sourceRoot.Find(name), casing); min = Vector3.Min(min, b.min); max = Vector3.Max(max, b.max); }
        max.z += .006f; min.x -= .003f; max.x += .003f;
        var shell = new GameObject("Office case panels and mounts");
        var cpuCase = casing.InverseTransformPoint(sourceRoot.Find("CPU").position);
        float fanSize = .12f / 5f;
        Vector3 rear = new Vector3(max.x, Mathf.Clamp(cpuCase.y, min.y + fanSize, max.y - fanSize), (min.z + max.z) * .5f);
        // Front intake centres follow the existing grille's upper/lower mounting region.
        Vector3 intake1 = new Vector3(min.x, max.y - fanSize * .75f, (min.z + max.z) * .5f);
        Vector3 intake2 = intake1 - Vector3.up * fanSize * 1.1f;
        PanelMesh(shell.transform, "Top steel panel", new Vector3(min.x, max.y, min.z), Vector3.right * (max.x-min.x), Vector3.forward * (max.z-min.z), null, data.metal);
        PanelMesh(shell.transform, "Bottom steel panel", min, Vector3.right * (max.x-min.x), Vector3.forward * (max.z-min.z), null, data.metal);
        var psuOpening = BoundsIn(sourceRoot.Find("PSU"), casing); psuOpening.Expand(.002f);
        PanelMesh(shell.transform, "Rear exhaust grille", new Vector3(max.x,min.y,min.z), Vector3.forward * (max.z-min.z), Vector3.up * (max.y-min.y), new[]{rear}, data.metal, psuOpening);
        var opening = new GameObject("PSU rear opening"); opening.transform.SetParent(shell.transform, false);
        opening.transform.localPosition = new Vector3(max.x, psuOpening.center.y, psuOpening.center.z);
        opening.transform.localScale = new Vector3(.001f, psuOpening.size.y, psuOpening.size.z);
        PanelMesh(shell.transform, "Front intake grille", new Vector3(min.x,min.y,min.z), Vector3.forward * (max.z-min.z), Vector3.up * (max.y-min.y), new[]{intake1,intake2}, data.metal);
        PanelMesh(shell.transform, "Removable side panel", new Vector3(min.x,min.y,max.z), Vector3.right * (max.x-min.x), Vector3.up * (max.y-min.y), null, data.metal);
        for (int i=0;i<3;i++)
        {
            var anchor=new GameObject(i==0?"Rear fan mount":"Front fan mount "+i); anchor.transform.SetParent(shell.transform,false);
            anchor.transform.localPosition=(i==0?rear:i==1?intake1:intake2)+(i==0?-Vector3.right:Vector3.right)*.003f;
            anchor.transform.localRotation=Quaternion.LookRotation(Vector3.right,Vector3.up); // +X: front intake to interior; rear exhaust to outside.
        }
        var prefab=PrefabUtility.SaveAsPrefabAsset(shell,Root+"/Generated/Office case additions.prefab"); UnityEngine.Object.DestroyImmediate(shell); return prefab;
    }
    static void PanelMesh(Transform parent,string name,Vector3 origin,Vector3 u,Vector3 v,Vector3[] vents,Material mat, Bounds? opening = null)
    {
        var vertices=new List<Vector3>(); var triangles=new List<int>();
        int columns=vents==null?1:36, rows=vents==null?1:64;
        for(int y=0;y<rows;y++) for(int x=0;x<columns;x++)
        {
            var center=origin+u*((x+.5f)/columns)+v*((y+.5f)/rows);
            if (opening.HasValue)
            {
                var hole = opening.Value;
                if (center.y >= hole.min.y - v.magnitude / rows * .5f && center.y <= hole.max.y + v.magnitude / rows * .5f &&
                    center.z >= hole.min.z - u.magnitude / columns * .5f && center.z <= hole.max.z + u.magnitude / columns * .5f) continue;
            }
            bool vent=vents!=null&&vents.Any(p=>Vector3.Distance(center,p)<.0105f);
            if(vent && x%3!=0 && y%3!=0) continue;
            int a=vertices.Count; vertices.Add(origin+u*((float)x/columns)+v*((float)y/rows)); vertices.Add(origin+u*((float)(x+1)/columns)+v*((float)y/rows)); vertices.Add(origin+u*((float)(x+1)/columns)+v*((float)(y+1)/rows)); vertices.Add(origin+u*((float)x/columns)+v*((float)(y+1)/rows));
            triangles.AddRange(new[]{a,a+1,a+2,a,a+2,a+3});
            // Separate rear-face vertices preserve correct lighting on both sides of the sheet.
            vertices.AddRange(new[]{vertices[a],vertices[a+1],vertices[a+2],vertices[a+3]});
            triangles.AddRange(new[]{a+6,a+5,a+4,a+7,a+6,a+4});
        }
        var mesh=new Mesh{name=name}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh,Root+"/Generated/"+name+".asset");
        var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); obj.transform.SetParent(parent,false); obj.GetComponent<MeshFilter>().sharedMesh=mesh; obj.GetComponent<MeshRenderer>().sharedMaterial=mat;
    }
    static Mesh Blob()
    {
        var mesh=new Mesh{name="Thermal paste droplet"}; var v=new List<Vector3>(); var t=new List<int>();
        for(int y=0;y<=10;y++) for(int x=0;x<=24;x++) { float a=x*Mathf.PI*2/24,b=y*Mathf.PI/10; float ripple=1+.04f*Mathf.Sin(a*5); v.Add(new Vector3(Mathf.Sin(b)*Mathf.Cos(a)*.004f*ripple,Mathf.Cos(b)*.0015f,Mathf.Sin(b)*Mathf.Sin(a)*.004f*ripple)); }
        for(int y=0;y<10;y++) for(int x=0;x<24;x++){int a=y*25+x; t.AddRange(new[]{a,a+25,a+1,a+1,a+25,a+26});}
        mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();return mesh;
    }
    static Mesh Standoff()
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { int n = vertices.Count; vertices.AddRange(new[]{a,b,c,d}); triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3}); }
        for (int i = 0; i < 6; i++)
        {
            Vector3 Point(int j, float radius, float height) => new Vector3(Mathf.Cos(j*Mathf.PI/3)*radius,Mathf.Sin(j*Mathf.PI/3)*radius,height);
            var a = Point(i,.003f,0); var b = Point(i+1,.003f,0); var c = Point(i+1,.003f,.006f); var d = Point(i,.003f,.006f);
            var e = Point(i,.0013f,0); var f = Point(i+1,.0013f,0); var g = Point(i+1,.0013f,.006f); var h = Point(i,.0013f,.006f);
            Quad(a,b,c,d); Quad(f,e,h,g); Quad(d,c,g,h); Quad(e,f,b,a);
        }
        var mesh = new Mesh { name = "Threaded-support hex standoff" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); return mesh;
    }
}
