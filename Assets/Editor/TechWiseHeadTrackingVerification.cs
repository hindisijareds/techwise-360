using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TechWiseHeadTrackingVerification
{
    const string RunKey = "TechWise.HeadTrackingVerification";
    static readonly string[] Scenes = { "MainMenu", "Singleplayer", "MainMenu", "Multiplayer", "MainMenu", "Singleplayer" };
    static XRHMD headset;
    static int sceneIndex;
    static int frames;
    static int lastFrame = -1;
    static InputAction firstPositionAction;
    static double deadline;
    static string previousMode;

    static TechWiseHeadTrackingVerification()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void Run()
    {
        SessionState.SetBool(RunKey, true);
        SessionState.SetString(RunKey + ".mode", PlayerPrefs.GetString(MainMenu.ControlModeKey, MainMenu.VrModeValue));
        PlayerPrefs.SetString(MainMenu.ControlModeKey, MainMenu.VrModeValue);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        EditorApplication.isPlaying = true;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunKey, false) || state != PlayModeStateChange.EnteredPlayMode) return;
        previousMode = SessionState.GetString(RunKey + ".mode", MainMenu.VrModeValue);
        headset = InputSystem.AddDevice<XRHMD>();
        sceneIndex = frames = 0;
        firstPositionAction = null;
        deadline = EditorApplication.timeSinceStartup + 240;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Tracking verification timed out.");
            if (!EditorApplication.isPlaying) throw new Exception("Play mode ended during verification.");
            // Existing scene UI errors can trigger the editor's Error Pause setting.
            // Keep this automated input test advancing through player frames.
            EditorApplication.isPaused = false;
            Application.runInBackground = true;
            EditorApplication.QueuePlayerLoopUpdate();
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            // Two distinct poses in each scene verify rotation AND translation.
            var pose = frames < 60 ? new Vector3(.2f, 1.65f, -.1f) : new Vector3(-.3f, 1.15f, .25f);
            var rotation = Quaternion.Euler(frames < 60 ? 10 : -15, frames < 60 ? 35 : 110, 0);
            InputSystem.QueueDeltaStateEvent(headset.centerEyePosition, pose);
            InputSystem.QueueDeltaStateEvent(headset.centerEyeRotation, rotation);
            InputSystem.QueueDeltaStateEvent(headset.trackingState, 3);
            InputSystem.QueueDeltaStateEvent(headset.isTracked, true);
            frames++;
            if (frames != 50 && frames != 110) return;
            var camera = Camera.main;
            if (camera == null) throw new Exception("No main camera in " + Scenes[sceneIndex]);
            var driver = camera.GetComponent<TrackedPoseDriver>();
            if (driver == null || !driver.isActiveAndEnabled) throw new Exception("Head driver inactive in " + Scenes[sceneIndex]);
            if (firstPositionAction == null) firstPositionAction = driver.positionInput.action;
            if (!ReferenceEquals(firstPositionAction, driver.positionInput.action)) throw new Exception("Head input recreated on transition.");
            if (Vector3.Distance(camera.transform.localPosition, pose) > .02f || Quaternion.Angle(camera.transform.localRotation, rotation) > 1f)
                throw new Exception($"{Scenes[sceneIndex]} pose mismatch: camera={camera.transform.localPosition}/{camera.transform.localRotation.eulerAngles}, expected={pose}/{rotation.eulerAngles}, device={headset.centerEyePosition.ReadValue()}, action={driver.positionInput.action.ReadValue<Vector3>()}, enabled={driver.positionInput.action.enabled}, state={driver.trackingStateInput.action.ReadValue<int>()}");
            Debug.Log("[HeadTrackingVerification] PASS " + Scenes[sceneIndex] + " pose " + frames);
            if (frames != 110) return;
            if (++sceneIndex == Scenes.Length) { Finish(null); return; }
            frames = 0;
            SceneManager.LoadScene(Scenes[sceneIndex]);
        }
        catch (Exception error) { Finish(error); }
    }

    static void Finish(Exception error)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(RunKey, false);
        PlayerPrefs.SetString(MainMenu.ControlModeKey, previousMode);
        if (headset != null && headset.added) InputSystem.RemoveDevice(headset);
        Directory.CreateDirectory("Logs/HeadTracking");
        File.WriteAllText("Logs/HeadTracking/verification.txt", error == null ? "PASS: 12 position/rotation checks across 6 scene visits; headset action retained across transitions." : error.ToString());
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
