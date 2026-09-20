using System;
using System.IO;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class TechWiseQuestMenuVerification
{
    const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    public static void RepairMainMenuScene()
    {
        var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var eventSystem = eventSystems.Length > 0
            ? eventSystems[0]
            : new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

        foreach (var extra in eventSystems)
            if (extra != eventSystem)
                UnityEngine.Object.DestroyImmediate(extra.gameObject);

        foreach (var module in eventSystem.GetComponents<BaseInputModule>())
            if (!(module is XRUIInputModule))
                UnityEngine.Object.DestroyImmediate(module);

        var xrModule = eventSystem.GetComponent<XRUIInputModule>() ?? eventSystem.gameObject.AddComponent<XRUIInputModule>();
        xrModule.enableXRInput = true;
        xrModule.enableMouseInput = true;
        xrModule.enableTouchInput = true;
        xrModule.enableGamepadInput = true;
        xrModule.enableJoystickInput = true;
        xrModule.enableBuiltinActionsAsFallback = true;
        xrModule.enabled = true;

        var menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>(FindObjectsInactive.Include);
        if (menu == null)
            throw new Exception("MainMenu scene has no MainMenu component.");
        var menuCanvas = Array.Find(
            UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None),
            canvas => canvas.name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0);
        if (menuCanvas == null)
            throw new Exception("MainMenu scene has no menu Canvas.");
        if (menuCanvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            menuCanvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        foreach (var nearFar in UnityEngine.Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            nearFar.enableUIInteraction = true;
            nearFar.enableFarCasting = true;
            if (nearFar.transform.parent == null ||
                nearFar.transform.parent.name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            var visual = nearFar.GetComponentInChildren<CurveVisualController>(true);
            if (visual != null)
            {
                visual.gameObject.SetActive(true);
                visual.enabled = true;
                visual.extendLineToEmptyHit = true;
                visual.maxVisualCurveDistance = 10f;
                var lr = visual.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.enabled = true;
                    UnityEditor.EditorUtility.SetDirty(lr);
                }
                UnityEditor.EditorUtility.SetDirty(visual);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            }
            UnityEditor.EditorUtility.SetDirty(nearFar);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(nearFar);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new Exception("Failed to save repaired MainMenu scene.");
    }

    public static void Verify()
    {
        if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        var original = SceneManager.GetActiveScene();
        var testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(testScene);
        try
        {
            var menu = new GameObject("Quest menu verification").AddComponent<MainMenu>();
            var camera = new GameObject("Quest test camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.6f, 0f);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(MainMenu).GetMethod("EnsureModernLayout", flags).Invoke(menu, null);
            typeof(MainMenu).GetMethod("ConfigureVrMenu", flags).Invoke(menu, new object[] { camera });
            typeof(MainMenu).GetMethod("MaintainVrInteractors", flags).Invoke(menu, null);
            var canvas = (Canvas)typeof(MainMenu).GetField("rootCanvas", flags).GetValue(menu);
            if (canvas.renderMode != RenderMode.WorldSpace || canvas.worldCamera != camera)
                throw new Exception("Quest menu must render in world space through the headset camera.");
            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                throw new Exception("Quest menu is missing tracked controller raycasting.");
            if (Vector3.Dot(canvas.transform.position - camera.transform.position, camera.transform.forward) < 1f)
                throw new Exception("Quest menu must be positioned in front of the headset.");
            var fields = new[] { "startButton", "practiceButton", "competitionButton", "desktopModeButton", "vrModeButton" };
            foreach (var field in fields)
                if (typeof(MainMenu).GetField(field, flags).GetValue(menu) == null)
                    throw new Exception($"Quest menu is missing {field}.");

            var modules = UnityEngine.Object.FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var xrModules = Array.FindAll(modules, module => module is XRUIInputModule && module.enabled);
            var competingModules = Array.FindAll(modules, module => !(module is XRUIInputModule) && module.enabled);
            if (xrModules.Length != 1 || competingModules.Length != 0)
                throw new Exception($"Expected one XRUIInputModule and no competing enabled modules; found {xrModules.Length} XR and {competingModules.Length} competing.");

            var controllerInteractors = Array.FindAll(
                UnityEngine.Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                interactor => interactor.transform.parent != null &&
                    interactor.transform.parent.name.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0);
            if (controllerInteractors.Length < 2)
                throw new Exception("Quest menu requires left and right controller NearFarInteractors.");
            foreach (var interactor in controllerInteractors)
            {
                var pressAction = interactor.uiPressInput?.inputActionReferencePerformed?.action;
                if (!interactor.enableUIInteraction || !interactor.enableFarCasting || pressAction == null ||
                    pressAction.name.IndexOf("UI Press", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new Exception($"{interactor.transform.parent.name} must use far UI interaction with a UI Press action.");
                var visual = interactor.GetComponentInChildren<CurveVisualController>(true);
                if (visual == null || !visual.enabled || !visual.extendLineToEmptyHit)
                    throw new Exception($"{interactor.transform.parent.name} is missing a visible ray visual.");
            }
            Directory.CreateDirectory("Logs/Quest2");
            File.WriteAllText("Logs/Quest2/menu-verification.txt",
                "PASS: saved XR UI module, no competing UI module, tracked raycaster, controller UI Press actions, visible rays, world-space placement, and menu buttons.\n");
        }
        finally
        {
            EditorSceneManager.CloseScene(testScene, true);
            if (original.IsValid()) SceneManager.SetActiveScene(original);
        }
    }

    public static void TestRaycastAtMenu()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== TEST RAYCAST AT MENU ===");

        // Find camera
        Camera camera = null;
        var allCameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"Total cameras found in scene: {allCameras.Length}");
        foreach (var cam in allCameras)
        {
            sb.AppendLine($"  Camera: {cam.gameObject.name} (tag: '{cam.gameObject.tag}'), enabled: {cam.enabled}, activeInHierarchy: {cam.gameObject.activeInHierarchy}");
            if (cam.isActiveAndEnabled && (cam.CompareTag("MainCamera") || cam.gameObject.name.IndexOf("Main Camera", StringComparison.OrdinalIgnoreCase) >= 0))
                camera = cam;
        }
        if (camera == null && Camera.main != null) camera = Camera.main;
        sb.AppendLine($"Camera.main currently returns: {(Camera.main != null ? Camera.main.gameObject.name : "null")}");

        // Find controllers
        var controllers = new[] { "Left Controller", "Right Controller" };
        foreach (var cName in controllers)
        {
            var cGo = GameObject.Find(cName);
            sb.AppendLine($"Controller GameObject '{cName}': {(cGo != null ? "FOUND" : "NOT FOUND")}");
            if (cGo != null)
            {
                sb.AppendLine($"  Active: {cGo.activeInHierarchy}, Pos: {cGo.transform.position}, Rot: {cGo.transform.rotation.eulerAngles}");
                foreach (var comp in cGo.GetComponents<Component>())
                {
                    sb.AppendLine($"    Component: {comp.GetType().FullName}, enabled: {(comp is Behaviour b ? b.enabled.ToString() : "N/A")}");
                }
            }
        }

        var menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>();
        if (menu == null)
        {
            sb.AppendLine("No MainMenu found in scene!");
            Directory.CreateDirectory("Logs/Quest2");
            File.WriteAllText("Logs/Quest2/raycast-test.txt", sb.ToString());
            return;
        }

        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        // Invoke Awake on MainMenu
        typeof(MainMenu).GetMethod("Awake", flags)?.Invoke(menu, null);

        if (camera == null)
        {
            var camGo = GameObject.Find("Main Camera") ?? GameObject.Find("XR Camera");
            camera = camGo?.GetComponent<Camera>();
        }
        if (camera == null) camera = Camera.main;
        sb.AppendLine($"Using Camera: {camera?.name ?? "null"}");
        if (camera != null)
        {
            typeof(MainMenu).GetMethod("ConfigureVrMenu", flags)?.Invoke(menu, new object[] { camera });
        }

        var canvas = (Canvas)typeof(MainMenu).GetField("rootCanvas", flags)?.GetValue(menu);
        sb.AppendLine($"Canvas: {canvas?.name ?? "null"}, renderMode: {canvas?.renderMode}, worldCamera: {canvas?.worldCamera?.name}");
        var trRaycaster = canvas?.GetComponent<TrackedDeviceGraphicRaycaster>();
        sb.AppendLine($"TrackedDeviceGraphicRaycaster on canvas: {trRaycaster != null}");

        var es = EventSystem.current ?? UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        sb.AppendLine($"EventSystem found: {es?.name ?? "null"}");
        if (es != null)
        {
            EventSystem.current = es;
            var xrui = es.GetComponent<XRUIInputModule>();
            sb.AppendLine($"XRUIInputModule on EventSystem: {xrui != null}, enabled: {xrui?.enabled}");
            foreach (var mod in es.GetComponents<BaseInputModule>())
            {
                sb.AppendLine($"  Module: {mod.GetType().Name}, enabled: {mod.enabled}");
            }
            if (xrui != null)
            {
                var regField = typeof(XRUIInputModule).GetField("m_RegisteredInteractors", BindingFlags.NonPublic | BindingFlags.Instance);
                var regList = regField?.GetValue(xrui) as System.Collections.IList;
                sb.AppendLine($"  Registered Interactors count: {regList?.Count ?? -1}");
                if (regList != null)
                {
                    for (int i = 0; i < regList.Count; i++)
                    {
                        var item = regList[i];
                        var interactorField = item.GetType().GetField("interactor");
                        var interactorObj = interactorField?.GetValue(item);
                        sb.AppendLine($"    [{i}] {interactorObj?.GetType().Name} on {(interactorObj is Component c ? c.gameObject.name : "non-component")}");
                    }
                }
            }
        }

        // Test raycast against canvas graphics directly
        if (canvas != null && camera != null && trRaycaster != null && es != null)
        {
            trRaycaster.ignoreReversedGraphics = false;
            trRaycaster.checkFor3DOcclusion = false;
            Canvas.ForceUpdateCanvases();

            var startBtn = (Button)typeof(MainMenu).GetField("startButton", flags)?.GetValue(menu);
            sb.AppendLine($"Start Button: {startBtn?.name ?? "null"}, active: {startBtn?.gameObject.activeInHierarchy}, interactable: {startBtn?.interactable}");
            var allGraphics = canvas.GetComponentsInChildren<Graphic>();
            sb.AppendLine($"Total graphics under canvas: {allGraphics.Length}");
            foreach (var g in allGraphics)
            {
                if (g.name.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0 || g.name.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sb.AppendLine($"  Graphic: {g.name} ({g.GetType().Name}), raycastTarget: {g.raycastTarget}, depth: {g.depth}, cull: {g.canvasRenderer.cull}, layer: {g.gameObject.layer}, active: {g.gameObject.activeInHierarchy}");
                }
            }

            if (startBtn != null)
            {
                var btnPos = startBtn.transform.position;
                sb.AppendLine($"Start Button world position: {btnPos}");
                var rayOrigin = camera.transform.position;
                var rayDir = (btnPos - rayOrigin).normalized;
                sb.AppendLine($"Test Ray origin: {rayOrigin}, dir: {rayDir}");

                var xruiModule = es.GetComponent<XRUIInputModule>();
                var nearFars = UnityEngine.Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var nf in nearFars)
                {
                    if (xruiModule != null) xruiModule.RegisterInteractor(nf);
                }

                var eventData = new TrackedDeviceEventData(es)
                {
                    position = new Vector2(-1, -1),
                    rayPoints = new System.Collections.Generic.List<Vector3> { rayOrigin, rayOrigin + rayDir * 5f },
                    layerMask = ~0,
                    pointerId = 1
                };
                var results = new System.Collections.Generic.List<RaycastResult>();
                trRaycaster.Raycast(eventData, results);
                sb.AppendLine($"Raycast results count: {results.Count}");
                foreach (var r in results)
                {
                    sb.AppendLine($"  Hit: {r.gameObject.name}, distance: {r.distance}, depth: {r.depth}");
                }
            }
        }

        Directory.CreateDirectory("Logs/Quest2");
        File.WriteAllText("Logs/Quest2/raycast-test.txt", sb.ToString());
        Debug.Log("Raycast test written to Logs/Quest2/raycast-test.txt");
    }

    public static void InspectMainMenuScene()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== INSPECTION OF MainMenu.unity ===");
        
        var roots = scene.GetRootGameObjects();
        sb.AppendLine($"Root count: {roots.Length}");

        var nearFars = UnityEngine.Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"NearFarInteractors found: {nearFars.Length}");
        foreach (var nf in nearFars)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var enableUi = typeof(NearFarInteractor).GetProperty("enableUIInteraction", flags)?.GetValue(nf) 
                        ?? typeof(NearFarInteractor).GetField("m_EnableUIInteraction", flags)?.GetValue(nf);
            var farCast = typeof(NearFarInteractor).GetProperty("enableFarCasting", flags)?.GetValue(nf)
                        ?? typeof(NearFarInteractor).GetField("m_EnableFarCasting", flags)?.GetValue(nf);
            sb.AppendLine($"  NearFar: {nf.gameObject.name} (parent: {nf.transform.parent?.name}), activeInHierarchy: {nf.gameObject.activeInHierarchy}, enabled: {nf.enabled}");
            sb.AppendLine($"    enableUIInteraction: {enableUi}, enableFarCasting: {farCast}");
        }

        var pokes = UnityEngine.Object.FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"PokeInteractors found: {pokes.Length}");
        foreach (var p in pokes)
        {
            sb.AppendLine($"  Poke: {p.gameObject.name} (parent: {p.transform.parent?.name}), activeInHierarchy: {p.gameObject.activeInHierarchy}, enabled: {p.enabled}, enableUIInteraction: {p.enableUIInteraction}");
        }

        var rays = UnityEngine.Object.FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"XRRayInteractors found: {rays.Length}");
        foreach (var r in rays)
        {
            sb.AppendLine($"  Ray: {r.gameObject.name} (parent: {r.transform.parent?.name}), activeInHierarchy: {r.gameObject.activeInHierarchy}, enabled: {r.enabled}, enableUIInteraction: {r.enableUIInteraction}");
        }



        var modalities = UnityEngine.Object.FindObjectsByType<XRInputModalityManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"XRInputModalityManagers found: {modalities.Length}");
        foreach (var m in modalities)
        {
            sb.AppendLine($"  Modality: {m.gameObject.name}, leftController: {m.leftController?.name}, rightController: {m.rightController?.name}, leftHand: {m.leftHand?.name}, rightHand: {m.rightHand?.name}");
        }

        var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"EventSystems found: {eventSystems.Length}");
        foreach (var es in eventSystems)
        {
            sb.AppendLine($"  EventSystem: {es.gameObject.name}, active: {es.gameObject.activeInHierarchy}, enabled: {es.enabled}");
            foreach (var mod in es.GetComponents<BaseInputModule>())
            {
                sb.AppendLine($"    Module: {mod.GetType().Name}, enabled: {mod.enabled}");
                if (mod is XRUIInputModule xrui)
                {
                    var regField = typeof(XRUIInputModule).GetField("m_RegisteredInteractors", BindingFlags.NonPublic | BindingFlags.Instance);
                    var regList = regField?.GetValue(xrui) as System.Collections.IList;
                    sb.AppendLine($"      Registered Interactors count: {regList?.Count ?? -1}");
                }
            }
        }

        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"Canvases found: {canvases.Length}");
        foreach (var c in canvases)
        {
            sb.AppendLine($"  Canvas: {c.gameObject.name}, renderMode: {c.renderMode}, worldCamera: {c.worldCamera?.name}, raycaster: {c.GetComponent<GraphicRaycaster>()?.GetType().Name}");
        }

        var iamList = UnityEngine.Object.FindObjectsByType<InputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"InputActionManagers found: {iamList.Length}");
        foreach (var iam in iamList)
        {
            sb.AppendLine($"  InputActionManager: {iam.gameObject.name}, active: {iam.gameObject.activeInHierarchy}, enabled: {iam.enabled}, actionAssets: {iam.actionAssets?.Count ?? 0}");
            if (iam.actionAssets != null)
            {
                foreach (var asset in iam.actionAssets)
                {
                    sb.AppendLine($"    Asset: {(asset != null ? asset.name : "null")}");
                }
            }
        }

        var lines = UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"LineRenderers found: {lines.Length}");
        foreach (var lr in lines)
        {
            sb.AppendLine($"  LineRenderer: {lr.gameObject.name} (parent: {lr.transform.parent?.name}), active: {lr.gameObject.activeInHierarchy}, enabled: {lr.enabled}");
        }

        var groups = UnityEngine.Object.FindObjectsByType<XRInteractionGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine($"XRInteractionGroups found: {groups.Length}");
        foreach (var g in groups)
        {
            sb.AppendLine($"  Group: {g.gameObject.name}, activeInHierarchy: {g.gameObject.activeInHierarchy}, enabled: {g.enabled}");
            var membersProp = typeof(XRInteractionGroup).GetField("m_StartingGroupMembers", BindingFlags.NonPublic | BindingFlags.Instance);
            var members = membersProp?.GetValue(g) as System.Collections.IEnumerable;
            if (members != null)
            {
                foreach (var m in members)
                {
                    sb.AppendLine($"    Starting Member: {m?.GetType().Name} - {(m is Component c ? c.gameObject.name : m?.ToString())}");
                }
            }
        }

        foreach (var nf in nearFars)
        {
            sb.AppendLine($"  NearFar components on {nf.gameObject.name} (parent: {nf.transform.parent?.name}):");
            foreach (var comp in nf.GetComponents<Component>())
            {
                sb.AppendLine($"    Component: {comp.GetType().FullName}, enabled: {(comp is Behaviour b ? b.enabled.ToString() : "N/A")}");
            }
            for (int ci = 0; ci < nf.transform.childCount; ci++)
            {
                var child = nf.transform.GetChild(ci);
                sb.AppendLine($"    Child: {child.name}, active: {child.gameObject.activeSelf}");
                foreach (var comp in child.GetComponents<Component>())
                {
                    sb.AppendLine($"      Child Component: {comp.GetType().FullName}, enabled: {(comp is Behaviour b ? b.enabled.ToString() : "N/A")}");
                }
            }
            // Check input actions
            var uiPressProp = typeof(NearFarInteractor).GetProperty("uiPressInput", BindingFlags.Public | BindingFlags.Instance);
            var uiPress = uiPressProp?.GetValue(nf);
            sb.AppendLine($"    uiPress: {uiPress}");
        }

        Directory.CreateDirectory("Logs/Quest2");
        File.WriteAllText("Logs/Quest2/mainmenu-inspection.txt", sb.ToString());
        Debug.Log("Inspection written to Logs/Quest2/mainmenu-inspection.txt");
    }

    public static void InspectGameplayScenes()
    {
        var sb = new System.Text.StringBuilder();
        var scenes = new[] { "Assets/Scenes/Singleplayer.unity", "Assets/Scenes/Multiplayer.unity" };

        foreach (var scenePath in scenes)
        {
            sb.AppendLine($"=== SCENE: {scenePath} ===");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var origins = UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"XROrigin count: {origins.Length}");
            foreach (var origin in origins)
            {
                sb.AppendLine($"  Origin: {origin.gameObject.name}, activeSelf: {origin.gameObject.activeSelf}, worldPos: {origin.transform.position}, scale: {origin.transform.lossyScale}");
                sb.AppendLine($"  RequestedTrackingOriginMode: {origin.RequestedTrackingOriginMode}, Current: {origin.CurrentTrackingOriginMode}");
                sb.AppendLine($"  CameraYOffset: {origin.CameraYOffset}");
                sb.AppendLine($"  CameraFloorOffsetObject: {(origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.name : "null")}");
                if (origin.CameraFloorOffsetObject != null)
                {
                    sb.AppendLine($"    Offset localPos: {origin.CameraFloorOffsetObject.transform.localPosition}, lossyScale: {origin.CameraFloorOffsetObject.transform.lossyScale}");
                }
                sb.AppendLine($"  Camera: {(origin.Camera != null ? origin.Camera.name : "null")}");
                if (origin.Camera != null)
                {
                    sb.AppendLine($"    Camera localPos: {origin.Camera.transform.localPosition}, worldPos: {origin.Camera.transform.position}, lossyScale: {origin.Camera.transform.lossyScale}");
                    var tpd = origin.Camera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                    sb.AppendLine($"    TrackedPoseDriver: {(tpd != null ? $"enabled={tpd.enabled}, trackingType={tpd.trackingType}" : "NONE")}");
                    var cam = origin.Camera.GetComponent<Camera>();
                    sb.AppendLine($"    Camera component: enabled={cam.enabled}, nearClip={cam.nearClipPlane}, farClip={cam.farClipPlane}");
                }

                var desktopControllers = origin.GetComponentsInChildren<TechWiseDesktopController>(true);
                sb.AppendLine($"  TechWiseDesktopController count: {desktopControllers.Length}");

                var characterControllers = origin.GetComponentsInChildren<CharacterController>(true);
                sb.AppendLine($"  CharacterController count: {characterControllers.Length}");
                foreach (var cc in characterControllers)
                {
                    sb.AppendLine($"    CC: {cc.gameObject.name}, enabled: {cc.enabled}, center: {cc.center}, height: {cc.height}, radius: {cc.radius}");
                }

                var resetters = origin.GetComponentsInChildren<XRMultiplayer.CharacterResetter>(true);
                sb.AppendLine($"  CharacterResetter count: {resetters.Length}");
                foreach (var cr in resetters)
                {
                    var offPosField = typeof(XRMultiplayer.CharacterResetter).GetField("offlinePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                    var onPosField = typeof(XRMultiplayer.CharacterResetter).GetField("onlinePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                    sb.AppendLine($"    CR offlinePos: {offPosField?.GetValue(cr)}, onlinePos: {onPosField?.GetValue(cr)}");
                }
            }

            var allCameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"All Cameras in scene: {allCameras.Length}");
            foreach (var c in allCameras)
            {
                var fullPath = c.name;
                var p = c.transform.parent;
                while (p != null) { fullPath = p.name + "/" + fullPath; p = p.parent; }
                sb.AppendLine($"  Cam path: {fullPath}, activeInHierarchy: {c.gameObject.activeInHierarchy}, enabled: {c.enabled}, pos: {c.transform.position}, tag: {c.tag}, depth: {c.depth}, targetTexture: {c.targetTexture?.name ?? "null"}, targetDisplay: {c.targetDisplay}");
                var tpd = c.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                sb.AppendLine($"    TrackedPoseDriver on {c.name}: {(tpd != null ? $"enabled={tpd.enabled}, trackingType={tpd.trackingType}" : "NONE")}");
            }

            var allDesktops = UnityEngine.Object.FindObjectsByType<TechWiseDesktopController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine($"All TechWiseDesktopControllers in scene: {allDesktops.Length}");
            foreach (var dc in allDesktops)
            {
                sb.AppendLine($"  DesktopController on: {dc.gameObject.name}, activeInHierarchy: {dc.gameObject.activeInHierarchy}, enabled: {dc.enabled}");
            }

            var allSpawns = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allSpawns)
            {
                if (t.name.IndexOf("Reset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.name.IndexOf("Spawn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.name.IndexOf("Table", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sb.AppendLine($"  KeyObject: {t.name}, pos: {t.position}, active: {t.gameObject.activeInHierarchy}");
                }
            }
        }

        Directory.CreateDirectory("Logs/Quest2");
        File.WriteAllText("Logs/Quest2/gameplay-inspection.txt", sb.ToString());
        Debug.Log("Gameplay inspection written to Logs/Quest2/gameplay-inspection.txt");
    }

    public static void RepairGameplayScenes()
    {
        // 1. Repair Singleplayer scene
        var spScene = EditorSceneManager.OpenScene("Assets/Scenes/Singleplayer.unity", OpenSceneMode.Single);
        foreach (var origin in UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            origin.RequestedTrackingOriginMode = Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 1.36144f;
            UnityEditor.EditorUtility.SetDirty(origin);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(origin);

            foreach (var resetter in origin.GetComponentsInChildren<XRMultiplayer.CharacterResetter>(true))
            {
                var offPosField = typeof(XRMultiplayer.CharacterResetter).GetField("offlinePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                var onPosField = typeof(XRMultiplayer.CharacterResetter).GetField("onlinePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                var spPos = new Vector3(0f, 0f, -1.47f);
                offPosField?.SetValue(resetter, spPos);
                onPosField?.SetValue(resetter, spPos);
                UnityEditor.EditorUtility.SetDirty(resetter);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(resetter);
            }
        }
        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.CompareTag("MainCamera"))
            {
                var tpd = c.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>() ?? c.gameObject.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                tpd.enabled = true;
                tpd.trackingType = UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType.RotationAndPosition;
                UnityEditor.EditorUtility.SetDirty(tpd);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(tpd);
            }
        }
        EditorSceneManager.MarkSceneDirty(spScene);
        EditorSceneManager.SaveScene(spScene);

        // 2. Repair Multiplayer scene (Practice & Competition)
        var mpScene = EditorSceneManager.OpenScene("Assets/Scenes/Multiplayer.unity", OpenSceneMode.Single);
        foreach (var origin in UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            origin.RequestedTrackingOriginMode = Unity.XR.CoreUtils.XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 1.36144f;
            origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            UnityEditor.EditorUtility.SetDirty(origin);
            UnityEditor.EditorUtility.SetDirty(origin.transform);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(origin);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(origin.transform);

            foreach (var resetter in origin.GetComponentsInChildren<XRMultiplayer.CharacterResetter>(true))
            {
                var offPosField = typeof(XRMultiplayer.CharacterResetter).GetField("offlinePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                var onPosField = typeof(XRMultiplayer.CharacterResetter).GetField("onlinePosition", BindingFlags.NonPublic | BindingFlags.Instance);
                offPosField?.SetValue(resetter, Vector3.zero);
                onPosField?.SetValue(resetter, Vector3.zero);
                UnityEditor.EditorUtility.SetDirty(resetter);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(resetter);
            }
        }
        foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c.CompareTag("MainCamera"))
            {
                var tpd = c.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>() ?? c.gameObject.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                tpd.enabled = true;
                tpd.trackingType = UnityEngine.InputSystem.XR.TrackedPoseDriver.TrackingType.RotationAndPosition;
                UnityEditor.EditorUtility.SetDirty(tpd);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(tpd);
            }
        }
        EditorSceneManager.MarkSceneDirty(mpScene);
        EditorSceneManager.SaveScene(mpScene);

        Debug.Log("Gameplay scenes repaired successfully (Singleplayer & Multiplayer).");
    }
}
