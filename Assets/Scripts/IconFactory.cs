using UnityEngine;

// Iconos del inventario dibujados por código con formas vectoriales suavizadas (contorno, degradado de metal, madera,
// ranuras, mira, cargador...). Se generan una vez y se guardan en caché.
public static class IconFactory
{
    const int N = 128;
    static readonly Texture2D[] icons = new Texture2D[4];

    public static Texture2D Get(ItemType t)
    {
        int i = (int)t;
        if (icons[i] == null) icons[i] = Build(t);
        return icons[i];
    }

    // ------------------------------------------------------------ lienzo
    class Canvas
    {
        public readonly Color[] px = new Color[N * N];

        static Color Over(Color d, Color s)
        {
            float a = s.a + d.a * (1f - s.a);
            if (a <= 0.0001f) return new Color(0f, 0f, 0f, 0f);
            return new Color((s.r * s.a + d.r * d.a * (1f - s.a)) / a, (s.g * s.a + d.g * d.a * (1f - s.a)) / a, (s.b * s.a + d.b * d.a * (1f - s.a)) / a, a);
        }

        public void Fill(System.Func<Vector2, float> sdf, System.Func<Vector2, Color> col, float outline)
        {
            Color oc = new Color(0.04f, 0.04f, 0.07f, 1f);
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = sdf(p);
                    if (d > outline + 1f) continue;
                    int i = y * N + x;
                    float ao = Mathf.Clamp01(outline + 0.5f - d);
                    if (ao > 0f && outline > 0f) px[i] = Over(px[i], new Color(oc.r, oc.g, oc.b, ao));
                    float af = Mathf.Clamp01(0.5f - d);
                    if (af > 0f) { Color c = col(p); c.a *= af; px[i] = Over(px[i], c); }
                }
        }
    }

    // ------------------------------------------------------------ distancias con signo (y hacia arriba)
    static float Disc(Vector2 p, Vector2 c, float r) { return (p - c).magnitude - r; }

    static float Ellipse(Vector2 p, Vector2 c, float rx, float ry)
    {
        Vector2 q = new Vector2((p.x - c.x) / rx, (p.y - c.y) / ry);
        return (q.magnitude - 1f) * Mathf.Min(rx, ry);
    }

    static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    static float Box(Vector2 p, Vector2 c, Vector2 half, float r, float angleDeg = 0f)
    {
        Vector2 q = p - c;
        if (angleDeg != 0f)
        {
            float a = -angleDeg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            q = new Vector2(q.x * ca - q.y * sa, q.x * sa + q.y * ca);
        }
        Vector2 d = new Vector2(Mathf.Abs(q.x) - half.x + r, Mathf.Abs(q.y) - half.y + r);
        return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - r;
    }

    static float Poly(Vector2 p, Vector2[] v)
    {
        float d = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
        {
            Vector2 a = v[i], b = v[j], e = b - a, w = p - a;
            float t = Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            d = Mathf.Min(d, (w - e * t).magnitude);
            if (((a.y > p.y) != (b.y > p.y)) && (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)) inside = !inside;
        }
        return inside ? -d : d;
    }

    static float Union(float a, float b) { return Mathf.Min(a, b); }

    // ------------------------------------------------------------ colores
    static System.Func<Vector2, Color> Grad(Color top, Color bot, float y0, float y1, float spec = 0f)
    {
        return p =>
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(y0, y1, p.y));
            Color c = Color.Lerp(bot, top, t);
            if (spec > 0f && t > 0.72f && t < 0.86f) c = Color.Lerp(c, Color.white, spec);       // brillo especular
            return c;
        };
    }

    static System.Func<Vector2, Color> Flat(Color c) { return p => c; }

    static readonly Color SteelT = new Color(0.82f, 0.85f, 0.92f), SteelB = new Color(0.34f, 0.37f, 0.46f);
    static readonly Color DarkT = new Color(0.34f, 0.35f, 0.42f), DarkB = new Color(0.09f, 0.09f, 0.12f);
    static readonly Color WoodT = new Color(0.80f, 0.50f, 0.24f), WoodB = new Color(0.40f, 0.20f, 0.07f);
    static readonly Color Line = new Color(0.05f, 0.05f, 0.08f, 0.9f);

    // ------------------------------------------------------------ iconos
    static Texture2D Build(ItemType t)
    {
        Canvas c = new Canvas();
        switch (t)
        {
            case ItemType.AK47: DrawAK(c); break;
            case ItemType.Glock: DrawGlock(c); break;
            case ItemType.Grenade: DrawGrenade(c); break;
            default: DrawBall(c); break;
        }
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(c.px);
        tex.Apply(true);
        return tex;
    }

    static void DrawAK(Canvas c)
    {
        // culata de madera
        Vector2[] stock = { new Vector2(6, 74), new Vector2(34, 72), new Vector2(36, 58), new Vector2(12, 50) };
        c.Fill(p => Poly(p, stock), Grad(WoodT, WoodB, 50, 74), 2f);
        // cargador curvo
        Vector2[] mag = { new Vector2(64, 60), new Vector2(76, 60), new Vector2(83, 46), new Vector2(88, 33), new Vector2(78, 31), new Vector2(72, 44), new Vector2(64, 52) };
        c.Fill(p => Poly(p, mag), Grad(DarkT, DarkB, 31, 60), 2f);
        for (int i = 0; i < 3; i++)
        {
            float yy = 54f - i * 7f, xx = 68f + (i * 5f);
            c.Fill(p => Capsule(p, new Vector2(xx, yy), new Vector2(xx + 7f, yy - 1.5f), 0.9f), Flat(Line), 0f);
        }
        // empuñadura
        Vector2[] grip = { new Vector2(47, 62), new Vector2(58, 62), new Vector2(54, 38), new Vector2(44, 41) };
        c.Fill(p => Poly(p, grip), Grad(DarkT, DarkB, 38, 62), 2f);
        // guardamonte
        c.Fill(p => Union(Capsule(p, new Vector2(58, 58), new Vector2(60, 52), 1.6f), Capsule(p, new Vector2(60, 52), new Vector2(72, 52), 1.6f)), Flat(new Color(0.2f, 0.2f, 0.26f)), 1.5f);
        // cañón y tubo de gases
        c.Fill(p => Capsule(p, new Vector2(100, 68), new Vector2(120, 68), 2.8f), Grad(SteelT, SteelB, 64, 72, 0.4f), 2f);
        c.Fill(p => Capsule(p, new Vector2(84, 79), new Vector2(112, 79), 2.2f), Grad(SteelT, SteelB, 76, 82), 1.8f);
        // cubierta del cajón, cajón y guardamanos
        c.Fill(p => Box(p, new Vector2(58, 75), new Vector2(21, 3.2f), 2f), Grad(SteelT, SteelB, 72, 78, 0.5f), 2f);
        c.Fill(p => Box(p, new Vector2(58, 66), new Vector2(22, 7.5f), 3f), Grad(DarkT, DarkB, 58, 74, 0.25f), 2f);
        c.Fill(p => Box(p, new Vector2(92, 66), new Vector2(15, 6.5f), 3f), Grad(WoodT, WoodB, 59, 73, 0.2f), 2f);
        c.Fill(p => Box(p, new Vector2(92, 73.5f), new Vector2(15, 2.2f), 1f), Grad(new Color(0.65f, 0.4f, 0.18f), WoodB, 71, 76), 1.5f);
        // freno de boca, punto de mira y alza
        c.Fill(p => Box(p, new Vector2(122, 68), new Vector2(2.6f, 5.5f), 1f), Grad(SteelT, SteelB, 62, 74), 1.8f);
        Vector2[] sight = { new Vector2(112, 71), new Vector2(116, 71), new Vector2(114, 81) };
        c.Fill(p => Poly(p, sight), Flat(new Color(0.3f, 0.3f, 0.36f)), 1.5f);
        c.Fill(p => Box(p, new Vector2(72, 80), new Vector2(3.5f, 2.2f), 1f), Flat(new Color(0.3f, 0.3f, 0.36f)), 1.5f);
        // ranura de expulsión
        c.Fill(p => Box(p, new Vector2(64, 68), new Vector2(7, 1.6f), 0.8f), Flat(Line), 0f);
    }

    static void DrawGlock(Canvas c)
    {
        // empuñadura
        Vector2[] grip = { new Vector2(34, 68), new Vector2(62, 68), new Vector2(55, 22), new Vector2(40, 24) };
        c.Fill(p => Poly(p, grip), Grad(DarkT, DarkB, 22, 68), 2f);
        for (int i = 0; i < 4; i++)
        {
            float yy = 56f - i * 8f;
            c.Fill(p => Capsule(p, new Vector2(42f + i * 0.3f, yy), new Vector2(54f - i * 0.6f, yy), 0.8f), Flat(Line), 0f);
        }
        c.Fill(p => Box(p, new Vector2(47.5f, 22), new Vector2(9.5f, 2.4f), 1f), Grad(SteelT, SteelB, 19, 25), 1.8f);
        // armazón y guardamonte
        Vector2[] frame = { new Vector2(32, 70), new Vector2(98, 70), new Vector2(96, 62), new Vector2(76, 60), new Vector2(64, 60), new Vector2(34, 62) };
        c.Fill(p => Poly(p, frame), Grad(DarkT, DarkB, 60, 70), 2f);
        c.Fill(p => Union(Capsule(p, new Vector2(64, 60), new Vector2(66, 50), 1.6f), Capsule(p, new Vector2(66, 50), new Vector2(82, 52), 1.6f)), Flat(new Color(0.14f, 0.14f, 0.18f)), 1.5f);
        c.Fill(p => Capsule(p, new Vector2(80, 52), new Vector2(84, 62), 1.6f), Flat(new Color(0.14f, 0.14f, 0.18f)), 1.5f);
        c.Fill(p => Box(p, new Vector2(72, 57), new Vector2(1.5f, 4f), 0.8f, -12f), Grad(SteelT, SteelB, 52, 62), 1.2f);
        // corredera
        c.Fill(p => Box(p, new Vector2(66, 79), new Vector2(40, 8.5f), 3.5f), Grad(SteelT, SteelB, 70, 88, 0.45f), 2f);
        for (int i = 0; i < 6; i++)
            c.Fill(p => Box(p, new Vector2(32f + i * 3.2f, 79), new Vector2(0.9f, 5f), 0.4f), Flat(Line), 0f);
        c.Fill(p => Box(p, new Vector2(86, 80), new Vector2(7f, 2.2f), 1f), Flat(Line), 0f);
        // boca del cañón, mira y alza
        c.Fill(p => Box(p, new Vector2(106, 79), new Vector2(3f, 6.5f), 1.2f), Grad(SteelT, SteelB, 72, 86), 1.8f);
        c.Fill(p => Box(p, new Vector2(104, 89), new Vector2(2.2f, 2f), 0.6f), Flat(new Color(0.9f, 0.9f, 0.95f)), 1.4f);
        c.Fill(p => Box(p, new Vector2(30, 89), new Vector2(3.5f, 2f), 0.6f), Flat(new Color(0.9f, 0.9f, 0.95f)), 1.4f);
    }

    static void DrawGrenade(Canvas c)
    {
        Vector2 center = new Vector2(62, 52);
        // cuerpo (piña) verde oliva con ranuras
        c.Fill(p => Ellipse(p, center, 34f, 38f), p =>
        {
            float t = Mathf.Clamp01((p.y - 14f) / 76f);
            Color col = Color.Lerp(new Color(0.12f, 0.2f, 0.07f), new Color(0.42f, 0.55f, 0.22f), t);
            float dx = (p.x - center.x) / 34f;
            col = Color.Lerp(col, Color.white, Mathf.Clamp01(0.55f - Mathf.Abs(dx + 0.38f) * 2.2f) * 0.35f);   // luz lateral
            return col;
        }, 2.2f);
        for (int i = 0; i < 4; i++)
        {
            float yy = 28f + i * 15f;
            c.Fill(p => Capsule(p, new Vector2(40f, yy), new Vector2(84f, yy), 0.9f), Flat(Line), 0f);
        }
        for (int i = -2; i <= 2; i++)
        {
            float xx = 62f + i * 11f;
            c.Fill(p => Capsule(p, new Vector2(xx, 20f), new Vector2(xx + i * 1.5f, 84f), 0.9f), Flat(Line), 0f);
        }
        // cabeza y palanca
        c.Fill(p => Box(p, new Vector2(62, 92), new Vector2(9, 6.5f), 2f), Grad(SteelT, SteelB, 86, 98, 0.4f), 2f);
        Vector2[] lever = { new Vector2(68, 97), new Vector2(96, 94), new Vector2(100, 76), new Vector2(93, 78), new Vector2(90, 88), new Vector2(70, 91) };
        c.Fill(p => Poly(p, lever), Grad(SteelT, SteelB, 76, 97), 2f);
        // anilla y seguro
        c.Fill(p => Mathf.Abs(Disc(p, new Vector2(44, 100), 10f)) - 1.6f, Grad(new Color(0.95f, 0.85f, 0.4f), new Color(0.6f, 0.45f, 0.1f), 90, 110), 1.2f);
        c.Fill(p => Capsule(p, new Vector2(52, 97), new Vector2(62, 94), 1.5f), Grad(SteelT, SteelB, 92, 99), 1.2f);
    }

    static void DrawBall(Canvas c)
    {
        Vector2 ctr = new Vector2(64, 64);
        float R = 48f;
        c.Fill(p => Disc(p, ctr, R), p =>
        {
            float d = (p - new Vector2(50, 80)).magnitude / R;
            return Color.Lerp(Color.white, new Color(0.62f, 0.64f, 0.72f), Mathf.Clamp01(d * 0.95f));
        }, 2.4f);
        Color dark = new Color(0.07f, 0.07f, 0.09f);
        // costuras
        for (int k = 0; k < 5; k++)
        {
            float a = (90f + k * 72f) * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 s0 = ctr + d * 14f, s1 = ctr + d * 31f;
            c.Fill(p => Mathf.Max(Capsule(p, s0, s1, 1.1f), Disc(p, ctr, R - 1f)), Flat(new Color(0.2f, 0.2f, 0.25f)), 0f);
        }
        // pentágono central y cinco parciales en el borde
        c.Fill(p => Mathf.Max(PentSdf(p, ctr, 15f, 90f), Disc(p, ctr, R - 1f)), Flat(dark), 0f);
        for (int k = 0; k < 5; k++)
        {
            float a = (90f + k * 72f + 36f) * Mathf.Deg2Rad;
            Vector2 pc = ctr + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 47f;
            float rot = 90f + k * 72f + 36f + 180f;
            c.Fill(p => Mathf.Max(PentSdf(p, pc, 15f, rot), Disc(p, ctr, R - 1f)), Flat(dark), 0f);
        }
        // brillo
        c.Fill(p => Ellipse(p, new Vector2(46, 86), 11f, 6f), Flat(new Color(1f, 1f, 1f, 0.55f)), 0f);
    }

    static float PentSdf(Vector2 p, Vector2 c, float r, float rotDeg)
    {
        Vector2[] v = new Vector2[5];
        for (int i = 0; i < 5; i++)
        {
            float a = (rotDeg + i * 72f) * Mathf.Deg2Rad;
            v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return Poly(p, v);
    }
}
