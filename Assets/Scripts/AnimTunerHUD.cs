using UnityEngine;

// Panel de DIAGNÓSTICO y AJUSTE de animaciones (tecla F1 dentro del juego).
//  - Te dice exactamente por qué una animación no se carga (sin Animator, no es Humanoide, falta un clip...).
//  - Te deja cambiar en vivo los tiempos de cada acción y probarla con el botón ▶ sobre tu jugador o tu portero.
//  - Los cambios se escriben en Assets/Resources/AnimationSet.asset (en el Editor se conservan al salir del Play).
public class AnimTunerHUD : MonoBehaviour
{
    public static bool Open;
    Vector2 scroll;
    int target;        // 0 = jugador que controlas, 1 = portero de tu equipo
    static readonly AnimKey[] actions =
    {
        AnimKey.Kick, AnimKey.Kick2, AnimKey.PenaltyKick, AnimKey.ThrowIn, AnimKey.Header,
        AnimKey.GkCatch, AnimKey.GkDropKick, AnimKey.GkPass, AnimKey.GkThrow, AnimKey.GkPlace, AnimKey.GkDive, AnimKey.Fallen,
        AnimKey.Tackle, AnimKey.Trip, AnimKey.Receive, AnimKey.StandUp
    };
    static readonly string[] names =
    {
        "Patada", "Patada (2)", "Penal", "Saque de banda", "Cabezazo",
        "Portero: atrapar", "Portero: despeje (drop kick)", "Portero: pase", "Portero: lanzar con la mano", "Portero: colocar balón", "Portero: lanzarse", "Caído",
        "Barrida", "Tropiezo", "Recibir pase", "Levantarse"
    };

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) { Open = !Open; if (Open) Cursor.lockState = CursorLockMode.None; }
    }

    MixamoAnimator Target()
    {
        MatchManager m = MatchManager.I;
        if (m == null) return null;
        PlayerAI p = null;
        if (target == 0 && PlayerController.I != null) p = PlayerController.I.Controlled;
        if (target == 1 && m.UserTeam >= 0) p = m.Teams[m.UserTeam][0];
        if (p == null) p = m.Teams[0][1 < m.Teams[0].Count ? 1 : 0];
        return p != null ? p.GetComponent<MixamoAnimator>() : null;
    }

    void OnGUI()
    {
        // aviso permanente si las animaciones no están activas
        if (!string.IsNullOrEmpty(MixamoAnimator.GlobalError) && !Open)
        {
            GUI.depth = -500;
            Color old = GUI.color;
            GUI.color = new Color(0.9f, 0.2f, 0.15f, 0.9f);
            GUI.DrawTexture(new Rect(0f, Screen.height - 30f, Screen.width, 30f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(10f, Screen.height - 27f, Screen.width - 20f, 26f), "ANIMACIONES MIXAMO NO ACTIVAS: " + MixamoAnimator.GlobalError + "   (F1 = diagnóstico)");
            GUI.color = old;
        }
        if (!Open) return;
        GUI.depth = -600;
        GUI.skin.label.richText = true;

        AnimationSet set = AnimLibrary.Set;
        float w = 560f, h = Screen.height - 40f;
        GUILayout.BeginArea(new Rect(20f, 20f, w, h), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("<b>ANIMACIONES · DIAGNÓSTICO Y AJUSTE</b>   (F1 para cerrar)");

        // ---- estado
        GUILayout.Label("AnimationSet: " + (set != null ? "✔ encontrado (Resources/AnimationSet)" : "✘ NO existe. Ejecuta  Clasico > Animaciones > Asistente"));
        GUILayout.Label("Estado global: " + (string.IsNullOrEmpty(MixamoAnimator.GlobalError) ? "✔ sin errores" : "✘ " + MixamoAnimator.GlobalError));
        int ok = 0, fail = 0;
        foreach (MixamoAnimator a in MixamoAnimator.All) { if (a.Ready) ok++; else if (a.Failed) fail++; }
        GUILayout.Label("Jugadores con clips activos: " + ok + "   con error: " + fail + "   total: " + MixamoAnimator.All.Count);
        MixamoAnimator t = Target();
        if (t != null) GUILayout.Label("Jugador de prueba → " + t.name + " : " + t.Status);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Probar en:");
        if (GUILayout.Toggle(target == 0, "Mi jugador", GUI.skin.button)) target = 0;
        if (GUILayout.Toggle(target == 1, "Mi portero", GUI.skin.button)) target = 1;
        GUILayout.EndHorizontal();

        if (set == null) { GUILayout.EndScrollView(); GUILayout.EndArea(); return; }

        // ---- clips de locomoción
        GUILayout.Space(6f);
        GUILayout.Label("<b>Correr</b>");
        ClipRow("Reposo", AnimKey.Idle); ClipRow("Reposo portero", AnimKey.GkIdle);
        ClipRow("Trote adelante", AnimKey.JogF); ClipRow("Trote atrás", AnimKey.JogB);
        ClipRow("Lateral izquierda", AnimKey.StrafeL); ClipRow("Lateral derecha", AnimKey.StrafeR);
        ClipRow("Sprint (opcional)", AnimKey.Sprint);
        set.jogRefSpeed = Slider("Velocidad natural del trote (m/s)", set.jogRefSpeed, 1.5f, 8f);
        set.maxPlaybackSpeed = Slider("Velocidad máx. de reproducción", set.maxPlaybackSpeed, 1f, 3f);
        set.blendTime = Slider("Tiempo de mezcla (s)", set.blendTime, 0.02f, 0.5f);
        set.stopBlendTime = Slider("Mezcla al frenar (s)", set.stopBlendTime, 0.05f, 1f);
        set.idleDelay = Slider("Quieto antes del reposo (s)", set.idleDelay, 0f, 15f);

        // ---- acciones
        for (int i = 0; i < actions.Length; i++)
        {
            ActionTiming a = set.Get(actions[i]);
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>" + names[i] + "</b>   " + (a.clip != null ? "✔ " + a.clip.name : "✘ FALTA EL CLIP"));
            GUI.enabled = a.clip != null && t != null && t.Ready;
            if (GUILayout.Button("▶ Probar", GUILayout.Width(80f))) t.Preview(actions[i]);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (a.clip == null) continue;
            GUILayout.Label("   duración del clip: " + a.clip.length.ToString("0.00") + " s");
            a.startOffset = Slider("   Empezar en (s)", a.startOffset, 0f, a.clip.length * 0.9f);
            a.speed = Slider("   Velocidad", a.speed, 0.3f, 2.5f);
            a.releaseDelay = Slider("   El balón sale a los (s)", a.releaseDelay, 0f, 1.2f);
            a.fadeIn = Slider("   Entrada suave (s)", a.fadeIn, 0.01f, 0.4f);
            a.fadeOut = Slider("   Salida suave (s)", a.fadeOut, 0.01f, 0.5f);
            a.endTrim = Slider("   Recortar al final (s)", a.endTrim, 0f, a.clip.length * 0.5f);
            a.moveFactor = Slider("   Velocidad de movimiento durante la acción", a.moveFactor, 0f, 1f);
            a.holdAtEnd = GUILayout.Toggle(a.holdAtEnd, "   Congelar en el último cuadro (atajada)");
        }
#if UNITY_EDITOR
        if (GUILayout.Button("Guardar cambios en el asset")) { UnityEditor.EditorUtility.SetDirty(set); UnityEditor.AssetDatabase.SaveAssets(); }
#endif
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void ClipRow(string label, AnimKey k)
    {
        AnimationClip c = AnimLibrary.Get(k);
        GUILayout.Label("   " + label + ": " + (c != null ? "✔ " + c.name : "✘ FALTA"));
    }

    static float Slider(string label, float v, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(300f));
        v = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(150f));
        GUILayout.Label(v.ToString("0.00"), GUILayout.Width(44f));
        GUILayout.EndHorizontal();
        return v;
    }
}
