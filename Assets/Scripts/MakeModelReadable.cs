#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Pon este archivo en una carpeta llamada "Editor" (Assets/Editor/MakeModelReadable.cs).
// Menú: Clasico > Activar Read-Write del modelo seleccionado
// Selecciona en Project el modelo del jugador (FBX) o su prefab y ejecuta el comando.
public static class MakeModelReadable
{
    [MenuItem("Clasico/Activar Read-Write del modelo seleccionado")]
    static void Run()
    {
        string path = FindModelPath(Selection.activeObject);
        if (string.IsNullOrEmpty(path))
        {
            EditorUtility.DisplayDialog("Read/Write",
                "Selecciona en la ventana Project el modelo del jugador (FBX) o su prefab y vuelve a ejecutar.", "OK");
            return;
        }
        ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
        mi.isReadable = true;
        mi.SaveAndReimport();
        Debug.Log("Read/Write activado en: " + path);
    }

    static string FindModelPath(Object sel)
    {
        if (sel == null) return null;
        string p = AssetDatabase.GetAssetPath(sel);
        if (!string.IsNullOrEmpty(p) && AssetImporter.GetAtPath(p) is ModelImporter) return p;

        GameObject go = sel as GameObject;
        if (go == null && !string.IsNullOrEmpty(p)) go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
        if (go != null)
        {
            foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null) continue;
                string mp = AssetDatabase.GetAssetPath(r.sharedMesh);
                if (!string.IsNullOrEmpty(mp) && AssetImporter.GetAtPath(mp) is ModelImporter) return mp;
            }
        }
        return null;
    }
}
#endif
