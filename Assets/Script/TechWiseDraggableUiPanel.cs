using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Enables dragging a World Space UI canvas in VR space via Grip while locking pitch/roll
/// (keeping the panel level and upright) and strictly locking its scale.
/// </summary>
[DisallowMultipleComponent]
public class TechWiseDraggableUiPanel : MonoBehaviour
{
    XRGrabInteractable grab;
    BoxCollider boxCollider;
    Rigidbody rb;
    Vector3 initialScale;
    bool initialized;

    public event System.Action Dragged;
    public bool WasDragged { get; private set; }

    public XRGrabInteractable GrabInteractable => grab;
    public BoxCollider Collider => boxCollider;

    void Awake()
    {
        EnsureComponents();
    }

    void EnsureComponents()
    {
        if (initialized)
            return;

        initialScale = transform.localScale;

        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
            boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.isTrigger = false;

        grab = GetComponent<XRGrabInteractable>();
        if (grab == null)
            grab = gameObject.AddComponent<XRGrabInteractable>();

        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.trackRotation = true;
        grab.throwOnDetach = false;
        grab.retainTransformParent = true;

        if (!grab.colliders.Contains(boxCollider))
        {
            grab.colliders.Clear();
            grab.colliders.Add(boxCollider);
        }

        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);

        // Keep upright at initialization
        var euler = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);

        initialized = true;
    }

    void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnSelectEntered);
            grab.selectExited.RemoveListener(OnSelectExited);
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        EnforceScaleAndUpright();
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        EnforceScaleAndUpright();
        WasDragged = true;
        Dragged?.Invoke();
    }

    public void SetBounds(Vector2 sizeDelta, Vector2 centerOffset, float depth = 40f)
    {
        EnsureComponents();
        if (boxCollider != null)
        {
            var targetSize = new Vector3(Mathf.Max(sizeDelta.x, 50f), Mathf.Max(sizeDelta.y, 50f), depth);
            // Bias collider center slightly in front of the UI canvas so raycasts detect it cleanly
            var targetCenter = new Vector3(centerOffset.x, centerOffset.y, -depth * 0.25f);
            if (boxCollider.size != targetSize)
                boxCollider.size = targetSize;
            if (boxCollider.center != targetCenter)
                boxCollider.center = targetCenter;
        }
    }

    void LateUpdate()
    {
        if (!initialized)
            return;

        EnforceScaleAndUpright();
    }

    void EnforceScaleAndUpright()
    {
        // Prevent scaling manipulation
        if (transform.localScale != initialScale)
            transform.localScale = initialScale;

        // Keep strictly upright and horizontal (pitch and roll locked to 0)
        var euler = transform.rotation.eulerAngles;
        if (Mathf.Abs(euler.x) > 0.001f || Mathf.Abs(euler.z) > 0.001f)
            transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
    }
}
