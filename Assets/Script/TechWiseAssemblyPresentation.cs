using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Disposable assembly-only labels and workbench fill lighting.</summary>
public sealed class TechWiseAssemblyPresentation : MonoBehaviour
{
    sealed class Entry { public XRGrabInteractable part; public TMP_Text label; }
    readonly List<Entry> entries = new();
    readonly List<Action> restore = new();
    readonly List<GameObject> retiredPlacards = new();
    static readonly HashSet<string> OldNames = new(StringComparer.OrdinalIgnoreCase)
    { "Motherboard", "CPU", "CPU COOLER", "RAM", "M.2 SSD", "SSD", "GRAPHIC CARD (GPU)", "GPU Connecter", "GPU Connector", "POWER SUPPLY (PSU)", "Thermal Paste" };
    internal void Initialize(TechWiseSimulationRuntime state)
    {
        foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
        {
            if (string.IsNullOrWhiteSpace(text.text) || !OldNames.Contains(System.Text.RegularExpressions.Regex.Replace(text.text, @"\s+", " ").Trim())) continue;
            if (TechWiseSimulationModeManager.IsKnowledgeDisplay(text.transform)) continue;
            if (TechWiseDetailedAssemblyRuntime.Instance.IsRetiredPaste(text.transform)) continue;
            // Hide only the component placard; the board, lesson panels and menus keep their existing UI.
            var target = text.gameObject;
            for (var parent = text.transform.parent; parent != null && parent.GetComponent<RectTransform>() != null; parent = parent.parent)
            {
                if (parent.GetComponent<Canvas>() != null) break;
                if (parent.GetComponent<Image>() != null || parent.GetComponent<RawImage>() != null) { target = parent.gameObject; break; }
            }
            bool active = target.activeSelf; var original = target;
            restore.Add(() => { if (original != null) original.SetActive(active); }); target.SetActive(false);
            retiredPlacards.Add(target);
        }
        foreach (var part in state.Parts)
        {
            var step = state.StepOf(part); if (part == null || step == null) continue;
            string name = step switch { "GPU" => "Graphics card (GPU)", "Storage" => "SATA SSD", "FanRear" => "Rear exhaust fan", "FanFront1" => "Front intake fan 1", "FanFront2" => "Front intake fan 2", _ => TechWiseSimulationRuntime.Label(step) };
            Add(part, name);
        }
        foreach (var tool in TechWiseAssemblyTool.Tools)
            Add(tool.GetComponent<XRGrabInteractable>(), tool.kind == TechWiseAssemblyTool.ToolKind.ThermalPaste ? "Thermal paste" : "Screwdriver");
        foreach (var part in FindObjectsByType<XRGrabInteractable>())
            if (part.name == "Removable side panel") Add(part, "Case side panel");
        var board = state.FindPart("Motherboard");
        if (board != null)
        {
            Fill("Workbench fill", board.transform.position + Vector3.up * 1.1f, .65f, 3);
            var socket = state.FindSocket("Motherboard");
            if (socket != null) Fill("Case interior fill", socket.transform.position + Vector3.up * .55f + Vector3.back * .35f, .45f, 1.8f);
        }
    }
    void Fill(string name, Vector3 position, float intensity, float range)
    {
        var obj = new GameObject(name, typeof(Light)); obj.transform.SetParent(transform, false); obj.transform.position = position;
        var light = obj.GetComponent<Light>(); light.type = LightType.Point; light.intensity = intensity; light.range = range;
        light.color = new Color(.92f, .96f, 1); light.shadows = LightShadows.None; light.renderMode = LightRenderMode.ForcePixel;
    }
    void Add(XRGrabInteractable part, string name)
    {
        if (part == null || entries.Any(e => e.part == part)) return;
        var obj = new GameObject("Component name: " + name, typeof(TextMeshPro)); obj.transform.SetParent(transform, false);
        var text = obj.GetComponent<TextMeshPro>(); text.text = name; text.fontSize = .105f; text.color = new Color(.2f, .82f, 1);
        text.alignment = TextAlignmentOptions.Bottom; text.rectTransform.sizeDelta = new Vector2(.25f, .022f);
        text.rectTransform.pivot = new Vector2(.5f, 0);
        text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
        entries.Add(new Entry { part = part, label = text });
    }
    void LateUpdate()
    {
        foreach (var placard in retiredPlacards) if (placard != null && placard.activeSelf) placard.SetActive(false);
        var camera = Camera.main; if (camera == null) return;
        foreach (var entry in entries)
        {
            bool visible = entry.part != null && entry.part.gameObject.activeInHierarchy && !TechWiseSimulationRuntime.IsHeld(entry.part);
            entry.label.gameObject.SetActive(visible); if (!visible) continue;
            if (!TechWiseComponentGeometry.BoundsOf(entry.part.transform, out var bounds)) { entry.label.gameObject.SetActive(false); continue; }
            var position = TechWiseComponentGeometry.Top(entry.part.transform, bounds);
            entry.label.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - camera.transform.position, camera.transform.up));
        }
    }
    void OnDestroy() { for (int i = restore.Count - 1; i >= 0; i--) restore[i](); }
}
