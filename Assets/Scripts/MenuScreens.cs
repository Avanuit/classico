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
        "[ESCRIBE AQUÍ TU NOMBRE]",
        "",
        "#MODELOS 3D",
        "Jugador, estadio y balón: Sketchfab",
        "(créditos de cada autor en la página del modelo)",
        "",
        "#MOTOR",
        "Unity 2019.4",
        "",
        "#MÚSICA Y SONIDO",
        "[ESCRIBE AQUÍ TUS FUENTES DE AUDIO]",
        "",
        "#ESCUDOS",
        "Millonarios F.C. y Atlético Nacional",
        "son marcas de sus respectivos clubes",
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

    // ------------------------------------------------------------ ajustes: audio, brillo y duración
    public static bool Settings()
    {
        float S = UIKit.S;
        Dim();
        Rect p = Centered(680, 640);
        UIKit.Panel(p, "AJUSTES");
        float x = p.x + 56f * S, w = p.width - 112f * S, y = p.y + 56f * S;

        GameSettings.Master = UIKit.Slider(new Rect(x, y, w, 52f * S), "Volumen general", GameSettings.Master, 0f, 1f, Pct(GameSettings.Master), UIKit.Blue); y += 70f * S;
        GameSettings.Music = UIKit.Slider(new Rect(x, y, w, 52f * S), "Música", GameSettings.Music, 0f, 1f, Pct(GameSettings.Music), UIKit.Purple); y += 70f * S;
        GameSettings.Sfx = UIKit.Slider(new Rect(x, y, w, 52f * S), "Efectos (disparos, pasos, explosiones)", GameSettings.Sfx, 0f, 1f, Pct(GameSettings.Sfx), UIKit.Green); y += 70f * S;
        GameSettings.Ambient = UIKit.Slider(new Rect(x, y, w, 52f * S), "Ambiente (grada y viento)", GameSettings.Ambient, 0f, 1f, Pct(GameSettings.Ambient), UIKit.Slate); y += 70f * S;
        GameSettings.Brightness = UIKit.Slider(new Rect(x, y, w, 52f * S), "Brillo", GameSettings.Brightness, 0.5f, 1.5f, Pct(GameSettings.Brightness), UIKit.Gold); y += 74f * S;

        UIKit.Text(new Rect(x, y + 4f * S, w * 0.5f, 36f * S), "Duración del partido", 18, Color.white, TextAnchor.MiddleLeft);
        int idx = 1;
        for (int i = 0; i < minuteOptions.Length; i++) if (minuteOptions[i] == GameSettings.MatchMinutes) idx = i;
        if (UIKit.Button(new Rect(x + w - 250f * S, y, 46f * S, 44f * S), "<", UIKit.Slate, 22)) idx = (idx + minuteOptions.Length - 1) % minuteOptions.Length;
        UIKit.Text(new Rect(x + w - 198f * S, y, 142f * S, 44f * S), minuteOptions[idx] + " min", 22, UIKit.Gold);
        if (UIKit.Button(new Rect(x + w - 50f * S, y, 46f * S, 44f * S), ">", UIKit.Slate, 22)) idx = (idx + 1) % minuteOptions.Length;
        GameSettings.MatchMinutes = minuteOptions[idx];

        if (UIKit.Button(new Rect(p.center.x - 115f * S, p.yMax - 84f * S, 230f * S, 54f * S), "VOLVER", UIKit.Blue))
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
        Rect p = Centered(900, 640);
        UIKit.Panel(p, "CONTROLES");
        int rows = controlRows.GetLength(0);
        for (int i = 0; i < rows; i++)
        {
            float y = p.y + 60f * S + i * 38f * S;
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

    // ------------------------------------------------------------ elección de equipo (tarjetas con escudo)
    // Devuelve: 0 Millonarios, 1 Nacional, 2 solo mirar, -2 volver, -3 nada todavía
    public static int TeamSelect(bool showBack)
    {
        float S = UIKit.S;
        int result = -3;
        Dim(0.55f);
        float cw = 400f * S, ch = 420f * S, gap = 44f * S;
        float x0 = (Screen.width - (cw * 2f + gap)) * 0.5f, y0 = (Screen.height - ch) * 0.5f - 24f * S;

        UIKit.Outline(new Rect(0f, y0 - 92f * S, Screen.width, 60f * S), "ELIGE TU EQUIPO", 46, Color.white, new Color(0.05f, 0.07f, 0.14f));
        UIKit.Divider(Screen.width * 0.5f, y0 - 24f * S, 360f * S, UIKit.Gold);

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
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * (1f + 0.035f * a), card.center);
            Color tc = UIKit.TeamColor(t);
            UIKit.Panel(card);
            UIKit.Gradient(new Rect(card.x + 16f * S, card.y + 16f * S, card.width - 32f * S, 250f * S),
                           new Color(tc.r * 1.4f, tc.g * 1.4f, tc.b * 1.4f, 0.55f + 0.2f * a), new Color(tc.r, tc.g, tc.b, 0f));
            UIKit.DrawCrest(new Rect(card.center.x - 80f * S, card.y + 32f * S, 160f * S, 160f * S), t, 1f);
            UIKit.Outline(new Rect(card.x, card.y + 198f * S, card.width, 50f * S), UIKit.TeamName(t), 38, Color.white, new Color(tc.r * 0.4f, tc.g * 0.4f, tc.b * 0.4f));
            UIKit.Text(new Rect(card.x, card.y + 246f * S, card.width, 26f * S), sub[t], 16, UIKit.Gold);
            UIKit.Text(new Rect(card.x + 36f * S, card.y + 276f * S, card.width - 72f * S, 52f * S), desc[t], 15, new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperCenter, false, true, true);
            if (UIKit.Button(new Rect(card.x + 56f * S, card.yMax - 76f * S, card.width - 112f * S, 52f * S), "JUGAR", new Color(tc.r * 1.5f, tc.g * 1.5f, tc.b * 1.5f))) result = t;
            GUI.matrix = saved;
        }
        float by = y0 + ch + 36f * S;
        if (UIKit.Button(new Rect(Screen.width * 0.5f - (showBack ? 250f : 125f) * S, by, 240f * S, 50f * S), "SOLO MIRAR", UIKit.Slate, 20)) result = 2;
        if (showBack && UIKit.Button(new Rect(Screen.width * 0.5f + 10f * S, by, 240f * S, 50f * S), "VOLVER", UIKit.Red, 20)) result = -2;
        return result;
    }
}
