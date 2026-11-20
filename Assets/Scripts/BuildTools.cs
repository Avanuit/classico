#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Clasico > Compilar > ...   Builds listas para subir a itch.io (carpeta Build/ junto a Assets).
public static class BuildTools
{
    static string[] Scenes()
    {
        return EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
    }

    static void Run(BuildTarget target, BuildTargetGroup group, string folder, string exeName)
    {
        string[] scenes = Scenes();
        if (scenes.Length == 0) { EditorUtility.DisplayDialog("Compilar", "No hay escenas activas en File > Build Settings.", "OK"); return; }
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build", folder));
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.CreateDirectory(root);
        BuildPlayerOptions o = new BuildPlayerOptions();
        o.scenes = scenes;
        o.target = target;
        o.targetGroup = group;
        o.locationPathName = exeName == null ? root : Path.Combine(root, exeName);
        o.options = BuildOptions.None;
        BuildReport r = BuildPipeline.BuildPlayer(o);
        if (r.summary.result == BuildResult.Succeeded)
        {
            EditorUtility.RevealInFinder(root);
            EditorUtility.DisplayDialog("Compilar", "Listo (" + (r.summary.totalSize / (1024 * 1024)) + " MB):\n" + root +
                "\n\nComprime en ZIP el CONTENIDO de esa carpeta y súbelo a itch.io.", "OK");
        }
        else EditorUtility.DisplayDialog("Compilar", "La compilación falló: revisa la Console.", "OK");
    }

    [MenuItem("Clasico/Compilar/Windows (descargable para itch.io)")]
    static void Windows()
    {
        PlayerSettings.productName = "Clásico";
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        Run(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Windows", "Clasico.exe");
    }

    // itch.io no envía las cabeceras de compresión que Unity 2019 necesita: se compila SIN comprimir.
    [MenuItem("Clasico/Compilar/WebGL (jugar en el navegador en itch.io)")]
    static void WebGL()
    {
        if (!EditorUtility.DisplayDialog("WebGL",
            "Cambiar a WebGL reimporta todo el proyecto (puede tardar bastante) y en el navegador el juego corre más lento " +
            "que en Windows.\n\n¿Continuar?", "Sí, compilar WebGL", "Cancelar")) return;
        PlayerSettings.productName = "Clásico";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.memorySize = 1024;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        Run(BuildTarget.WebGL, BuildTargetGroup.WebGL, "WebGL", null);
    }
}
#endif
