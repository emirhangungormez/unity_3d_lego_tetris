using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Experimental.SceneManagement;
using System.Collections.Generic;

public static class ConfettiPrefabFixer
{
    [MenuItem("Tools/Confetti/Fix Prefabs Remove Missing Scripts (Project)")]
    public static void FixAllPrefabsInProject()
    {
        if (!EditorUtility.DisplayDialog("Confetti Prefab Fixer",
            "This will scan all prefabs in the project and remove missing script components. This operation is irreversible. Continue?",
            "Yes", "No"))
            return;

        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int fixedCount = 0;
        int total = guids.Length;

        try
        {
            for (int i = 0; i < total; i++)
            {
                string guid = guids[i];
                string path = AssetDatabase.GUIDToAssetPath(guid);
                EditorUtility.DisplayProgressBar("Fixing Prefabs", path, (float)i / total);

                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                bool changed = false;
                // Check root and all children
                var transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in transforms)
                {
                    var go = t.gameObject;
                    // This returns how many components were removed
                    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    if (removed > 0) changed = true;
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    fixedCount++;
                    Debug.Log($"Confetti Prefab Fixer: Cleaned missing scripts from prefab: {path}");
                }

                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.DisplayDialog("Confetti Prefab Fixer",
            fixedCount > 0 ? $"Finished. Fixed {fixedCount} prefabs." : "Finished. No prefabs needed fixing.", "OK");

        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Confetti/Fix Selected Prefab(s)")]
    public static void FixSelectedPrefabs()
    {
        var objs = Selection.GetFiltered<GameObject>(SelectionMode.Assets);
        if (objs == null || objs.Length == 0)
        {
            EditorUtility.DisplayDialog("Confetti Prefab Fixer", "No prefab assets selected. Please select prefab(s) in the Project window.", "OK");
            return;
        }

        int fixedCount = 0;
        foreach (var obj in objs)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            bool changed = false;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                var go = t.gameObject;
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (removed > 0) changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                fixedCount++;
                Debug.Log($"Confetti Prefab Fixer: Cleaned missing scripts from prefab: {path}");
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        EditorUtility.DisplayDialog("Confetti Prefab Fixer",
            fixedCount > 0 ? $"Finished. Fixed {fixedCount} selected prefab(s)." : "Finished. No selected prefabs needed fixing.", "OK");

        AssetDatabase.Refresh();
    }
}
#endif
