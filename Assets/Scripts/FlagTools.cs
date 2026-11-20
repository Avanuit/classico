#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Menú  Clasico > Banderas:  convierte los planos que tengas seleccionados en banderas de tela con un clic.
public static class FlagTools
{
    static int Convert(FlagStyle style, FlagAttach attach)
    {
        int n = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go.GetComponent<MeshFilter>() == null) continue;
            FlagCloth f = go.GetComponent<FlagCloth>();
            if (f == null) f = Undo.AddComponent<FlagCloth>(go);
            f.style = style;
            f.attach = attach;
            EditorUtility.SetDirty(f);
            n++;
        }
        if (n == 0) EditorUtility.DisplayDialog("Banderas", "Selecciona uno o más planos/quads en la Hierarchy.", "OK");
        return n;
    }

    [MenuItem("Clasico/Banderas/Convertir selección en bandera de POSTE")]
    static void Pole() { Convert(FlagStyle.Pole, FlagAttach.Left); }

    [MenuItem("Clasico/Banderas/Convertir selección en bandera de HINCHA")]
    static void Spectator() { Convert(FlagStyle.Spectator, FlagAttach.Left); }

    [MenuItem("Clasico/Banderas/Convertir selección en BANDERA COLGANTE (estadio)")]
    static void Banner() { Convert(FlagStyle.Banner, FlagAttach.Top); }

    [MenuItem("Clasico/Banderas/Crear viento global en la escena")]
    static void Wind()
    {
        GameObject go = new GameObject("ViendoBanderas");
        go.AddComponent<FlagWindSettings>();
        Undo.RegisterCreatedObjectUndo(go, "Viento de banderas");
        Selection.activeGameObject = go;
    }
}
#endif
