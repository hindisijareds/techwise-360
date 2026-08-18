using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-7300)]
public sealed class TechWiseCrosshairUiBridge : MonoBehaviour
{
    static readonly List<GraphicRaycaster> Raycasters = new();
    static readonly List<RaycastResult> Results = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (FindAnyObjectByType<TechWiseCrosshairUiBridge>() != null)
            return;

        var bridgeObject = new GameObject("TechWise Crosshair UI Bridge");
        DontDestroyOnLoad(bridgeObject);
        bridgeObject.AddComponent<TechWiseCrosshairUiBridge>();
    }

    public static bool TryPressCenteredButton(Camera camera)
    {
        if (camera == null || EventSystem.current == null)
            return false;

        EnsureCanvasRaycasters();

        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        var eventData = new PointerEventData(EventSystem.current)
        {
            position = center,
            pressPosition = center,
            button = PointerEventData.InputButton.Left,
        };

        Raycasters.Clear();
        foreach (var raycaster in FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (raycaster != null && raycaster.isActiveAndEnabled)
                Raycasters.Add(raycaster);
        }

        Raycasters.Sort((a, b) =>
        {
            var aCanvas = a.GetComponent<Canvas>();
            var bCanvas = b.GetComponent<Canvas>();
            var aOrder = aCanvas != null ? aCanvas.sortingOrder : 0;
            var bOrder = bCanvas != null ? bCanvas.sortingOrder : 0;
            return bOrder.CompareTo(aOrder);
        });

        foreach (var raycaster in Raycasters)
        {
            Results.Clear();
            raycaster.Raycast(eventData, Results);
            foreach (var result in Results)
            {
                var button = result.gameObject != null
                    ? result.gameObject.GetComponentInParent<Button>()
                    : null;

                if (button == null || !button.IsActive() || !button.interactable)
                    continue;

                button.onClick.Invoke();
                return true;
            }
        }

        return false;
    }

    static void EnsureCanvasRaycasters()
    {
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (canvas == null || !canvas.isActiveAndEnabled)
                continue;

            if (canvas.GetComponentInChildren<Button>(false) == null)
                continue;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }
}
