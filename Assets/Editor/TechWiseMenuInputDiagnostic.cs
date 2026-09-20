using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class TechWiseMenuInputDiagnostic
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run()
    {
        var log = new StringBuilder();
        TechWiseInGameMenu diagnosticMenu = null;
        try
        {
            PlayerPrefs.SetString(MainMenu.ControlModeKey, MainMenu.VrModeValue);
            PlayerPrefs.SetInt("TechWise360.ControlModeExplicit", 1);
            TechWiseSimulationModeManager.SetMode("assembly", "practice");
            EditorSceneManager.OpenScene("Assets/Scenes/Multiplayer.unity");

            log.AppendLine("=== TECHWISE MENU INPUT DIAGNOSTIC ===");
            log.AppendLine($"Active Scene: {SceneManager.GetActiveScene().name}");
            log.AppendLine($"Control Mode: {PlayerPrefs.GetString(MainMenu.ControlModeKey)}");

            // Check EventSystems
            var eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            log.AppendLine($"EventSystems count: {eventSystems.Length}");
            foreach (var es in eventSystems)
            {
                log.AppendLine($"  EventSystem: {es.name}, active: {es.gameObject.activeInHierarchy}, enabled: {es.enabled}, isCurrent: {EventSystem.current == es}");
                foreach (var mod in es.GetComponents<BaseInputModule>())
                {
                    log.AppendLine($"    Module: {mod.GetType().Name}, active: {mod.gameObject.activeInHierarchy}, enabled: {mod.enabled}");
                    if (mod is XRUIInputModule xrui)
                    {
                        var regField = typeof(XRUIInputModule).GetField("m_RegisteredInteractors", Hidden);
                        var regList = regField?.GetValue(xrui) as System.Collections.IList;
                        log.AppendLine($"    XRUI Registered Interactors: {regList?.Count ?? -1}");
                        if (regList != null)
                        {
                            for (int i = 0; i < regList.Count; i++)
                            {
                                var item = regList[i];
                                var interactorField = item.GetType().GetField("interactor");
                                var interactorObj = interactorField?.GetValue(item);
                                var activeField = item.GetType().GetField("active");
                                log.AppendLine($"      [{i}] {interactorObj?.GetType().Name} on {(interactorObj is Component c ? c.gameObject.name : "non-component")}, active={activeField?.GetValue(item)}");
                            }
                        }
                    }
                }
            }

            // Check Interactors
            var nearFars = UnityEngine.Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            log.AppendLine($"NearFarInteractors count: {nearFars.Length}");
            foreach (var nf in nearFars)
            {
                log.AppendLine($"  NearFar: {nf.name} (parent: {nf.transform.parent?.name}), active: {nf.gameObject.activeInHierarchy}, enabled: {nf.enabled}, layers: {nf.interactionLayers.value}, farCast: {nf.enableFarCasting}, uiInt: {nf.enableUIInteraction}");
                var uiReader = nf.uiPressInput;
                log.AppendLine($"    uiPressInput: reader={uiReader != null}, bypass={uiReader?.bypass?.GetType().Name}, performed={uiReader?.ReadIsPerformed()}");
            }

            // Ensure menu
            var menu = UnityEngine.Object.FindAnyObjectByType<TechWiseInGameMenu>(FindObjectsInactive.Include);
            if (menu == null)
            {
                typeof(TechWiseInGameMenu).GetMethod("Install", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
                menu = UnityEngine.Object.FindAnyObjectByType<TechWiseInGameMenu>(FindObjectsInactive.Include);
            }
            log.AppendLine($"TechWiseInGameMenu: {menu != null}");
            diagnosticMenu = menu;
            if (menu != null)
            {
                typeof(TechWiseInGameMenu).GetMethod("EnsureUi", Hidden)?.Invoke(menu, null);
                typeof(TechWiseInGameMenu).GetMethod("SetMenuOpen", Hidden)?.Invoke(menu, new object[] { true });

                log.AppendLine($"Menu isOpen after SetMenuOpen(true): {typeof(TechWiseInGameMenu).GetField("isOpen", Hidden)?.GetValue(menu)}");
                log.AppendLine($"TechWisePauseSession.Active: {TechWisePauseSession.Active}");
                log.AppendLine($"Time.timeScale: {Time.timeScale}");

                // Check menu canvas
                var canvas = (Canvas)typeof(TechWiseInGameMenu).GetField("canvas", Hidden)?.GetValue(menu);
                log.AppendLine($"Menu Canvas: {canvas?.name}, active: {canvas?.gameObject.activeInHierarchy}, renderMode: {canvas?.renderMode}, worldCam: {canvas?.worldCamera?.name}");
                var trRaycaster = canvas?.GetComponent<TrackedDeviceGraphicRaycaster>();
                log.AppendLine($"TrackedDeviceGraphicRaycaster on canvas: {trRaycaster != null}, enabled: {trRaycaster?.enabled}");
                if (trRaycaster != null)
                {
                    log.AppendLine($"  ignoreReversed: {trRaycaster.ignoreReversedGraphics}, 3dOcc: {trRaycaster.checkFor3DOcclusion}, 2dOcc: {trRaycaster.checkFor2DOcclusion}");
                }

                // Render once so editor-mode Canvas graphics have valid raycast depths
                var cam = Camera.main ?? canvas.worldCamera;
                if (cam != null)
                {
                    var rt = new RenderTexture(1024, 768, 24);
                    var oldTarget = cam.targetTexture;
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = oldTarget;
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }

                // Check buttons
                Canvas.ForceUpdateCanvases();
                var buttons = canvas?.GetComponentsInChildren<Button>(true);
                log.AppendLine($"Buttons on Menu Canvas: {buttons?.Length ?? 0}");
                if (buttons != null)
                {
                    foreach (var btn in buttons)
                    {
                        var graphic = btn.targetGraphic;
                        log.AppendLine($"  Button: '{btn.name}', active: {btn.gameObject.activeInHierarchy}, interactable: {btn.interactable}, graphic: {graphic?.name} (raycastTarget: {graphic?.raycastTarget}, depth: {graphic?.depth})");
                    }
                }

                // Test Raycast from controller or camera towards Resume Button
                var resumeBtn = buttons?.FirstOrDefault(b => b.name == "Resume Button");
                try
                {
                    if (resumeBtn != null && canvas != null && trRaycaster != null)
                    {
                        var rectTransform = resumeBtn.GetComponent<RectTransform>();
                        var worldPos = rectTransform.TransformPoint(rectTransform.rect.center);
                        log.AppendLine($"Resume Button worldPos: {worldPos}");

                        var rayOrigin = cam.transform.position;
                        var rayDir = (worldPos - rayOrigin).normalized;

                        var es = EventSystem.current ?? UnityEngine.Object.FindAnyObjectByType<EventSystem>();
                        var xrui = es?.GetComponent<XRUIInputModule>();
                        var eventData = new TrackedDeviceEventData(es)
                        {
                            rayPoints = new System.Collections.Generic.List<Vector3> { rayOrigin, rayOrigin + rayDir * 5f },
                            layerMask = -1,
                            button = PointerEventData.InputButton.Left
                        };
                        var results = new System.Collections.Generic.List<RaycastResult>();
                        trRaycaster.Raycast(eventData, results);
                        log.AppendLine($"Raycast against Resume Button hit count: {results.Count}");
                        foreach (var r in results)
                        {
                            log.AppendLine($"  Hit: {r.gameObject.name}, dist: {r.distance}, depth: {r.depth}");
                        }
                    }
                }
                catch (Exception rEx)
                {
                    log.AppendLine($"Raycast probe exception: {rEx.Message}");
                }

                // Verify Issue 2: Wrist shortcut hide/show and Settings toggle
                log.AppendLine("=== VERIFY ISSUE 2: WRIST SHORTCUT & SETTINGS ===");
                typeof(TechWiseInGameMenu).GetMethod("SetShortcutHidden", Hidden)?.Invoke(menu, new object[] { true });
                var wrist = (Canvas)typeof(TechWiseInGameMenu).GetField("vrPauseShortcut", Hidden)?.GetValue(menu);
                bool hiddenState = (bool)typeof(TechWiseInGameMenu).GetField("shortcutHidden", Hidden)?.GetValue(menu);
                log.AppendLine($"Wrist shortcutHidden after hide: {hiddenState}, canvas active: {wrist?.gameObject.activeInHierarchy}");
                typeof(TechWiseInGameMenu).GetMethod("ShowSettingsHelp", Hidden)?.Invoke(menu, null);
                var toggleBtn = (Button)typeof(TechWiseInGameMenu).GetField("shortcutVisibilityButton", Hidden)?.GetValue(menu);
                log.AppendLine($"Settings shortcutVisibilityButton active: {toggleBtn?.gameObject.activeInHierarchy}, label: {toggleBtn?.GetComponentInChildren<TMPro.TMP_Text>()?.text}");
                toggleBtn?.onClick.Invoke();
                hiddenState = (bool)typeof(TechWiseInGameMenu).GetField("shortcutHidden", Hidden)?.GetValue(menu);
                log.AppendLine($"Wrist shortcutHidden after settings toggle: {hiddenState}");

                // Verify Issue 3: Tutorial View Drag & Position preservation
                log.AppendLine("=== VERIFY ISSUE 3: TUTORIAL DRAG & LEAVE IN PLACE ===");
                var tutObj = new GameObject("Test Tutorial View");
                var tutView = tutObj.AddComponent<TechWiseTutorialView>();
                var tutOwner = tutObj.AddComponent<TechWiseTutorialRuntime>();
                typeof(TechWiseTutorialView).GetMethod("Create", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tutView, new object[] { cam, tutOwner });
                var panelRect = (RectTransform)typeof(TechWiseTutorialView).GetField("panel", Hidden)?.GetValue(tutView);
                var draggable = panelRect?.GetComponent<TechWiseDraggableUiPanel>();
                log.AppendLine($"Tutorial panel created: {panelRect != null}, draggable: {draggable != null}");
                Vector3 initialPos = panelRect != null ? panelRect.position : Vector3.zero;
                // Move panel to a custom position and simulate drag release
                if (panelRect != null) panelRect.position += Vector3.right * 1.5f;
                typeof(TechWiseDraggableUiPanel).GetMethod("OnSelectExited", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(draggable, new object[] { null });
                bool userRepo = (bool)(typeof(TechWiseTutorialView).GetField("userRepositioned", Hidden)?.GetValue(tutView) ?? false);
                log.AppendLine($"userRepositioned after drag: {userRepo}, WasDragged: {draggable?.WasDragged}");
                // Call PositionPanel(true) and EndControls() - position must NOT change
                Vector3 movedPos = panelRect != null ? panelRect.position : Vector3.zero;
                typeof(TechWiseTutorialView).GetMethod("PositionPanel", Hidden)?.Invoke(tutView, new object[] { true });
                typeof(TechWiseTutorialView).GetMethod("EndControls", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(tutView, null);
                Vector3 finalPos = panelRect != null ? panelRect.position : Vector3.zero;
                log.AppendLine($"Preserved placement after EndControls: {movedPos == finalPos} (pos: {finalPos})");
                UnityEngine.Object.DestroyImmediate(tutObj);

                // Verify Issue 4: Arrow directions
                log.AppendLine("=== VERIFY ISSUE 4: ARROW DIRECTIONS ===");
                var lineObj = new GameObject("Test Arrow Line");
                var lineRenderer = lineObj.AddComponent<LineRenderer>();
                var geomType = typeof(TechWiseTutorialView).Assembly.GetType("TechWiseComponentGeometry");
                var targetArrowMethod = geomType?.GetMethod("TargetArrow", BindingFlags.Static | BindingFlags.NonPublic);
                var ramSocket = UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Content.Interaction.XRLockSocketInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(s => s.name.IndexOf("RAM", StringComparison.OrdinalIgnoreCase) >= 0);
                var gpuSocket = UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Content.Interaction.XRLockSocketInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(s => s.name.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0);
                if (ramSocket != null && targetArrowMethod != null)
                {
                    targetArrowMethod.Invoke(null, new object[] { lineRenderer, ramSocket.transform, 0.12f });
                    Vector3 tail = lineRenderer.GetPosition(0);
                    Vector3 tip = lineRenderer.GetPosition(1);
                    Vector3 dir = (tail - tip).normalized;
                    log.AppendLine($"RAM Socket arrow: approach={dir:F3}, isDownward={(dir.y > 0.8f)}");
                }
                if (gpuSocket != null && targetArrowMethod != null)
                {
                    targetArrowMethod.Invoke(null, new object[] { lineRenderer, gpuSocket.transform, 0.12f });
                    Vector3 tail = lineRenderer.GetPosition(0);
                    Vector3 tip = lineRenderer.GetPosition(1);
                    Vector3 dir = (tail - tip).normalized;
                    log.AppendLine($"GPU Socket arrow: approach={dir:F3}, isSide={(Mathf.Abs(dir.y) < 0.2f)}");
                }
                UnityEngine.Object.DestroyImmediate(lineObj);

                // Verify Issue 5: Zoom Full-View Magnification
                log.AppendLine("=== VERIFY ISSUE 5: ZOOM FULL-VIEW MAGNIFICATION ===");
                var screwDetailObj = new GameObject("Test Screw Detail");
                var screwDetail = screwDetailObj.AddComponent<TechWiseScrewDetail>();
                log.AppendLine($"TechWiseScrewDetail created, CurrentZoom={screwDetail.CurrentZoom}, IsMagnifying={screwDetail.IsMagnifying}");
                UnityEngine.Object.DestroyImmediate(screwDetailObj);
            }

            // Dump sockets
            log.AppendLine("=== SOCKET ORIENTATIONS ===");
            var sockets = UnityEngine.Object.FindObjectsByType<UnityEngine.XR.Content.Interaction.XRLockSocketInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            log.AppendLine($"Found {sockets.Length} XRLockSocketInteractors:");
            foreach (var s in sockets)
            {
                var at = s.attachTransform != null ? s.attachTransform : s.transform;
                log.AppendLine($"  Socket '{s.name}' (parent: {s.transform.parent?.name}): attach={at.name}, pos={at.position:F3}, fwd={at.forward:F3}, up={at.up:F3}, right={at.right:F3}");
            }
        }
        catch (Exception ex)
        {
            log.AppendLine($"EXCEPTION: {ex}");
        }
        finally
        {
            if (diagnosticMenu != null)
                typeof(TechWiseInGameMenu).GetMethod("SetMenuOpen", Hidden)?.Invoke(diagnosticMenu, new object[] { false });
        }

        Directory.CreateDirectory("Logs/Diagnostic");
        File.WriteAllText("Logs/Diagnostic/menu-input.txt", log.ToString());
        Debug.Log(log.ToString());
    }
}
