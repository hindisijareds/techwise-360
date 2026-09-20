using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Content.Interaction;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class TechWiseAssemblyCompletionProbe
{
    public static void Run()
    {
        Directory.CreateDirectory("Logs/AssemblyCompletion");
        EditorSceneManager.OpenScene("Assets/Scenes/Multiplayer.unity");
        var log = new StringBuilder();
        string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
        foreach (var p in Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include))
        {
            if (TechWiseSimulationModeManager.IsKnowledgeDisplay(p.transform)) continue;
            log.AppendLine("PART " + PathOf(p.transform) + " step=" + TechWiseSimulationModeManager.ResolveStepId(p.transform) + " pos=" + p.transform.position + " scale=" + p.transform.lossyScale);
            if (p.name.ToLower().Contains("gpu") || p.name.ToLower().Contains("psu") || p.name.ToLower().Contains("cooler"))
                foreach (var r in p.GetComponentsInChildren<MeshRenderer>()) log.AppendLine("  mesh " + r.name + " " + r.bounds);
        }
        foreach (var s in Object.FindObjectsByType<XRLockSocketInteractor>(FindObjectsInactive.Include))
            if (!TechWiseSimulationModeManager.IsKnowledgeDisplay(s.transform)) log.AppendLine("SOCKET " + PathOf(s.transform) + " step=" + TechWiseSimulationModeManager.ResolveStepId(s.transform) + " pose=" + s.GetAttachTransform(null).position);
        foreach (var light in Object.FindObjectsByType<Light>()) log.AppendLine("LIGHT " + light.name + " " + light.type + " " + light.intensity + " " + light.range);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/PC/dream-computer-setup/source/maya2sketchfab.fbx");
        var model = Object.Instantiate(source);
        foreach (var name in new[]{"PSU","WaterCooling/WaterBlock","RTX2080ti"})
            foreach(var r in model.transform.Find(name).GetComponentsInChildren<MeshRenderer>()) log.AppendLine("SOURCE " + name + "/" + r.name + " " + r.bounds);
        Object.DestroyImmediate(model);
        File.WriteAllText("Logs/AssemblyCompletion/probe.txt",log.ToString());
    }
}
