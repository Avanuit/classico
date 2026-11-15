using System.Collections.Generic;
using UnityEngine;

// Biblioteca visual compartida por TODOS los menús y el HUD: marcos dorados, botones animados con brillo,
// sliders, textos con sombra, degradados y escudos de los equipos. Todo generado por código (sin imágenes).
public static class UIKit
{
    public static float S = 1f;
    public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
    public static readonly Color Blue = new Color(0.22f, 0.48f, 1f);
    public static readonly Color Green = new Color(0.2f, 0.78f, 0.35f);
    public static readonly Color Red = new Color(0.9f, 0.22f, 0.2f);
    public static readonly Color Purple = new Color(0.62f, 0.38f, 0.95f);
    public static readonly Color Slate = new Color(0.45f, 0.5f, 0.62f);

    public static Color TeamColor(int t) { return t == 0 ? new Color(0.05f, 0.25f, 0.8f) : new Color(0f, 0.6f, 0.2f); }
    public static string TeamName(int t) { return t == 0 ? "MILLONARIOS" : "NACIONAL"; }

    static bool ready;
    static Texture2D texBtn, texBtnHot, texPanel, texCircle;
    static GUIStyle btnStyle, btnHotStyle, panelStyle, textStyle;
    static readonly Dictionary<int, float> hover = new Dictionary<int, float>();
    static readonly Dictionary<int, bool> wasHot = new Dictionary<int, bool>();
    static readonly Dictionary<string, Texture2D> grads = new Dictionary<string, Texture2D>();
    static readonly Texture2D[] crests = new Texture2D[2];
    static readonly bool[] crestReal = new bool[2];

    public static void Begin()
    {
        S = Screen.height / 720f;
        if (!ready) Init();
    }

    // ------------------------------------------------------------ texturas
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
                float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;   // <0 dentro
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

    static void Init()
    {
        ready = true;
        Color wTop = new Color(1f, 1f, 1f, 1f), wBot = new Color(0.55f, 0.58f, 0.66f, 1f);
        texBtn = RoundTex(64, 16, 3, wTop, wBot, Color.white, new Color(0.85f, 0.85f, 0.9f), new Color(1f, 1f, 1f, 0.22f));
        texBtnHot = RoundTex(64, 16, 3, new Color(1f, 1f, 1f, 1f), new Color(0.78f, 0.8f, 0.9f, 1f), Color.white, Color.white, new Color(1f, 1f, 1f, 0.35f));
        texPanel = RoundTex(96, 22, 3, new Color(0.10f, 0.13f, 0.24f, 0.96f), new Color(0.04f, 0.05f, 0.10f, 0.97f),
                            new Color(1f, 0.85f, 0.35f), new Color(0.72f, 0.5f, 0.12f), new Color(1f, 0.9f, 0.5f, 0.35f));
        texCircle = MakeCircle(64);

        btnStyle = new GUIStyle(); btnStyle.normal.background = texBtn; btnStyle.border = new RectOffset(18, 18, 18, 18);
        btnHotStyle = new GUIStyle(); btnHotStyle.normal.background = texBtnHot; btnHotStyle.border = new RectOffset(18, 18, 18, 18);
        panelStyle = new GUIStyle(); panelStyle.normal.background = texPanel; panelStyle.border = new RectOffset(26, 26, 26, 26);
        textStyle = new GUIStyle();
        textStyle.clipping = TextClipping.Overflow;
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

    public static Texture2D CircleTex { get { if (!ready) Init(); return texCircle; } }

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

    // ------------------------------------------------------------ escudos de los equipos
    // Si existe Assets/Resources/Logos/millonarios.png / nacional.png se usan (logos oficiales que tú pongas).
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

    // ------------------------------------------------------------ primitivas
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

    // caja redondeada con borde brillante, tintada con el color dado
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

    public static void Divider(float cx, float y, float width, Color c)
    {
        Box(new Rect(cx - width * 0.5f, y, width * 0.42f, 2f * S), new Color(c.r, c.g, c.b, 0.7f));
        Box(new Rect(cx + width * 0.08f, y, width * 0.42f, 2f * S), new Color(c.r, c.g, c.b, 0.7f));
        Diamond(new Vector2(cx, y + S), 9f * S, c);
    }

    // ------------------------------------------------------------ texto
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

    // ------------------------------------------------------------ paneles con marco
    public static void Panel(Rect r, string title = null)
    {
        if (!ready) Init();
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.Box(new Rect(r.x + 6f * S, r.y + 9f * S, r.width, r.height), GUIContent.none, panelStyle);
        GUI.color = Color.white;
        GUI.Box(r, GUIContent.none, panelStyle);
        Rect inner = new Rect(r.x + 12f * S, r.y + 12f * S, r.width - 24f * S, r.height - 24f * S);
        Corners(inner, new Color(Gold.r, Gold.g, Gold.b, 0.85f), 16f * S, 2f * S);
        Diamond(new Vector2(r.x + 9f * S, r.y + 9f * S), 8f * S, Gold);
        Diamond(new Vector2(r.xMax - 9f * S, r.y + 9f * S), 8f * S, Gold);
        Diamond(new Vector2(r.x + 9f * S, r.yMax - 9f * S), 8f * S, Gold);
        Diamond(new Vector2(r.xMax - 9f * S, r.yMax - 9f * S), 8f * S, Gold);
        GUI.color = old;
        if (!string.IsNullOrEmpty(title))
        {
            Rect rb = new Rect(r.center.x - 190f * S, r.y - 22f * S, 380f * S, 44f * S);
            GUI.color = new Color(1f, 0.82f, 0.25f, 1f);
            GUI.Box(rb, GUIContent.none, btnStyle);
            GUI.color = old;
            Text(rb, title, 24, new Color(0.12f, 0.08f, 0.02f), TextAnchor.MiddleCenter, true, false);
        }
    }

    // ------------------------------------------------------------ botón animado (hover con brillo y escala)
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

    public static bool Button(Rect r, string label, Color accent, int size = 24, bool enabled = true)
    {
        if (!ready) Init();
        bool hot;
        float a = HoverAnim(r, label.GetHashCode(), out hot);
        if (!enabled) { a = 0f; hot = false; }
        bool down = hot && Input.GetMouseButton(0);

        Matrix4x4 saved = GUI.matrix;
        float sc = (1f + 0.07f * a) * (down ? 0.965f : 1f);
        GUIUtility.ScaleAroundPivot(new Vector2(sc, sc), r.center);
        Color old = GUI.color;

        if (a > 0.01f)   // resplandor
        {
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.55f * a);
            GUI.Box(new Rect(r.x - 6f * S, r.y - 6f * S, r.width + 12f * S, r.height + 12f * S), GUIContent.none, btnStyle);
        }
        Color body = enabled ? accent : new Color(0.35f, 0.35f, 0.38f);
        GUI.color = new Color(body.r, body.g, body.b, 1f);
        GUI.Box(r, GUIContent.none, a > 0.5f ? btnHotStyle : btnStyle);
        GUI.color = new Color(1f, 1f, 1f, 0.16f);     // brillo superior
        GUI.DrawTexture(new Rect(r.x + 8f * S, r.y + 4f * S, r.width - 16f * S, r.height * 0.38f), Texture2D.whiteTexture);
        GUI.color = old;
        Text(r, label, size, enabled ? Color.white : new Color(1f, 1f, 1f, 0.5f));
        if (a > 0.02f)
        {
            Diamond(new Vector2(r.x - 14f * S * a, r.center.y), 9f * S, Gold);
            Diamond(new Vector2(r.xMax + 14f * S * a, r.center.y), 9f * S, Gold);
        }
        bool clicked = enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
        GUI.matrix = saved;
        if (clicked && GameAudio.I != null) GameAudio.I.UiClick();
        return clicked;
    }

    // ------------------------------------------------------------ slider con marco
    public static float Slider(Rect r, string label, float value, float min, float max, string valueText, Color accent)
    {
        Text(new Rect(r.x, r.y, r.width * 0.65f, 24f * S), label, 18, Color.white, TextAnchor.MiddleLeft);
        Text(new Rect(r.x + r.width * 0.6f, r.y, r.width * 0.4f, 24f * S), valueText, 18, Gold, TextAnchor.MiddleRight);
        Rect tr = new Rect(r.x, r.y + 34f * S, r.width, 10f * S);
        float t = Mathf.InverseLerp(min, max, value);
        Box(new Rect(tr.x - 2f * S, tr.y - 2f * S, tr.width + 4f * S, tr.height + 4f * S), new Color(1f, 1f, 1f, 0.18f));
        Box(tr, new Color(0.03f, 0.04f, 0.08f, 1f));
        Box(new Rect(tr.x, tr.y, tr.width * t, tr.height), accent);
        float kx = tr.x + tr.width * t;
        Circle(new Rect(kx - 13f * S, tr.center.y - 13f * S, 26f * S, 26f * S), Color.white);
        Circle(new Rect(kx - 8f * S, tr.center.y - 8f * S, 16f * S, 16f * S), accent);
        Rect hit = new Rect(r.x - 12f * S, tr.y - 12f * S, r.width + 24f * S, 34f * S);
        return GUI.HorizontalSlider(hit, value, min, max, GUIStyle.none, GUIStyle.none);
    }
}
