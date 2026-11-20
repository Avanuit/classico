using UnityEngine;

// MENÚ PRINCIPAL del juego (dentro de la escena del partido, con el estadio y los jugadores de fondo; ver MenuShowcase).
// Flujo:  [título: los escudos chocan y revelan CLÁSICO] -> panel lateral (JUGAR / OPCIONES / CRÉDITOS / SALIR)
//         JUGAR -> se abre la elección de equipo (animada) -> SIGUIENTE -> se abre la confirmación del tiempo (animada) -> ¡A JUGAR!
public static class MainMenuUI
{
    enum Step { Home, Team, Time, Settings, Credits }
    static Step step = Step.Home;
    static float homeA, teamA, timeA, setA, credA, creditsScroll, enterTime;
    static bool firstTime = true, impactPlayed;
    static int sel = -1;                   // 0 Millonarios, 1 Nacional, 2 solo mirar
    static readonly int[] minuteOptions = { 3, 5, 8, 10 };
    const float HitTime = 1.5f;

    // La intro NO arranca hasta que el juego deja de dar tirones (carga de escena, shaders, telas, animaciones...):
    // si arrancara de una vez, el choque de escudos pasaría durante el lag y no se vería.
    static bool waitingStable;
    static float waitSince;
    static int stableFrames, lastCountedFrame;

    static bool WaitForSmoothFrames()
    {
        if (!waitingStable) return false;
        if (Event.current.type == EventType.Repaint && Time.frameCount != lastCountedFrame)
        {
            lastCountedFrame = Time.frameCount;
            stableFrames = Time.unscaledDeltaTime < 1f / 40f ? stableFrames + 1 : 0;
        }
        float waited = Time.unscaledTime - waitSince;
        if ((stableFrames >= 12 && waited > 0.6f) || waited > 5f)
        {
            waitingStable = false;
            enterTime = Time.unscaledTime;
            return false;
        }
        UIKit.Box(new Rect(0f, 0f, Screen.width, Screen.height), Color.black);
        return true;
    }

    public static void OnEnterMenu()
    {
        enterTime = Time.unscaledTime;
        waitingStable = firstTime;
        waitSince = Time.unscaledTime;
        stableFrames = 0;
        step = Step.Home;
        homeA = teamA = timeA = setA = credA = 0f;
        sel = -1;
        impactPlayed = false;
    }

    static float Ease(float x) { return 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f); }
    static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

    public static void Draw(MatchManager m)
    {
        UIKit.Begin();
        if (WaitForSmoothFrames()) return;
        float S = UIKit.S, W = Screen.width, H = Screen.height;
        float t = Time.unscaledTime - enterTime;
        float ready = firstTime ? 3.8f : 0.25f;
        float animStart = firstTime ? 2.6f : 0f;

        if (Input.GetKeyDown(KeyCode.Escape) && step != Step.Home && t > ready) step = Step.Home;
        if (firstTime && t > 0.35f && t < ready && Input.anyKeyDown) { enterTime = Time.unscaledTime - ready; t = ready; impactPlayed = true; }

        if (Event.current.type == EventType.Repaint && t >= animStart)
        {
            float k = Time.unscaledDeltaTime * 2.6f;
            homeA = Mathf.MoveTowards(homeA, step == Step.Home ? 1f : 0f, k);
            teamA = Mathf.MoveTowards(teamA, step == Step.Team ? 1f : 0f, k);
            timeA = Mathf.MoveTowards(timeA, step == Step.Time ? 1f : 0f, k);
            setA = Mathf.MoveTowards(setA, step == Step.Settings ? 1f : 0f, k);
            credA = Mathf.MoveTowards(credA, step == Step.Credits ? 1f : 0f, k);
        }

        float pw = 440f * S;
        Vector2 logoFinal = new Vector2(pw * 0.5f, 140f * S);
        if (firstTime && t < ready) DrawSplash(t, S, W, H, logoFinal);

        if (t >= ready - 0.2f)
        {
            if (homeA > 0.01f) DrawHome(S, W, H, pw, logoFinal, t, ready);
            if (teamA > 0.01f) DrawTeam(S, W, H);
            if (timeA > 0.01f) DrawTime(m, S, W, H);
            if (setA > 0.01f) { Pop(setA); if (MenuScreens.Settings()) step = Step.Home; EndPop(); }
            if (credA > 0.01f) { Pop(credA); if (MenuScreens.Credits(ref creditsScroll)) step = Step.Home; EndPop(); }
        }
        if (firstTime && t >= ready) firstTime = false;
    }

    static Matrix4x4 popSaved;
    static void Pop(float a)
    {
        popSaved = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * (0.92f + 0.08f * Ease(a)), new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
    }
    static void EndPop() { GUI.matrix = popSaved; }

    // ============================================================ logo
    static void DrawLogo(Vector2 c, float scale, float alpha)
    {
        float S = UIKit.S;
        float k = S * scale;
        Color gold = A(UIKit.Gold, alpha);
        // fila de escudos ENCIMA del título (antes tapaban la C y la O y se salían del panel)
        float crest = 60f * k, crestY = c.y - 80f * k;
        UIKit.Divider(c.x, crestY, 360f * k, gold);
        UIKit.Box(new Rect(c.x - 62f * k, crestY - 34f * k, 124f * k, 68f * k), new Color(0.05f, 0.06f, 0.12f, 0.9f * alpha));
        UIKit.DrawCrest(new Rect(c.x - 58f * k, crestY - crest * 0.5f, crest, crest), 0, alpha);
        UIKit.DrawCrest(new Rect(c.x - 2f * k, crestY - crest * 0.5f, crest, crest), 1, alpha);
        UIKit.Outline(new Rect(c.x - 220f * k, c.y - 44f * k, 440f * k, 88f * k), "CLÁSICO", Mathf.RoundToInt(70f * scale),
                      new Color(1f, 1f, 1f, alpha), new Color(0.03f, 0.05f, 0.16f, alpha));
        Rect rb = new Rect(c.x - 180f * k, c.y + 46f * k, 360f * k, 30f * k);
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        UIKit.Slant(rb, 1);
        GUI.color = old;
        UIKit.Text(rb, "NACIONAL  vs  MILLONARIOS", Mathf.RoundToInt(17f * scale), new Color(0.16f, 0.09f, 0.02f, alpha), TextAnchor.MiddleCenter, true, false);
        UIKit.Text(new Rect(c.x - 200f * k, c.y + 80f * k, 400f * k, 22f * k), "EDICIÓN  CALI", Mathf.RoundToInt(14f * scale), new Color(1f, 1f, 1f, 0.75f * alpha));
    }

    // los escudos vuelan desde los lados, chocan y revelan el título, que baja a la cabecera del panel
    static void DrawSplash(float t, float S, float W, float H, Vector2 logoFinal)
    {
        float since = t - HitTime;
        Matrix4x4 saved = GUI.matrix;
        if (since > 0f && since < 0.5f)
        {
            float k = 1f - since / 0.5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(Mathf.Sin(t * 95f) * 11f * k * S, Mathf.Cos(t * 80f) * 8f * k * S, 0f), Quaternion.identity, Vector3.one);
        }
        UIKit.Box(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.5f * (1f - Ease((t - 2.2f) / 1.4f))));
        float size = 200f * S, cy = H * 0.40f;

        if (t < HitTime + 0.7f)
        {
            float p = Mathf.Clamp01(t / HitTime), e = p * p * p;
            float off = Mathf.Lerp(W * 0.55f, size * 0.46f, e);
            float scale = 1f, alpha = 1f;
            if (since > 0f)
            {
                float q = Mathf.Clamp01(since / 0.7f);
                off += q * size * 0.3f; scale = 1f - 0.35f * q; alpha = 1f - q;
            }
            for (int team = 0; team < 2; team++)
            {
                Vector2 c = new Vector2(W * 0.5f + (team == 0 ? -off : off), cy);
                Matrix4x4 sv = GUI.matrix;
                GUIUtility.RotateAroundPivot((team == 0 ? -1f : 1f) * (1f - e) * 360f, c);
                UIKit.DrawCrest(new Rect(c.x - size * scale * 0.5f, c.y - size * scale * 0.5f, size * scale, size * scale), team, alpha);
                GUI.matrix = sv;
            }
        }
        if (!impactPlayed && t >= HitTime)
        {
            impactPlayed = true;
            if (GameAudio.I != null) GameAudio.I.Impact();
        }
        if (since > 0f && since < 0.9f)
            for (int i = 0; i < 18; i++)
            {
                float ang = i * (Mathf.PI * 2f / 18f) + (i % 3) * 0.17f;
                float dist = since * (560f + (i % 4) * 90f) * S;
                UIKit.Box(new Rect(W * 0.5f + Mathf.Cos(ang) * dist - 4f * S, cy + Mathf.Sin(ang) * dist - 4f * S, 8f * S, 8f * S), new Color(1f, 0.9f, 0.4f, 1f - since / 0.9f));
            }
        if (since > 0f && since < 0.4f) UIKit.Box(new Rect(0f, 0f, W, H), new Color(1f, 1f, 1f, 0.85f * (1f - since / 0.4f)));

        if (since > 0.1f)
        {
            float q = Mathf.Clamp01((since - 0.1f) / 0.7f);
            float g = Ease((t - 2.4f) / 1.2f);
            Vector2 pos = Vector2.Lerp(new Vector2(W * 0.5f, cy), logoFinal, g);
            float sc = Mathf.Lerp(1.55f, 1f, g) * Mathf.Lerp(0.6f, 1f, 1f - (1f - q) * (1f - q));
            DrawLogo(pos, sc, q);
        }
        GUI.matrix = saved;
        if (t > 0.5f && t < 3.4f)
            UIKit.Text(new Rect(W - 420f * S, H - 40f * S, 400f * S, 26f * S), "Pulsa cualquier tecla para saltar", 14, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleRight, false);
    }

    // ============================================================ panel principal
    static void DrawHome(float S, float W, float H, float pw, Vector2 logoFinal, float t, float ready)
    {
        float ea = Ease(homeA);
        float px = -pw * (1f - ea);
        Rect panel = new Rect(px, 0f, pw, H);
        UIKit.Gradient(panel, new Color(0.04f, 0.05f, 0.10f, 0.95f), new Color(0.08f, 0.10f, 0.19f, 0.95f));
        // marca de agua de cancha
        GUI.BeginGroup(panel);
        float d = pw * 1.25f;
        UIKit.Ring(new Rect(pw * 0.5f - d * 0.5f, H * 0.62f - d * 0.5f, d, d), new Color(1f, 1f, 1f, 0.05f));
        UIKit.Box(new Rect(0f, H * 0.62f - S, pw, 2f * S), new Color(1f, 1f, 1f, 0.04f));
        for (int i = 0; i < 30; i++) UIKit.Box(new Rect(pw - 22f * S, i * 26f * S + 6f * S, 10f * S, 2f * S), new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.35f));
        GUI.EndGroup();
        // borde derecho doble con rombos
        UIKit.Box(new Rect(panel.xMax - 4f * S, 0f, 4f * S, H), UIKit.Gold);
        UIKit.Box(new Rect(panel.xMax - 11f * S, 0f, 1.5f * S, H), A(UIKit.Gold, 0.45f));
        for (int i = 0; i < 5; i++) UIKit.Diamond(new Vector2(panel.xMax - 2f * S, H * (0.15f + 0.175f * i)), 9f * S, UIKit.Gold);

        if (!(firstTime && t < ready)) DrawLogo(new Vector2(px + logoFinal.x, logoFinal.y), 1f, ea);

        bool act = step == Step.Home && homeA > 0.85f;
        string[] labels = { "JUGAR", "OPCIONES", "CRÉDITOS", "SALIR" };
        float[] ys = { 268f, 372f, 440f, 508f };
        for (int i = 0; i < 4; i++)
        {
            float e = Ease(homeA * 1.7f - i * 0.18f);
            bool primary = i == 0;
            float bw = (primary ? 360f : 310f) * S, bh = (primary ? 80f : 58f) * S;
            Rect r = new Rect(px + (pw - bw) * 0.5f - (1f - e) * 120f * S, ys[i] * S, bw, bh);
            if (UIKit.MenuButton(r, labels[i], primary, primary ? 32 : 24, act))
            {
                if (i == 0) { step = Step.Team; sel = -1; }
                else if (i == 1) step = Step.Settings;
                else if (i == 2) { step = Step.Credits; creditsScroll = 0f; }
                else GameSettings.Quit();
            }
        }
        UIKit.Box(new Rect(px + 40f * S, H - 78f * S, pw - 80f * S, 1.5f * S), A(UIKit.Gold, 0.35f));
        UIKit.Text(new Rect(px, H - 70f * S, pw, 24f * S), "v1.0  ·  Unity 2019.4", 13, new Color(1f, 1f, 1f, 0.55f), TextAnchor.MiddleCenter, false);
        UIKit.Text(new Rect(px, H - 46f * S, pw, 24f * S), "Millonarios F.C. y Atlético Nacional: marcas de sus clubes", 11, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleCenter, false);
    }

    // ============================================================ paso 1: elegir equipo
    static void DrawTeam(float S, float W, float H)
    {
        float ea = Ease(teamA);
        bool act = step == Step.Team && teamA > 0.85f;
        UIKit.Box(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.55f * teamA));
        float ty = 22f * S - (1f - ea) * 130f * S;
        UIKit.Outline(new Rect(0f, ty, W, 64f * S), "ELIGE TU EQUIPO", 48, Color.white, new Color(0.04f, 0.06f, 0.16f));
        UIKit.Divider(W * 0.5f, ty + 74f * S, 420f * S, UIKit.Gold);

        float cw = 390f * S, ch = 352f * S, gap = 40f * S;
        float y0 = 124f * S;
        string[] sub = { "BOGOTÁ · EL EMBAJADOR", "MEDELLÍN · EL VERDOLAGA" };
        string[] desc =
        {
            "Azul y blanco. La capital llega a Cali con ganas de dar la pelea.",
            "Verde y blanco. La montaña baja al valle a defender su historia."
        };
        for (int t = 0; t < 2; t++)
        {
            float slide = (1f - ea) * W * 0.6f * (t == 0 ? -1f : 1f);
            Rect card = new Rect(W * 0.5f + (t == 0 ? -cw - gap * 0.5f : gap * 0.5f) + slide, y0, cw, ch);
            bool hot;
            float a = UIKit.HoverAnim(card, 700 + t, out hot);
            if (!act) a = 0f;
            bool isSel = sel == t;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.ScaleAroundPivot(Vector2.one * (1f + 0.03f * a + (isSel ? 0.025f : 0f)), card.center);
            Color tc = UIKit.TeamColor(t);
            UIKit.Panel(card);
            UIKit.Gradient(new Rect(card.x + 18f * S, card.y + 18f * S, card.width - 36f * S, 232f * S),
                           new Color(tc.r * 1.5f, tc.g * 1.5f, tc.b * 1.5f, 0.50f + 0.2f * a + (isSel ? 0.15f : 0f)), new Color(tc.r, tc.g, tc.b, 0f));
            // aureola pulsante detrás del escudo
            float pulse = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 2.2f + t);
            float rd = 190f * S * pulse;
            UIKit.Ring(new Rect(card.center.x - rd * 0.5f, card.y + 100f * S - rd * 0.5f + 8f * S, rd, rd), new Color(1f, 1f, 1f, 0.18f));
            UIKit.Ring(new Rect(card.center.x - rd * 0.7f, card.y + 100f * S - rd * 0.7f + 8f * S, rd * 1.4f, rd * 1.4f), new Color(UIKit.Gold.r, UIKit.Gold.g, UIKit.Gold.b, 0.10f));
            UIKit.DrawCrest(new Rect(card.center.x - 68f * S, card.y + 36f * S, 136f * S, 136f * S), t, 1f);
            UIKit.Outline(new Rect(card.x, card.y + 176f * S, card.width, 46f * S), UIKit.TeamName(t), 34, Color.white, new Color(tc.r * 0.4f, tc.g * 0.4f, tc.b * 0.4f));
            UIKit.Text(new Rect(card.x, card.y + 220f * S, card.width, 24f * S), sub[t], 15, UIKit.Gold);
            UIKit.Text(new Rect(card.x + 36f * S, card.y + 250f * S, card.width - 72f * S, 48f * S), desc[t], 14, new Color(1f, 1f, 1f, 0.8f), TextAnchor.UpperCenter, false, true, true);
            if (isSel)
            {
                UIKit.Corners(new Rect(card.x + 2f * S, card.y + 2f * S, card.width - 4f * S, card.height - 4f * S), UIKit.Gold, 40f * S, 5f * S);
                Rect rb = new Rect(card.center.x - 90f * S, card.yMax - 44f * S, 180f * S, 32f * S);
                UIKit.Slant(rb, 1);
                UIKit.Text(rb, "ELEGIDO", 17, new Color(0.15f, 0.09f, 0.02f), TextAnchor.MiddleCenter, true, false);
            }
            else UIKit.Text(new Rect(card.x, card.yMax - 44f * S, card.width, 30f * S), "CLIC PARA ELEGIR", 13, new Color(1f, 1f, 1f, 0.5f + 0.3f * a), TextAnchor.MiddleCenter, false);
            if (act && GUI.Button(card, GUIContent.none, GUIStyle.none)) { sel = t; if (GameAudio.I != null) GameAudio.I.UiClick(); }
            GUI.matrix = saved;
        }

        float by = y0 + ch + 34f * S + (1f - ea) * 140f * S;
        bool can = sel >= 0;
        if (UIKit.MenuButton(new Rect(W * 0.5f - 190f * S, by, 380f * S, 70f * S), can ? "SIGUIENTE" : "ELIGE UN EQUIPO", true, can ? 30 : 20, act && can)) step = Step.Time;
        if (UIKit.Button(new Rect(W * 0.5f - 190f * S - 250f * S, by + 10f * S, 230f * S, 52f * S), "SOLO MIRAR", UIKit.Slate, 18, act)) { sel = 2; step = Step.Time; }
        if (UIKit.Button(new Rect(W * 0.5f + 190f * S + 20f * S, by + 10f * S, 230f * S, 52f * S), "VOLVER", UIKit.Red, 18, act)) step = Step.Home;
    }

    // ============================================================ paso 2: confirmar el tiempo del partido
    static void DrawTime(MatchManager m, float S, float W, float H)
    {
        float ea = Ease(timeA);
        bool act = step == Step.Time && timeA > 0.85f;
        UIKit.Box(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.6f * timeA));
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.ScaleAroundPivot(Vector2.one * (0.86f + 0.14f * ea), new Vector2(W * 0.5f, H * 0.5f));
        Rect p = MenuScreens.Centered(720, 520);
        UIKit.Panel(p, "CONFIRMA EL PARTIDO");

        // enfrentamiento
        int mine = sel == 2 ? 0 : sel;
        float cy = p.y + 120f * S;
        for (int team = 0; team < 2; team++)
        {
            float cx = p.center.x + (team == 0 ? -170f * S : 170f * S);
            bool you = sel != 2 && team == mine;
            float s2 = you ? 1f : 0.82f;
            if (you) UIKit.Ring(new Rect(cx - 82f * S, cy - 82f * S, 164f * S, 164f * S), A(UIKit.Gold, 0.5f + 0.2f * Mathf.Sin(Time.unscaledTime * 3f)));
            UIKit.DrawCrest(new Rect(cx - 56f * S * s2, cy - 56f * S * s2, 112f * S * s2, 112f * S * s2), team, you ? 1f : 0.75f);
            UIKit.Text(new Rect(cx - 120f * S, cy + 62f * S, 240f * S, 26f * S), UIKit.TeamName(team), 18, you ? Color.white : new Color(1f, 1f, 1f, 0.7f));
            if (you) UIKit.Text(new Rect(cx - 60f * S, cy + 86f * S, 120f * S, 22f * S), "TÚ", 14, UIKit.Gold);
        }
        UIKit.Outline(new Rect(p.center.x - 50f * S, cy - 30f * S, 100f * S, 60f * S), "VS", 40, UIKit.Gold, new Color(0.1f, 0.06f, 0f));
        if (sel == 2) UIKit.Text(new Rect(p.x, cy + 100f * S, p.width, 24f * S), "MODO ESPECTADOR: la IA juega por los dos", 15, new Color(1f, 1f, 1f, 0.8f));

        UIKit.Divider(p.center.x, p.y + 252f * S, 460f * S, UIKit.Gold);
        UIKit.Text(new Rect(p.x, p.y + 264f * S, p.width, 26f * S), "DURACIÓN DEL PARTIDO", 18, UIKit.Gold);
        float pw = 128f * S, pg = 14f * S;
        float px = p.center.x - (pw * 4f + pg * 3f) * 0.5f;
        for (int i = 0; i < minuteOptions.Length; i++)
        {
            bool on = GameSettings.MatchMinutes == minuteOptions[i];
            Rect r = new Rect(px + i * (pw + pg), p.y + 298f * S, pw, 54f * S);
            if (on) UIKit.Ring(new Rect(r.x - 10f * S, r.y - 14f * S, r.width + 20f * S, r.height + 28f * S), A(UIKit.Gold, 0.18f));
            if (UIKit.Button(r, minuteOptions[i] + " MIN", on ? new Color(0.98f, 0.74f, 0.1f) : UIKit.Slate, 20, act)) GameSettings.MatchMinutes = minuteOptions[i];
        }
        UIKit.Text(new Rect(p.x + 30f * S, p.y + 360f * S, p.width - 60f * S, 24f * S), "Tiempo real. Los 90 minutos del Clásico se reparten en ese tiempo.", 13, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleCenter, false);

        if (UIKit.MenuButton(new Rect(p.center.x - 190f * S, p.y + 396f * S, 380f * S, 72f * S), "¡A JUGAR!", true, 32, act))
        {
            GameSettings.Save();
            firstTime = false;
            m.StartMatch(sel == 2 ? -1 : sel);
        }
        if (UIKit.Button(new Rect(p.center.x - 80f * S, p.yMax - 52f * S, 160f * S, 40f * S), "ATRÁS", UIKit.Slate, 16, act)) step = Step.Team;
        GUI.matrix = saved;
    }
}
