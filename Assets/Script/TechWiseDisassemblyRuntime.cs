using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.Content.Interaction;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DefaultExecutionOrder(-8800)]
public sealed class TechWiseDisassemblyRuntime : MonoBehaviour
{
    const string SingleplayerSceneName = "Singleplayer";
    const string PracticeSceneName = "Multiplayer";

    public static bool IsPreparing { get; private set; }
    public static string[] LastSkippedSteps { get; private set; } = Array.Empty<string>();

    Coroutine prepareCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (FindAnyObjectByType<TechWiseDisassemblyRuntime>() != null)
            return;

        var runtimeObject = new GameObject("TechWise Disassembly Runtime");
        DontDestroyOnLoad(runtimeObject);
        runtimeObject.AddComponent<TechWiseDisassemblyRuntime>();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        PrepareIfNeeded();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (prepareCoroutine != null)
            StopCoroutine(prepareCoroutine);

        prepareCoroutine = null;
        IsPreparing = false;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PrepareIfNeeded();
    }

    void PrepareIfNeeded()
    {
        if (!IsGameplayScene(SceneManager.GetActiveScene().name) || !TechWiseSimulationModeManager.IsDisassembly)
        {
            IsPreparing = false;
            return;
        }

        if (prepareCoroutine != null)
            StopCoroutine(prepareCoroutine);

        prepareCoroutine = StartCoroutine(AutoAssembleForDisassemblyCoroutine());
    }

    public static bool InstallStepIntoSocket(string stepId)
    {
        if (string.IsNullOrEmpty(stepId))
            return false;

        bool found = false;
        var sceneParts = TechWiseSimulationRuntime.Instance != null
            ? TechWiseSimulationRuntime.Instance.Parts.ToArray()
            : FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Exclude);
        foreach (var interactable in sceneParts)
        {
            if (TechWiseSimulationModeManager.ResolveStepId(interactable.transform) != stepId) continue;
            found = true;
            XRLockSocketInteractor target = null;
            foreach (var socket in FindObjectsByType<XRLockSocketInteractor>(FindObjectsInactive.Exclude))
            {
                if (TechWiseSimulationModeManager.ResolveStepId(socket.transform) != stepId || !TechWiseSimulationRuntime.Matches(socket, interactable.transform)) continue;
                if (socket.IsSelecting(interactable)) { target = socket; break; }
                if (!socket.hasSelection && target == null) target = socket;
            }
            if (target == null || !InstallIntoSocket(interactable, target)) return false;
        }
        return found;
    }

    IEnumerator AutoAssembleForDisassemblyCoroutine()
    {
        IsPreparing = true; LastSkippedSteps = Array.Empty<string>();
        var deadline=Time.realtimeSinceStartup+15;
        while(TechWiseDetailedAssemblyRuntime.Instance==null || !TechWiseDetailedAssemblyRuntime.Instance.Ready)
        {
            if(Time.realtimeSinceStartup>deadline) { LastSkippedSteps=TechWiseBuildDefinition.AssemblyOrder; IsPreparing=false; Debug.LogError("Complete disassembly hardware failed to initialize."); yield break; }
            yield return null;
        }
        var state=TechWiseSimulationRuntime.Instance;
        foreach(var part in state.Parts)
            for(var t=part.transform;t!=null;t=t.parent) if(!t.gameObject.activeSelf) t.gameObject.SetActive(true);
        var skipped=new List<string>();
        foreach(var step in TechWiseBuildDefinition.AssemblyOrder)
        {
            if(!InstallStepIntoSocket(step)) skipped.Add(step);
            yield return null;
        }
        TechWiseDetailedAssemblyRuntime.Instance.PrepareDisassemblyHardware();
        yield return null;
        LastSkippedSteps=skipped.ToArray(); IsPreparing=false; prepareCoroutine=null;
        if(skipped.Count>0) Debug.LogError("Incomplete disassembly preparation: "+string.Join(", ",skipped));
    }

    static XRGrabInteractable FindStepInteractable(string stepId)
    {
        foreach (var interactable in FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Exclude))
        {
            if (interactable != null && TechWiseSimulationModeManager.ResolveStepId(interactable.transform) == stepId)
                return interactable;
        }

        return null;
    }

    static XRLockSocketInteractor FindStepSocket(string stepId)
    {
        foreach (var socket in FindObjectsByType<XRLockSocketInteractor>(FindObjectsInactive.Exclude))
        {
            if (socket != null && TechWiseSimulationModeManager.ResolveStepId(socket.transform) == stepId)
                return socket;
        }

        return null;
    }

    static bool InstallIntoSocket(XRGrabInteractable interactable, XRLockSocketInteractor socket)
    {
        if (interactable == null || socket == null)
            return false;

        if (socket.interactablesSelected.Count > 0)
            return socket.IsSelecting(interactable);

        if (!TechWiseSimulationRuntime.Matches(socket, interactable.transform))
            return false;

        var manager = socket.interactionManager ?? interactable.interactionManager ?? FindAnyObjectByType<XRInteractionManager>();
        var rigidbody = interactable.GetComponent<Rigidbody>() ?? interactable.GetComponentInParent<Rigidbody>();
        var wasKinematic = rigidbody != null && rigidbody.isKinematic;
        var originalPose = new Pose(interactable.transform.position, interactable.transform.rotation);
        if (manager == null) return false;

        if (interactable.isSelected && interactable.interactionManager != null)
            interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)interactable);

        if (rigidbody != null)
        {
            if (!rigidbody.isKinematic)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }
            rigidbody.isKinematic = true;
        }

        var target = socket.GetAttachTransform(interactable);
        var partAttach = interactable.GetAttachTransform(socket);
        interactable.transform.rotation = target.rotation * Quaternion.Inverse(partAttach.rotation) * interactable.transform.rotation;
        interactable.transform.position += target.position - partAttach.position;

        if (manager != null)
        {
            try
            {
                if (manager.IsSelectPossible((IXRSelectInteractor)socket, (IXRSelectInteractable)interactable))
                    manager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)interactable);
                else
                    Debug.LogWarning($"Preparation rejected for {interactable.name} in {socket.name}; part layers {interactable.interactionLayers.value}, socket layers {socket.interactionLayers.value}.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not prepare {interactable.name}: {exception.Message}");
            }
        }

        var installed = socket.IsSelecting(interactable);
        if (!installed) interactable.transform.SetPositionAndRotation(originalPose.position, originalPose.rotation);
        if (rigidbody != null && !installed)
            rigidbody.isKinematic = wasKinematic;

        return installed;
    }

    static bool IsGameplayScene(string sceneName)
    {
        return sceneName == SingleplayerSceneName || sceneName == PracticeSceneName;
    }
}
