#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ===========================================================================================================
//  Clasico > Animaciones > Asistente (TODO en un clic)
//  Arrastra el FBX de tu personaje y la carpeta con los FBX de Mixamo. El asistente:
//    1. importa el personaje como Humanoide (con mallas legibles para pintar la camiseta),
//    2. importa cada animación como Humanoide con el avatar del personaje (bucle en trote/reposo, sin desplazamiento de raíz),
//    3. crea Assets/Resources/AnimationSet.asset con todos los clips VINCULADOS (aquí ajustas tiempos),
//    4. crea el prefab Assets/Prefabs/Jugador.prefab (con Animator y Avatar) y lo asigna en MatchSetup.
// ===========================================================================================================
public static class MixamoSetup
{
    public static Avatar ConfigureCharacter(string path, StringBuilder log)
    {
        ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) { log.AppendLine("✘ El personaje no es un modelo FBX: " + path); return null; }
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.isReadable = true;
        mi.SaveAndReimport();
        Avatar avatar = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Avatar) avatar = (Avatar)o;
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            log.AppendLine("✘ El avatar del personaje NO es un humanoide válido. Selecciona el FBX > Inspector > Rig > Configure y corrige los huesos.");
            return null;
        }
        log.AppendLine("✔ Personaje Humanoide listo: " + Path.GetFileName(path));
        return avatar;
    }

    static bool IsLoop(string n)
    {
        n = n.ToLower();
        return n.Contains("jog") || n.Contains("idle") || n.Contains("stall") || n.Contains("strafe") || n.Contains("run") || n.Contains("sprint");
    }

    // Los FBX de Mixamo "sin piel" tienen  <archivo>/Hips  y el personaje de Meshy  Armature/Hips: copiar el avatar
    // del personaje (CopyFromOther) falla con "Transform hierarchy does not match" y Unity NO genera ningún clip.
    // Y si cada animación crea su avatar sola, Unity toma como "pose de descanso" el PRIMER CUADRO de la animación
    // (no la T-pose): el retargeting sale torcido (pies de lado, rodillas raras, personaje flotando).
    // Solución: cada animación crea su avatar con la jerarquía de SU archivo pero con la pose de descanso y el mapeo
    // de huesos DEL PERSONAJE (los huesos tienen los mismos nombres y largos).
    static bool IsDive(string n) { n = n.ToLower(); return n.Contains("diving"); }

    static bool HasHips(HumanDescription hd)
    {
        if (hd.skeleton == null) return false;
        foreach (SkeletonBone b in hd.skeleton) if (b.name == "Hips") return true;
        return false;
    }

    public static int ConfigureAnimations(string folder, Avatar avatar, string charPath, StringBuilder log)
    {
        ModelImporter ci = AssetImporter.GetAtPath(charPath) as ModelImporter;
        HumanDescription charHd = ci.humanDescription;
        var charBones = new System.Collections.Generic.Dictionary<string, SkeletonBone>();
        foreach (SkeletonBone b in charHd.skeleton) charBones[b.name] = b;

        string[] guids = AssetDatabase.FindAssets("t:Model", new string[] { folder });
        string charFile = Path.GetFileName(charPath);
        int count = 0, bad = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path == charPath || Path.GetFileName(path) == charFile) continue;   // copia del personaje: no es animación
            EditorUtility.DisplayProgressBar("Configurando animaciones", Path.GetFileName(path), (float)i / guids.Length);
            ModelImporter mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) continue;

            // 1) si aún no tiene esqueleto propio, que Unity lo genere (solo para conocer la jerarquía del archivo)
            if (mi.animationType != ModelImporterAnimationType.Human || mi.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel || !HasHips(mi.humanDescription))
            {
                mi.importAnimation = true;
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.sourceAvatar = null;
                mi.autoGenerateAvatarMappingIfUnspecified = true;
                mi.humanDescription = new HumanDescription();
                mi.clipAnimations = new ModelImporterClipAnimation[0];
                mi.SaveAndReimport();
                mi = AssetImporter.GetAtPath(path) as ModelImporter;
            }

            // 2) misma jerarquía del archivo, pero con la pose de descanso y el mapeo del personaje
            HumanDescription own = mi.humanDescription;
            if (HasHips(own))
            {
                SkeletonBone[] sk = own.skeleton;
                for (int s = 1; s < sk.Length; s++)                     // s = 0 es la raíz del archivo: se deja
                {
                    SkeletonBone cb;
                    if (!charBones.TryGetValue(sk[s].name, out cb)) continue;
                    sk[s].position = cb.position;
                    sk[s].rotation = cb.rotation;
                    sk[s].scale = cb.scale;
                }
                HumanDescription hd = charHd;
                hd.skeleton = sk;
                hd.human = charHd.human;
                hd.hasTranslationDoF = false;
                mi.humanDescription = hd;
                mi.SaveAndReimport();
                mi = AssetImporter.GetAtPath(path) as ModelImporter;
            }

            Avatar ownAvatar = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Avatar) ownAvatar = (Avatar)o;
            ModelImporterClipAnimation[] defs = mi.defaultClipAnimations;
            if (ownAvatar == null || !ownAvatar.isValid || !ownAvatar.isHuman || defs.Length == 0)
            {
                bad++;
                log.AppendLine("  ✘ " + Path.GetFileName(path) + ": Unity no pudo crear el humanoide. Selecciónalo > Rig > Configure.");
                continue;
            }

            // 3) clips: sin desplazamiento de raíz, altura según los PIES (así no flotan ni se hunden)
            string baseName = Path.GetFileNameWithoutExtension(path);
            bool loop = IsLoop(baseName);
            var list = new System.Collections.Generic.List<ModelImporterClipAnimation>();
            for (int c = 0; c < defs.Length; c++)
            {
                ModelImporterClipAnimation a = defs[c];
                if (c == 0) a.name = baseName;
                a.loopTime = loop;
                a.loopPose = loop;
                a.lockRootRotation = false;                // giro y desplazamiento de la raíz NO van en la pose:
                a.lockRootHeightY = true;
                a.lockRootPositionXZ = false;              // el cuerpo no se aleja del jugador (ni de su hitbox); lo mueve el código
                a.keepOriginalOrientation = true;
                a.keepOriginalPositionY = false;
                a.heightFromFeet = true;
                a.keepOriginalPositionXZ = true;
                a.mirror = false;
                list.Add(a);
            }
            if (IsDive(baseName))                            // copia en espejo: estirada hacia el otro lado
            {
                ModelImporterClipAnimation src = list[0];
                ModelImporterClipAnimation mr = new ModelImporterClipAnimation();
                mr.takeName = src.takeName; mr.firstFrame = src.firstFrame; mr.lastFrame = src.lastFrame;
                mr.name = baseName + "_mirror";
                mr.loopTime = false; mr.loopPose = false;
                mr.lockRootRotation = false; mr.lockRootHeightY = true; mr.lockRootPositionXZ = false;
                mr.keepOriginalOrientation = true; mr.keepOriginalPositionY = false; mr.heightFromFeet = true; mr.keepOriginalPositionXZ = true;
                mr.mirror = true;
                list.Add(mr);
            }
            mi.clipAnimations = list.ToArray();
            mi.SaveAndReimport();
            count++;
        }
        EditorUtility.ClearProgressBar();
        log.AppendLine("✔ " + count + " animaciones importadas como Humanoide" + (bad > 0 ? "  (" + bad + " con error, ver arriba)" : ""));
        return count;
    }

    public static AnimationSet BuildSet(string folder, Avatar avatar, StringBuilder log)
    {
        Directory.CreateDirectory("Assets/Resources");
        string setPath = "Assets/Resources/AnimationSet.asset";
        AnimationSet set = AssetDatabase.LoadAssetAtPath<AnimationSet>(setPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<AnimationSet>();
            AssetDatabase.CreateAsset(set, setPath);
        }
        set.characterAvatar = avatar;

        var byName = new System.Collections.Generic.Dictionary<string, AnimationClip>();
        var mirrors = new System.Collections.Generic.Dictionary<string, AnimationClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new string[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string baseName = AnimLibrary.Norm(Path.GetFileNameWithoutExtension(path));
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                AnimationClip clip = o as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                if (clip.name.EndsWith("_mirror")) mirrors[baseName] = clip;
                else byName[baseName] = clip;
            }
        }

        int found = 0, missing = 0, optional = 0;
        foreach (AnimKey k in System.Enum.GetValues(typeof(AnimKey)))
        {
            AnimationClip hit = null;
            foreach (string f in AnimLibrary.FileNames(k))
                if (byName.TryGetValue(AnimLibrary.Norm(f), out hit)) break;
            if (hit != null) { set.SetClip(k, hit); found++; }
            else if (k == AnimKey.Sprint) { optional++; log.AppendLine("  · (opcional) no hay sprint: descarga 'Fast Run' de Mixamo, guárdalo como 'sprint.fbx' en la carpeta y repite"); }
            else { missing++; log.AppendLine("  · falta el archivo de: " + k + "  (" + AnimLibrary.FileNames(k)[0] + ".fbx)"); }
        }

        // estirada del portero: copia en espejo + hacia qué lado va la original (curva de la raíz)
        AnimationClip dm;
        if (mirrors.TryGetValue(AnimLibrary.Norm("goalkeeper diving save"), out dm)) set.gkDiveMirror = dm;
        if (set.gkDive.clip != null)
        {
            AnimationCurve cx = AnimationUtility.GetEditorCurve(set.gkDive.clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x"));
            if (cx != null && cx.length > 1)
            {
                float dx = cx.Evaluate(set.gkDive.clip.length) - cx.Evaluate(0f);
                if (Mathf.Abs(dx) > 0.05f) set.gkDiveSide = Mathf.Sign(dx);
                log.AppendLine("✔ La estirada original va hacia la " + (set.gkDiveSide > 0f ? "DERECHA" : "IZQUIERDA") + " (la otra es su espejo)");
            }
        }
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        log.AppendLine("✔ AnimationSet creado/actualizado: " + found + " clips vinculados, " + missing + " sin archivo");
        return set;
    }

    public static GameObject BuildPrefab(string charPath, Avatar avatar, StringBuilder log)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(charPath);
        if (model == null) { log.AppendLine("✘ No pude cargar el modelo del personaje"); return null; }
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Animator an = inst.GetComponentInChildren<Animator>();
        if (an == null) an = inst.AddComponent<Animator>();
        an.avatar = avatar;
        an.applyRootMotion = false;
        an.runtimeAnimatorController = null;
        inst.name = "Jugador";
        Directory.CreateDirectory("Assets/Prefabs");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(inst, "Assets/Prefabs/Jugador.prefab");
        Object.DestroyImmediate(inst);
        log.AppendLine("✔ Prefab creado: Assets/Prefabs/Jugador.prefab (Animator + Avatar)");
        return prefab;
    }

    public static void AssignToMatchSetup(GameObject prefab, StringBuilder log)
    {
        MatchSetup ms = Object.FindObjectOfType<MatchSetup>();
        if (ms == null)
        {
            log.AppendLine("⚠ No hay MatchSetup en la escena abierta: abre tu escena del partido y arrastra Assets/Prefabs/Jugador.prefab al campo 'Player Prefab'.");
            return;
        }
        Undo.RecordObject(ms, "Asignar jugador");
        ms.playerPrefab = prefab;
        ms.modelYawOffset = 0f;
        ms.tPoseArmsDown = true;
        EditorUtility.SetDirty(ms);
        EditorSceneManager.MarkSceneDirty(ms.gameObject.scene);
        log.AppendLine("✔ 'Player Prefab' de MatchSetup apuntando al nuevo jugador (guarda la escena con Ctrl+S)");
    }

    public const string DefaultCharacter = "Assets/Models/Player/source/Meshy_AI_Character_output (1).fbx";
    public const string DefaultFolder = "Assets/Resources/Animations";

    // Un clic: usa el personaje y la carpeta de este proyecto (no hace falta arrastrar nada)
    [MenuItem("Clasico/Animaciones/Reparar animaciones (rutas del proyecto)")]
    static void RepairMenu() { RepairDefault(false); }

    // versión del importador: si el AnimationSet tiene una menor, se repara SOLO al abrir/compilar el proyecto
    public const int ImportVersion = 3;   // 3: sin desplazamiento de raíz en ningún clip

    public static void RepairDefault(bool silent)
    {
        StringBuilder log = new StringBuilder();
        Avatar avatar = ConfigureCharacter(DefaultCharacter, log);
        if (avatar != null)
        {
            ConfigureAnimations(DefaultFolder, avatar, DefaultCharacter, log);
            AnimationSet set = BuildSet(DefaultFolder, avatar, log);
            if (set != null) { set.importVersion = ImportVersion; EditorUtility.SetDirty(set); AssetDatabase.SaveAssets(); }
            GameObject prefab = BuildPrefab(DefaultCharacter, avatar, log);
            if (prefab != null) AssignToMatchSetup(prefab, log);
            AssetDatabase.Refresh();
        }
        Debug.Log("Animaciones reparadas:\n" + log.ToString());
        if (!silent) EditorUtility.DisplayDialog("Animaciones", log.ToString(), "OK");
    }

    // ---- menús sueltos (por si prefieres hacerlo paso a paso)
    [MenuItem("Clasico/Animaciones/Solo: marcar el modelo seleccionado como Humanoide")]
    static void OnlyCharacter()
    {
        StringBuilder log = new StringBuilder();
        ConfigureCharacter(AssetDatabase.GetAssetPath(Selection.activeObject), log);
        EditorUtility.DisplayDialog("Animaciones", log.ToString(), "OK");
    }
}

public class MixamoWizard : EditorWindow
{
    Object characterFbx, animFolder;
    string log = "";
    Vector2 scroll;

    [MenuItem("Clasico/Animaciones/Asistente (TODO en un clic)")]
    static void Open() { GetWindow<MixamoWizard>("Asistente de animaciones").minSize = new Vector2(480f, 420f); }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1) Arrastra el FBX de tu personaje (el de Mixamo/Meshy).\n" +
            "2) Arrastra la CARPETA donde están los FBX de animaciones de Mixamo.\n" +
            "3) Abre tu escena del partido y pulsa 'Configurar todo'.", MessageType.Info);
        characterFbx = EditorGUILayout.ObjectField("Personaje (FBX)", characterFbx, typeof(GameObject), false);
        animFolder = EditorGUILayout.ObjectField("Carpeta de animaciones", animFolder, typeof(DefaultAsset), false);
        if (GUILayout.Button("Configurar todo", GUILayout.Height(38f))) Run();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(log, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    void Run()
    {
        StringBuilder sb = new StringBuilder();
        if (characterFbx == null || animFolder == null) { log = "✘ Falta el personaje o la carpeta de animaciones."; return; }
        string charPath = AssetDatabase.GetAssetPath(characterFbx);
        string folder = AssetDatabase.GetAssetPath(animFolder);
        if (!AssetDatabase.IsValidFolder(folder)) { log = "✘ Arrastra una CARPETA en 'Carpeta de animaciones'."; return; }

        Avatar avatar = MixamoSetup.ConfigureCharacter(charPath, sb);
        if (avatar == null) { log = sb.ToString(); return; }
        MixamoSetup.ConfigureAnimations(folder, avatar, charPath, sb);
        AnimationSet set = MixamoSetup.BuildSet(folder, avatar, sb);
        if (set != null) { set.importVersion = MixamoSetup.ImportVersion; EditorUtility.SetDirty(set); AssetDatabase.SaveAssets(); }
        GameObject prefab = MixamoSetup.BuildPrefab(charPath, avatar, sb);
        if (prefab != null) MixamoSetup.AssignToMatchSetup(prefab, sb);
        AssetDatabase.Refresh();
        sb.AppendLine("\nListo. Dale Play y pulsa F1 en el juego para ajustar los tiempos de cada animación.");
        log = sb.ToString();
        EditorUtility.DisplayDialog("Asistente de animaciones", "Terminado. Revisa el resumen en la ventana.", "OK");
    }
}

// Repara las animaciones SOLO si se importaron con una versión vieja del asistente (pies torcidos, flotando).
[InitializeOnLoad]
public static class MixamoAutoRepair
{
    static MixamoAutoRepair() { EditorApplication.delayCall += Check; }

    static void Check()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        AnimationSet set = AssetDatabase.LoadAssetAtPath<AnimationSet>("Assets/Resources/AnimationSet.asset");
        if (set == null || set.importVersion >= MixamoSetup.ImportVersion) return;
        if (AssetImporter.GetAtPath(MixamoSetup.DefaultCharacter) == null) return;
        Debug.Log("Clasico: actualizando la importación de las animaciones Mixamo (una sola vez, tarda un poco)...");
        MixamoSetup.RepairDefault(true);
    }
}
#endif
