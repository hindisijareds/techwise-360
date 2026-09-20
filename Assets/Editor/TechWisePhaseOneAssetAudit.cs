using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Content.Interaction;

public static class TechWisePhaseOneAssetAudit
{
    const string Output = "Logs/PhaseOne";
    public static void Run()
    {
        Directory.CreateDirectory(Output);
        foreach (var scene in new[] { "Multiplayer", "Singleplayer" })
        {
            EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
            var report = new StringBuilder();
            foreach (var root in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (TechWiseSimulationModeManager.IsKnowledgeDisplay(root)) continue;
                if (root.name != "Case_Box" && root.GetComponent<XRGrabInteractable>() == null && root.GetComponent<XRLockSocketInteractor>() == null && !root.name.Contains("MotherBoard")) continue;
                report.AppendLine("ROOT " + PathOf(root) + " world=" + root.position.ToString("F6") + " rotation=" + root.rotation.eulerAngles.ToString("F3") + " scale=" + root.lossyScale.ToString("F6") + " active=" + root.gameObject.activeInHierarchy);
                foreach (var mesh in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mesh.sharedMesh == null) continue;
                    var renderer = mesh.GetComponent<Renderer>();
                    var b = renderer != null ? renderer.bounds : new Bounds(mesh.transform.position, Vector3.zero);
                    report.AppendLine("MESH " + PathOf(mesh.transform) + " center=" + b.center.ToString("F6") + " size=" + b.size.ToString("F6") + " local=" + mesh.transform.localPosition.ToString("F6") + " rotation=" + mesh.transform.localEulerAngles.ToString("F3") + " vertices=" + mesh.sharedMesh.vertexCount + " material=" + (renderer != null ? string.Join(",", renderer.sharedMaterials.Select(m => m != null ? m.name : "NULL")) : "none"));
                }
                if (root.name == "Case_Box") Capture(root, scene + "-case", new Vector3(1, .35f, -1));
                if (root.name == "MotherBoard (Component)" || root.name == "MotherBoard" && root.GetComponent<XRGrabInteractable>() != null) Capture(root, scene + "-board", Vector3.up);
            }
            File.WriteAllText(Output + "/" + scene + "-geometry.txt", report.ToString());
        }
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/PC/dream-computer-setup/source/maya2sketchfab.fbx");
        var model = UnityEngine.Object.Instantiate(fbx);
        File.WriteAllLines(Output + "/model-hierarchy.txt", model.GetComponentsInChildren<Transform>(true).Select(t => PathOf(t)));
        Capture(model.transform, "complete-model", new Vector3(1, .35f, -1));
        UnityEngine.Object.DestroyImmediate(model);
    }
    static string PathOf(Transform t) { string path = t.name; while (t.parent != null) { t = t.parent; path = t.name + "/" + path; } return path; }
    static void Capture(Transform source, string name, Vector3 direction)
    {
        var root = new GameObject("Audit geometry"); var bounds = new Bounds(); bool first = true;
        foreach (var mesh in source.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = mesh.GetComponent<Renderer>(); if (renderer == null || mesh.sharedMesh == null) continue;
            var copy = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer)); copy.layer = 31; copy.transform.SetParent(root.transform);
            copy.transform.SetPositionAndRotation(mesh.transform.position, mesh.transform.rotation); copy.transform.localScale = mesh.transform.lossyScale;
            copy.GetComponent<MeshFilter>().sharedMesh = mesh.sharedMesh; copy.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            if (first) { bounds = copy.GetComponent<Renderer>().bounds; first = false; } else bounds.Encapsulate(copy.GetComponent<Renderer>().bounds);
        }
        var obj = new GameObject("Audit camera"); var camera = obj.AddComponent<Camera>(); camera.cullingMask = 1 << 31;
        camera.transform.position = bounds.center + direction.normalized * bounds.size.magnitude * 2;
        camera.transform.LookAt(bounds.center, direction == Vector3.up ? Vector3.forward : Vector3.up);
        camera.orthographic = true; camera.orthographicSize = bounds.size.magnitude * .48f; camera.nearClipPlane = .001f; camera.farClipPlane = bounds.size.magnitude * 5 + 10;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.24f, .27f, .31f);
        var rt = new RenderTexture(1400, 1100, 24); camera.targetTexture = rt; camera.Render(); var old = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(1400, 1100, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1400, 1100), 0, 0); tex.Apply(); File.WriteAllBytes(Output + "/" + name + ".png", tex.EncodeToPNG());
        RenderTexture.active = old; camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(obj); UnityEngine.Object.DestroyImmediate(root);
    }
}
