using UnityEngine;

// MovementManager: reads a FixedJoystick and issues movement commands to GameManager
// Behavior:
// - Horizontal joystick moves the current brick left/right; Vertical can optionally move forward/back.
// - Movement is stepped (one grid cell per command) but repeat speed scales with joystick magnitude.
// - Uses deadzone and configurable min/max repeat intervals to avoid runaway moves.

public class MovementManager : MonoBehaviour
{
    [Header("References")]
    public WorldSpaceJoystick joystick; // assign in inspector or will try to find
    public GameManager gameManager; // assign in inspector or will try to find
    public Camera mainCamera; // if not set, will use Camera.main

    [Header("Joystick Movement Settings")]
    [Tooltip("Joystick magnitude below this is ignored")]
    public float deadzone = 0.2f;

    [Tooltip("How quickly repeated move commands happen at maximum tilt (moves/sec)")]
    public float maxRepeatRate = 10f; // at full tilt

    [Tooltip("How slowly repeated move commands happen at threshold tilt (moves/sec)")]
    public float minRepeatRate = 1.5f; // at low tilt

    [Tooltip("Minimum absolute horizontal magnitude required to issue repeated moves")]
    public float minTiltForRepeat = 0.25f;
    
    [Tooltip("Camera axis angle threshold (degrees). 45 = split on diagonal lines.")]
    public float cameraAngleThreshold = 45f;

    [Header("Debug / Options")]
    [Tooltip("When true, joystick directions are mapped relative to camera. When false, uses world X/Z axes.")]
    public bool useCameraRelative = true;

    [Tooltip("Enable to log joystick input and mapped grid moves for debugging.")]
    public bool debugJoystick = false;

    // internal
    float horizTimer = 0f;
    float vertTimer = 0f;

    void Awake()
    {
        if (joystick == null)
            joystick = FindObjectOfType<WorldSpaceJoystick>();
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        // Ensure joystick handle is centered at startup if found
        if (joystick != null)
            joystick.CenterHandle();
        else
        {
            // Try to find joystick even if it's on an inactive GameObject (useful if UI was disabled at edit time)
            var all = Resources.FindObjectsOfTypeAll<WorldSpaceJoystick>();
            if (all != null && all.Length > 0)
            {
                joystick = all[0];
                // If the joystick GameObject is inactive, enable it so player can use it immediately
                if (joystick != null && joystick.gameObject != null && !joystick.gameObject.activeSelf)
                {
                    joystick.gameObject.SetActive(true);
                }
            }
        }
    }

    void Start()
    {
        // Some UI layout systems modify RectTransforms after Awake; ensure joystick is found
        if (joystick == null)
            joystick = FindObjectOfType<WorldSpaceJoystick>();
    }

    void Update()
    {
        if (gameManager == null || joystick == null) return;
        if (!gameManager.IsGameActive) return;

        float hx = joystick.Horizontal; // -1..1
        float hy = joystick.Vertical;   // -1..1 (optional: map to forward/back grid moves)

        HandleAxis(ref horizTimer, hx, true);
        HandleAxis(ref vertTimer, hy, false);
    }

    // axisTimer reference, value from joystick, isHorizontal true=>left/right else up/down
    void HandleAxis(ref float axisTimer, float value, bool isHorizontal)
    {
        float abs = Mathf.Abs(value);
        if (abs < deadzone)
        {
            axisTimer = 0f; // reset so next push triggers immediately
            return;
        }

        // Determine rate based on tilt magnitude (linear interpolation between min and max)
        float t = Mathf.InverseLerp(minTiltForRepeat, 1f, abs);
        t = Mathf.Clamp01(t);
        float rate = Mathf.Lerp(minRepeatRate, maxRepeatRate, t);
        float interval = 1f / Mathf.Max(0.0001f, rate);

        axisTimer += Time.deltaTime * (abs * 1f);

        // If timer exceeds interval scaled by tilt (so stronger tilt yields faster triggers)
        if (axisTimer >= interval)
        {
            axisTimer = 0f;
            int dir = value > 0f ? 1 : -1;
            if (isHorizontal)
            {
                // Choose mapping mode
                if (useCameraRelative)
                {
                    if (mainCamera == null) mainCamera = Camera.main;
                    if (mainCamera == null)
                    {
                        if (dir > 0) gameManager.OnMoveRightButton();
                        else gameManager.OnMoveLeftButton();
                    }
                    else
                    {
                        var move = MovementManagerHelpers.MapCameraDirectionToGrid(mainCamera.transform.right, dir, cameraAngleThreshold);
                        if (debugJoystick) Debug.Log($"Joystick H:{value} mapped to {move}");
                        switch (move)
                        {
                            case GridMove.Right: gameManager.OnMoveRightButton(); break;
                            case GridMove.Left: gameManager.OnMoveLeftButton(); break;
                            case GridMove.Up: gameManager.OnMoveUpButton(); break;
                            case GridMove.Down: gameManager.OnMoveDownButton(); break;
                        }
                    }
                }
                else
                {
                    if (dir > 0) gameManager.OnMoveRightButton();
                    else gameManager.OnMoveLeftButton();
                    if (debugJoystick) Debug.Log($"Joystick H:{value} mapped to world X {(dir>0?"Right":"Left")}");
                }
            }
            else
            {
                if (useCameraRelative)
                {
                    if (mainCamera == null) mainCamera = Camera.main;
                    if (mainCamera == null)
                    {
                        if (dir > 0) gameManager.OnMoveUpButton();
                        else gameManager.OnMoveDownButton();
                    }
                    else
                    {
                        var move = MovementManagerHelpers.MapCameraDirectionToGrid(mainCamera.transform.forward, dir, cameraAngleThreshold);
                        if (debugJoystick) Debug.Log($"Joystick V:{value} mapped to {move}");
                        switch (move)
                        {
                            case GridMove.Right: gameManager.OnMoveRightButton(); break;
                            case GridMove.Left: gameManager.OnMoveLeftButton(); break;
                            case GridMove.Up: gameManager.OnMoveUpButton(); break;
                            case GridMove.Down: gameManager.OnMoveDownButton(); break;
                        }
                    }
                }
                else
                {
                    if (dir > 0) gameManager.OnMoveUpButton();
                    else gameManager.OnMoveDownButton();
                    if (debugJoystick) Debug.Log($"Joystick V:{value} mapped to world Z {(dir>0?"Up":"Down")}");
                }
            }
        }
    }
}

public enum GridMove { Right, Left, Up, Down }

// Helper functions appended at end of file
public static class MovementManagerHelpers
{
    // Project vector to XZ and determine which grid direction (Right/Left/Up/Down)
    // corresponds to the input vector (camDir) multiplied by joystick sign (dir)
    // angleThresholdDeg: treat an axis as dominant if it's within this many degrees from the axis (45 is the diagonal split)
    public static GridMove MapCameraDirectionToGrid(Vector3 camDir3, int dir, float angleThresholdDeg = 45f)
    {
        Vector2 cam = new Vector2(camDir3.x, camDir3.z);
        if (cam.sqrMagnitude < 0.0001f)
        {
            // fallback: assume X axis
            return dir > 0 ? GridMove.Right : GridMove.Left;
        }

        cam.Normalize();

        // World grid unit vectors in XZ: right = (1,0), up = (0,1) [world Z]
        Vector2 unitX = new Vector2(1f, 0f);
        Vector2 unitZ = new Vector2(0f, 1f);

        float dotX = Vector2.Dot(cam, unitX); // cos(angle to +X)
        float dotZ = Vector2.Dot(cam, unitZ); // cos(angle to +Z)

        float absDotX = Mathf.Abs(dotX);
        float absDotZ = Mathf.Abs(dotZ);

        float cosThreshold = Mathf.Cos(angleThresholdDeg * Mathf.Deg2Rad);

        // If camera is strongly aligned to X within threshold degrees, pick X
        if (absDotX >= cosThreshold && absDotX >= absDotZ)
        {
            return (dotX * dir) > 0f ? GridMove.Right : GridMove.Left;
        }

        // If camera is strongly aligned to Z within threshold degrees, pick Z
        if (absDotZ >= cosThreshold && absDotZ > absDotX)
        {
            return (dotZ * dir) > 0f ? GridMove.Up : GridMove.Down;
        }

        // Fallback: choose the dominant projection (diagonal split at ~45°)
        if (absDotX >= absDotZ)
            return (dotX * dir) > 0f ? GridMove.Right : GridMove.Left;
        else
            return (dotZ * dir) > 0f ? GridMove.Up : GridMove.Down;
    }
}
