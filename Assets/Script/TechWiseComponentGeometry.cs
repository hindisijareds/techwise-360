using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Bounds from visible model meshes, never an off-centre FBX pivot, label, socket or outline.
internal static class TechWiseComponentGeometry
{
    internal static bool BoundsOf(Transform part, out Bounds bounds, bool includeInactive = false)
    {
        bounds = default; bool found = false;
        var worldToPart = part.worldToLocalMatrix;
        foreach (var filter in part.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled || !includeInactive && !renderer.gameObject.activeInHierarchy || filter.sharedMesh == null || filter.GetComponentInParent<TMP_Text>() != null) continue;
            var owner = filter.GetComponentInParent<XRGrabInteractable>();
            if (owner != null && owner.transform != part) continue;
            var meshBounds = filter.sharedMesh.bounds;
            var matrix = worldToPart * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var point = matrix.MultiplyPoint3x4(meshBounds.center + Vector3.Scale(meshBounds.extents,
                    new Vector3((i&1)==0?-1:1, (i&2)==0?-1:1, (i&4)==0?-1:1)));
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
            }
        }
        return found;
    }
    internal static bool Held(Transform part) => part != null && part.TryGetComponent<XRGrabInteractable>(out var grab) && TechWiseSimulationRuntime.IsHeld(grab);
    internal static Vector3 Top(Transform part, Bounds bounds)
    {
        var center = part.TransformPoint(bounds.center); float height = float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
            height = Mathf.Max(height, part.TransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i&1)==0?-1:1, (i&2)==0?-1:1, (i&4)==0?-1:1))).y);
        return new Vector3(center.x, height + .018f, center.z);
    }
    internal static void Outline(LineRenderer line, Transform part)
    {
        bool show = part != null && !Held(part) && BoundsOf(part, out _);
        line.enabled = show; if (!show) { line.positionCount = 0; return; }
        BoundsOf(part, out var bounds);
        var scale = part.lossyScale;
        bounds.Expand(new Vector3(.003f / Mathf.Abs(scale.x), .003f / Mathf.Abs(scale.y), .003f / Mathf.Abs(scale.z)));
        int[] path = {0,1,3,2,0,4,5,1,5,7,3,7,6,2,6,4}; line.positionCount = path.Length;
        for (int n = 0; n < path.Length; n++)
        {
            int i = path[n]; line.SetPosition(n, part.TransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i&1)==0?-1:1, (i&2)==0?-1:1, (i&4)==0?-1:1))));
        }
    }
    internal static void TargetArrow(LineRenderer line, Transform target, float length)
    {
        line.enabled = target != null; if (target == null) { line.positionCount = 0; return; }
        var tip = target.position;
        bool fan = target.GetComponent<TechWiseAssemblyPartId>()?.step?.StartsWith("Fan") == true ||
                   target.name.IndexOf("Fan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (target.parent != null && target.parent.name.IndexOf("Fan", StringComparison.OrdinalIgnoreCase) >= 0);

        Vector3 approach;
        Vector3 side;

        if (fan)
        {
            bool rear = target.name.IndexOf("Rear", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (target.parent != null && target.parent.name.IndexOf("Rear", StringComparison.OrdinalIgnoreCase) >= 0);
            approach = rear ? -target.forward : target.forward;
            side = Vector3.up;
        }
        else
        {
            // Determine candidate approach axis from target's orientation.
            Vector3 normal = target.forward;
            float fwdVertical = Vector3.Dot(target.forward, Vector3.up);
            float upVertical = Vector3.Dot(target.up, Vector3.up);

            if (Mathf.Abs(fwdVertical) < 0.35f && Mathf.Abs(upVertical) > 0.65f)
            {
                // forward is horizontal while up is vertical:
                // Check if this is a top-loaded socket on a horizontal surface (e.g. CPU socket)
                bool isUpwardSocket = target.name.IndexOf("CPU", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                      target.name.IndexOf("Cooler", StringComparison.OrdinalIgnoreCase) < 0;
                if (isUpwardSocket)
                {
                    normal = upVertical > 0f ? target.up : -target.up;
                }
                else
                {
                    // True side-facing target (GPU, PCIe, Bracket, Side SSD, Case Mounts):
                    normal = target.forward;
                }
            }
            else if (Mathf.Abs(fwdVertical) > 0.65f)
            {
                normal = fwdVertical > 0f ? target.forward : -target.forward;
            }
            else if (Mathf.Abs(upVertical) > 0.65f)
            {
                normal = upVertical > 0f ? target.up : -target.up;
            }

            float vertical = Vector3.Dot(normal, Vector3.up);
            if (vertical > 0.5f)
            {
                // Flat upward target surface: arrow points DOWN into the target.
                approach = normal.normalized;
                side = Vector3.ProjectOnPlane(target.right, approach).normalized;
                if (side.sqrMagnitude < 0.01f) side = target.right;
            }
            else if (vertical < -0.5f)
            {
                // Inverted target surface: approach from below.
                approach = normal.normalized;
                side = target.right;
            }
            else
            {
                // Side-facing target surface: horizontal approach from the side into the socket.
                var horiz = Vector3.ProjectOnPlane(normal, Vector3.up);
                if (horiz.sqrMagnitude < 0.01f) horiz = target.forward;
                approach = horiz.normalized;
                side = Vector3.up;
            }
        }

        line.positionCount = 5;
        line.SetPosition(0, tip + approach * length);
        line.SetPosition(1, tip);
        line.SetPosition(2, tip + approach * length * .25f + side * length * .16f);
        line.SetPosition(3, tip);
        line.SetPosition(4, tip + approach * length * .25f - side * length * .16f);
    }
}
