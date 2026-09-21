using System;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
public static class TechWiseMountAudit
{
    [Serializable] class MeshData { public string name; public Vector3[] vertices; public int[] triangles; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Multiplayer.unity");
        Directory.CreateDirectory("Logs/CompleteBuildAudit/Mounts");
        foreach(var id in new[]{"GPU","PSU","Storage"})
        {
            var part=UnityEngine.Object.FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Include).First(p=>!TechWiseSimulationModeManager.IsKnowledgeDisplay(p.transform)&&TechWiseSimulationModeManager.ResolveStepId(p.transform)==id);
            var meshes=part.GetComponentsInChildren<MeshFilter>(true).Where(f=>id=="Storage"||id=="GPU"&&f.name=="pCube1167"||id=="PSU"&&new[]{"pCube927","pCube928","pCube940"}.Contains(f.name));
            foreach(var mesh in meshes)
            {
                var data=new MeshData {name=id+"-"+mesh.name,vertices=mesh.sharedMesh.vertices.Select(v=>part.transform.InverseTransformPoint(mesh.transform.TransformPoint(v))).ToArray(),triangles=mesh.sharedMesh.triangles};
                File.WriteAllText("Logs/CompleteBuildAudit/Mounts/"+data.name+".json",JsonUtility.ToJson(data));
            }
        }
        TechWisePauseResetVerification.Run();
    }
}
