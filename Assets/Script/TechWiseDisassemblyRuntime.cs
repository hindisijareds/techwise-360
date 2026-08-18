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

        var interactable = FindStepInteractable(stepId);
        var socket = FindStepSocket(stepId);
        if (interactable == null || socket == null)
            return false;

        return InstallIntoSocket(interactable, socket);
    }

    IEnumerator AutoAssembleForDisassemblyCoroutine()
    {
        IsPreparing = true;
        LastSkippedSteps = Array.Empty<string>();

        yield return null;
        yield return new WaitForSeconds(0.2f);

        var interactablesByStep = new Dictionary<string, XRGrabInteractable>();
        foreach (var interactable in FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Exclude))
        {
            var stepId = TechWiseSimulationModeManager.ResolveStepId(interactable.transform);
            if (!string.IsNullOrEmpty(stepId) && !interactablesByStep.ContainsKey(stepId))
                interactablesByStep[stepId] = interactable;
        }

        var socketsByStep = new Dictionary<string, XRLockSocketInteractor>();
        foreach (var socket in FindObjectsByType<XRLockSocketInteractor>(FindObjectsInactive.Exclude))
        {
            var stepId = TechWiseSimulationModeManager.ResolveStepId(socket.transform);
            if (!string.IsNullOrEmpty(stepId) && !socketsByStep.ContainsKey(stepId))
                socketsByStep[stepId] = socket;
        }

        var skipped = new List<string>();
        foreach (var stepId in TechWiseSimulationModeManager.DisassemblyOrder)
        {
            if (!interactablesByStep.ContainsKey(stepId) || !socketsByStep.ContainsKey(stepId))
                skipped.Add(stepId);
        }

        foreach (var stepId in TechWiseSimulationModeManager.AssemblyOrder)
        {
            if (!interactablesByStep.TryGetValue(stepId, out var interactable) ||
                !socketsByStep.TryGetValue(stepId, out var socket))
            {
                continue;
            }

            InstallIntoSocket(interactable, socket);
            yield return null;
        }

        yield return new WaitForSeconds(0.25f);
        LastSkippedSteps = skipped.ToArray();
        IsPreparing = false;
        prepareCoroutine = null;
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
            return true;

        var manager = socket.interactionManager ?? interactable.interactionManager ?? FindAnyObjectByType<XRInteractionManager>();
        var rigidbody = interactable.GetComponent<Rigidbody>() ?? interactable.GetComponentInParent<Rigidbody>();
        var wasKinematic = rigidbody != null && rigidbody.isKinematic;

        if (interactable.isSelected && interactable.interactionManager != null)
            interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)interactable);

        if (rigidbody != null)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.isKinematic = true;
        }

        var target = socket.attachTransform != null ? socket.attachTransform : socket.transform;
        interactable.transform.SetPositionAndRotation(target.position, target.rotation);

        if (manager != null)
        {
            try
            {
                manager.SelectEnter((IXRSelectInteractor)socket, (IXRSelectInteractable)interactable);
                if (socket.interactablesSelected.Count == 0)
                    manager.SelectEnterUnconditionally((IXRSelectInteractor)socket, (IXRSelectInteractable)interactable);
            }
            catch
            {
                try
                {
                    manager.SelectEnterUnconditionally((IXRSelectInteractor)socket, (IXRSelectInteractable)interactable);
                }
                catch
                {
                    // The scoring runtime also recognizes moved-away parts, so a failed manual select is recoverable.
                }
            }
        }

        if (rigidbody != null)
            rigidbody.isKinematic = wasKinematic;

        return true;
    }

    static bool IsGameplayScene(string sceneName)
    {
        return sceneName == SingleplayerSceneName || sceneName == PracticeSceneName;
    }
}
