using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class CleanSceneScripts
{
    [MenuItem("Tools/Clean Missing Scripts In Scene")]
    public static void Clean()
    {
        var scenePath = "Assets/Scenes/DofusUnity.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[CleanScene] Scene not valid: {scenePath}");
            return;
        }

        int totalRemoved = 0;
        var roots = scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            totalRemoved += CleanRecursive(root);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[CleanScene] SUCCESS: Removed {totalRemoved} missing scripts from {scenePath}");
    }

    static int CleanRecursive(GameObject go)
    {
        int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        for (int i = 0; i < go.transform.childCount; i++)
        {
            count += CleanRecursive(go.transform.GetChild(i).gameObject);
        }
        return count;
    }
}

