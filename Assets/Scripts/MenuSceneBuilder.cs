#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pon este archivo en Assets/Editor/.  Menú: Clasico > Crear escena de Menú Principal
// Crea Assets/Scenes/MainMenu.unity, la deja como escena 0 y tu escena de juego como escena 1 en Build Settings.
public static class MenuSceneBuilder
{
    const string MenuPath = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Clasico/Crear escena de Menú Principal")]
    static void Create()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (active.name == "MainMenu")
        {
            EditorUtility.DisplayDialog("Menú", "Abre primero tu escena de juego (la del partido) y vuelve a ejecutar.", "OK");
            return;
        }
        if (string.IsNullOrEmpty(active.path))
        {
            EditorUtility.DisplayDialog("Menú", "Guarda primero tu escena de juego (Ctrl+S) y vuelve a ejecutar.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string gamePath = active.path;
        string gameName = Path.GetFileNameWithoutExtension(gamePath);

        Scene menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        Camera c = cam.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.02f, 0.03f, 0.08f);
        cam.AddComponent<AudioListener>();
        GameObject mm = new GameObject("MainMenu");
        MainMenu comp = mm.AddComponent<MainMenu>();
        comp.gameSceneName = gameName;

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(menu, MenuPath);
        AssetDatabase.Refresh();

        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>();
        list.Add(new EditorBuildSettingsScene(MenuPath, true));
        list.Add(new EditorBuildSettingsScene(gamePath, true));
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.path != MenuPath && s.path != gamePath) list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();

        EditorUtility.DisplayDialog("Listo",
            "Escena creada: " + MenuPath + "\nBuild Settings: 0 = MainMenu, 1 = " + gameName +
            "\n\nUsa 'Clasico > Jugar desde el Menú Principal' para probarlo.", "OK");
    }

    [MenuItem("Clasico/Jugar desde el Menú Principal")]
    static void PlayFromMenu()
    {
        if (!File.Exists(MenuPath))
        {
            EditorUtility.DisplayDialog("Menú", "Primero ejecuta 'Clasico > Crear escena de Menú Principal'.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(MenuPath);
        EditorApplication.isPlaying = true;
    }
}
#endif
