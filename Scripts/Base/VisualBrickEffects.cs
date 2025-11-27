using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class VisualBrickEffects : MonoBehaviour
{
    [Header("Rotate Visual")]
    public float rotateDuration = 0.35f;
    
    [Header("Snap Visual")]
    public float snapUp = 0.12f;
    public float snapDuration = 0.18f;
    public float snapVibrate = 0.04f;
    [Tooltip("Maximum horizontal offset applied during snap (local space). Positive/negative chosen randomly to vary direction.")]
    public float snapHorizontalMax = 0.03f;

    Transform visual;
    Coroutine rotateCoroutine;
    Coroutine snapCoroutine;

    void Awake()
    {
        EnsureVisual();
    }

    void EnsureVisual()
    {
        if (visual != null) return;
        Transform existing = transform.Find("_Visual");
        if (existing != null)
        {
            visual = existing;
            return;
        }

        GameObject go = new GameObject("_Visual");
        visual = go.transform;
        visual.SetParent(transform, false);
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;

        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
        {
            if (child == visual) continue;
            children.Add(child);
        }
        foreach (var ch in children)
        {
            ch.SetParent(visual, true);
        }
    }

    /// <summary>
    /// Prepare visual so that an immediate rotation applied to the root will appear smooth.
    /// Call this immediately before rotating the root by +90*dir degrees.
    /// </summary>
    public void PrepareForInstantRootRotation(int dir)
    {
        EnsureVisual();
        // Stop any running rotate coroutine and clear it
        if (rotateCoroutine != null) { StopCoroutine(rotateCoroutine); rotateCoroutine = null; }
        // Ensure visual has no pre-rotation compensation: rotation will be immediate on the root
        visual.localRotation = Quaternion.identity;
    }

    public void PlayRotateVisual()
    {
        EnsureVisual();
        // Rotation animation disabled: ensure no coroutine is running and set visual to identity
        if (rotateCoroutine != null) { StopCoroutine(rotateCoroutine); rotateCoroutine = null; }
        visual.localRotation = Quaternion.identity;
    }

    IEnumerator RotateToIdentity()
    {
        // Rotation animation removed: immediately set identity and exit
        visual.localRotation = Quaternion.identity;
        rotateCoroutine = null;
        yield break;
    }

    [ContextMenu("Preview Rotate Animation")]
    void PreviewRotateContext()
    {
        // Rotation animation disabled: simply reset visual rotation for preview
        EnsureVisual();
        visual.localRotation = Quaternion.identity;
    }

    [ContextMenu("Preview Snap Animation")]
    void PreviewSnapContext()
    {
        EnsureVisual();
        PlaySnapVisual();
    }

    public void SetVisualLocalPosition(Vector3 pos)
    {
        EnsureVisual();
        visual.localPosition = pos;
    }

    public void PlaySnapVisual()
    {
        EnsureVisual();
        if (snapCoroutine != null) { StopCoroutine(snapCoroutine); snapCoroutine = null; }
        snapCoroutine = StartCoroutine(SnapCoroutine());
    }

    IEnumerator SnapCoroutine()
    {
        // Simple snap: upward motion then back to origin (no scale changes)
        Vector3 origPos = visual.localPosition;
        Quaternion origRot = visual.localRotation;

        // Choose a horizontal direction randomly so snaps vary (left/right/random)
        float horiz = Random.value < 0.5f ? -1f : 1f;
        float horizAmount = snapHorizontalMax * (0.5f + Random.value * 0.5f); // vary magnitude a bit
        Vector3 horizOffset = visual.right * (horiz * horizAmount);

        // Move up quickly (including a small horizontal displacement)
        Vector3 upPos = origPos + Vector3.up * snapUp + horizOffset;
        float half = snapDuration * 0.5f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / half);
            visual.localPosition = Vector3.Lerp(origPos, upPos, t);
            visual.localRotation = Quaternion.Slerp(origRot, Quaternion.Euler(0f, 0f, 3f), t * 0.5f);
            yield return null;
        }

        // Move back down to original (no undershoot)
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / half);
            Vector3 curHoriz = Vector3.Lerp(horizOffset, Vector3.zero, t);
            visual.localPosition = Vector3.Lerp(upPos, origPos, t) + curHoriz;
            visual.localRotation = Quaternion.Slerp(Quaternion.Euler(0f, 0f, 3f), Quaternion.identity, t * 0.5f);
            yield return null;
        }

        // Short decaying horizontal jitter for tactile feel (no vertical overshoot)
        float vibDur = Mathf.Max(0.001f, snapVibrate);
        float vibElapsed = 0f;
        while (vibElapsed < vibDur)
        {
            vibElapsed += Time.deltaTime;
            float f = 1f - (vibElapsed / vibDur);
            float shake = Mathf.Sin(vibElapsed * 80f) * 0.006f * f * (horizAmount * 10f);
            visual.localPosition = origPos + visual.right * shake;
            yield return null;
        }

        visual.localPosition = origPos;
        visual.localRotation = Quaternion.identity;
        snapCoroutine = null;
    }
}