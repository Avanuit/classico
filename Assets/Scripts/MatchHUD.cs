using UnityEngine;

// Intro (escudos que chocan y revelan "CLÁSICO"), menú de equipo con balón girando, pausa, controles,
// marcador, reloj, avisos, mira, barra de fuerza y cuenta atrás de saques. Usa OnGUI: sin Canvas.
//
// Logos reales: pon "millonarios.png" y "nacional.png" en Assets/Resources/Logos/ y se usarán en la intro.
// Si no existen, se dibujan escudos genéricos con la inicial de cada equipo.
public class MatchHUD : MonoBehaviour
{
    static string bannerText = "";
    static Color bannerColor = Color.black;
    static float bannerStart, bannerUntil;

    public static void Show(string text, Color color, float seconds)
    {
        bannerText = text; bannerColor = color;
        bannerStart = Time.time; bannerUntil = Time.time + seconds;
    }

    GUIStyle teamLeft, teamRight, scoreStyle, timeStyle, bannerStyle, titleStyle, btnStyle, smallBtn,
             promptStyle, subStyle, keyStyle, descStyle, crestStyle;
    bool showControls, wasMenu, impactPlayed;
    static bool introDone;
    float introStart = -1f, uiReadyTime = float.MaxValue;
    Texture2D crestM, crestN;
    bool realLogoM, realLogoN;

    static readonly string[,] controlRows = {
        { "W A S D",    "Mover al jugador" },
        { "Shift",      "Correr" },
        { "Mouse",      "Mover la cámara y apuntar" },
        { "Z  /  X",    "Alejar / acercar la cámara" },
        { "1 2 3 4",    "Elegir: balón · Glock · AK-47 · granadas (o rueda del mouse)" },
        { "Clic izq.",  "Balón: tiro raso (>50% va a la mira) · Arma: disparar · Granada: mantén y suelta" },
        { "Clic der.",  "Balón: tiro aéreo · Arma: apuntar con mira (ADS)" },
        { "R",          "Recargar el arma" },
        { "V",          "Vista en primera persona" },
        { "Tab",        "Cambiar de jugador (sin armas se cambia solo)" },
        { "Esc",        "Pausa" },
        { "M  /  F5",   "Menú de equipo  /  Reiniciar partido" }
    };

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void Update()
    {
        // cualquier tecla o clic salta la intro
        if (!introDone && introStart >= 0f && Time.time - introStart > 0.4f && Input.anyKeyDown)
        {
            introDone = true;
            uiReadyTime = Time.time;
        }
    }

    void CreateStyles()
    {
        teamLeft = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        teamRight = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        scoreStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        timeStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        bannerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        subStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        promptStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        keyStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        descStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
        crestStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        btnStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        smallBtn = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        teamLeft.normal.textColor = Color.white;
        teamRight.normal.textColor = Color.white;
        scoreStyle.normal.textColor = Color.white;
        timeStyle.normal.textColor = Color.white;
        titleStyle.normal.textColor = Color.white;
        crestStyle.normal.textColor = Color.white;
        subStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
        promptStyle.normal.textColor = Color.white;
        keyStyle.normal.textColor = new Color(0.1f, 0.1f, 0.12f);
        descStyle.normal.textColor = new Color(1f, 1f, 1f, 0.92f);
        foreach (GUIStyle b in new GUIStyle[] { btnStyle, smallBtn })
        {
            b.normal.textColor = Color.white; b.hover.textColor = Color.white; b.active.textColor = Color.white;
        }
    }

    static void Box(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    static void LabelShadow(Rect r, string text, GUIStyle st, float alpha)
    {
        st.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, st);
        st.normal.textColor = new Color(1f, 1f, 1f, alpha);
        GUI.Label(r, text, st);
    }

    // escudo genérico: círculo con aro (si no hay logo real en Resources/Logos)
    static Texture2D MakeCrest(Color fill, Color ring)
    {
        int n = 128;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / c;
                Color col;
                if (d > 1f) col = new Color(0f, 0f, 0f, 0f);
                else if (d > 0.90f) col = ring;
                else if (d > 0.82f) col = new Color(fill.r * 0.6f, fill.g * 0.6f, fill.b * 0.6f, 1f);
                else if (d > 0.77f) col = Color.white;
                else col = fill;
                col.a *= Mathf.Clamp01((1f - d) * c / 1.5f);
                px[y * n + x] = col;
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    void EnsureCrests(MatchManager m)
    {
        if (crestM != null) return;
        Texture2D lm = LoadLogo("millonarios", "millonarios_fc", "escudo_millonarios");
        Texture2D ln = LoadLogo("nacional", "atletico_nacional", "escudo_nacional");
        realLogoM = lm != null; realLogoN = ln != null;
        crestM = lm != null ? lm : MakeCrest(m.TeamColors[0], new Color(0.92f, 0.92f, 0.96f));
        crestN = ln != null ? ln : MakeCrest(m.TeamColors[1], new Color(0.96f, 0.96f, 0.96f));
        if (!realLogoM || !realLogoN)
            Debug.Log("MatchHUD: no encontré " + (realLogoM ? "" : "Resources/Logos/millonarios.png ") + (realLogoN ? "" : "Resources/Logos/nacional.png ") +
                      "- se usan escudos genéricos en la intro.");
    }

    static Texture2D LoadLogo(params string[] names)
    {
        foreach (string n in names)
        {
            Texture2D t = Resources.Load<Texture2D>("Logos/" + n);
            if (t != null) return t;
        }
        return null;
    }

    void OnGUI()
    {
        MatchManager m = MatchManager.I;
        if (m == null) return;
        if (teamLeft == null) CreateStyles();
        float s = Screen.height / 720f;

        if (m.State != MatchState.Menu && !m.Paused) showControls = false;
        if (m.State != MatchState.Menu) wasMenu = false;

        if (m.State == MatchState.Menu)
        {
            if (!wasMenu) { wasMenu = true; uiReadyTime = introDone ? Time.time : float.MaxValue; }
            EnsureCrests(m);
            if (!introDone) { DrawIntro(m, s); return; }
            if (showControls) DrawControls(s); else DrawMenu(m, s);
            return;
        }

        DrawScoreboard(m, s);
        DrawBanner(s);
        DrawKillFeed(m, s);
        if (m.UserTeam >= 0 && !m.Paused)
        {
            DrawUserHud(m, PlayerController.I, s);
            DrawCombatHud(m, PlayerController.I, s);
        }
        if (m.Paused)
        {
            if (showControls) DrawControls(s); else DrawPause(m, s);
        }
    }

    // ------------------------------------------------------------ intro: escudos que chocan → CLÁSICO
    void DrawCrest(Texture2D tex, string letter, Vector2 center, float size, float alpha, float rot)
    {
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(rot, center);
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        float w = size;
        if (letter == null && tex.height > 0) w = size * tex.width / (float)tex.height;   // logo real: respeta su proporción
        GUI.DrawTexture(new Rect(center.x - w * 0.5f, center.y - size * 0.5f, w, size), tex, ScaleMode.ScaleToFit);
        if (letter != null)
        {
            crestStyle.fontSize = Mathf.RoundToInt(size * 0.5f);
            GUI.Label(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), letter, crestStyle);
        }
        GUI.color = old;
        GUI.matrix = saved;
    }

    void DrawIntro(MatchManager m, float s)
    {
        if (introStart < 0f) introStart = Time.time;
        float T = Time.time - introStart;
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.42f;
        float size = 200f * s;
        float tHit = 1.15f;
        float since = T - tHit;
        Rect full = new Rect(0f, 0f, Screen.width, Screen.height);

        Box(full, new Color(0f, 0f, 0f, 0.45f));

        Matrix4x4 saved = GUI.matrix;
        if (since > 0f && since < 0.45f)
        {
            float k = 1f - since / 0.45f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(Mathf.Sin(T * 90f) * 12f * k * s, Mathf.Cos(T * 77f) * 9f * k * s, 0f),
                                       Quaternion.identity, Vector3.one);
        }

        // escudos entrando desde los lados y acelerando
        if (T < tHit + 0.7f)
        {
            float p = Mathf.Clamp01(T / tHit);
            float e = p * p * p;
            float off = Mathf.Lerp(Screen.width * 0.55f, size * 0.48f, e);
            float scale = 1f, alpha = 1f;
            if (since > 0f)
            {
                float q = Mathf.Clamp01(since / 0.7f);
                off += q * size * 0.3f;
                scale = 1f - 0.35f * q;
                alpha = 1f - q;
            }
            DrawCrest(crestM, realLogoM ? null : "M", new Vector2(cx - off, cy), size * scale, alpha, -(1f - e) * 360f);
            DrawCrest(crestN, realLogoN ? null : "N", new Vector2(cx + off, cy), size * scale, alpha, (1f - e) * 360f);
        }

        // choque: sonido, destello y chispas
        if (!impactPlayed && T >= tHit)
        {
            impactPlayed = true;
            if (GameAudio.I != null) GameAudio.I.Impact();
        }
        if (since > 0f && since < 0.9f)
        {
            for (int i = 0; i < 16; i++)
            {
                float ang = i * (Mathf.PI * 2f / 16f) + (i % 3) * 0.17f;
                float dist = since * (520f + (i % 4) * 90f) * s;
                float a = 1f - since / 0.9f;
                Box(new Rect(cx + Mathf.Cos(ang) * dist - 4f * s, cy + Mathf.Sin(ang) * dist - 4f * s, 8f * s, 8f * s),
                    new Color(1f, 0.9f, 0.4f, a));
            }
        }
        if (since > 0f && since < 0.4f) Box(full, new Color(1f, 1f, 1f, 0.85f * (1f - since / 0.4f)));

        // título revelado por el choque
        if (since > 0.15f)
        {
            float q = Mathf.Clamp01((since - 0.15f) / 0.7f);
            float pop = Mathf.Lerp(0.55f, 1f, 1f - (1f - q) * (1f - q) * (1f - q));
            titleStyle.fontSize = Mathf.RoundToInt(120f * s * pop);
            LabelShadow(new Rect(0f, cy - 90f * s, Screen.width, 180f * s), "CLÁSICO", titleStyle, q);
            titleStyle.normal.textColor = Color.white;
            if (q > 0.5f)
            {
                subStyle.fontSize = Mathf.RoundToInt(28f * s);
                Color old = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01((q - 0.5f) * 2f));
                GUI.Label(new Rect(0f, cy + 85f * s, Screen.width, 40f * s), "MILLONARIOS  vs  NACIONAL", subStyle);
                GUI.color = old;
            }
        }
        GUI.matrix = saved;

        if (T > tHit + 2.4f) { introDone = true; uiReadyTime = Time.time; }
    }

    // ------------------------------------------------------------ paneles
    void DrawVolume(float x, float y, float w, float s)
    {
        subStyle.fontSize = Mathf.RoundToInt(15f * s);
        GUI.Label(new Rect(x, y - 24f * s, w, 22f * s), "Volumen general  " + Mathf.RoundToInt(AudioListener.volume * 100f) + "%", subStyle);
        AudioListener.volume = GUI.HorizontalSlider(new Rect(x, y, w, 20f * s), AudioListener.volume, 0f, 1f);
        float x2 = x + w + 30f * s;
        GUI.Label(new Rect(x2, y - 24f * s, w, 22f * s), "Ambiente (grada y viento)  " + Mathf.RoundToInt(GameAudio.AmbientLevel * 100f) + "%", subStyle);
        GameAudio.AmbientLevel = GUI.HorizontalSlider(new Rect(x2, y, w, 20f * s), GameAudio.AmbientLevel, 0f, 1f);
    }

    void DrawControls(float s)
    {
        Box(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.68f));
        float pw = 860f * s, ph = 600f * s;
        Rect panel = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
        Box(panel, new Color(0.08f, 0.09f, 0.12f, 0.97f));
        Box(new Rect(panel.x, panel.y, pw, 6f * s), new Color(1f, 0.85f, 0.1f));

        titleStyle.fontSize = Mathf.RoundToInt(34f * s);
        GUI.Label(new Rect(panel.x, panel.y + 16f * s, pw, 50f * s), "CONTROLES", titleStyle);

        keyStyle.fontSize = Mathf.RoundToInt(16f * s);
        descStyle.fontSize = Mathf.RoundToInt(17f * s);
        int rows = controlRows.GetLength(0);
        for (int i = 0; i < rows; i++)
        {
            float y = panel.y + 80f * s + i * 36f * s;
            Rect chip = new Rect(panel.x + 36f * s, y, 150f * s, 30f * s);
            Box(chip, new Color(0.93f, 0.93f, 0.95f));
            GUI.Label(chip, controlRows[i, 0], keyStyle);
            GUI.Label(new Rect(panel.x + 210f * s, y, pw - 240f * s, 30f * s), controlRows[i, 1], descStyle);
        }

        smallBtn.fontSize = Mathf.RoundToInt(20f * s);
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.25f, 0.4f, 0.8f);
        if (GUI.Button(new Rect(panel.center.x - 90f * s, panel.yMax - 62f * s, 180f * s, 44f * s), "Volver", smallBtn)) showControls = false;
        GUI.backgroundColor = old;
    }

    void DrawPause(MatchManager m, float s)
    {
        Box(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        titleStyle.fontSize = Mathf.RoundToInt(54f * s);
        LabelShadow(new Rect(0f, cy - 230f * s, Screen.width, 80f * s), "PAUSA", titleStyle, 1f);
        titleStyle.normal.textColor = Color.white;

        btnStyle.fontSize = Mathf.RoundToInt(24f * s);
        float bw = 480f * s, bh = 60f * s;
        float x = cx - bw * 0.5f;
        Color old = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.25f);
        if (GUI.Button(new Rect(x, cy - 140f * s, bw, bh), "Continuar", btnStyle)) m.SetPaused(false);
        GUI.backgroundColor = new Color(0.45f, 0.45f, 0.5f);
        if (GUI.Button(new Rect(x, cy - 66f * s, bw, bh), "Controles", btnStyle)) showControls = true;
        GUI.backgroundColor = new Color(0.2f, 0.35f, 0.8f);
        if (GUI.Button(new Rect(x, cy + 8f * s, bw, bh), "Menú principal (reinicia la partida)", btnStyle)) m.ShowMenu();
        GUI.backgroundColor = new Color(0.7f, 0.15f, 0.15f);
        if (GUI.Button(new Rect(x, cy + 82f * s, bw, bh), "Salir de la aplicación", btnStyle)) m.QuitGame();
        GUI.backgroundColor = old;

        DrawVolume(cx - 270f * s, cy + 192f * s, 240f * s, s);
    }

    // ------------------------------------------------------------ menú principal (balón girando de fondo)
    void DrawMenu(MatchManager m, float s)
    {
        float fade = Mathf.Clamp01((Time.time - uiReadyTime) / 0.5f);
        Color oldC = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, fade);

        float cx = Screen.width * 0.5f, H = Screen.height;

        titleStyle.fontSize = Mathf.RoundToInt(84f * s);
        LabelShadow(new Rect(0f, H * 0.04f, Screen.width, 110f * s), "CLÁSICO", titleStyle, 1f);
        titleStyle.normal.textColor = Color.white;
        subStyle.fontSize = Mathf.RoundToInt(24f * s);
        GUI.Label(new Rect(0f, H * 0.04f + 104f * s, Screen.width, 36f * s), "MILLONARIOS  vs  NACIONAL", subStyle);

        subStyle.fontSize = Mathf.RoundToInt(22f * s);
        GUI.Label(new Rect(0f, H * 0.655f, Screen.width, 32f * s), "Elige tu equipo", subStyle);

        btnStyle.fontSize = Mathf.RoundToInt(28f * s);
        smallBtn.fontSize = Mathf.RoundToInt(19f * s);
        float bw = 310f * s, bh = 84f * s;
        float by = H * 0.70f;
        Color old = GUI.backgroundColor;

        GUI.backgroundColor = m.TeamColors[0] * 1.4f;
        if (GUI.Button(new Rect(cx - bw - 16f * s, by, bw, bh), "MILLONARIOS", btnStyle)) m.StartMatch(0);
        GUI.backgroundColor = m.TeamColors[1] * 1.4f;
        if (GUI.Button(new Rect(cx + 16f * s, by, bw, bh), "NACIONAL", btnStyle)) m.StartMatch(1);

        float sy = by + bh + 14f * s, sw = 200f * s, sh = 44f * s;
        GUI.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
        if (GUI.Button(new Rect(cx - sw * 1.5f - 10f * s, sy, sw, sh), "Solo mirar", smallBtn)) m.StartMatch(-1);
        GUI.backgroundColor = new Color(0.45f, 0.45f, 0.5f);
        if (GUI.Button(new Rect(cx - sw * 0.5f, sy, sw, sh), "Controles", smallBtn)) showControls = true;
        GUI.backgroundColor = new Color(0.65f, 0.15f, 0.15f);
        if (GUI.Button(new Rect(cx + sw * 0.5f + 10f * s, sy, sw, sh), "Salir", smallBtn)) m.QuitGame();
        GUI.backgroundColor = old;

        DrawVolume(cx - 270f * s, H * 0.955f, 240f * s, s);
        GUI.color = oldC;
    }

    // ------------------------------------------------------------ combate: vida, inventario, puntos, bajas
    Texture2D circleTex;

    Texture2D Circle()
    {
        if (circleTex != null) return circleTex;
        int n = 64;
        circleTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        Color[] px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * c / 1.5f));
            }
        circleTex.SetPixels(px);
        circleTex.Apply();
        return circleTex;
    }

    void DrawCircle(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Circle());
        GUI.color = old;
    }

    // iconos de la barra de inventario dibujados con formas simples
    void DrawIcon(ItemType t, Rect r, float a)
    {
        float w = r.width, h = r.height;
        Color steel = new Color(0.75f, 0.78f, 0.82f, a), dark = new Color(0.25f, 0.27f, 0.3f, a), wood = new Color(0.65f, 0.4f, 0.2f, a);
        switch (t)
        {
            case ItemType.Ball:
                DrawCircle(new Rect(r.x + w * 0.18f, r.y + h * 0.18f, w * 0.64f, h * 0.64f), new Color(0.95f, 0.95f, 0.95f, a));
                DrawCircle(new Rect(r.x + w * 0.38f, r.y + h * 0.38f, w * 0.24f, h * 0.24f), new Color(0.1f, 0.1f, 0.1f, a));
                DrawCircle(new Rect(r.x + w * 0.24f, r.y + h * 0.28f, w * 0.12f, h * 0.12f), new Color(0.1f, 0.1f, 0.1f, a));
                DrawCircle(new Rect(r.x + w * 0.62f, r.y + h * 0.30f, w * 0.12f, h * 0.12f), new Color(0.1f, 0.1f, 0.1f, a));
                DrawCircle(new Rect(r.x + w * 0.44f, r.y + h * 0.66f, w * 0.12f, h * 0.12f), new Color(0.1f, 0.1f, 0.1f, a));
                break;
            case ItemType.Glock:
                Box(new Rect(r.x + w * 0.20f, r.y + h * 0.36f, w * 0.60f, h * 0.17f), steel);
                Box(new Rect(r.x + w * 0.22f, r.y + h * 0.53f, w * 0.16f, h * 0.27f), dark);
                Box(new Rect(r.x + w * 0.38f, r.y + h * 0.53f, w * 0.12f, h * 0.06f), dark);
                break;
            case ItemType.AK47:
                Box(new Rect(r.x + w * 0.08f, r.y + h * 0.40f, w * 0.84f, h * 0.10f), steel);
                Box(new Rect(r.x + w * 0.06f, r.y + h * 0.37f, w * 0.22f, h * 0.20f), wood);
                Box(new Rect(r.x + w * 0.52f, r.y + h * 0.37f, w * 0.20f, h * 0.10f), wood);
                Box(new Rect(r.x + w * 0.42f, r.y + h * 0.50f, w * 0.10f, h * 0.28f), dark);
                Box(new Rect(r.x + w * 0.30f, r.y + h * 0.50f, w * 0.07f, h * 0.18f), dark);
                break;
            default:
                DrawCircle(new Rect(r.x + w * 0.27f, r.y + h * 0.30f, w * 0.46f, h * 0.50f), new Color(0.4f, 0.65f, 0.3f, a));
                Box(new Rect(r.x + w * 0.42f, r.y + h * 0.18f, w * 0.16f, h * 0.12f), steel);
                Box(new Rect(r.x + w * 0.5f, r.y + h * 0.14f, w * 0.18f, h * 0.05f), steel);
                break;
        }
    }

    void DrawKillFeed(MatchManager m, float s)
    {
        float W = Screen.width;
        descStyle.fontSize = Mathf.RoundToInt(15f * s);
        int shown = 0;
        for (int i = m.Feed.Count - 1; i >= 0; i--)
        {
            MatchManager.FeedItem f = m.Feed[i];
            float age = Time.time - f.time;
            if (age > 6f) continue;
            float a = Mathf.Clamp01(6f - age);
            Rect r = new Rect(W - 400f * s, 14f * s + shown * 30f * s, 384f * s, 26f * s);
            Box(r, f.user ? new Color(0.5f, 0.35f, 0.05f, 0.65f * a) : new Color(0f, 0f, 0f, 0.55f * a));
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(r.x + 8f * s, r.y, r.width - 12f * s, r.height), f.text, descStyle);
            GUI.color = old;
            shown++;
            if (shown >= 6) break;
        }
        descStyle.normal.textColor = new Color(1f, 1f, 1f, 0.92f);
    }

    void DrawCombatHud(MatchManager m, PlayerController pc, float s)
    {
        PlayerCombat cb = pc != null ? pc.Combat : null;
        float W = Screen.width, H = Screen.height;
        float cx = W * 0.5f, cy = H * 0.5f;

        // daño recibido: viñeta roja en los bordes
        float dmgAge = Time.time - m.UserDamageTime;
        if (dmgAge < 0.7f)
        {
            float a = 0.5f * (1f - dmgAge / 0.7f), e = 80f * s;
            Color c = new Color(0.9f, 0.05f, 0.05f, a);
            Box(new Rect(0f, 0f, W, e), c); Box(new Rect(0f, H - e, W, e), c);
            Box(new Rect(0f, 0f, e, H), c); Box(new Rect(W - e, 0f, e, H), c);
        }

        // marcador de impacto sobre la mira (rojo si fue baja)
        float hitAge = Time.time - m.UserHitTime;
        if (hitAge < 0.22f)
        {
            Color hc = m.UserHitKill ? new Color(1f, 0.15f, 0.15f, 1f - hitAge / 0.22f) : new Color(1f, 1f, 1f, 1f - hitAge / 0.22f);
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
            float len = 9f * s, th = 2.5f * s, gap = 10f * s;
            Box(new Rect(cx - gap - len, cy - th * 0.5f, len, th), hc);
            Box(new Rect(cx + gap, cy - th * 0.5f, len, th), hc);
            Box(new Rect(cx - th * 0.5f, cy - gap - len, th, len), hc);
            Box(new Rect(cx - th * 0.5f, cy + gap, th, len), hc);
            GUI.matrix = saved;
        }

        // puntos
        Rect pr = new Rect(16f * s, 14f * s, 236f * s, 92f * s);
        Box(pr, new Color(0f, 0f, 0f, 0.62f));
        Box(new Rect(pr.x, pr.y, 5f * s, pr.height), new Color(1f, 0.85f, 0.1f));
        subStyle.fontSize = Mathf.RoundToInt(14f * s);
        subStyle.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(pr.x + 16f * s, pr.y + 4f * s, 200f * s, 20f * s), "PUNTOS", subStyle);
        scoreStyle.fontSize = Mathf.RoundToInt(34f * s);
        scoreStyle.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(pr.x + 16f * s, pr.y + 20f * s, 210f * s, 42f * s), m.UserPoints.ToString(), scoreStyle);
        scoreStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(pr.x + 16f * s, pr.y + 64f * s, 210f * s, 22f * s), "BAJAS " + m.UserKills + "    MUERTES " + m.UserDeaths, subStyle);
        subStyle.alignment = TextAnchor.MiddleCenter;
        float pa = Time.time - m.PointsTextTime;
        if (pa < 1.6f)
        {
            promptStyle.fontSize = Mathf.RoundToInt(24f * s);
            LabelShadow(new Rect(cx + 50f * s, cy - 50f * s - pa * 30f * s, 300f * s, 34f * s), m.PointsText, promptStyle, 1f - pa / 1.6f);
        }

        if (cb != null)
        {
            // vida
            Rect hr = new Rect(24f * s, H - 64f * s, 300f * s, 26f * s);
            Box(hr, new Color(0f, 0f, 0f, 0.6f));
            float hp = Mathf.Clamp01(cb.health / cb.maxHealth);
            Box(new Rect(hr.x + 3f * s, hr.y + 3f * s, (hr.width - 6f * s) * hp, hr.height - 6f * s),
                Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.25f, 0.9f, 0.3f), hp));
            promptStyle.fontSize = Mathf.RoundToInt(16f * s);
            GUI.Label(hr, Mathf.CeilToInt(cb.health) + " / " + Mathf.CeilToInt(cb.maxHealth), promptStyle);

            DrawHotbar(cb, s, W, H);
        }

        // aviso de recogida / reaparición
        float na = Time.time - m.NoticeTime;
        if (na < 2.6f)
        {
            float a = Mathf.Clamp01(2.6f - na);
            Rect nr = new Rect(24f * s, H - 108f * s, 360f * s, 32f * s);
            Box(nr, new Color(0f, 0f, 0f, 0.6f * a));
            subStyle.fontSize = Mathf.RoundToInt(16f * s);
            Color old = GUI.color; GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(nr, m.NoticeText, subStyle);
            GUI.color = old;
        }

        // eliminado
        if (cb != null && cb.IsDead)
        {
            Box(new Rect(0f, 0f, W, H), new Color(0.35f, 0f, 0f, 0.38f));
            titleStyle.fontSize = Mathf.RoundToInt(72f * s);
            LabelShadow(new Rect(0f, cy - 90f * s, W, 100f * s), "ELIMINADO", titleStyle, 1f);
            titleStyle.normal.textColor = Color.white;
            int left = Mathf.Max(0, Mathf.CeilToInt(cb.respawnAt - Time.time));
            promptStyle.fontSize = Mathf.RoundToInt(28f * s);
            LabelShadow(new Rect(0f, cy + 10f * s, W, 40f * s), "Reapareces en " + left + " s", promptStyle, 1f);
        }
    }

    void DrawHotbar(PlayerCombat cb, float s, float W, float H)
    {
        float slot = 76f * s, gap = 8f * s;
        float total = 4f * slot + 3f * gap;
        float x0 = (W - total) * 0.5f, y = H - slot - 20f * s;
        for (int i = 0; i < 4; i++)
        {
            ItemType t = (ItemType)i;
            bool has = cb.HasItem(t), sel = cb.equipped == t;
            float lift = sel ? 10f * s : 0f;
            Rect r = new Rect(x0 + i * (slot + gap), y - lift, slot, slot);
            Box(r, new Color(0f, 0f, 0f, has ? 0.62f : 0.35f));
            if (sel)
            {
                Box(new Rect(r.x, r.y, r.width, 3f * s), new Color(1f, 0.85f, 0.1f));
                Box(new Rect(r.x, r.yMax - 3f * s, r.width, 3f * s), new Color(1f, 0.85f, 0.1f));
                Box(new Rect(r.x, r.y, 3f * s, r.height), new Color(1f, 0.85f, 0.1f));
                Box(new Rect(r.xMax - 3f * s, r.y, 3f * s, r.height), new Color(1f, 0.85f, 0.1f));
                Box(new Rect(r.x + 3f * s, r.y + 3f * s, r.width - 6f * s, r.height - 6f * s), new Color(1f, 1f, 1f, 0.10f));
            }
            Color rc = Items.Rarity(t);
            Box(new Rect(r.x, r.yMax - 6f * s, r.width, 6f * s), new Color(rc.r, rc.g, rc.b, has ? 1f : 0.25f));
            DrawIcon(t, new Rect(r.x + 4f * s, r.y + 2f * s, r.width - 8f * s, r.height - 10f * s), has ? 1f : 0.3f);

            keyStyle.fontSize = Mathf.RoundToInt(14f * s);
            keyStyle.alignment = TextAnchor.UpperLeft;
            keyStyle.normal.textColor = new Color(1f, 1f, 1f, has ? 0.95f : 0.4f);
            GUI.Label(new Rect(r.x + 5f * s, r.y + 2f * s, 20f * s, 20f * s), (i + 1).ToString(), keyStyle);
            keyStyle.alignment = TextAnchor.UpperRight;
            string count = t == ItemType.Grenade ? (cb.grenades > 0 ? "x" + cb.grenades : "")
                         : (t == ItemType.Ball ? "" : (has ? cb.MagOf(t) + "/" + cb.ReserveOf(t) : ""));
            GUI.Label(new Rect(r.x, r.y + 2f * s, r.width - 5f * s, 20f * s), count, keyStyle);
            keyStyle.alignment = TextAnchor.MiddleCenter;
            keyStyle.normal.textColor = new Color(0.1f, 0.1f, 0.12f);
        }

        // nombre del arma equipada + munición grande + recarga
        ItemType e = cb.equipped;
        subStyle.fontSize = Mathf.RoundToInt(15f * s);
        GUI.Label(new Rect(x0, y - 36f * s, total, 22f * s), Items.Name(e), subStyle);
        if (e == ItemType.Glock || e == ItemType.AK47)
        {
            promptStyle.fontSize = Mathf.RoundToInt(30f * s);
            promptStyle.alignment = TextAnchor.MiddleLeft;
            LabelShadow(new Rect(x0 + total + 24f * s, y + 8f * s, 200f * s, 40f * s), cb.MagOf(e) + " / " + cb.ReserveOf(e), promptStyle, 1f);
            promptStyle.alignment = TextAnchor.MiddleCenter;
            if (cb.IsReloading)
            {
                Rect br = new Rect(x0 + total + 24f * s, y + 52f * s, 150f * s, 8f * s);
                Box(br, new Color(0f, 0f, 0f, 0.6f));
                Box(new Rect(br.x, br.y, br.width * cb.ReloadProgress, br.height), new Color(1f, 0.85f, 0.1f));
                subStyle.fontSize = Mathf.RoundToInt(13f * s);
                subStyle.alignment = TextAnchor.MiddleLeft;
                GUI.Label(new Rect(br.x, br.y + 8f * s, 150f * s, 18f * s), "RECARGANDO", subStyle);
                subStyle.alignment = TextAnchor.MiddleCenter;
            }
        }
    }

    // ------------------------------------------------------------ partido
    void DrawScoreboard(MatchManager m, float s)
    {
        float w = 580f * s, h = 54f * s;
        Rect r = new Rect((Screen.width - w) * 0.5f, 10f * s, w, h);
        Box(r, new Color(0f, 0f, 0f, 0.72f));
        Box(new Rect(r.x, r.y, 12f * s, h), m.TeamColors[0]);
        Box(new Rect(r.xMax - 12f * s, r.y, 12f * s, h), m.TeamColors[1]);
        teamLeft.fontSize = teamRight.fontSize = Mathf.RoundToInt(22f * s);
        scoreStyle.fontSize = Mathf.RoundToInt(34f * s);
        GUI.Label(new Rect(r.x + 24f * s, r.y, w * 0.38f, h), m.TeamNames[0], teamLeft);
        GUI.Label(new Rect(r.xMax - 24f * s - w * 0.38f, r.y, w * 0.38f, h), m.TeamNames[1], teamRight);
        GUI.Label(new Rect(r.center.x - 70f * s, r.y, 140f * s, h), m.Score[0] + " - " + m.Score[1], scoreStyle);

        float t = Mathf.Clamp01(m.MatchTime / m.matchLengthSeconds) * 90f * 60f;
        string clock = string.Format("{0:00}:{1:00}", (int)(t / 60f), (int)(t % 60f));
        timeStyle.fontSize = Mathf.RoundToInt(18f * s);
        Rect tr = new Rect(r.center.x - 50f * s, r.yMax + 2f * s, 100f * s, 26f * s);
        Box(tr, new Color(0f, 0f, 0f, 0.72f));
        GUI.Label(tr, clock, timeStyle);
    }

    void DrawBanner(float s)
    {
        if (Time.time >= bannerUntil) return;
        float age = Time.time - bannerStart;
        float left = bannerUntil - Time.time;
        float alpha = Mathf.Clamp01(left / 0.4f) * Mathf.Clamp01(age / 0.15f);
        float pop = Mathf.Lerp(1.35f, 1f, Mathf.Clamp01(age / 0.25f));
        int lines = bannerText.Split('\n').Length;
        float bh = (lines * 62f + 24f) * s;
        Rect band = new Rect(0f, Screen.height * 0.22f, Screen.width, bh);
        Color bc = bannerColor; bc.a = 0.8f * alpha;
        Box(band, bc);
        bannerStyle.fontSize = Mathf.RoundToInt(52f * s * pop);
        LabelShadow(band, bannerText, bannerStyle, alpha);
    }

    void DrawUserHud(MatchManager m, PlayerController pc, float s)
    {
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;

        // mira: punto central con cuatro marcas, discreta
        float len = 9f * s, th = 2f * s, gap = 6f * s;
        Color c = new Color(1f, 1f, 1f, 0.85f);
        Box(new Rect(cx - gap - len, cy - th * 0.5f, len, th), c);
        Box(new Rect(cx + gap, cy - th * 0.5f, len, th), c);
        Box(new Rect(cx - th * 0.5f, cy - gap - len, th, len), c);
        Box(new Rect(cx - th * 0.5f, cy + gap, th, len), c);
        Box(new Rect(cx - th, cy - th, th * 2f, th * 2f), new Color(1f, 0.85f, 0.1f, 0.95f));

        // barra de fuerza (la marca del 50% indica cuándo el tiro va a la mira)
        if (pc != null && pc.Charging)
        {
            float bw = 340f * s, bh = 22f * s;
            Rect rr = new Rect(cx - bw * 0.5f, Screen.height - 110f * s, bw, bh);
            Box(rr, new Color(0f, 0f, 0f, 0.7f));
            Box(new Rect(rr.x + 3f * s, rr.y + 3f * s, (bw - 6f * s) * pc.ChargePower, bh - 6f * s),
                Color.Lerp(new Color(0.2f, 0.9f, 0.2f), new Color(0.95f, 0.15f, 0.1f), pc.ChargePower));
            if (!pc.ChargingAerial && pc.Equipped == ItemType.Ball) Box(new Rect(rr.x + bw * 0.5f - s, rr.y - 3f * s, 2f * s, bh + 6f * s), new Color(1f, 1f, 1f, 0.9f));
            promptStyle.fontSize = Mathf.RoundToInt(17f * s);
            string label = pc.Equipped == ItemType.Grenade ? "GRANADA" : (pc.ChargingAerial ? "TIRO AÉREO" : (pc.AimAssistActive ? "TIRO RASO · A LA MIRA" : "TIRO RASO"));
            LabelShadow(new Rect(rr.x - 40f * s, rr.y - 30f * s, bw + 80f * s, 26f * s),
                label + "   " + Mathf.RoundToInt(pc.ChargePower * 100f) + "%", promptStyle, 1f);
        }

        // saque a balón parado del humano
        if (m.State == MatchState.SetPiece && m.UserTakesSetPiece && m.Taker != null)
        {
            string what = m.SetType == SetPieceType.ThrowIn ? "SAQUE DE BANDA"
                        : m.SetType == SetPieceType.Corner ? "TIRO DE ESQUINA"
                        : m.SetType == SetPieceType.GoalKick ? "SAQUE DE META" : "SAQUE DE CENTRO";
            bool near = m.UserTakerLocked(m.Taker);
            float rem = m.UserSetPieceRemaining;
            float pw = 460f * s;
            Rect box = new Rect(cx - pw * 0.5f, Screen.height * 0.72f, pw, 78f * s);
            Box(box, new Color(0f, 0f, 0f, 0.7f));
            Box(new Rect(box.x, box.yMax - 6f * s, pw * Mathf.Clamp01(rem / m.setPieceChoiceSeconds), 6f * s), new Color(1f, 0.85f, 0.1f));
            promptStyle.fontSize = Mathf.RoundToInt(21f * s);
            LabelShadow(new Rect(box.x, box.y + 4f * s, pw, 30f * s), what + "  ·  " + Mathf.CeilToInt(rem) + " s", promptStyle, 1f);
            subStyle.fontSize = Mathf.RoundToInt(16f * s);
            GUI.Label(new Rect(box.x, box.y + 38f * s, pw, 26f * s),
                near ? "Apunta, mantén el clic y suelta para sacar  ·  si se acaba el tiempo, saca la IA" : "Camina hasta el balón", subStyle);
        }
    }
}
