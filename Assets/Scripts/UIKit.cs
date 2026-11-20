using System.Collections.Generic;
using UnityEngine;

// Biblioteca visual compartida por TODOS los menús y el HUD. Identidad: marino profundo + dorado, esquinas biseladas,
// botones inclinados con brillo animado, marcas de cancha (círculo central) como marca de agua y rombos con propósito.
// Todo se genera por código (sin imágenes).  Si pones una fuente en Assets/Resources/Fonts/menu.ttf se usa sola.
public static class UIKit
{
    public static float S = 1f;
    public static readonly Color Gold = new Color(1f, 0.80f, 0.25f);
    public static readonly Color Blue = new Color(0.22f, 0.48f, 1f);
    public static readonly Color Green = new Color(0.2f, 0.78f, 0.35f);
    public static readonly Color Red = new Color(0.9f, 0.22f, 0.2f);
    public static readonly Color Purple = new Color(0.62f, 0.38f, 0.95f);
    public static readonly Color Slate = new Color(0.45f, 0.5f, 0.62f);
    public static readonly Color Navy = new Color(0.05f, 0.07f, 0.14f);

    public static Color TeamColor(int t) { return t == 0 ? new Color(0.05f, 0.25f, 0.8f) : new Color(0f, 0.6f, 0.2f); }
    public static string TeamName(int t) { return t == 0 ? "MILLONARIOS" : "NACIONAL"; }

    static bool ready;
    static Texture2D texBtn, texBtnHot, texPanel, texCircle, texRing;
    static GUIStyle btnStyle, btnHotStyle, panelStyle, textStyle;
    static Font menuFont;
    static readonly Dictionary<int, float> hover = new Dictionary<int, float>();
    static readonly Dictionary<int, bool> wasHot = new Dictionary<int, bool>();
    static readonly Dictionary<string, Texture2D> grads = new Dictionary<string, Texture2D>();
    static readonly Dictionary<int, Texture2D> slants = new Dictionary<int, Texture2D>();
    static readonly Texture2D[] crests = new Texture2D[2];
    static readonly bool[] crestReal = new bool[2];

    public static void Begin()
    {
        S = Screen.height / 720f;
        if (!ready) Init();
    }

    // ============================================================ texturas
    static Texture2D RoundTex(int size, int radius, int border, Color top, Color bottom, Color bTop, Color bBottom, Color inner)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f - half, fy = y + 0.5f - half;
                float qx = Mathf.Abs(fx) - (half - radius), qy = Mathf.Abs(fy) - (half - radius);
                float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float v = (y + 0.5f) / size;
                Color fill = Color.Lerp(bottom, top, v);
                Color bc = Color.Lerp(bBottom, bTop, v);
                float k = Mathf.Clamp01(d + border + 0.5f);
                Color c = Color.Lerp(fill, bc, k);
                if (inner.a > 0f)
                {
                    float dl = Mathf.Abs(d + border + 3f);
                    if (dl < 0.8f) c = Color.Lerp(c, new Color(inner.r, inner.g, inner.b, 1f), inner.a * (1f - dl / 0.8f));
                }
                c.a *= Mathf.Clamp01(0.5f - d);
                px[y * size + x] = c;
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    // panel con esquinas BISELADAS (cortadas a 45°), doble borde dorado y degradado marino
    static Texture2D ChamferTex(int size, int chamfer)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[size * size];
        float h = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = Mathf.Abs(x + 0.5f - h), fy = Mathf.Abs(y + 0.5f - h);
                float box = Mathf.Max(fx - h, fy - h);
                float cut = (fx + fy - (2f * h - chamfer)) * 0.7071f;
                float d = Mathf.Max(box, cut);                 // <0 dentro
                float v = (y + 0.5f) / size;
                Color fill = Color.Lerp(new Color(0.03f, 0.04f, 0.09f, 0.97f), new Color(0.10f, 0.13f, 0.25f, 0.97f), v);
                Color bc = Color.Lerp(new Color(0.70f, 0.48f, 0.10f), new Color(1f, 0.86f, 0.40f), v);
                float k = Mathf.Clamp01(d + 3.5f);
                Color c = Color.Lerp(fill, bc, k);
                float dl = Mathf.Abs(d + 8f);                    // segunda línea fina por dentro
                if (dl < 0.8f) c = Color.Lerp(c, new Color(1f, 0.9f, 0.55f, 1f), 0.35f * (1f - dl / 0.8f));
                c.a *= Mathf.Clamp01(0.5f - d);
                px[y * size + x] = c;
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    static Texture2D MakeCircle(int n)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * c / 1.5f));
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    static Texture2D MakeRing(int n)
    {
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) * c / 1.6f);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    static void Init()
    {
        ready = true;
        Color wTop = new Color(1f, 1f, 1f, 1f), wBot = new Color(0.55f, 0.58f, 0.66f, 1f);
        texBtn = RoundTex(64, 16, 3, wTop, wBot, Color.white, new Color(0.85f, 0.85f, 0.9f), new Color(1f, 1f, 1f, 0.22f));
        texBtnHot = RoundTex(64, 16, 3, new Color(1f, 1f, 1f, 1f), new Color(0.78f, 0.8f, 0.9f, 1f), Color.white, Color.white, new Color(1f, 1f, 1f, 0.35f));
        texPanel = ChamferTex(128, 30);
        texCircle = MakeCircle(64);
        texRing = MakeRing(128);

        btnStyle = new GUIStyle(); btnStyle.normal.background = texBtn; btnStyle.border = new RectOffset(18, 18, 18, 18);
        btnHotStyle = new GUIStyle(); btnHotStyle.normal.background = texBtnHot; btnHotStyle.border = new RectOffset(18, 18, 18, 18);
        panelStyle = new GUIStyle(); panelStyle.normal.background = texPanel; panelStyle.border = new RectOffset(34, 34, 34, 34);
        textStyle = new GUIStyle();
        textStyle.clipping = TextClipping.Overflow;
        menuFont = Resources.Load<Font>("Fonts/menu");
        if (menuFont != null) textStyle.font = menuFont;
    }

    public static Texture2D CircleTex { get { if (!ready) Init(); return texCircle; } }
    public static Texture2D RingTex { get { if (!ready) Init(); return texRing; } }

    public static Texture2D GradTex(Color top, Color bottom)
    {
        string key = ColorUtility.ToHtmlStringRGBA(top) + ColorUtility.ToHtmlStringRGBA(bottom);
        Texture2D t;
        if (grads.TryGetValue(key, out t)) return t;
        t = new Texture2D(1, 32, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int i = 0; i < 32; i++) t.SetPixel(0, i, Color.Lerp(bottom, top, i / 31f));
        t.Apply();
        grads[key] = t;
        return t;
    }

    // paralelogramo (botón inclinado) a tamaño exacto en píxeles. type 0 = tintable, 1 = dorado, 2 = oscuro
    static Texture2D SlantTex(int w, int h, int type, bool hot)
    {
        int key = ((w * 7919 + h) * 8) + type * 2 + (hot ? 1 : 0);
        Texture2D t;
        if (slants.TryGetValue(key, out t)) return t;
        t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        float skew = h * 0.28f, L = Mathf.Sqrt(h * h + skew * skew);
        Color top, bot, bor;
        if (type == 1) { top = hot ? new Color(1f, 0.94f, 0.55f) : new Color(1f, 0.86f, 0.36f); bot = hot ? new Color(1f, 0.7f, 0.15f) : new Color(0.93f, 0.55f, 0.08f); bor = new Color(1f, 0.97f, 0.75f); }
        else if (type == 2) { top = hot ? new Color(0.36f, 0.39f, 0.52f) : new Color(0.25f, 0.27f, 0.36f); bot = hot ? new Color(0.2f, 0.22f, 0.32f) : new Color(0.11f, 0.12f, 0.19f); bor = hot ? new Color(1f, 0.82f, 0.3f) : new Color(0.55f, 0.6f, 0.76f); }
        else { top = Color.white; bot = hot ? new Color(0.84f, 0.87f, 0.94f) : new Color(0.62f, 0.65f, 0.73f); bor = Color.white; }
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px_ = x + 0.5f, py_ = y + 0.5f;
                float dB = -py_;
                float dT = py_ - h;
                float dR = (h * (px_ - (w - skew)) - skew * py_) / L;
                float dL = (-h * px_ + skew * py_) / L;
                float d = Mathf.Max(Mathf.Max(dB, dT), Mathf.Max(dR, dL));
                float v = py_ / h;
                Color c = Color.Lerp(bot, top, v);
                if (v > 0.55f) c = Color.Lerp(c, Color.white, 0.10f * (v - 0.55f) / 0.45f);         // brillo superior
                float k = Mathf.Clamp01(d + 2.8f);
                c = Color.Lerp(c, bor, k);
                c.a *= Mathf.Clamp01(0.5f - d);
                px[y * w + x] = c;
            }
        t.SetPixels(px);
        t.Apply();
        slants[key] = t;
        return t;
    }

    // ============================================================ escudos de los equipos
    public static Texture2D Crest(int team)
    {
        if (crests[team] != null) return crests[team];
        string[] names = team == 0 ? new string[] { "millonarios", "millonarios_fc", "escudo_millonarios" }
                                   : new string[] { "nacional", "atletico_nacional", "escudo_nacional" };
        Texture2D t = null;
        foreach (string n in names) { t = Resources.Load<Texture2D>("Logos/" + n); if (t != null) break; }
        crestReal[team] = t != null;
        if (t == null) t = MakeCrestTex(TeamColor(team));
        crests[team] = t;
        return t;
    }

    public static bool CrestIsReal(int team) { Crest(team); return crestReal[team]; }

    static Texture2D MakeCrestTex(Color fill)
    {
        int n = 128;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                Color col;
                if (d > 1f) col = new Color(0f, 0f, 0f, 0f);
                else if (d > 0.90f) col = new Color(0.95f, 0.95f, 0.98f);
                else if (d > 0.82f) col = new Color(fill.r * 0.6f, fill.g * 0.6f, fill.b * 0.6f, 1f);
                else if (d > 0.77f) col = Color.white;
                else col = Color.Lerp(fill * 0.75f, fill * 1.15f, y / (float)n);
                col.a *= Mathf.Clamp01((1f - d) * c / 1.5f);
                px[y * n + x] = col;
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    public static void DrawCrest(Rect r, int team, float alpha)
    {
        Texture2D tex = Crest(team);
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha * old.a);
        float w = r.width;
        if (crestReal[team] && tex.height > 0) w = r.height * tex.width / (float)tex.height;
        GUI.DrawTexture(new Rect(r.center.x - w * 0.5f, r.y, w, r.height), tex, ScaleMode.ScaleToFit);
        GUI.color = old;
        if (!crestReal[team]) Text(r, team == 0 ? "M" : "N", Mathf.RoundToInt(r.height / S * 0.5f), new Color(1f, 1f, 1f, alpha));
    }

    // ============================================================ primitivas
    public static void Box(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    public static void Gradient(Rect r, Color top, Color bottom)
    {
        Color old = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(r, GradTex(top, bottom));
        GUI.color = old;
    }

    public static void Round(Rect r, Color tint)
    {
        if (!ready) Init();
        Color old = GUI.color;
        GUI.color = tint;
        GUI.Box(r, GUIContent.none, btnStyle);
        GUI.color = old;
    }

    public static void Circle(Rect r, Color c)
    {
        if (!ready) Init();
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, texCircle);
        GUI.color = old;
    }

    public static void Ring(Rect r, Color c)
    {
        if (!ready) Init();
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, texRing);
        GUI.color = old;
    }

    public static void Diamond(Vector2 c, float size, Color col)
    {
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, c);
        Box(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), col);
        GUI.matrix = saved;
    }

    public static void Corners(Rect r, Color c, float len, float th)
    {
        Box(new Rect(r.x, r.y, len, th), c); Box(new Rect(r.x, r.y, th, len), c);
        Box(new Rect(r.xMax - len, r.y, len, th), c); Box(new Rect(r.xMax - th, r.y, th, len), c);
        Box(new Rect(r.x, r.yMax - th, len, th), c); Box(new Rect(r.x, r.yMax - len, th, len), c);
        Box(new Rect(r.xMax - len, r.yMax - th, len, th), c); Box(new Rect(r.xMax - th, r.yMax - len, th, len), c);
    }

    // línea con cadena de rombos al centro y remates (separador ornamental)
    public static void Divider(float cx, float y, float width, Color c)
    {
        float half = width * 0.5f;
        Box(new Rect(cx - half, y, half - 26f * S, 2f * S), new Color(c.r, c.g, c.b, 0.75f * c.a));
        Box(new Rect(cx + 26f * S, y, half - 26f * S, 2f * S), new Color(c.r, c.g, c.b, 0.75f * c.a));
        Diamond(new Vector2(cx, y + S), 10f * S, c);
        Diamond(new Vector2(cx - 17f * S, y + S), 5f * S, c);
        Diamond(new Vector2(cx + 17f * S, y + S), 5f * S, c);
        Diamond(new Vector2(cx - half, y + S), 6f * S, c);
        Diamond(new Vector2(cx + half, y + S), 6f * S, c);
    }

    // adorno lateral de los botones:  ○──◆
    public static void Ornament(float x, float cy, bool leftSide, Color c)
    {
        float dir = leftSide ? 1f : -1f;
        float len = 24f * S;
        Box(new Rect(leftSide ? x + 9f * S : x - 9f * S - len, cy - S, len, 2f * S), c);
        Ring(new Rect(x - (leftSide ? 0f : 9f * S), cy - 5f * S, 10f * S, 10f * S), c);
        Diamond(new Vector2(x + dir * (9f * S + len + 3f * S), cy), 7f * S, c);
    }

    // ============================================================ texto
    public static void Text(Rect r, string text, int size, Color col, TextAnchor anchor = TextAnchor.MiddleCenter,
                            bool bold = true, bool shadow = true, bool wrap = false)
    {
        if (!ready) Init();
        textStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * S));
        textStyle.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        textStyle.alignment = anchor;
        textStyle.wordWrap = wrap;
        if (shadow)
        {
            textStyle.normal.textColor = new Color(0f, 0f, 0f, 0.75f * col.a);
            float o = Mathf.Max(1f, size * S * 0.07f);
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, textStyle);
        }
        textStyle.normal.textColor = col;
        GUI.Label(r, text, textStyle);
    }

    public static void Outline(Rect r, string text, int size, Color col, Color outline, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        if (!ready) Init();
        textStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * S));
        textStyle.fontStyle = FontStyle.Bold;
        textStyle.alignment = anchor;
        textStyle.wordWrap = false;
        float o = Mathf.Max(2f, size * S * 0.05f);
        textStyle.normal.textColor = outline;
        GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), text, textStyle);
        GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), text, textStyle);
        GUI.Label(new Rect(r.x, r.y - o, r.width, r.height), text, textStyle);
        GUI.Label(new Rect(r.x, r.y + o, r.width, r.height), text, textStyle);
        GUI.Label(new Rect(r.x + o, r.y + o * 1.6f, r.width, r.height), text, textStyle);
        textStyle.normal.textColor = col;
        GUI.Label(r, text, textStyle);
    }

    // ============================================================ paneles
    public static void Panel(Rect r, string title = null)
    {
        if (!ready) Init();
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.Box(new Rect(r.x + 6f * S, r.y + 9f * S, r.width, r.height), GUIContent.none, panelStyle);
        GUI.color = Color.white;
        GUI.Box(r, GUIContent.none, panelStyle);

        // marca de agua: círculo central y línea de medio campo, muy tenues
        Rect inner = new Rect(r.x + 14f * S, r.y + 14f * S, r.width - 28f * S, r.height - 28f * S);
        GUI.BeginGroup(inner);
        float d = Mathf.Min(inner.width, inner.height) * 0.78f;
        Ring(new Rect(inner.width * 0.5f - d * 0.5f, inner.height * 0.5f - d * 0.5f, d, d), new Color(1f, 1f, 1f, 0.045f));
        Box(new Rect(inner.width * 0.5f - S, 0f, 2f * S, inner.height), new Color(1f, 1f, 1f, 0.03f));
        GUI.EndGroup();

        // esquinas con ménsula y marcas de regla
        Color g = new Color(Gold.r, Gold.g, Gold.b, 0.95f);
        Rect e = new Rect(r.x + 8f * S, r.y + 8f * S, r.width - 16f * S, r.height - 16f * S);
        Corners(e, g, 22f * S, 3f * S);
        for (int i = 0; i < 3; i++)
        {
            float o = (34f + i * 9f) * S;
            Box(new Rect(e.x + o, e.y, 2f * S, 5f * S), new Color(g.r, g.g, g.b, 0.55f));
            Box(new Rect(e.xMax - o, e.y, 2f * S, 5f * S), new Color(g.r, g.g, g.b, 0.55f));
            Box(new Rect(e.x + o, e.yMax - 5f * S, 2f * S, 5f * S), new Color(g.r, g.g, g.b, 0.55f));
            Box(new Rect(e.xMax - o, e.yMax - 5f * S, 2f * S, 5f * S), new Color(g.r, g.g, g.b, 0.55f));
        }
        Diamond(new Vector2(e.x, e.center.y), 8f * S, g);
        Diamond(new Vector2(e.xMax, e.center.y), 8f * S, g);
        GUI.color = old;

        if (!string.IsNullOrEmpty(title))
        {
            Rect rb = new Rect(r.center.x - 200f * S, r.y - 24f * S, 400f * S, 48f * S);
            GUI.color = Color.white;
            GUI.DrawTexture(rb, SlantTex(Mathf.RoundToInt(rb.width), Mathf.RoundToInt(rb.height), 1, false));
            GUI.color = old;
            Text(rb, title, 24, new Color(0.14f, 0.08f, 0.02f), TextAnchor.MiddleCenter, true, false);
            Diamond(new Vector2(rb.x - 14f * S, rb.center.y), 9f * S, Gold);
            Diamond(new Vector2(rb.xMax + 14f * S, rb.center.y), 9f * S, Gold);
        }
    }

    // ============================================================ botones
    // cinta inclinada (type 1 = dorada, 2 = oscura, 0 = blanca para teñir con GUI.color)
    public static void Slant(Rect r, int type)
    {
        if (!ready) Init();
        GUI.DrawTexture(r, SlantTex(Mathf.Max(16, Mathf.RoundToInt(r.width)), Mathf.Max(16, Mathf.RoundToInt(r.height)), type, false));
    }

    public static float HoverAnim(Rect r, int salt, out bool hot)
    {
        int id = salt * 7919 + (int)(r.x * 3f + r.y * 11f);
        Event e = Event.current;
        hot = r.Contains(e.mousePosition);
        float a;
        hover.TryGetValue(id, out a);
        if (e.type == EventType.Repaint)
        {
            a = Mathf.MoveTowards(a, hot ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            hover[id] = a;
            bool was;
            wasHot.TryGetValue(id, out was);
            if (hot && !was && GameAudio.I != null) GameAudio.I.UiHover();
            wasHot[id] = hot;
        }
        return a;
    }

    // brillo diagonal que recorre el botón mientras el mouse está encima
    static void Shine(Rect r, float a)
    {
        if (a < 0.02f) return;
        GUI.BeginGroup(r);
        float cyc = (Time.unscaledTime * 1.1f) % 1.6f;
        float x = Mathf.Lerp(-40f * S, r.width + 40f * S, Mathf.Clamp01(cyc / 1.1f));
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(-20f, new Vector2(x, r.height * 0.5f));
        Box(new Rect(x - 14f * S, -r.height, 28f * S, r.height * 3f), new Color(1f, 1f, 1f, 0.20f * a));
        Box(new Rect(x + 22f * S, -r.height, 8f * S, r.height * 3f), new Color(1f, 1f, 1f, 0.10f * a));
        GUI.matrix = saved;
        GUI.EndGroup();
    }

    static bool ButtonCore(Rect r, string label, int type, Color tint, Color textCol, int size, bool enabled, bool ornaments, Color ornCol)
    {
        if (!ready) Init();
        bool hot;
        float a = HoverAnim(r, label.GetHashCode() ^ (type * 977), out hot);
        if (!enabled) { a = 0f; hot = false; }
        bool down = hot && Input.GetMouseButton(0);

        Matrix4x4 saved = GUI.matrix;
        float sc = (1f + 0.045f * a) * (down ? 0.97f : 1f);
        GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), r.center);
        Color old = GUI.color;
        int pw = Mathf.Max(16, Mathf.RoundToInt(r.width)), ph = Mathf.Max(16, Mathf.RoundToInt(r.height));

        if (a > 0.01f)       // resplandor
        {
            GUI.color = new Color(tint.r, tint.g, tint.b, 0.40f * a);
            GUI.DrawTexture(new Rect(r.x - 7f * S, r.y - 5f * S, r.width + 14f * S, r.height + 10f * S), SlantTex(Mathf.RoundToInt(r.width + 14f * S), Mathf.RoundToInt(r.height + 10f * S), 0, false));
        }
        GUI.color = new Color(0f, 0f, 0f, 0.35f);          // sombra
        GUI.DrawTexture(new Rect(r.x + 3f * S, r.y + 5f * S, r.width, r.height), SlantTex(pw, ph, 0, false));
        Color body = enabled ? tint : new Color(0.35f, 0.35f, 0.38f);
        GUI.color = type == 0 ? body : Color.white;
        if (!enabled && type != 0) GUI.color = new Color(0.45f, 0.45f, 0.48f, 1f);
        GUI.DrawTexture(r, SlantTex(pw, ph, type, a > 0.5f));
        GUI.color = old;
        Shine(r, a);
        Text(r, label, size, enabled ? textCol : new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter, true, type != 1);
        if (ornaments)
        {
            Ornament(r.x + 24f * S, r.center.y, true, ornCol);
            Ornament(r.xMax - 24f * S, r.center.y, false, ornCol);
        }
        else if (a > 0.02f)
        {
            Diamond(new Vector2(r.x - 14f * S * a, r.center.y), 9f * S, Gold);
            Diamond(new Vector2(r.xMax + 14f * S * a, r.center.y), 9f * S, Gold);
        }
        bool clicked = enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
        GUI.matrix = saved;
        if (clicked && GameAudio.I != null) GameAudio.I.UiClick();
        return clicked;
    }

    // botón general (inclinado, tintado con el color de acento)
    public static bool Button(Rect r, string label, Color accent, int size = 24, bool enabled = true)
    {
        return ButtonCore(r, label, 0, accent, Color.white, size, enabled, false, Color.white);
    }

    // botones del menú principal: primario dorado con adornos / secundario oscuro con adornos
    public static bool MenuButton(Rect r, string label, bool primary, int size = 26, bool enabled = true)
    {
        if (primary) return ButtonCore(r, label, 1, Gold, new Color(0.17f, 0.09f, 0.02f), size, enabled, true, new Color(0.17f, 0.09f, 0.02f, 0.85f));
        return ButtonCore(r, label, 2, Slate, Color.white, size, enabled, true, new Color(Gold.r, Gold.g, Gold.b, 0.9f));
    }

    // ============================================================ slider (rombo)
    public static float Slider(Rect r, string label, float value, float min, float max, string valueText, Color accent)
    {
        Text(new Rect(r.x, r.y, r.width * 0.65f, 24f * S), label, 18, Color.white, TextAnchor.MiddleLeft);
        Text(new Rect(r.x + r.width * 0.6f, r.y, r.width * 0.4f, 24f * S), valueText, 18, Gold, TextAnchor.MiddleRight);
        Rect tr = new Rect(r.x, r.y + 34f * S, r.width, 10f * S);
        float t = Mathf.InverseLerp(min, max, value);
        Box(new Rect(tr.x - 2f * S, tr.y - 2f * S, tr.width + 4f * S, tr.height + 4f * S), new Color(Gold.r, Gold.g, Gold.b, 0.35f));
        Box(tr, new Color(0.03f, 0.04f, 0.08f, 1f));
        Box(new Rect(tr.x, tr.y, tr.width * t, tr.height), accent);
        Box(new Rect(tr.x, tr.y, tr.width * t, tr.height * 0.4f), new Color(1f, 1f, 1f, 0.22f));
        for (int i = 1; i < 10; i++) Box(new Rect(tr.x + tr.width * i / 10f - S * 0.5f, tr.yMax, S, 3f * S), new Color(1f, 1f, 1f, 0.25f));
        float kx = tr.x + tr.width * t;
        Diamond(new Vector2(kx, tr.center.y), 24f * S, new Color(0f, 0f, 0f, 0.5f));
        Diamond(new Vector2(kx, tr.center.y), 20f * S, Color.white);
        Diamond(new Vector2(kx, tr.center.y), 11f * S, accent);
        Rect hit = new Rect(r.x - 12f * S, tr.y - 12f * S, r.width + 24f * S, 34f * S);
        return GUI.HorizontalSlider(hit, value, min, max, GUIStyle.none, GUIStyle.none);
    }
}
