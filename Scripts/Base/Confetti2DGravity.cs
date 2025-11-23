using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Confetti2DGravity : MonoBehaviour
{
    [Tooltip("Base gravity force applied (m/s^2). Final force = gravity * gravityScale * mass.")]
    public float gravity = 9.81f;
    [Tooltip("Multiplier for gravity to tune fall speed")]
    public float gravityScale = 1.0f;

    private Rigidbody rb;
    private Camera cam;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        // We'll drive gravity ourselves so disable built-in gravity
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        cam = Camera.main;
    }

    void FixedUpdate()
    {
        if (rb == null) return;
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        // Camera-space 'down' as a world vector
        Vector3 screenDownWorld = cam.transform.TransformDirection(Vector3.down);

        // Apply continuous force toward screen-down so confetti falls to screen bottom
        Vector3 force = screenDownWorld * gravity * gravityScale * rb.mass;
        rb.AddForce(force, ForceMode.Force);
    }
}
