using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

/// <summary>Use the rig's existing analog action and input mediation without changing bindings.</summary>
internal sealed class TechWiseSmoothTurning : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        var root=new GameObject("TechWise smooth turn configuration"); DontDestroyOnLoad(root); root.AddComponent<TechWiseSmoothTurning>();
    }
    void OnEnable() { SceneManager.sceneLoaded+=Loaded; StartCoroutine(Configure()); }
    void OnDisable() { SceneManager.sceneLoaded-=Loaded; }
    void Loaded(Scene scene,LoadSceneMode mode) { StopAllCoroutines(); StartCoroutine(Configure()); }
    IEnumerator Configure()
    {
        // The rig can be spawned a few frames after sceneLoaded; apply once per new manager.
        var configured=new System.Collections.Generic.HashSet<ControllerInputActionManager>();
        for(int frame=0;frame<180;frame++)
        {
            foreach(var manager in FindObjectsByType<ControllerInputActionManager>(FindObjectsInactive.Include))
                if(configured.Add(manager)) { manager.smoothTurnEnabled=true; manager.uiScrollingEnabled=true; }
            foreach(var turn in FindObjectsByType<ContinuousTurnProvider>(FindObjectsInactive.Include)) { turn.turnSpeed=60; turn.enableTurnAround=false; }
            yield return null;
        }
    }
}
