using UnityEngine;

// Pantallas compartidas (menú principal, pausa, fin de partido): ajustes, controles, créditos y elección de equipo.
// Todas usan UIKit: marcos dorados, botones animados y sliders.
public static class MenuScreens
{
    static readonly int[] minuteOptions = { 3, 5, 8, 10 };

    // ---- EDITA AQUÍ LOS CRÉDITOS (las líneas que empiezan con # son títulos de sección)
    public static string[] CreditLines =
    {
        "#CLÁSICO · EDICIÓN CALI",
        "Fútbol, balas, granadas y mucha salsa",
        "",
        "#DISEÑO Y PROGRAMACIÓN",
        "Juan Pablo Chaves A.K.A Avanuit",
        "",
        "#MODELOS 3D",
        "Jugador, estadio y balón: Sketchfab",
        "Football Player Model — tomleeosborn (Sketchfab)",
        "Adidas Soccer Ball — Welham14 (Sketchfab)",
        "Soccer Modern Stadium Model — Razny (Sketchfab)",
        "",
        "#MOTOR Y ASSETS",
        "Unity 2019.4",
        "Super Casual Shooter — olusola olaoye (Unity Asset Store)",
        "",
        "#MÚSICA Y SONIDO",
        "Football Track, Football Ambience — The Mountain (Pixabay)",
        "",
        "#ESCUDOS",
        "Millonarios F.C. y Atlético Nacional",
        "Marcas de sus respectivos clubes",
        "",
        "#AGRADECIMIENTOS",
        "A las hinchadas de Bogotá y Medellín",
        "A Cali, capital mundial de la salsa",
        "",
        "¡Gracias por jugar!"
    };

    static readonly string[,] controlRows =
    {
        { "W A S D",   "Mover al jugador" },
        { "Shift",     "Correr" },
        { "Espacio",   "Saltar y cabecear el balón" },
        { "E",         "Barrida (quita el balón al rival)" },
        { "Mouse",     "Mover la cámara y apuntar" },
        { "Z  /  X",   "Alejar / acercar la cámara" },
        { "1 2 3 4",   "Balón · Glock · AK-47 · Granadas (o rueda del mouse)" },
        { "Clic izq.", "Balón: tiro raso (>50% va a la mira) · Arma: disparar · Granada: mantén y suelta" },
        { "Clic der.", "Balón: tiro aéreo · Arma: apuntar con mira" },
        { "R",         "Recargar el arma" },
        { "V",         "Vista en primera persona" },
        { "Tab",       "Cambiar de jugador (sin armas se cambia solo)" },
        { "Esc",       "Pausa" },
        { "M  /  F5",  "Menú de equipo  /  Reiniciar partido" }
    };

    public static Rect Centered(float w, float h)
    {
        float S = UIKit.S;
        return new Rect((Screen.width - w * S) * 0.5f, (Screen.height - h * S) * 0.5f, w * S, h * S);
    }

    public static void Dim(float a = 0.62f)
    {
        UIKit.Box(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, a));
    }

    static string Pct(float v) { return Mathf.RoundToInt(v * 100f) + "%"; }

    // ------------------------------------------------------------ ajustes: audio y brillo (+ acceso a los controles)
    static bool settingsControls;

    public static bool Settings()
    {
        if (settingsControls) { if (Controls()) settingsControls = false; return false; }
        float S = UIKit.S;
        Dim();
        Rect p = Centered(680, 620);
        UIKit.Panel(p, "AJUSTES");
        float x = p.x + 56f * S, w = p.width - 112f * S, y = p.y + 58f * S;

        GameSettings.Master = UIKit.Slider(new Rect(x, y, w, 52f * S), "Volumen general", GameSettings.Master, 0f, 1f, Pct(GameSettings.Master), UIKit.Blue); y += 72f * S;
        GameSettings.Music = UIKit.Slider(new Rect(x, y, w, 52f * S), "Música", GameSettings.Music, 0f, 1f, Pct(GameSettings.Music), UIKit.Purple); y += 72f * S;
        GameSettings.Sfx = UIKit.Slider(new Rect(x, y, w, 52f * S), "Efectos (disparos, pasos, explosiones)", GameSettings.Sfx, 0f, 1f, Pct(GameSettings.Sfx), UIKit.Green); y += 72f * S;
        GameSettings.Ambient = UIKit.Slider(new Rect(x, y, w, 52f * S), "Ambiente (grada y viento)", GameSettings.Ambient, 0f, 1f, Pct(GameSettings.Ambient), UIKit.Slate); y += 72f * S;
        GameSettings.Brightness = UIKit.Slider(new Rect(x, y, w, 52f * S), "Brillo", GameSettings.Brightness, 0.5f, 1.5f, Pct(GameSettings.Brightness), UIKit.Gold); y += 76f * S;

        if (UIKit.Button(new Rect(p.center.x - 150f * S, y, 300f * S, 48f * S), "VER CONTROLES", UIKit.Slate, 20)) settingsControls = true;

        if (UIKit.Button(new Rect(p.center.x - 115f * S, p.yMax - 78f * S, 230f * S, 52f * S), "VOLVER", UIKit.Blue))
        {
            GameSettings.Save();
            return true;
        }
        return false;
    }

    // ------------------------------------------------------------ controles
    public static bool Controls()
    {
        float S = UIKit.S;
        Dim();
        Rect p = Centered(900, 680);
        UIKit.Panel(p, "CONTROLES");
        int rows = controlRows.GetLength(0);
        for (int i = 0; i < rows; i++)
        {
            float y = p.y + 60f * S + i * 34f * S;
            Rect chip = new Rect(p.x + 50f * S, y, 150f * S, 30f * S);
            UIKit.Box(new Rect(chip.x - 2f * S, chip.y - 2f * S, chip.width + 4f * S, chip.height + 4f * S), new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.9f));
            UIKit.Box(chip, new Color(0.93f, 0.93f, 0.96f));
            UIKit.Text(chip, controlRows[i, 0], 16, new Color(0.1f, 0.1f, 0.14f), TextAnchor.MiddleCenter, true, false);
            UIKit.Text(new Rect(p.x + 224f * S, y, p.width - 270f * S, 30f * S), controlRows[i, 1], 17, new Color(1f, 1f, 1f, 0.92f), TextAnchor.MiddleLeft, false);
        }
        return UIKit.Button(new Rect(p.center.x - 115f * S, p.yMax - 78f * S, 230f * S, 52f * S), "VOLVER", UIKit.Blue);
    }

    // ------------------------------------------------------------ créditos con desplazamiento automático
    public static bool Credits(ref float scroll)
    {
        float S = UIKit.S;
        Dim();
        Rect p = Centered(720, 600);
        UIKit.Panel(p, "CRÉDITOS");
        Rect area = new Rect(p.x + 40f * S, p.y + 50f * S, p.width - 80f * S, p.height - 150f * S);
        if (Event.current.type == EventType.Repaint) scroll += Time.unscaledDeltaTime * 42f * S;

        float total = 0f;
        for (int i = 0; i < CreditLines.Length; i++) total += CreditLines[i].Length == 0 ? 20f * S : (CreditLines[i].StartsWith("#") ? 46f * S : 32f * S);
        if (scroll > total + area.height) scroll = 0f;

        GUI.BeginGroup(area);
        float y = area.height - scroll;
        for (int i = 0; i < CreditLines.Length; i++)
        {
            string line = CreditLines[i];
            if (line.Length == 0) { y += 20f * S; continue; }
            bool head = line.StartsWith("#");
            float h = head ? 46f * S : 32f * S;
            if (y > -h && y < area.height)
            {
                float edge = Mathf.Clamp01(Mathf.Min(y, area.height - y) / (60f * S));      // se desvanece arriba y abajo
                Color col = head ? new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, edge) : new Color(1f, 1f, 1f, 0.92f * edge);
                UIKit.Text(new Rect(0f, y, area.width, h), head ? line.Substring(1) : line, head ? 26 : 19, col);
            }
            y += h;
        }
        GUI.EndGroup();
        UIKit.Divider(p.center.x, p.yMax - 96f * S, 300f * S, UIKit.Gold);
        return UIKit.Button(new Rect(p.center.x - 115f * S, p.yMax - 78f * S, 230f * S, 52f * S), "VOLVER", UIKit.Blue);
    }

    // ------------------------------------------------------------ JUGAR: elegir equipo y duración del partido
    static int selTeam = -1;
    public static void ResetSelection() { selTeam = -1; }

    // Devuelve: 0 Millonarios, 1 Nacional (con JUGAR), 2 solo mirar, -2 volver, -3 nada todavía
    public static int TeamSelect(bool showBack)
    {
        float S = UIKit.S, W = Screen.width;
        int result = -3;
        Dim(0.55f);
        UIKit.Outline(new Rect(0f, 20f * S, W, 64f * S), "ELIGE TU EQUIPO", 46, Color.white, new Color(0.05f, 0.07f, 0.14f));
        UIKit.Divider(W * 0.5f, 90f * S, 360f * S, UIKit.Gold);

        float cw = 380f * S, ch = 330f * S, gap = 40f * S;
        float x0 = (W - (cw * 2f + gap)) * 0.5f, y0 = 112f * S;
        string[] sub = { "BOGOTÁ · EL EMBAJADOR", "MEDELLÍN · EL VERDOLAGA" };
        string[] desc =
        {
            "Azul y blanco. La capital llega a Cali con ganas de dar la pelea.",
            "Verde y blanco. La montaña baja al valle a defender su historia."
        };
        for (int t = 0; t < 2; t++)
        {
            Rect card = new Rect(x0 + t * (cw + gap), y0, cw, ch);
            bool hot;
            float a = UIKit.HoverAnim(card, 500 + t, out hot);
            bool sel = selTeam == t;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * (1f + 0.03f * a + (sel ? 0.025f : 0f)), card.center);
            Color tc = UIKit.TeamColor(t);
            UIKit.Panel(card);
            UIKit.Gradient(new Rect(card.x + 16f * S, card.y + 16f * S, card.width - 32f * S, 215f * S),
                           new Color(tc.r * 1.4f, tc.g * 1.4f, tc.b * 1.4f, 0.5f + 0.2f * a + (sel ? 0.15f : 0f)), new Color(tc.r, tc.g, tc.b, 0f));
            UIKit.DrawCrest(new Rect(card.center.x - 66f * S, card.y + 28f * S, 132f * S, 132f * S), t, 1f);
            UIKit.Outline(new Rect(card.x, card.y + 164f * S, card.width, 46f * S), UIKit.TeamName(t), 34, Color.white, new Color(tc.r * 0.4f, tc.g * 0.4f, tc.b * 0.4f));
            UIKit.Text(new Rect(card.x, card.y + 208f * S, card.width, 24f * S), sub[t], 15, UIKit.Gold);
            UIKit.Text(new Rect(card.x + 34f * S, card.y + 238f * S, card.width - 68f * S, 48f * S), desc[t], 14, new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperCenter, false, true, true);
            if (sel)
            {
                UIKit.Corners(new Rect(card.x + 4f * S, card.y + 4f * S, card.width - 8f * S, card.height - 8f * S), UIKit.Gold, 34f * S, 4f * S);
                Rect rb = new Rect(card.center.x - 80f * S, card.yMax - 40f * S, 160f * S, 30f * S);
                UIKit.Round(rb, UIKit.Gold);
                UIKit.Text(rb, "ELEGIDO", 16, new Color(0.15f, 0.1f, 0.02f), TextAnchor.MiddleCenter, true, false);
            }
            else UIKit.Text(new Rect(card.x, card.yMax - 40f * S, card.width, 30f * S), "CLIC PARA ELEGIR", 14, new Color(1f, 1f, 1f, 0.55f + 0.3f * a), TextAnchor.MiddleCenter, false);
            if (GUI.Button(card, GUIContent.none, GUIStyle.none)) { selTeam = t; if (GameAudio.I != null) GameAudio.I.UiClick(); }
            GUI.matrix = saved;
        }

        // duración del partido
        Rect dp = new Rect(W * 0.5f - 310f * S, y0 + ch + 20f * S, 620f * S, 96f * S);
        UIKit.Panel(dp);
        UIKit.Text(new Rect(dp.x, dp.y + 12f * S, dp.width, 22f * S), "DURACIÓN DEL PARTIDO", 15, UIKit.Gold);
        float pw = 124f * S, pg = 12f * S;
        float px = dp.center.x - (pw * 4f + pg * 3f) * 0.5f;
        for (int i = 0; i < minuteOptions.Length; i++)
        {
            bool on = GameSettings.MatchMinutes == minuteOptions[i];
            if (UIKit.Button(new Rect(px + i * (pw + pg), dp.y + 40f * S, pw, 44f * S), minuteOptions[i] + " min", on ? new Color(0.95f, 0.72f, 0.1f) : UIKit.Slate, 19))
                GameSettings.MatchMinutes = minuteOptions[i];
        }

        // acciones
        float by = dp.yMax + 20f * S;
        bool can = selTeam >= 0;
        if (UIKit.Button(new Rect(W * 0.5f - 140f * S, by, 280f * S, 62f * S), can ? "JUGAR" : "ELIGE UN EQUIPO", UIKit.Green, can ? 30 : 20, can))
        {
            GameSettings.Save();
            result = selTeam;
        }
        if (UIKit.Button(new Rect(W * 0.5f - 140f * S - 224f * S, by + 6f * S, 208f * S, 50f * S), "SOLO MIRAR", UIKit.Slate, 18)) { GameSettings.Save(); result = 2; }
        if (showBack && UIKit.Button(new Rect(W * 0.5f + 140f * S + 16f * S, by + 6f * S, 208f * S, 50f * S), "VOLVER", UIKit.Red, 18)) result = -2;
        return result;
    }
}
