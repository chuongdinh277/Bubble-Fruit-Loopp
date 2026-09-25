using UnityEngine;
using UnityEditor;

public class MapExtractor : EditorWindow
{
    [MenuItem("Tools/1. Convert Current Scene To Level")]
    public static void ExtractMap()
    {
        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        GameObject envRoot = new GameObject("Level_New");
        
        foreach (var root in roots)
        {
            if (root.name != "Main Camera" && root.name != "Directional Light" && root.name != envRoot.name && root.name != "EventSystem")
            {
                root.transform.SetParent(envRoot.transform);
            }
        }

        BoxCollider groundCollider = envRoot.AddComponent<BoxCollider>();
        groundCollider.size = new Vector3(80f, 1f, 80f); 

        GameObject spawnPoint = new GameObject("PlayerSpawnPoint");
        spawnPoint.transform.SetParent(envRoot.transform);
        spawnPoint.transform.position = Vector3.up * 1f;

        Selection.activeGameObject = envRoot;
        Debug.Log("Gom nhóm thành công. Level root, ground collider và PlayerSpawnPoint đã được tạo.");
    }
}

