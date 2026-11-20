using System.Collections.Generic;
using UnityEngine;

// ===========================================================================================================
//  MODELOS LOW-POLY DE LAS ARMAS (hechos por código, sin archivos externos)
//  AK-47: cargador curvo de baquelita, culata y guardamanos de madera, mira trasera, alza delantera con orejas,
//         tubo de gases, baquetón, freno de boca, palanca selectora y manija de carga.
//  Glock 17: corredera con estrías, ventana de expulsión, miras con puntos blancos, riel, guardamonte cuadrado,
//         empuñadura inclinada con muescas para los dedos y textura, base del cargador.
//  Granada (tipo "piña" Mk2): cuerpo segmentado verde oliva, espoleta, cuchara y anilla con pasador.
//  Cada arma es UNA malla (una sub-malla por material), sombreado plano estilo low-poly. Se generan una sola vez.
//  Ejes: +Z = hacia donde apunta el cañón, +Y = arriba. Unidades en metros (tamaño real).
// ===========================================================================================================
public static class WeaponMeshes
{
    // ---------------------------------------------------------------- materiales
    enum M { Steel, Blued, Wood, Bakelite, Polymer, Slide, Olive, Bright, Dot, Skin, Sleeve, Count }

    static Material[] mats;
    static Material MakeMat(Color c, float metal, float smooth, bool emissive = false)
    {
        Shader sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Diffuse");
        Material m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (emissive && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 0.8f);
        }
        return m;
    }

    static Material[] Mats()
    {
        if (mats != null && mats[0] != null) return mats;
        mats = new Material[(int)M.Count];
        mats[(int)M.Steel] = MakeMat(new Color(0.13f, 0.13f, 0.14f), 0.75f, 0.5f);
        mats[(int)M.Blued] = MakeMat(new Color(0.21f, 0.22f, 0.25f), 0.7f, 0.45f);
        mats[(int)M.Wood] = MakeMat(new Color(0.46f, 0.22f, 0.08f), 0f, 0.5f);
        mats[(int)M.Bakelite] = MakeMat(new Color(0.40f, 0.12f, 0.05f), 0f, 0.6f);
        mats[(int)M.Polymer] = MakeMat(new Color(0.07f, 0.07f, 0.075f), 0f, 0.25f);
        mats[(int)M.Slide] = MakeMat(new Color(0.11f, 0.11f, 0.12f), 0.6f, 0.55f);
        mats[(int)M.Olive] = MakeMat(new Color(0.28f, 0.32f, 0.15f), 0.1f, 0.3f);
        mats[(int)M.Bright] = MakeMat(new Color(0.62f, 0.62f, 0.64f), 0.9f, 0.65f);
        mats[(int)M.Dot] = MakeMat(new Color(0.95f, 0.95f, 0.88f), 0f, 0.4f, true);
        mats[(int)M.Skin] = MakeMat(new Color(0.80f, 0.60f, 0.47f), 0f, 0.25f);
        mats[(int)M.Sleeve] = MakeMat(Color.white, 0f, 0.2f);
        return mats;
    }

    // ---------------------------------------------------------------- constructor de mallas (caras planas)
    class Kit
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<int>[] tris = new List<int>[(int)M.Count];
        public Kit() { for (int i = 0; i < tris.Length; i++) tris[i] = new List<int>(); }

        void Tri(Vector3 a, Vector3 b, Vector3 c, M m)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(nn); n.Add(nn); n.Add(nn);
            tris[(int)m].Add(i); tris[(int)m].Add(i + 1); tris[(int)m].Add(i + 2);
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, M m) { Tri(a, b, c, m); Tri(a, c, d, m); }

        // caja deformable: 8 esquinas (atrás: 0-3, delante: 4-7), orden  abajo-izq, arriba-izq, arriba-der, abajo-der
        void Hexa(Vector3[] p, M m)
        {
            Quad(p[0], p[1], p[2], p[3], m);        // atrás  (-Z)
            Quad(p[7], p[6], p[5], p[4], m);        // delante (+Z)
            Quad(p[4], p[5], p[1], p[0], m);        // izquierda (-X)
            Quad(p[3], p[2], p[6], p[7], m);        // derecha (+X)
            Quad(p[1], p[5], p[6], p[2], m);        // arriba
            Quad(p[4], p[0], p[3], p[7], m);        // abajo
        }

        public void Box(Vector3 c, Vector3 s, Vector3 euler, M m)
        {
            Taper(c, new Vector2(s.x, s.y), new Vector2(s.x, s.y), s.z, 0f, euler, m);
        }
        public void Box(Vector3 c, Vector3 s, M m) { Box(c, s, Vector3.zero, m); }

        // prisma que cambia de sección a lo largo de Z (culatas, guardamanos). dropFront: desplazamiento Y del frente
        public void Taper(Vector3 c, Vector2 back, Vector2 front, float len, float dropFront, Vector3 euler, M m)
        {
            Quaternion r = Quaternion.Euler(euler);
            float z0 = -len * 0.5f, z1 = len * 0.5f;
            Vector3[] p =
            {
                new Vector3(-back.x * 0.5f, -back.y * 0.5f, z0), new Vector3(-back.x * 0.5f, back.y * 0.5f, z0),
                new Vector3(back.x * 0.5f, back.y * 0.5f, z0), new Vector3(back.x * 0.5f, -back.y * 0.5f, z0),
                new Vector3(-front.x * 0.5f, -front.y * 0.5f + dropFront, z1), new Vector3(-front.x * 0.5f, front.y * 0.5f + dropFront, z1),
                new Vector3(front.x * 0.5f, front.y * 0.5f + dropFront, z1), new Vector3(front.x * 0.5f, -front.y * 0.5f + dropFront, z1)
            };
            for (int i = 0; i < 8; i++) p[i] = c + r * p[i];
            Hexa(p, m);
        }

        // cilindro a lo largo del Z local
        public void Cyl(Vector3 c, Vector3 euler, float radius, float len, int sides, M m, bool caps = true)
        {
            Quaternion r = Quaternion.Euler(euler);
            Vector3 a = c + r * new Vector3(0f, 0f, -len * 0.5f), b = c + r * new Vector3(0f, 0f, len * 0.5f);
            for (int i = 0; i < sides; i++)
            {
                float t0 = i * Mathf.PI * 2f / sides, t1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 o0 = r * new Vector3(Mathf.Cos(t0) * radius, Mathf.Sin(t0) * radius, 0f);
                Vector3 o1 = r * new Vector3(Mathf.Cos(t1) * radius, Mathf.Sin(t1) * radius, 0f);
                Quad(a + o0, a + o1, b + o1, b + o0, m);
                if (caps) { Tri(b, b + o0, b + o1, m); Tri(a, a + o1, a + o0, m); }
            }
        }
        public void CylY(Vector3 c, float radius, float len, int sides, M m) { Cyl(c, new Vector3(-90f, 0f, 0f), radius, len, sides, m); }

        // anillo (anilla de la granada) en el plano XY local
        public void Torus(Vector3 c, Vector3 euler, float R, float tube, int seg, int sides, M m)
        {
            Quaternion r = Quaternion.Euler(euler);
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < sides; j++)
                {
                    Vector3 p00 = TP(i, j, seg, sides, R, tube), p10 = TP(i + 1, j, seg, sides, R, tube);
                    Vector3 p11 = TP(i + 1, j + 1, seg, sides, R, tube), p01 = TP(i, j + 1, seg, sides, R, tube);
                    Quad(c + r * p00, c + r * p10, c + r * p11, c + r * p01, m);
                }
        }
        static Vector3 TP(int i, int j, int seg, int sides, float R, float tube)
        {
            float u = i * Mathf.PI * 2f / seg, w = j * Mathf.PI * 2f / sides;
            float rr = R + Mathf.Cos(w) * tube;
            return new Vector3(Mathf.Cos(u) * rr, Mathf.Sin(u) * rr, Mathf.Sin(w) * tube);
        }

        // elipsoide low-poly
        public void Ellipsoid(Vector3 c, Vector3 rad, int lat, int lon, M m)
        {
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    Vector3 a = EP(i, j, lat, lon, rad), b = EP(i + 1, j, lat, lon, rad), d = EP(i, j + 1, lat, lon, rad), e = EP(i + 1, j + 1, lat, lon, rad);
                    if (i == 0) Tri(c + a, c + b, c + e, m);
                    else if (i == lat - 1) Tri(c + a, c + b, c + d, m);
                    else Quad(c + a, c + b, c + e, c + d, m);
                }
        }
        static Vector3 EP(int i, int j, int lat, int lon, Vector3 rad)
        {
            float phi = -Mathf.PI * 0.5f + i * Mathf.PI / lat, th = j * Mathf.PI * 2f / lon;
            return new Vector3(Mathf.Cos(phi) * Mathf.Cos(th) * rad.x, Mathf.Sin(phi) * rad.y, Mathf.Cos(phi) * Mathf.Sin(th) * rad.z);
        }

        public Mesh Build(string name, out Material[] used)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            List<Material> ms = new List<Material>();
            List<List<int>> subs = new List<List<int>>();
            Material[] all = Mats();
            for (int i = 0; i < tris.Length; i++)
                if (tris[i].Count > 0) { subs.Add(tris[i]); ms.Add(all[i]); }
            mesh.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
            mesh.RecalculateBounds();
            used = ms.ToArray();
            return mesh;
        }
    }

    // ---------------------------------------------------------------- AK-47
    static Mesh BuildAK(out Material[] used)
    {
        Kit k = new Kit();
        // cajón de mecanismos y tapa
        k.Box(new Vector3(0f, 0.005f, -0.035f), new Vector3(0.05f, 0.07f, 0.27f), M.Steel);
        k.Box(new Vector3(0f, 0.046f, -0.05f), new Vector3(0.044f, 0.018f, 0.24f), M.Blued);
        k.Box(new Vector3(0f, 0.05f, -0.165f), new Vector3(0.03f, 0.012f, 0.02f), M.Blued);
        k.Box(new Vector3(0.0255f, 0.024f, 0.0f), new Vector3(0.002f, 0.018f, 0.07f), M.Polymer);       // ventana de expulsión
        k.Box(new Vector3(0.031f, 0.022f, 0.07f), new Vector3(0.014f, 0.012f, 0.012f), M.Blued);         // manija de carga
        k.Box(new Vector3(0.027f, 0.008f, -0.045f), new Vector3(0.004f, 0.012f, 0.11f), M.Blued);        // selector
        k.Box(new Vector3(0.029f, -0.004f, 0.012f), new Vector3(0.006f, 0.014f, 0.014f), M.Blued);
        // brocal del cargador, guardamonte y gatillo
        k.Box(new Vector3(0f, -0.036f, 0.05f), new Vector3(0.046f, 0.02f, 0.07f), M.Steel);
        k.Box(new Vector3(0f, -0.077f, -0.03f), new Vector3(0.012f, 0.006f, 0.1f), M.Steel);
        k.Box(new Vector3(0f, -0.056f, 0.016f), new Vector3(0.012f, 0.042f, 0.006f), M.Steel);
        k.Box(new Vector3(0f, -0.05f, -0.015f), new Vector3(0.006f, 0.03f, 0.006f), new Vector3(15f, 0f, 0f), M.Steel);
        // empuñadura (baquelita) inclinada hacia atrás
        k.Taper(new Vector3(0f, -0.088f, -0.088f), new Vector2(0.03f, 0.04f), new Vector2(0.03f, 0.046f), 0.11f, 0f, new Vector3(-70f, 0f, 0f), M.Bakelite);
        // culata de madera (se ensancha y cae hacia atrás) + cantonera
        k.Taper(new Vector3(0f, -0.018f, -0.32f), new Vector2(0.042f, 0.125f), new Vector2(0.038f, 0.062f), 0.30f, 0.018f, Vector3.zero, M.Wood);
        k.Box(new Vector3(0f, -0.036f, -0.475f), new Vector3(0.044f, 0.128f, 0.012f), M.Steel);
        // guardamanos inferior y superior (madera) con abrazaderas
        k.Taper(new Vector3(0f, -0.006f, 0.20f), new Vector2(0.056f, 0.054f), new Vector2(0.05f, 0.046f), 0.2f, 0.002f, Vector3.zero, M.Wood);
        for (int i = 0; i < 3; i++) k.Box(new Vector3(0f, -0.032f, 0.14f + i * 0.04f), new Vector3(0.054f, 0.004f, 0.012f), M.Steel);   // ranuras
        k.Box(new Vector3(0f, -0.005f, 0.098f), new Vector3(0.058f, 0.058f, 0.012f), M.Steel);
        k.Box(new Vector3(0f, -0.004f, 0.305f), new Vector3(0.052f, 0.05f, 0.012f), M.Steel);
        k.Box(new Vector3(0f, 0.043f, 0.205f), new Vector3(0.038f, 0.026f, 0.19f), M.Wood);
        k.Box(new Vector3(0f, 0.043f, 0.305f), new Vector3(0.03f, 0.024f, 0.014f), M.Steel);
        // alza trasera
        k.Box(new Vector3(0f, 0.04f, 0.085f), new Vector3(0.032f, 0.03f, 0.04f), M.Steel);
        k.Box(new Vector3(0f, 0.058f, 0.075f), new Vector3(0.026f, 0.006f, 0.06f), M.Blued);
        // cañón, bloque de gases, tubo y baqueta
        k.Cyl(new Vector3(0f, 0.012f, 0.43f), Vector3.zero, 0.0115f, 0.26f, 10, M.Steel);
        k.Box(new Vector3(0f, 0.03f, 0.36f), new Vector3(0.026f, 0.044f, 0.03f), M.Steel);
        k.Cyl(new Vector3(0f, 0.043f, 0.33f), Vector3.zero, 0.012f, 0.04f, 8, M.Steel);
        k.Cyl(new Vector3(0f, -0.008f, 0.43f), Vector3.zero, 0.004f, 0.18f, 6, M.Bright);
        // mira delantera con orejas
        k.Box(new Vector3(0f, 0.03f, 0.505f), new Vector3(0.022f, 0.042f, 0.03f), M.Steel);
        k.Box(new Vector3(0.009f, 0.064f, 0.505f), new Vector3(0.004f, 0.03f, 0.012f), M.Steel);
        k.Box(new Vector3(-0.009f, 0.064f, 0.505f), new Vector3(0.004f, 0.03f, 0.012f), M.Steel);
        k.Box(new Vector3(0f, 0.06f, 0.505f), new Vector3(0.003f, 0.022f, 0.003f), M.Steel);
        // freno de boca (con corte en bisel)
        k.Cyl(new Vector3(0f, 0.012f, 0.575f), Vector3.zero, 0.015f, 0.035f, 10, M.Steel);
        k.Box(new Vector3(0f, 0.028f, 0.585f), new Vector3(0.016f, 0.006f, 0.014f), new Vector3(-30f, 0f, 0f), M.Polymer);
        // cargador curvo de baquelita: 7 segmentos que se curvan hacia delante
        Vector3 p = new Vector3(0f, -0.046f, 0.05f);
        float seg = 0.03f;
        for (int i = 0; i < 7; i++)
        {
            float ang = 8f + i * 6f;
            Vector3 dir = Quaternion.Euler(-ang, 0f, 0f) * Vector3.down;
            Vector3 c = p + dir * seg * 0.5f;
            k.Box(c, new Vector3(0.028f, seg + 0.003f, 0.066f), new Vector3(-ang, 0f, 0f), M.Bakelite);
            if (i % 2 == 1) k.Box(c, new Vector3(0.0305f, 0.005f, 0.05f), new Vector3(-ang, 0f, 0f), M.Bakelite);   // nervios
            p += dir * seg;
        }
        k.Box(p, new Vector3(0.031f, 0.008f, 0.07f), new Vector3(-50f, 0f, 0f), M.Steel);                  // base del cargador
        return k.Build("AK47_lowpoly", out used);
    }

    // ---------------------------------------------------------------- Glock 17
    static Mesh BuildGlock(out Material[] used)
    {
        Kit k = new Kit();
        // corredera
        k.Box(new Vector3(0f, 0.03f, 0.003f), new Vector3(0.025f, 0.028f, 0.186f), M.Slide);
        k.Box(new Vector3(0f, 0.0455f, 0.003f), new Vector3(0.019f, 0.004f, 0.186f), M.Slide);
        for (int i = 0; i < 7; i++)                                                                    // estrías traseras
            k.Box(new Vector3(0f, 0.031f, -0.085f + i * 0.0055f), new Vector3(0.0264f, 0.022f, 0.0022f), M.Polymer);
        k.Box(new Vector3(0.005f, 0.0472f, 0.03f), new Vector3(0.012f, 0.0016f, 0.034f), M.Polymer);  // ventana de expulsión
        k.Box(new Vector3(0.004f, 0.0476f, 0.03f), new Vector3(0.008f, 0.0012f, 0.03f), M.Bright);    // recámara visible
        k.Cyl(new Vector3(0f, 0.032f, 0.097f), Vector3.zero, 0.0075f, 0.003f, 10, M.Bright);           // boca del cañón
        k.Cyl(new Vector3(0f, 0.032f, 0.0985f), Vector3.zero, 0.0045f, 0.002f, 8, M.Polymer);
        // miras con puntos blancos
        k.Box(new Vector3(0f, 0.0505f, -0.08f), new Vector3(0.02f, 0.007f, 0.009f), M.Polymer);
        k.Box(new Vector3(0.0055f, 0.0515f, -0.0753f), new Vector3(0.0022f, 0.0022f, 0.0006f), M.Dot);
        k.Box(new Vector3(-0.0055f, 0.0515f, -0.0753f), new Vector3(0.0022f, 0.0022f, 0.0006f), M.Dot);
        k.Box(new Vector3(0f, 0.0505f, 0.088f), new Vector3(0.0036f, 0.0068f, 0.006f), M.Polymer);
        k.Box(new Vector3(0f, 0.0515f, 0.0848f), new Vector3(0.0019f, 0.0019f, 0.0006f), M.Dot);
        // armazón, riel y palancas
        k.Box(new Vector3(0f, 0.008f, 0.015f), new Vector3(0.023f, 0.016f, 0.15f), M.Polymer);
        k.Box(new Vector3(0f, -0.002f, 0.065f), new Vector3(0.02f, 0.008f, 0.05f), M.Polymer);
        for (int i = 0; i < 3; i++) k.Box(new Vector3(0f, -0.0065f, 0.05f + i * 0.013f), new Vector3(0.021f, 0.002f, 0.004f), M.Slide);
        k.Box(new Vector3(-0.0128f, 0.016f, 0.01f), new Vector3(0.002f, 0.004f, 0.016f), M.Blued);     // retén de corredera
        k.Box(new Vector3(0.0122f, 0.012f, 0.04f), new Vector3(0.002f, 0.003f, 0.008f), M.Blued);      // palanca de desarme
        k.Box(new Vector3(-0.0122f, 0.012f, 0.04f), new Vector3(0.002f, 0.003f, 0.008f), M.Blued);
        // guardamonte cuadrado y gatillo
        k.Box(new Vector3(0f, -0.016f, 0.031f), new Vector3(0.008f, 0.026f, 0.006f), M.Polymer);
        k.Box(new Vector3(0f, -0.028f, 0.007f), new Vector3(0.008f, 0.005f, 0.052f), M.Polymer);
        k.Box(new Vector3(0f, -0.012f, 0.005f), new Vector3(0.005f, 0.02f, 0.005f), new Vector3(10f, 0f, 0f), M.Polymer);
        k.Box(new Vector3(0f, -0.010f, 0.0075f), new Vector3(0.0015f, 0.012f, 0.0025f), new Vector3(10f, 0f, 0f), M.Blued);
        // empuñadura inclinada: muescas para los dedos, paneles con textura y base del cargador
        Vector3 gc = new Vector3(0f, -0.05f, -0.055f);
        Vector3 ge = new Vector3(18f, 0f, 0f);
        Quaternion gr = Quaternion.Euler(ge);
        k.Box(gc, new Vector3(0.027f, 0.095f, 0.047f), ge, M.Polymer);
        k.Box(new Vector3(0f, 0.001f, -0.077f), new Vector3(0.024f, 0.012f, 0.022f), M.Polymer);         // cola de castor
        for (int i = 0; i < 3; i++)
            k.Box(gc + gr * new Vector3(0f, -0.03f + i * 0.02f, 0.0245f), new Vector3(0.024f, 0.008f, 0.005f), ge, M.Polymer);
        k.Box(gc + gr * new Vector3(0.0138f, -0.005f, -0.002f), new Vector3(0.0008f, 0.062f, 0.036f), ge, M.Blued);
        k.Box(gc + gr * new Vector3(-0.0138f, -0.005f, -0.002f), new Vector3(0.0008f, 0.062f, 0.036f), ge, M.Blued);
        k.Box(gc + gr * new Vector3(0f, -0.051f, 0.002f), new Vector3(0.029f, 0.008f, 0.05f), ge, M.Polymer);
        return k.Build("Glock_lowpoly", out used);
    }

    // ---------------------------------------------------------------- granada de fragmentación tipo piña
    static Mesh BuildGrenade(out Material[] used)
    {
        Kit k = new Kit();
        Vector3 rad = new Vector3(0.028f, 0.036f, 0.028f);
        k.Ellipsoid(Vector3.zero, rad * 0.9f, 8, 12, M.Olive);
        int rows = 6, cols = 8;
        for (int i = 0; i < rows; i++)
        {
            float phi = Mathf.Lerp(-58f, 58f, i / (float)(rows - 1)) * Mathf.Deg2Rad;
            for (int j = 0; j < cols; j++)
            {
                float th = (j + (i % 2) * 0.5f) * Mathf.PI * 2f / cols;
                Vector3 s = new Vector3(Mathf.Cos(phi) * Mathf.Cos(th) * rad.x, Mathf.Sin(phi) * rad.y, Mathf.Cos(phi) * Mathf.Sin(th) * rad.z);
                Vector3 nrm = new Vector3(s.x / (rad.x * rad.x), s.y / (rad.y * rad.y), s.z / (rad.z * rad.z)).normalized;
                float w = 2f * Mathf.PI * rad.x * Mathf.Cos(phi) / cols * 0.8f;
                float h = rad.y * (116f * Mathf.Deg2Rad) / rows * 0.95f;
                Vector3 e = Quaternion.LookRotation(nrm, Vector3.up).eulerAngles;
                k.Box(s - nrm * 0.0015f, new Vector3(w, h, 0.006f), e, M.Olive);
            }
        }
        k.CylY(new Vector3(0f, -0.036f, 0f), 0.008f, 0.006f, 8, M.Olive);         // tapón inferior
        k.CylY(new Vector3(0f, 0.039f, 0f), 0.0085f, 0.012f, 8, M.Blued);         // cuello
        k.CylY(new Vector3(0f, 0.05f, 0f), 0.011f, 0.014f, 8, M.Blued);           // espoleta
        k.CylY(new Vector3(0f, 0.059f, 0f), 0.0075f, 0.005f, 8, M.Blued);
        // cuchara (palanca) bajando por un costado
        k.Box(new Vector3(0.013f, 0.0585f, 0f), new Vector3(0.016f, 0.003f, 0.011f), M.Bright);
        k.Box(new Vector3(0.0225f, 0.044f, 0f), new Vector3(0.003f, 0.03f, 0.01f), new Vector3(0f, 0f, 14f), M.Bright);
        k.Box(new Vector3(0.028f, 0.019f, 0f), new Vector3(0.003f, 0.024f, 0.009f), new Vector3(0f, 0f, 12f), M.Bright);
        // pasador y anilla
        k.Cyl(new Vector3(0f, 0.05f, 0.014f), Vector3.zero, 0.0016f, 0.03f, 6, M.Bright);
        k.Torus(new Vector3(0f, 0.05f, 0.036f), new Vector3(0f, 90f, 0f), 0.011f, 0.0017f, 14, 5, M.Bright);
        return k.Build("Granada_lowpoly", out used);
    }

    // ---------------------------------------------------------------- brazos y manos de primera persona
    // tubo cónico entre dos puntos (antebrazo)
    static void Limb(Kit k, Vector3 from, Vector3 to, float r0, float r1, M m)
    {
        Vector3 d = to - from;
        Vector3 e = Quaternion.LookRotation(d.normalized, Vector3.up).eulerAngles;
        k.Taper((from + to) * 0.5f, new Vector2(r0 * 2f, r0 * 1.7f), new Vector2(r1 * 2f, r1 * 1.7f), d.magnitude, 0f, e, m);
    }

    // mano cerrada sobre un punto: palma + dedos + pulgar
    static void Fist(Kit k, Vector3 c, Vector3 euler, float s)
    {
        Quaternion r = Quaternion.Euler(euler);
        k.Box(c, new Vector3(0.05f, 0.05f, 0.075f) * s, euler, M.Skin);
        for (int i = 0; i < 4; i++)
            k.Box(c + r * new Vector3(-0.024f * s, (0.018f - i * 0.012f) * s, 0.032f * s), new Vector3(0.012f, 0.011f, 0.022f) * s, euler, M.Skin);
        k.Box(c + r * new Vector3(0.006f * s, 0.028f * s, 0.03f * s), new Vector3(0.014f, 0.012f, 0.035f) * s, euler, M.Skin);
    }

    static Mesh BuildArms(ItemType t, out Material[] used)
    {
        Kit k = new Kit();
        if (t == ItemType.AK47)
        {
            Fist(k, new Vector3(0.012f, -0.085f, -0.095f), new Vector3(-20f, 0f, 0f), 1f);                // derecha en la empuñadura
            Limb(k, new Vector3(0.02f, -0.11f, -0.13f), new Vector3(0.17f, -0.3f, -0.42f), 0.03f, 0.04f, M.Skin);
            Limb(k, new Vector3(0.17f, -0.3f, -0.42f), new Vector3(0.22f, -0.36f, -0.52f), 0.043f, 0.05f, M.Sleeve);
            Fist(k, new Vector3(-0.012f, -0.04f, 0.2f), new Vector3(0f, 0f, 70f), 1f);                    // izquierda bajo el guardamanos
            Limb(k, new Vector3(-0.03f, -0.06f, 0.17f), new Vector3(-0.2f, -0.3f, -0.15f), 0.03f, 0.04f, M.Skin);
            Limb(k, new Vector3(-0.2f, -0.3f, -0.15f), new Vector3(-0.26f, -0.37f, -0.26f), 0.043f, 0.05f, M.Sleeve);
        }
        else if (t == ItemType.Glock)
        {
            Fist(k, new Vector3(0.008f, -0.05f, -0.06f), new Vector3(-18f, 0f, 0f), 0.95f);
            Limb(k, new Vector3(0.012f, -0.08f, -0.09f), new Vector3(0.11f, -0.27f, -0.36f), 0.029f, 0.039f, M.Skin);
            Limb(k, new Vector3(0.11f, -0.27f, -0.36f), new Vector3(0.15f, -0.33f, -0.46f), 0.042f, 0.049f, M.Sleeve);
            Fist(k, new Vector3(-0.024f, -0.06f, -0.05f), new Vector3(-10f, 0f, -40f), 0.95f);             // izquierda de apoyo
            Limb(k, new Vector3(-0.035f, -0.085f, -0.08f), new Vector3(-0.15f, -0.27f, -0.34f), 0.029f, 0.039f, M.Skin);
            Limb(k, new Vector3(-0.15f, -0.27f, -0.34f), new Vector3(-0.2f, -0.33f, -0.44f), 0.042f, 0.049f, M.Sleeve);
        }
        else
        {
            Fist(k, new Vector3(0.006f, -0.03f, -0.012f), new Vector3(-30f, 0f, 0f), 1f);                  // granada en la mano
            Limb(k, new Vector3(0.01f, -0.06f, -0.04f), new Vector3(0.1f, -0.27f, -0.34f), 0.03f, 0.04f, M.Skin);
            Limb(k, new Vector3(0.1f, -0.27f, -0.34f), new Vector3(0.14f, -0.33f, -0.44f), 0.043f, 0.05f, M.Sleeve);
        }
        return k.Build("BrazosFP", out used);
    }

    static readonly Dictionary<int, Mesh> armsMesh = new Dictionary<int, Mesh>();
    static readonly Dictionary<int, Material[]> armsMats = new Dictionary<int, Material[]>();

    // brazos del jugador en primera persona, con la manga del color del equipo
    public static GameObject CreateFPArms(ItemType t, Transform parent, Color sleeve)
    {
        int key = (int)t;
        Mesh mesh; Material[] used;
        if (!armsMesh.TryGetValue(key, out mesh)) { mesh = BuildArms(t, out used); armsMesh[key] = mesh; armsMats[key] = used; }
        used = (Material[])armsMats[key].Clone();
        for (int i = 0; i < used.Length; i++)
            if (used[i] == Mats()[(int)M.Sleeve]) { used[i] = new Material(used[i]); used[i].color = sleeve; }
        GameObject go = new GameObject("BrazosFP");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = used;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    // ---------------------------------------------------------------- acceso (mallas en caché)
    static Mesh ak, glock, grenade;
    static Material[] akM, glockM, grenadeM;

    public static GameObject Create(ItemType t, Transform parent)
    {
        Mesh mesh; Material[] used;
        if (t == ItemType.AK47) { if (ak == null) ak = BuildAK(out akM); mesh = ak; used = akM; }
        else if (t == ItemType.Glock) { if (glock == null) glock = BuildGlock(out glockM); mesh = glock; used = glockM; }
        else { if (grenade == null) grenade = BuildGrenade(out grenadeM); mesh = grenade; used = grenadeM; }
        GameObject go = new GameObject("Modelo");
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = used;
        if (t == ItemType.Grenade) go.transform.localScale = Vector3.one * 1.2f;   // un poco más grande para que se lea en pantalla
        return go;
    }
}
