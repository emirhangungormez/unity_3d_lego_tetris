using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;

public static class ConfettiPrefabCleaner
{
    [MenuItem("Tools/Confetti/Clean Prefabs in Scene")] 
    public static void CleanPrefabsInScene()
    {
        var runners = UnityEngine.Object.FindObjectsOfType<WinPanelConfetti>();
        if (runners == null || runners.Length == 0)
        {
            EditorUtility.DisplayDialog("Confetti Cleaner", "No WinPanelConfetti components found in the current scene.", "OK");
            return;
        }

        int totalRemoved = 0;
        foreach (var runner in runners)
        {
            var arr = runner.confettiBrickPrefabs;
            if (arr == null || arr.Length == 0) continue;
            var cleaned = new List<GameObject>();
            int removed = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                var p = arr[i];
                if (p == null) { removed++; continue; }
                var comps = p.GetComponents<Component>();
                bool hasMissing = false;
                foreach (var c in comps) if (c == null) { hasMissing = true; break; }
                if (hasMissing)
                {
                    removed++; 
                    Debug.LogError($"Confetti Cleaner: Prefab '{p.name}' in '{runner.gameObject.name}' contains a missing script and was removed.");
                    continue;
                }
                cleaned.Add(p);
            }

            if (removed > 0)
            {
                runner.confettiBrickPrefabs = cleaned.ToArray();
                EditorUtility.SetDirty(runner);
                totalRemoved += removed;
            }
        }

        if (totalRemoved > 0)
            EditorUtility.DisplayDialog("Confetti Cleaner", $"Removed {totalRemoved} invalid prefab entries from WinPanelConfetti components.", "OK");
        else
            EditorUtility.DisplayDialog("Confetti Cleaner", "No invalid confetti prefabs found.", "OK");
    }
}
#endif
