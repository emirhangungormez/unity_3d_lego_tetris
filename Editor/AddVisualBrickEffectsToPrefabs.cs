#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

// Small editor utility to add VisualBrickEffects to all prefabs under a given folder
public class AddVisualBrickEffectsToPrefabs : EditorWindow
{
    string searchFolder = "Assets/Prefabs";

    [MenuItem("Tools/Prefabs/Add VisualBrickEffects to Prefabs...")]
    static void OpenWindow()
    {
        GetWindow<AddVisualBrickEffectsToPrefabs>("Add Visual Effects");
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Add VisualBrickEffects to prefabs", EditorStyles.boldLabel);
        searchFolder = EditorGUILayout.TextField("Folder to scan:", searchFolder);

        if (GUILayout.Button("Scan and Add"))
        {
            AddToPrefabs(searchFolder);
        }
    }

    static void AddToPrefabs(string folder)
    {
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });
        int added = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // Load contents to safely edit prefab
            GameObject content = PrefabUtility.LoadPrefabContents(path);
            if (content == null) continue;

            if (content.GetComponent<VisualBrickEffects>() == null)
            {
                content.AddComponent(typeof(VisualBrickEffects));
                PrefabUtility.SaveAsPrefabAsset(content, path);
                added++;
            }

            PrefabUtility.UnloadPrefabContents(content);
        }

        EditorUtility.DisplayDialog("VisualBrickEffects", $"Finished. Added to {added} prefabs.", "OK");
    }
}
#endif
