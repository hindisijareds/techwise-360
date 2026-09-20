using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Full-view magnification while driving fasteners.
/// Smoothly scales stereo projection matrices in VR (or camera FOV on desktop),
/// preserving normal 6DOF headset tracking, keeping both eyes consistent, without scaling world geometry.
/// Replaces previous floating picture-in-picture boxed inset panel.
/// </summary>
public sealed class TechWiseScrewDetail : MonoBehaviour
{
    internal static TechWiseScrewDetail Instance { get; private set; }

    const float TargetZoom = 1.6f;
    const float ZoomSpeed = 5f;

    TechWiseFastener screw;
    TechWiseAssemblyTool tool;
    float drivenAt;
    float currentZoom = 1f;
    float baseFov = -1f;
    bool stereoApplied;

    // Retained for compatibility with reflection lookups in verification tools
    RenderTexture texture;

    public float CurrentZoom => currentZoom;
    public bool IsMagnifying => currentZoom > 1.05f;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        Camera.onPreCull += OnPreCullCamera;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        Camera.onPreCull -= OnPreCullCamera;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        ResetMagnification();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Camera.onPreCull -= OnPreCullCamera;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        ResetMagnification();
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
            texture = null;
        }
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        OnPreCullCamera(cam);
    }

    internal void Driving(TechWiseFastener fastener, TechWiseAssemblyTool driver)
    {
        screw = fastener;
        tool = driver;
        drivenAt = Time.unscaledTime;
    }

    void LateUpdate()
    {
        bool active = screw != null && tool != null && tool.TriggerPressed &&
                      !TechWisePauseSession.Active && (Time.unscaledTime - drivenAt < .18f);

        float target = active ? TargetZoom : 1f;
        currentZoom = Mathf.MoveTowards(currentZoom, target, Time.unscaledDeltaTime * ZoomSpeed);

        var cam = Camera.main;
        if (cam != null && !cam.stereoEnabled)
        {
            if (baseFov <= 0f) baseFov = cam.fieldOfView > 0 ? cam.fieldOfView : 60f;
            if (currentZoom > 1.001f)
            {
                cam.fieldOfView = baseFov / currentZoom;
            }
            else if (cam.fieldOfView != baseFov && baseFov > 0f)
            {
                cam.fieldOfView = baseFov;
            }
        }
    }

    void OnPreCullCamera(Camera cam)
    {
        if (cam == null || cam != Camera.main) return;

        if (cam.stereoEnabled && currentZoom > 1.001f)
        {
            var left = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
            var right = cam.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
            left.m00 *= currentZoom;
            left.m11 *= currentZoom;
            right.m00 *= currentZoom;
            right.m11 *= currentZoom;
            cam.SetStereoProjectionMatrix(Camera.StereoscopicEye.Left, left);
            cam.SetStereoProjectionMatrix(Camera.StereoscopicEye.Right, right);
            stereoApplied = true;
        }
        else if (stereoApplied && currentZoom <= 1.001f)
        {
            cam.ResetStereoProjectionMatrices();
            cam.ResetProjectionMatrix();
            stereoApplied = false;
        }
    }

    void ResetMagnification()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            if (stereoApplied || cam.stereoEnabled)
            {
                cam.ResetStereoProjectionMatrices();
                cam.ResetProjectionMatrix();
                stereoApplied = false;
            }
            if (baseFov > 0f)
            {
                cam.fieldOfView = baseFov;
            }
        }
        currentZoom = 1f;
    }

    // Retained for backwards compatibility with reflection calls
    internal void RenderDetail()
    {
        if (texture == null)
            texture = new RenderTexture(384, 384, 24) { name = "Screw detail texture" };
    }
}
