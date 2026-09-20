using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class TechWiseTutorialInteractionVerification
{
    public static void Verify()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Singleplayer.unity");
        var label = UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(t => t.text.Equals("Let's get started", StringComparison.OrdinalIgnoreCase));
        var button = label.GetComponentInParent<Button>();
        var canvas = button.GetComponentInParent<Canvas>();
        var system = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        if (system.GetComponent<XRUIInputModule>() == null || system.GetComponents<BaseInputModule>().Count(m => m.enabled) != 1)
            throw new Exception("Tutorial requires one active XR UI input module.");
        if (!button.IsInteractable() || !button.gameObject.activeInHierarchy || canvas.renderMode != RenderMode.WorldSpace)
            throw new Exception("Tutorial button is not available.");
        var caster = canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
        if (caster == null || !caster.enabled) throw new Exception("Missing tracked raycaster.");
        Canvas.ForceUpdateCanvases();
        var center = button.transform.TransformPoint(((RectTransform)button.transform).rect.center);
        // Render once so editor-mode Canvas graphics have valid raycast depths.
        var testCamera = new GameObject("Tutorial verification camera").AddComponent<Camera>();
        testCamera.transform.SetPositionAndRotation(center - button.transform.forward * 2, button.transform.rotation);
        testCamera.targetTexture = new RenderTexture(512, 512, 24);
        canvas.worldCamera = testCamera;
        testCamera.Render();
        var data = new TrackedDeviceEventData(system) { layerMask = ~0,
            rayPoints = new List<Vector3> { center - button.transform.forward * 2, center + button.transform.forward } };
        var hits = new List<RaycastResult>();
        caster.Raycast(data, hits);
        if (!hits.Any(h => ExecuteEvents.GetEventHandler<IPointerClickHandler>(h.gameObject) == button.gameObject))
            throw new Exception("Tracked controller ray did not hit tutorial button.");
        var report = new System.Text.StringBuilder("PASS: XR input module, world canvas, interactable button, tracked ray hit.\nExisting click actions:\n");
        var calls = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        int checkedCalls = 0;
        var expected = new List<(GameObject target, bool active)>();
        // Allow runtime-only callbacks in this disposable editor scene; never save it.
        for (int i = 0; i < calls.arraySize; i++)
        {
            var call = calls.GetArrayElementAtIndex(i);
            var method = call.FindPropertyRelative("m_MethodName").stringValue;
            if (string.IsNullOrEmpty(method)) continue;
            var target = call.FindPropertyRelative("m_Target").objectReferenceValue;
            if (target == null) throw new Exception("Missing tutorial callback target.");
            report.AppendLine(target.name + "." + method);
            if (method != "SetActive" || !(target is GameObject go))
                throw new Exception("Unexpected tutorial action: " + method);
            bool active = call.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue;
            expected.Add((go, active));
            button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
            checkedCalls++;
        }
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        foreach (var state in expected)
            if (state.target.activeSelf != state.active) throw new Exception("Tutorial click state transition failed.");
        if (checkedCalls == 0 || button.gameObject.activeInHierarchy)
            throw new Exception("Tutorial welcome did not close.");
        report.AppendLine("PASS: Pointer enter/down/up/click dispatched; existing simulation-start callbacks ran and welcome closed. Device trigger testing still required.");
        Directory.CreateDirectory("Logs/TutorialInteraction");
        File.WriteAllText("Logs/TutorialInteraction/verification.txt", report.ToString());
        EditorSceneManager.OpenScene("Assets/Scenes/Singleplayer.unity");
        Debug.Log(report);
    }

    public static void BuildQuest()
    {
        Verify();
        var settings = File.ReadAllBytes("ProjectSettings/ProjectSettings.asset");
        var preloaded = PlayerSettings.GetPreloadedAssets();
        try
        {
            Directory.CreateDirectory("Builds/TutorialInteraction");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Singleplayer.unity", "Assets/Scenes/Multiplayer.unity" },
                locationPathName = "Builds/TutorialInteraction/TechWise360.apk",
                target = BuildTarget.Android, options = BuildOptions.Development });
            File.WriteAllText("Logs/TutorialInteraction/build-summary.txt",
                $"{report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Quest APK build failed.");
        }
        finally
        {
            PlayerSettings.SetPreloadedAssets(preloaded);
            File.WriteAllBytes("ProjectSettings/ProjectSettings.asset", settings);
        }
    }
}
