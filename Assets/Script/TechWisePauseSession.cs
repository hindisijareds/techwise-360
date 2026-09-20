using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

// Pause gameplay without disabling tracking, the XR input module, or controller UI rays.
public sealed class TechWisePauseSession : IDisposable
{
    public static bool Active { get; private set; }
    readonly float previousTimeScale;
    readonly bool affectsRuntimeTime;
    bool disposed;
    readonly List<Behaviour> locomotion = new();
    readonly List<XRInteractionManager> managers = new();
    readonly List<Action> restoreInteraction = new();
    readonly XRSelectFilterDelegate filter = new((hand, part) =>
        hand is XRSocketInteractor && hand.IsSelecting(part));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void InitializeSession()
    {
        Active = false;
        Time.timeScale = 1f;
    }

    public TechWisePauseSession()
    {
        previousTimeScale = Time.timeScale;
        affectsRuntimeTime = Application.isPlaying;
        Active = true;
        // Edit-mode diagnostics must never persist a paused TimeManager into the APK.
        if (affectsRuntimeTime) Time.timeScale = 0f;
        foreach (var provider in UnityEngine.Object.FindObjectsByType<LocomotionProvider>())
            Suspend(provider);
        foreach (var transformer in UnityEngine.Object.FindObjectsByType<XRBodyTransformer>())
            Suspend(transformer);
        foreach (var raycaster in UnityEngine.Object.FindObjectsByType<BaseRaycaster>())
            if (raycaster.name != "TechWise In-Game Menu Canvas") Suspend(raycaster);
        foreach (var manager in UnityEngine.Object.FindObjectsByType<XRInteractionManager>())
        {
            manager.selectFilters.Add(filter);
            managers.Add(manager);
        }
        // Canceled releases must not install parts, count mistakes, or complete lessons.
        // Socket selections stay intact, so installed parts never become loose on pause.
        foreach (var part in UnityEngine.Object.FindObjectsByType<XRBaseInteractable>())
            foreach (var hand in part.interactorsSelecting.ToArray())
                if (hand is not XRSocketInteractor && part.interactionManager != null)
                    part.interactionManager.SelectCancel(hand, part);
        // A nearby hover wins over UI in NearFarInteractor.UpdateUIModel, even when
        // selection is filtered. Make the existing controller rays UI-only during pause.
        foreach (var hand in UnityEngine.Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include))
        {
            if (hand is XRSocketInteractor) continue;
            var layers = hand.interactionLayers;
            restoreInteraction.Add(() => { if (hand != null) hand.interactionLayers = layers; });
            hand.interactionLayers = 0;
            if (hand is NearFarInteractor nearFar)
            {
                bool near = nearFar.enableNearCasting, block = nearFar.blockUIOnInteractableSelection;
                restoreInteraction.Add(() => { if (nearFar != null) { nearFar.enableNearCasting = near; nearFar.blockUIOnInteractableSelection = block; } });
                nearFar.enableNearCasting = false; nearFar.blockUIOnInteractableSelection = false;
            }
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Suspend(Behaviour behaviour)
    {
        if (!behaviour.enabled) return;
        locomotion.Add(behaviour);
        behaviour.enabled = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var restore in restoreInteraction) restore(); restoreInteraction.Clear();
        foreach (var manager in managers) if (manager != null) manager.selectFilters.Remove(filter);
        foreach (var behaviour in locomotion) if (behaviour != null) behaviour.enabled = true;
        managers.Clear(); locomotion.Clear();
        if (affectsRuntimeTime) Time.timeScale = previousTimeScale;
        Active = false;
    }
}
