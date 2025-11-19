using UnityEngine;

/// <summary>
/// BrickData: stores metadata about a brick prefab including grid dimensions,
/// collider-based pivot offset, and visual dimensions. This ensures correct
/// grid placement regardless of prefab mesh/collider structure.
/// </summary>
public class BrickData : MonoBehaviour
{
    [Header("Grid Dimensions")]
    public int gridWidth = 1;
    public int gridHeight = 1;
    
    [Header("Collider Info")]
    [Tooltip("Offset from grid center (in world units) to apply when placing brick on grid.")]
    public Vector3 colliderCenterOffset = Vector3.zero;
    
    [Tooltip("When true, use this data; when false, fall back to name parsing and bounds detection.")]
    public bool useExplicitData = true;
    
    /// <summary>
    /// Calculate and cache the collider center offset based on BoxCollider bounds.
    /// Call this in the editor or at runtime to sync offset with current collider state.
    /// </summary>
    public void CacheColliderOffset(float cellSize)
    {
        var colliders = GetComponentsInChildren<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            colliderCenterOffset = Vector3.zero;
            return;
        }
        
        // Compute combined bounds of all colliders
        Bounds combined = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
            combined.Encapsulate(colliders[i].bounds);
        
        // Expected center if brick were perfectly aligned: grid center
        Vector3 gridCenter = transform.position + new Vector3(gridWidth * cellSize * 0.5f, 0, gridHeight * cellSize * 0.5f);
        
        // Actual collider center
        Vector3 actualCenter = combined.center;
        
        // Delta (offset): how far collider center is from grid center
        colliderCenterOffset = new Vector3(actualCenter.x - gridCenter.x, 0, actualCenter.z - gridCenter.z);
    }
}
