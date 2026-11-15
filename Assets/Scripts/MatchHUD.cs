using UnityEngine;

// Todo lo que se dibuja en pantalla durante la partida: marcador con escudos, reloj de tiempo límite, avisos, lista de bajas,
// inventario tipo Fortnite, vida, puntos, minimapa, etiquetas de jugadores, intro con lore, pantalla final con podio
// y los menús de pausa. Usa UIKit (marcos dorados, botones animados).
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

    int pausePage, menuPage;
    float creditsScroll;

    static float Ease(float x) { return 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f); }
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

    // ============================================================ despacho por estado
    void OnGUI()
    {
        MatchManager m = MatchManager.I;
        if (m == null) return;
        UIKit.Begin();

        if (m.State == MatchState.Menu) { DrawFallbackMenu(m); return; }
        if (m.State == MatchState.Intro) { DrawIntro(m); return; }
        if (m.State == MatchState.Finished) { DrawFinished(m); return; }

        DrawScoreboard(m);
        DrawBanner();
        DrawKillFeed(m);
        DrawMinimap(m);
        if (m.UserTeam >= 0 && !m.Paused)
        {
            DrawNameTags(m);
            DrawUserHud(m, PlayerController.I);
            DrawCombatHud(m, PlayerController.I);
        }
        else if (!m.Paused) DrawNameTags(m);

        if (m.Paused) DrawPause(m); else pausePage = 0;
    }

    // ============================================================ marcador y reloj
    void DrawScoreboard(MatchManager m)
    {
        float S = UIKit.S, W = Screen.width;
        float w = 720f * S, h = 64f * S;
        Rect r = new Rect((W - w) * 0.5f, 10f * S, w, h);
        UIKit.Box(new Rect(r.x - 3f * S, r.y - 3f * S, r.width + 6f * S, r.height + 6f * S), A(UIKit.Gold, 0.95f));
        UIKit.Gradient(r, new Color(0.10f, 0.13f, 0.25f, 0.97f), new Color(0.03f, 0.04f, 0.09f, 0.97f));
        UIKit.Box(new Rect(r.x, r.y, 12f * S, h), UIKit.TeamColor(0) * 1.5f);
        UIKit.Box(new Rect(r.xMax - 12f * S, r.y, 12f * S, h), UIKit.TeamColor(1) * 1.5f);
        UIKit.DrawCrest(new Rect(r.x + 22f * S, r.y + 8f * S, 48f * S, 48f * S), 0, 1f);
        UIKit.DrawCrest(new Rect(r.xMax - 70f * S, r.y + 8f * S, 48f * S, 48f * S), 1, 1f);
        UIKit.Text(new Rect(r.x + 80f * S, r.y, 220f * S, h), "MILLONARIOS", 24, Color.white, TextAnchor.MiddleLeft);
        UIKit.Text(new Rect(r.xMax - 300f * S, r.y, 220f * S, h), "NACIONAL", 24, Color.white, TextAnchor.MiddleRight);

        Rect sc = new Rect(r.center.x - 78f * S, r.y + 4f * S, 156f * S, h - 8f * S);
        UIKit.Box(sc, new Color(0f, 0f, 0f, 0.6f));
        UIKit.Corners(sc, UIKit.Gold, 10f * S, 2f * S);
        UIKit.Text(sc, m.Score[0] + "  -  " + m.Score[1], 38, Color.white);

        // reloj de tiempo límite (cuenta regresiva)
        float left = m.TimeLeft;
        string clock = string.Format("{0:00}:{1:00}", (int)(left / 60f), (int)(left % 60f));
        bool low = left < 30f && m.State != MatchState.Intro;
        float pulse = low ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 4f)) : 1f;
        Rect tr = new Rect(r.center.x - 72f * S, r.yMax + 4f * S, 144f * S, 34f * S);
        UIKit.Round(tr, low ? new Color(0.9f, 0.25f * pulse, 0.25f * pulse) : new Color(0.25f, 0.28f, 0.45f));
        UIKit.Text(tr, clock, 22, Color.white);
        UIKit.Text(new Rect(tr.x - 120f * S, tr.y, 112f * S, tr.height), "TIEMPO", 13, A(UIKit.Gold, 0.9f), TextAnchor.MiddleRight);
    }

    void DrawBanner()
    {
        if (Time.time >= bannerUntil || string.IsNullOrEmpty(bannerText)) return;
        float S = UIKit.S;
        float age = Time.time - bannerStart, left = bannerUntil - Time.time;
        float alpha = Mathf.Clamp01(left / 0.4f) * Mathf.Clamp01(age / 0.15f);
        float pop = Mathf.Lerp(1.3f, 1f, Mathf.Clamp01(age / 0.25f));
        string[] lines = bannerText.Split('\n');
        float bh = (lines.Length > 1 ? 136f : 100f) * S;
        Rect band = new Rect(0f, Screen.height * 0.20f, Screen.width, bh);
        Color bc = bannerColor;
        UIKit.Box(band, new Color(bc.r * 0.8f, bc.g * 0.8f, bc.b * 0.8f, 0.82f * alpha));
        UIKit.Box(new Rect(0f, band.y, band.width, 3f * S), A(UIKit.Gold, alpha));
        UIKit.Box(new Rect(0f, band.yMax - 3f * S, band.width, 3f * S), A(UIKit.Gold, alpha));
        UIKit.Diamond(new Vector2(band.width * 0.5f - 280f * S, band.center.y), 12f * S, A(UIKit.Gold, alpha));
        UIKit.Diamond(new Vector2(band.width * 0.5f + 280f * S, band.center.y), 12f * S, A(UIKit.Gold, alpha));
        Rect first = new Rect(0f, band.y + (lines.Length > 1 ? 6f * S : 0f), band.width, (lines.Length > 1 ? 86f : bh / S) * S);
        UIKit.Outline(first, lines[0], Mathf.RoundToInt(54f * pop), A(Color.white, alpha), A(new Color(0f, 0f, 0f), alpha * 0.8f));
        for (int i = 1; i < lines.Length; i++)
            UIKit.Text(new Rect(0f, band.y + 82f * S + (i - 1) * 34f * S, band.width, 34f * S), lines[i], 30, A(UIKit.Gold, alpha));
    }

    void DrawKillFeed(MatchManager m)
    {
        float S = UIKit.S, W = Screen.width;
        int shown = 0;
        for (int i = m.Feed.Count - 1; i >= 0 && shown < 6; i--)
        {
            MatchManager.FeedItem f = m.Feed[i];
            float age = Time.time - f.time;
            if (age > 6f) continue;
            float a = Mathf.Clamp01(6f - age);
            Rect r = new Rect(W - 410f * S, 14f * S + shown * 32f * S, 394f * S, 28f * S);
            UIKit.Box(r, f.user ? new Color(0.45f, 0.32f, 0.04f, 0.78f * a) : new Color(0f, 0f, 0f, 0.6f * a));
            UIKit.Box(new Rect(r.x, r.y, 4f * S, r.height), A(UIKit.Gold, a));
            UIKit.Text(new Rect(r.x + 10f * S, r.y, r.width - 14f * S, r.height), f.text, 14, A(Color.white, a), TextAnchor.MiddleLeft, true, false);
            shown++;
        }
    }

    // ============================================================ mira, fuerza de tiro y saques
    void DrawUserHud(MatchManager m, PlayerController pc)
    {
        float S = UIKit.S, cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;

        float len = 9f * S, th = 2f * S, gap = 6f * S;
        Color c = new Color(1f, 1f, 1f, 0.88f);
        UIKit.Box(new Rect(cx - gap - len, cy - th * 0.5f, len, th), c);
        UIKit.Box(new Rect(cx + gap, cy - th * 0.5f, len, th), c);
        UIKit.Box(new Rect(cx - th * 0.5f, cy - gap - len, th, len), c);
        UIKit.Box(new Rect(cx - th * 0.5f, cy + gap, th, len), c);
        UIKit.Box(new Rect(cx - th, cy - th, th * 2f, th * 2f), new Color(1f, 0.85f, 0.1f, 0.95f));

        if (pc != null && pc.Charging)
        {
            float bw = 360f * S, bh = 22f * S;
            Rect rr = new Rect(cx - bw * 0.5f, Screen.height - 128f * S, bw, bh);
            UIKit.Box(new Rect(rr.x - 3f * S, rr.y - 3f * S, rr.width + 6f * S, rr.height + 6f * S), A(UIKit.Gold, 0.9f));
            UIKit.Box(rr, new Color(0.03f, 0.04f, 0.08f, 0.95f));
            UIKit.Box(new Rect(rr.x + 2f * S, rr.y + 2f * S, (bw - 4f * S) * pc.ChargePower, bh - 4f * S),
                      Color.Lerp(new Color(0.2f, 0.9f, 0.3f), new Color(0.95f, 0.18f, 0.1f), pc.ChargePower));
            if (!pc.ChargingAerial && pc.Equipped == ItemType.Ball)
                UIKit.Box(new Rect(rr.x + bw * 0.5f - S, rr.y - 4f * S, 2f * S, bh + 8f * S), new Color(1f, 1f, 1f, 0.95f));
            string label = pc.Equipped == ItemType.Grenade ? "GRANADA" : (pc.ChargingAerial ? "TIRO AÉREO" : (pc.AimAssistActive ? "TIRO RASO · A LA MIRA" : "TIRO RASO"));
            UIKit.Text(new Rect(rr.x - 40f * S, rr.y - 32f * S, bw + 80f * S, 28f * S), label + "   " + Mathf.RoundToInt(pc.ChargePower * 100f) + "%", 18, Color.white);
        }

        if (m.State == MatchState.SetPiece && m.UserTakesSetPiece && m.Taker != null)
        {
            string what = m.SetType == SetPieceType.ThrowIn ? "SAQUE DE BANDA"
                        : m.SetType == SetPieceType.Corner ? "TIRO DE ESQUINA"
                        : m.SetType == SetPieceType.GoalKick ? "SAQUE DE META" : "SAQUE DE CENTRO";
            bool near = m.UserTakerLocked(m.Taker);
            float rem = m.UserSetPieceRemaining;
            Rect box = new Rect(cx - 250f * S, Screen.height * 0.68f, 500f * S, 96f * S);
            UIKit.Panel(box);
            UIKit.Text(new Rect(box.x, box.y + 14f * S, box.width, 30f * S), what + "  ·  " + Mathf.CeilToInt(rem) + " s", 22, UIKit.Gold);
            UIKit.Text(new Rect(box.x + 20f * S, box.y + 46f * S, box.width - 40f * S, 28f * S),
                       near ? "Apunta, mantén el clic y suelta  ·  si se acaba el tiempo saca la IA" : "Camina hasta el balón", 15, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter, false);
            UIKit.Box(new Rect(box.x + 24f * S, box.yMax - 20f * S, (box.width - 48f * S) * Mathf.Clamp01(rem / m.setPieceChoiceSeconds), 5f * S), UIKit.Gold);
        }
    }

    // ============================================================ combate: vida, inventario, puntos
    void DrawIcon(ItemType t, Rect r, float a)
    {
        float w = r.width, h = r.height;
        Color steel = new Color(0.78f, 0.8f, 0.85f, a), dark = new Color(0.25f, 0.27f, 0.3f, a), wood = new Color(0.65f, 0.4f, 0.2f, a);
        switch (t)
        {
            case ItemType.Ball:
                UIKit.Circle(new Rect(r.x + w * 0.18f, r.y + h * 0.18f, w * 0.64f, h * 0.64f), new Color(0.96f, 0.96f, 0.96f, a));
                UIKit.Circle(new Rect(r.x + w * 0.38f, r.y + h * 0.38f, w * 0.24f, h * 0.24f), new Color(0.1f, 0.1f, 0.1f, a));
                UIKit.Circle(new Rect(r.x + w * 0.24f, r.y + h * 0.28f, w * 0.12f, h * 0.12f), new Color(0.1f, 0.1f, 0.1f, a));
                UIKit.Circle(new Rect(r.x + w * 0.62f, r.y + h * 0.30f, w * 0.12f, h * 0.12f), new Color(0.1f, 0.1f, 0.1f, a));
                UIKit.Circle(new Rect(r.x + w * 0.44f, r.y + h * 0.66f, w * 0.12f, h * 0.12f), new Color(0.1f, 0.1f, 0.1f, a));
                break;
            case ItemType.Glock:
                UIKit.Box(new Rect(r.x + w * 0.20f, r.y + h * 0.36f, w * 0.60f, h * 0.17f), steel);
                UIKit.Box(new Rect(r.x + w * 0.22f, r.y + h * 0.53f, w * 0.16f, h * 0.27f), dark);
                UIKit.Box(new Rect(r.x + w * 0.38f, r.y + h * 0.53f, w * 0.12f, h * 0.06f), dark);
                break;
            case ItemType.AK47:
                UIKit.Box(new Rect(r.x + w * 0.08f, r.y + h * 0.40f, w * 0.84f, h * 0.10f), steel);
                UIKit.Box(new Rect(r.x + w * 0.06f, r.y + h * 0.37f, w * 0.22f, h * 0.20f), wood);
                UIKit.Box(new Rect(r.x + w * 0.52f, r.y + h * 0.37f, w * 0.20f, h * 0.10f), wood);
                UIKit.Box(new Rect(r.x + w * 0.42f, r.y + h * 0.50f, w * 0.10f, h * 0.28f), dark);
                UIKit.Box(new Rect(r.x + w * 0.30f, r.y + h * 0.50f, w * 0.07f, h * 0.18f), dark);
                break;
            default:
                UIKit.Circle(new Rect(r.x + w * 0.27f, r.y + h * 0.30f, w * 0.46f, h * 0.50f), new Color(0.4f, 0.65f, 0.3f, a));
                UIKit.Box(new Rect(r.x + w * 0.42f, r.y + h * 0.18f, w * 0.16f, h * 0.12f), steel);
                UIKit.Box(new Rect(r.x + w * 0.5f, r.y + h * 0.14f, w * 0.18f, h * 0.05f), steel);
                break;
        }
    }

    void DrawCombatHud(MatchManager m, PlayerController pc)
    {
        float S = UIKit.S;
        PlayerCombat cb = pc != null ? pc.Combat : null;
        float W = Screen.width, H = Screen.height, cx = W * 0.5f, cy = H * 0.5f;

        // daño recibido: viñeta roja en los bordes
        float dmgAge = Time.time - m.UserDamageTime;
        if (dmgAge < 0.7f)
        {
            float a = 0.5f * (1f - dmgAge / 0.7f), e = 80f * S;
            Color c = new Color(0.9f, 0.05f, 0.05f, a);
            UIKit.Box(new Rect(0f, 0f, W, e), c); UIKit.Box(new Rect(0f, H - e, W, e), c);
            UIKit.Box(new Rect(0f, 0f, e, H), c); UIKit.Box(new Rect(W - e, 0f, e, H), c);
        }

        // marcador de impacto (rojo si fue baja)
        float hitAge = Time.time - m.UserHitTime;
        if (hitAge < 0.22f)
        {
            Color hc = m.UserHitKill ? new Color(1f, 0.15f, 0.15f, 1f - hitAge / 0.22f) : new Color(1f, 1f, 1f, 1f - hitAge / 0.22f);
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(cx, cy));
            float len = 9f * S, th = 2.5f * S, gap = 10f * S;
            UIKit.Box(new Rect(cx - gap - len, cy - th * 0.5f, len, th), hc);
            UIKit.Box(new Rect(cx + gap, cy - th * 0.5f, len, th), hc);
            UIKit.Box(new Rect(cx - th * 0.5f, cy - gap - len, th, len), hc);
            UIKit.Box(new Rect(cx - th * 0.5f, cy + gap, th, len), hc);
            GUI.matrix = saved;
        }

        // puntos
        Rect pr = new Rect(16f * S, 14f * S, 250f * S, 96f * S);
        UIKit.Round(pr, new Color(0.22f, 0.25f, 0.42f));
        UIKit.Text(new Rect(pr.x + 20f * S, pr.y + 8f * S, 200f * S, 20f * S), "PUNTOS", 14, A(UIKit.Gold, 0.95f), TextAnchor.MiddleLeft);
        UIKit.Text(new Rect(pr.x + 20f * S, pr.y + 24f * S, 220f * S, 46f * S), m.UserPoints.ToString(), 38, Color.white, TextAnchor.MiddleLeft);
        UIKit.Text(new Rect(pr.x + 20f * S, pr.y + 68f * S, 220f * S, 22f * S), "BAJAS " + m.UserKills + "     MUERTES " + m.UserDeaths, 14, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleLeft);
        float pa = Time.time - m.PointsTextTime;
        if (pa < 1.6f)
            UIKit.Text(new Rect(cx + 50f * S, cy - 50f * S - pa * 30f * S, 320f * S, 36f * S), m.PointsText, 26, A(UIKit.Gold, 1f - pa / 1.6f), TextAnchor.MiddleLeft);

        if (cb != null)
        {
            // vida
            Rect hr = new Rect(24f * S, H - 70f * S, 320f * S, 32f * S);
            UIKit.Round(new Rect(hr.x - 6f * S, hr.y - 6f * S, hr.width + 12f * S, hr.height + 12f * S), new Color(0.2f, 0.22f, 0.38f));
            UIKit.Box(hr, new Color(0.02f, 0.03f, 0.06f, 0.95f));
            float hp = Mathf.Clamp01(cb.health / cb.maxHealth);
            UIKit.Box(new Rect(hr.x + 2f * S, hr.y + 2f * S, (hr.width - 4f * S) * hp, hr.height - 4f * S), Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.25f, 0.9f, 0.3f), hp));
            UIKit.Text(hr, Mathf.CeilToInt(cb.health) + " / " + Mathf.CeilToInt(cb.maxHealth), 16, Color.white);
            DrawHotbar(cb);
        }

        float na = Time.time - m.NoticeTime;
        if (na < 2.6f)
        {
            float a = Mathf.Clamp01(2.6f - na);
            Rect nr = new Rect(24f * S, H - 122f * S, 380f * S, 38f * S);
            UIKit.Round(nr, new Color(0.2f, 0.22f, 0.38f, a));
            UIKit.Text(nr, m.NoticeText, 16, A(Color.white, a));
        }

        if (cb != null && cb.IsDead)
        {
            UIKit.Box(new Rect(0f, 0f, W, H), new Color(0.35f, 0f, 0f, 0.4f));
            UIKit.Outline(new Rect(0f, cy - 100f * S, W, 110f * S), "ELIMINADO", 76, Color.white, new Color(0.3f, 0f, 0f));
            int left = Mathf.Max(0, Mathf.CeilToInt(cb.respawnAt - Time.time));
            UIKit.Text(new Rect(0f, cy + 10f * S, W, 44f * S), "Reapareces en " + left + " s", 30, UIKit.Gold);
        }
    }

    void DrawHotbar(PlayerCombat cb)
    {
        float S = UIKit.S, W = Screen.width, H = Screen.height;
        float slot = 78f * S, gap = 8f * S, total = 4f * slot + 3f * gap;
        float x0 = (W - total) * 0.5f, y = H - slot - 22f * S;
        for (int i = 0; i < 4; i++)
        {
            ItemType t = (ItemType)i;
            bool has = cb.HasItem(t), sel = cb.equipped == t;
            Rect r = new Rect(x0 + i * (slot + gap), y - (sel ? 12f * S : 0f), slot, slot);
            Color tint = sel ? new Color(1f, 0.85f, 0.35f) : (has ? new Color(0.28f, 0.32f, 0.5f) : new Color(0.12f, 0.13f, 0.2f));
            UIKit.Round(r, tint);
            Color rc = Items.Rarity(t);
            UIKit.Box(new Rect(r.x + 10f * S, r.yMax - 10f * S, r.width - 20f * S, 4f * S), A(rc, has ? 1f : 0.25f));
            DrawIcon(t, new Rect(r.x + 8f * S, r.y + 6f * S, r.width - 16f * S, r.height - 20f * S), has ? 1f : 0.3f);
            UIKit.Text(new Rect(r.x + 8f * S, r.y + 4f * S, 22f * S, 20f * S), (i + 1).ToString(), 14, A(Color.white, has ? 0.95f : 0.4f), TextAnchor.UpperLeft, true, false);
            string count = t == ItemType.Grenade ? (cb.grenades > 0 ? "x" + cb.grenades : "")
                         : (t == ItemType.Ball ? "" : (has ? cb.MagOf(t) + "/" + cb.ReserveOf(t) : ""));
            UIKit.Text(new Rect(r.x, r.y + 4f * S, r.width - 8f * S, 20f * S), count, 13, Color.white, TextAnchor.UpperRight, true, false);
        }
        ItemType e = cb.equipped;
        UIKit.Text(new Rect(x0, y - 44f * S, total, 24f * S), Items.Name(e), 17, UIKit.Gold);
        if (e == ItemType.Glock || e == ItemType.AK47)
        {
            UIKit.Outline(new Rect(x0 + total + 26f * S, y + 4f * S, 190f * S, 44f * S), cb.MagOf(e) + " / " + cb.ReserveOf(e), 32, Color.white, new Color(0f, 0f, 0f), TextAnchor.MiddleLeft);
            if (cb.IsReloading)
            {
                Rect br = new Rect(x0 + total + 28f * S, y + 52f * S, 150f * S, 8f * S);
                UIKit.Box(br, new Color(0f, 0f, 0f, 0.7f));
                UIKit.Box(new Rect(br.x, br.y, br.width * cb.ReloadProgress, br.height), UIKit.Gold);
                UIKit.Text(new Rect(br.x, br.y + 8f * S, 160f * S, 20f * S), "RECARGANDO", 13, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleLeft);
            }
        }
    }

    // ============================================================ etiquetas sobre los jugadores
    void DrawNameTags(MatchManager m)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        float S = UIKit.S;
        PlayerController pc = PlayerController.I;
        for (int t = 0; t < 2; t++)
            for (int i = 0; i < m.Teams[t].Count; i++)
            {
                PlayerAI p = m.Teams[t][i];
                if (p.IsDead || (pc != null && pc.Controlled == p)) continue;
                Vector3 sp = cam.WorldToScreenPoint(p.transform.position + Vector3.up * (p.playerHeight + 0.4f));
                if (sp.z < 0.5f || sp.z > 45f) continue;
                float x = sp.x, y = Screen.height - sp.y;
                float k = Mathf.Clamp(1.25f - sp.z / 45f, 0.45f, 1f);
                bool mate = m.UserTeam >= 0 && p.teamId == m.UserTeam;
                Color tc = UIKit.TeamColor(p.teamId) * 1.5f; tc.a = 1f;
                UIKit.Diamond(new Vector2(x, y), 12f * S * k, tc);
                UIKit.Diamond(new Vector2(x, y), 6f * S * k, Color.white);
                PlayerCombat c = p.Combat;
                if (c != null && !mate && sp.z < 28f)
                {
                    float w = 54f * S * k;
                    UIKit.Box(new Rect(x - w * 0.5f - S, y - 20f * S * k - S, w + 2f * S, 7f * S * k + 2f * S), new Color(0f, 0f, 0f, 0.8f));
                    UIKit.Box(new Rect(x - w * 0.5f, y - 20f * S * k, w * Mathf.Clamp01(c.health / c.maxHealth), 7f * S * k), new Color(0.9f, 0.2f, 0.15f));
                }
                if (sp.z < 18f)
                    UIKit.Text(new Rect(x - 40f * S, y - 40f * S * k, 80f * S, 18f * S), "#" + p.index, Mathf.RoundToInt(13f * k), new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleCenter, true, true);
            }
    }

    // ============================================================ minimapa
    void DrawMinimap(MatchManager m)
    {
        float S = UIKit.S;
        float mw = 220f * S, mh = mw * (m.HW / m.HL);
        Rect r = new Rect(Screen.width - mw - 22f * S, Screen.height - mh - 22f * S, mw, mh);
        UIKit.Round(new Rect(r.x - 9f * S, r.y - 9f * S, r.width + 18f * S, r.height + 18f * S), new Color(0.2f, 0.22f, 0.38f));
        UIKit.Box(r, new Color(0.07f, 0.3f, 0.12f, 0.97f));
        Color ln = new Color(1f, 1f, 1f, 0.55f);
        float th = 1.5f * S;
        UIKit.Box(new Rect(r.x, r.y, r.width, th), ln); UIKit.Box(new Rect(r.x, r.yMax - th, r.width, th), ln);
        UIKit.Box(new Rect(r.x, r.y, th, r.height), ln); UIKit.Box(new Rect(r.xMax - th, r.y, th, r.height), ln);
        UIKit.Box(new Rect(r.center.x - th * 0.5f, r.y, th, r.height), ln);
        float cd = r.height * 0.27f;
        UIKit.Circle(new Rect(r.center.x - cd, r.center.y - cd, cd * 2f, cd * 2f), ln);
        UIKit.Circle(new Rect(r.center.x - cd + th, r.center.y - cd + th, cd * 2f - th * 2f, cd * 2f - th * 2f), new Color(0.07f, 0.3f, 0.12f, 1f));
        float bw = r.width * 0.157f, bh = r.height * 0.59f;
        UIKit.Box(new Rect(r.x, r.center.y - bh * 0.5f, bw, th), ln); UIKit.Box(new Rect(r.x, r.center.y + bh * 0.5f, bw, th), ln); UIKit.Box(new Rect(r.x + bw, r.center.y - bh * 0.5f, th, bh), ln);
        UIKit.Box(new Rect(r.xMax - bw, r.center.y - bh * 0.5f, bw, th), ln); UIKit.Box(new Rect(r.xMax - bw, r.center.y + bh * 0.5f, bw, th), ln); UIKit.Box(new Rect(r.xMax - bw - th, r.center.y - bh * 0.5f, th, bh), ln);

        float flip = m.UserTeam == 1 ? -1f : 1f;
        Vector2 Map(Vector3 w)
        {
            return new Vector2(r.x + (0.5f + flip * w.x / (2f * m.HL)) * r.width, r.y + (0.5f - flip * w.z / (2f * m.HW)) * r.height);
        }

        for (int i = 0; i < WeaponPickup.All.Count; i++)
            UIKit.Diamond(Map(WeaponPickup.All[i].transform.position), 8f * S, Items.Rarity(WeaponPickup.All[i].type));
        PlayerController pc = PlayerController.I;
        for (int t = 0; t < 2; t++)
            for (int i = 0; i < m.Teams[t].Count; i++)
            {
                PlayerAI p = m.Teams[t][i];
                if (p.IsDead) continue;
                Vector2 mp = Map(p.transform.position);
                bool user = pc != null && pc.Controlled == p;
                if (user) UIKit.Circle(new Rect(mp.x - 8f * S, mp.y - 8f * S, 16f * S, 16f * S), UIKit.Gold);
                Color tc = UIKit.TeamColor(t) * 1.6f; tc.a = 1f;
                UIKit.Circle(new Rect(mp.x - 5f * S, mp.y - 5f * S, 10f * S, 10f * S), Color.white);
                UIKit.Circle(new Rect(mp.x - 4f * S, mp.y - 4f * S, 8f * S, 8f * S), tc);
            }
        Vector2 bp = Map(m.Ball.position);
        UIKit.Circle(new Rect(bp.x - 5f * S, bp.y - 5f * S, 10f * S, 10f * S), Color.black);
        UIKit.Circle(new Rect(bp.x - 3.5f * S, bp.y - 3.5f * S, 7f * S, 7f * S), Color.white);
    }

    // ============================================================ intro con lore y cuenta atrás
    class IntroCard { public float t0, t1; public string small, big, body; public int size; }
    static readonly IntroCard[] cards =
    {
        new IntroCard { t0 = 0.6f, t1 = 3.9f, small = "CALI · COLOMBIA", big = "LA SUCURSAL DEL CIELO", body = "Capital mundial de la salsa", size = 60 },
        new IntroCard { t0 = 4.2f, t1 = 9.2f, small = "", big = "¿QUÉ PASARÍA SI EL CLÁSICO NACIONAL–MILLONARIOS SE JUGARA EN CALI?", body = "", size = 46 },
        new IntroCard { t0 = 9.5f, t1 = 12.4f, small = "", big = "Dos hinchadas. Un solo balón.", body = "Y esa noche, algo más cayó del cielo…", size = 48 },
        new IntroCard { t0 = 12.6f, t1 = 14.7f, small = "", big = "ARMAS. GRANADAS. CERO REGLAS.", body = "Pero el gol sigue siendo sagrado.", size = 48 }
    };

    static string Typed(string s, float seconds, float cps)
    {
        int n = Mathf.Min(s.Length, Mathf.FloorToInt(Mathf.Max(0f, seconds) * cps));
        return s.Substring(0, n);
    }

    void DrawIntro(MatchManager m)
    {
        float S = UIKit.S, W = Screen.width, H = Screen.height;
        float t = m.IntroTime;
        UIKit.Gradient(new Rect(0f, 0f, W, H), new Color(1f, 0.45f, 0.2f, 0.14f), new Color(0.3f, 0.1f, 0.45f, 0.30f));
        float bar = Mathf.Lerp(0f, H * 0.13f, Ease(t / 1.2f));
        UIKit.Box(new Rect(0f, 0f, W, bar), Color.black);
        UIKit.Box(new Rect(0f, H - bar, W, bar), Color.black);
        UIKit.Box(new Rect(0f, bar - 2f * S, W, 2f * S), A(UIKit.Gold, 0.8f));
        UIKit.Box(new Rect(0f, H - bar, W, 2f * S), A(UIKit.Gold, 0.8f));

        for (int i = 0; i < cards.Length; i++)
        {
            IntroCard c = cards[i];
            if (t < c.t0 || t > c.t1) continue;
            float a = Mathf.Min(Mathf.Clamp01((t - c.t0) / 0.5f), Mathf.Clamp01((c.t1 - t) / 0.5f));
            float local = t - c.t0;
            float cy = H * 0.5f;
            if (c.small.Length > 0)
            {
                UIKit.Text(new Rect(0f, cy - 140f * S, W, 34f * S), c.small, 24, A(UIKit.Gold, a));
                UIKit.Divider(W * 0.5f, cy - 100f * S, 300f * S, A(UIKit.Gold, a));
            }
            float cps = 34f;
            UIKit.Text(new Rect(W * 0.1f, cy - 90f * S, W * 0.8f, 190f * S), Typed(c.big, local, cps), c.size, A(Color.white, a), TextAnchor.MiddleCenter, true, true, true);
            if (c.body.Length > 0)
                UIKit.Text(new Rect(W * 0.15f, cy + 100f * S, W * 0.7f, 70f * S), Typed(c.body, local - c.big.Length / cps - 0.25f, 40f), 28, A(new Color(1f, 0.92f, 0.75f), a), TextAnchor.UpperCenter, false, true, true);
        }

        if (m.CountdownNumber > 0)
        {
            float f = (t - MatchManager.CountdownStart) % 1f;
            float pop = Mathf.Lerp(1.6f, 1f, Ease(f * 3f));
            float a = 1f - Mathf.Clamp01((f - 0.7f) / 0.3f);
            float d = 330f * S * pop;
            UIKit.Circle(new Rect(W * 0.5f - d * 0.5f, H * 0.5f - d * 0.5f, d, d), new Color(1f, 1f, 1f, 0.10f * a));
            UIKit.Outline(new Rect(0f, H * 0.5f - 150f * S, W, 300f * S), m.CountdownNumber.ToString(), Mathf.RoundToInt(230f * pop), A(UIKit.Gold, a), new Color(0.12f, 0.06f, 0f, a));
        }
        if (t < MatchManager.CountdownStart && t > 1f)
            UIKit.Text(new Rect(W - 440f * S, H - bar * 0.5f - 14f * S, 420f * S, 28f * S), "Pulsa cualquier tecla para saltar",
                       15, new Color(1f, 1f, 1f, 0.5f + 0.4f * Mathf.Sin(Time.time * 3f)), TextAnchor.MiddleRight, false);
    }

    // ============================================================ final: podio, resultado y opciones
    void DrawFinished(MatchManager m)
    {
        float S = UIKit.S, W = Screen.width, H = Screen.height;
        float t = Time.time - m.FinishTime;

        // título y resultado
        float e1 = Ease(t / 0.8f);
        float ty = 14f * S - (1f - e1) * 120f * S;
        UIKit.Outline(new Rect(0f, ty, W, 80f * S), "FIN DEL PARTIDO", 66, Color.white, new Color(0.04f, 0.06f, 0.18f));
        Rect sr = new Rect(W * 0.5f - 250f * S, ty + 84f * S, 500f * S, 78f * S);
        UIKit.Round(sr, new Color(0.22f, 0.25f, 0.42f));
        UIKit.DrawCrest(new Rect(sr.x + 18f * S, sr.y + 8f * S, 62f * S, 62f * S), 0, 1f);
        UIKit.DrawCrest(new Rect(sr.xMax - 80f * S, sr.y + 8f * S, 62f * S, 62f * S), 1, 1f);
        UIKit.Text(sr, m.Score[0] + "  -  " + m.Score[1], 48, UIKit.Gold);

        string res; Color rc = Color.white;
        if (m.Score[0] == m.Score[1]) res = "EMPATE EN EL CLÁSICO";
        else
        {
            int win = m.Score[0] > m.Score[1] ? 0 : 1;
            if (m.UserTeam >= 0) { res = win == m.UserTeam ? "¡VICTORIA!" : "DERROTA"; rc = win == m.UserTeam ? UIKit.Gold : new Color(1f, 0.45f, 0.4f); }
            else res = "GANA " + UIKit.TeamName(win);
        }
        UIKit.Outline(new Rect(0f, sr.yMax + 4f * S, W, 50f * S), res, 34, rc, new Color(0f, 0f, 0f));

        // etiquetas sobre los tres mejores
        Camera cam = Camera.main;
        Podium p = Podium.I;
        if (cam != null && p != null)
        {
            Color[] pc = { new Color(1f, 0.8f, 0.25f), new Color(0.82f, 0.86f, 0.95f), new Color(0.85f, 0.55f, 0.3f) };
            for (int i = 0; i < p.Entries.Count; i++)
            {
                Podium.Entry en = p.Entries[i];
                Vector3 sp = cam.WorldToScreenPoint(en.ai.transform.position + Vector3.up * (en.ai.playerHeight + 0.55f));
                if (sp.z < 0.5f) continue;
                float a = Ease((t - 0.8f - i * 0.25f) / 0.5f);
                Rect tag = new Rect(sp.x - 120f * S, Screen.height - sp.y - 70f * S * a, 240f * S, 70f * S);
                UIKit.Round(tag, Color.Lerp(Color.white, pc[i], 0.85f));
                UIKit.Text(new Rect(tag.x, tag.y + 4f * S, tag.width, 22f * S), (en.place == 1 ? "MVP · " : "") + en.place + "° PUESTO", 14, new Color(0.15f, 0.1f, 0.02f), TextAnchor.MiddleCenter, true, false);
                UIKit.Text(new Rect(tag.x, tag.y + 22f * S, tag.width, 24f * S), en.label, 17, new Color(0.08f, 0.06f, 0.02f), TextAnchor.MiddleCenter, true, false);
                UIKit.Text(new Rect(tag.x, tag.y + 44f * S, tag.width, 22f * S), en.kills + " bajas · " + en.deaths + " muertes", 14, new Color(0.15f, 0.1f, 0.02f), TextAnchor.MiddleCenter, false, false);
            }
        }

        // opciones (aparecen unos segundos después)
        float e = Ease((t - 2.2f) / 0.6f);
        float bw = 250f * S, bh = 58f * S, gap = 16f * S;
        float x0 = (W - (bw * 4f + gap * 3f)) * 0.5f, y = H - 116f * S + (1f - e) * 170f * S;
        bool on = e > 0.85f;
        if (UIKit.Button(new Rect(x0, y, bw, bh), "REVANCHA", UIKit.Green, 22, on)) m.Rematch();
        if (UIKit.Button(new Rect(x0 + (bw + gap), y, bw, bh), "CAMBIAR DE EQUIPO", UIKit.Blue, 19, on)) m.ChangeTeam();
        if (UIKit.Button(new Rect(x0 + (bw + gap) * 2f, y, bw, bh), "MENÚ PRINCIPAL", UIKit.Purple, 20, on)) m.GoToMainMenu();
        if (UIKit.Button(new Rect(x0 + (bw + gap) * 3f, y, bw, bh), "SALIR", UIKit.Red, 22, on)) GameSettings.Quit();
    }

    // ============================================================ menús: pausa y selección de equipo dentro del partido
    void DrawPause(MatchManager m)
    {
        float S = UIKit.S;
        if (pausePage == 1) { if (MenuScreens.Settings()) pausePage = 0; return; }
        if (pausePage == 2) { if (MenuScreens.Controls()) pausePage = 0; return; }
        MenuScreens.Dim();
        Rect p = MenuScreens.Centered(460, 560);
        UIKit.Panel(p, "PAUSA");
        float bw = 340f * S, bh = 56f * S, gap = 16f * S, x = p.center.x - bw * 0.5f, y = p.y + 62f * S;
        if (UIKit.Button(new Rect(x, y, bw, bh), "CONTINUAR", UIKit.Green, 26)) m.SetPaused(false);
        if (UIKit.Button(new Rect(x, y + (bh + gap), bw, bh), "AJUSTES", UIKit.Blue)) pausePage = 1;
        if (UIKit.Button(new Rect(x, y + (bh + gap) * 2f, bw, bh), "CONTROLES", UIKit.Slate)) pausePage = 2;
        if (UIKit.Button(new Rect(x, y + (bh + gap) * 3f, bw, bh), "MENÚ PRINCIPAL", UIKit.Purple)) m.GoToMainMenu();
        if (UIKit.Button(new Rect(x, y + (bh + gap) * 4f, bw, bh), "SALIR", UIKit.Red)) GameSettings.Quit();
    }

    // Se usa cuando se juega la escena del partido sin pasar por la escena MainMenu, y al "Cambiar de equipo".
    void DrawFallbackMenu(MatchManager m)
    {
        float S = UIKit.S, H = Screen.height;
        if (menuPage == 1) { if (MenuScreens.Settings()) menuPage = 0; return; }
        if (menuPage == 2) { if (MenuScreens.Controls()) menuPage = 0; return; }
        if (menuPage == 3) { if (MenuScreens.Credits(ref creditsScroll)) menuPage = 0; return; }

        int r = MenuScreens.TeamSelect(false);
        if (r == 0 || r == 1) m.StartMatch(r);
        else if (r == 2) m.StartMatch(-1);

        float bw = 150f * S, bh = 44f * S, y = H - 66f * S;
        if (UIKit.Button(new Rect(24f * S, y, bw, bh), "AJUSTES", UIKit.Blue, 17)) menuPage = 1;
        if (UIKit.Button(new Rect(24f * S + bw + 12f * S, y, bw, bh), "CONTROLES", UIKit.Slate, 17)) menuPage = 2;
        if (UIKit.Button(new Rect(24f * S + (bw + 12f * S) * 2f, y, bw, bh), "CRÉDITOS", UIKit.Purple, 17)) { menuPage = 3; creditsScroll = 0f; }
        if (UIKit.Button(new Rect(24f * S + (bw + 12f * S) * 3f, y, bw, bh), "SALIR", UIKit.Red, 17)) GameSettings.Quit();
        if (GameSession.MenuAvailable && UIKit.Button(new Rect(Screen.width - 24f * S - 220f * S, y, 220f * S, bh), "MENÚ PRINCIPAL", UIKit.Gold, 17)) m.GoToMainMenu();
    }
}
