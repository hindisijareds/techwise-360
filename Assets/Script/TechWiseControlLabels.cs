using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Resolve the current action/override rather than a guessed controller button.</summary>
public static class TechWiseControlLabels
{
    public static string Binding(string map, string actionName)
    {
        var action = Resources.FindObjectsOfTypeAll<InputActionAsset>()
            .Select(a => a.FindAction(map + "/" + actionName, false)).Where(a => a != null)
            .OrderByDescending(a => a.enabled).FirstOrDefault();
        if (action == null) return actionName + " (check Controls)";
        int active = action.activeControl == null ? -1 : action.GetBindingIndexForControl(action.activeControl);
        var indices = Enumerable.Range(0, action.bindings.Count).OrderByDescending(i => i == active);
        foreach (int i in indices)
        {
            var b = action.bindings[i]; var path = b.effectivePath?.ToLowerInvariant();
            if (b.isComposite || b.isPartOfComposite || string.IsNullOrEmpty(path)) continue;
            if (active < 0 && !path.Contains("xrcontroller")) continue;
            string side = path.Contains("lefthand") ? "left " : path.Contains("righthand") ? "right " : "";
            if (path.Contains("grip")) return side + "Grip";
            if (path.Contains("trigger")) return side + "Trigger";
            if (path.Contains("primary2daxis") || path.Contains("thumbstick")) return side + "joystick";
            return action.GetBindingDisplayString(i);
        }
        return actionName + " (check Controls)";
    }
    public static string Both(string action) => Binding("XRI Left Interaction", action) + " / " + Binding("XRI Right Interaction", action);
    public static string Grab => Both("Select");
    public static string Activate => Both("Activate");
    public static string Ui => Both("UI Press");
    public static string Scroll => Both("UI Scroll");
}
