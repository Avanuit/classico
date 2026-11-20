using System.Collections.Generic;
using UnityEngine;

// ===========================================================================================================
//  UNIFORMES: repinta la TEXTURA del jugador (camiseta + pantaloneta + medias) y le estampa escudo, número y nombre.
//  El modelo de Meshy trae un uniforme blanco con detalles rojos en UNA textura. Aquí:
//   1) Se marca qué texel es camiseta / pantaloneta / medias (triángulos del modelo + pesos de los huesos).
//   2) Solo se repinta la TELA (píxeles blancos → color del equipo, rojos → color de detalle) conservando
//      sombras y pliegues; la piel, el pelo y los guayos no se tocan.
//   3) Escudo (pecho izquierdo), nombre y número (espalda) se proyectan desde 3D, así no los cortan las costuras.
//  Se genera una textura por jugador (comprimida) repartida en varios cuadros para no trabar el juego.
// ===========================================================================================================
public class KitDef
{
    public Color shirt, stripe, trim, shorts, shortsTrim, socks, numFill, numOutline;
    public bool stripes;
}

public static class KitPainter
{
    const int Size = 2048;
    enum Region : byte { None = 0, Shirt = 1, Shorts = 2, Socks = 3, Other = 4 }   // Other: piel, pelo, guayos (cubierto pero no se pinta)

    class Prepared
    {
        public Color32[] basePx;
        public byte[] region;
        public int[] clothIdx;                 // texels de tela (camiseta, pantaloneta, medias)
        public int[] shirtIdx;                 // texels de camiseta con su posición en el torso
        public float[] shirtX, shirtY, shirtF; // X lateral, Y altura (en largos de torso), F: +1 de frente, -1 de espaldas
        public Texture baseTex;
        public int[] padDst, padSrc;          // relleno alrededor de las islas UV (evita las líneas en las costuras)
    }

    static readonly Dictionary<Mesh, Prepared> cache = new Dictionary<Mesh, Prepared>();

    // ------------------------------------------------------------------ API
    // Versión del pintado: si cambias el algoritmo, súbela para que se regeneren los uniformes guardados.
    const int Version = 4;
    public const string CacheFolder = "Kits";           // Assets/Resources/Kits

    // Los uniformes se GUARDAN en Assets/Resources/Kits la primera vez que se generan (en el editor):
    // las siguientes veces (y en el juego compilado) solo se cargan, sin ningún tirón.
    public static bool Paint(GameObject player, KitDef kit, int team, string number, string name)
    {
        SkinnedMeshRenderer smr = player.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr == null || smr.sharedMesh == null) return false;
        Texture baseTex = BaseTexture(smr.sharedMaterial);
        if (baseTex == null) return false;

        string key = Key(kit, team, number, name);
        Texture2D cached = Resources.Load<Texture2D>(CacheFolder + "/" + key);
        if (cached != null) { Assign(smr, baseTex, cached); return true; }

        Prepared p = Prepare(smr);
        if (p == null) return false;

        Color32[] px = (Color32[])p.basePx.Clone();
        Recolor(p, px, kit);
        Decals(p, px, kit, team, number, name);
        for (int i = 0; i < p.padDst.Length; i++) px[p.padDst[i]] = px[p.padSrc[i]];   // relleno de costuras

        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
        tex.name = "Uniforme_" + player.name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply(true);
#if UNITY_EDITOR
        try
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "Resources/" + CacheFolder);
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, key + ".png"), tex.EncodeToPNG());
            pendingImport = true;
        }
        catch (System.Exception e) { Debug.LogWarning("KitPainter: no pude guardar el uniforme: " + e.Message); }
#endif
        tex.Compress(false);
        tex.Apply(false, true);                // libera la copia en RAM
        Assign(smr, baseTex, tex);
        return true;
    }

#if UNITY_EDITOR
    static bool pendingImport;
    // al salir de Play, Unity importa los PNG nuevos (así la próxima partida ya los encuentra)
    [UnityEditor.InitializeOnLoadMethod]
    static void HookImport()
    {
        UnityEditor.EditorApplication.playModeStateChanged += s =>
        {
            if (s == UnityEditor.PlayModeStateChange.EnteredEditMode && pendingImport)
            {
                pendingImport = false;
                UnityEditor.AssetDatabase.Refresh();
            }
        };
    }
#endif

    static Texture BaseTexture(Material mat)
    {
        foreach (string prop in new string[] { "_MainTex", "_BaseMap", "_EmissionMap" })
            if (mat != null && mat.HasProperty(prop) && mat.GetTexture(prop) != null) return mat.GetTexture(prop);
        return null;
    }

    static void Assign(SkinnedMeshRenderer smr, Texture baseTex, Texture tex)
    {
        Material[] mats = smr.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null) continue;
            Material m = new Material(mats[i]);
            foreach (string prop in new string[] { "_MainTex", "_BaseMap", "_EmissionMap" })
                if (m.HasProperty(prop) && m.GetTexture(prop) == baseTex) m.SetTexture(prop, tex);
            mats[i] = m;
        }
        smr.sharedMaterials = mats;
    }

    // nombre de archivo estable según TODO lo que cambia el uniforme (colores, número, nombre, versión)
    static string Key(KitDef k, int team, string number, string name)
    {
        string s = Version + "|" + team + "|" + number + "|" + name + "|" + k.stripes + "|" +
                   C(k.shirt) + C(k.stripe) + C(k.trim) + C(k.shorts) + C(k.shortsTrim) + C(k.socks) + C(k.numFill) + C(k.numOutline);
        uint h = 2166136261;
        foreach (byte b in System.Text.Encoding.UTF8.GetBytes(s)) { h ^= b; h *= 16777619; }
        return "kit_" + team + "_" + number + "_" + h.ToString("x8");
    }
    static string C(Color c) { return ColorUtility.ToHtmlStringRGB(c); }

    // relleno: los texels vacíos alrededor de cada isla UV copian al vecino pintado más cercano (BFS)
    static void BuildPadding(Prepared p, int radius)
    {
        int n = Size * Size;
        byte[] dist = new byte[n];
        int[] queue = new int[n];
        int head = 0, tail = 0;
        for (int i = 0; i < n; i++)
            if (p.region[i] != 0) { dist[i] = 1; queue[tail++] = i; }
        List<int> dst = new List<int>(), src = new List<int>();
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        while (head < tail)
        {
            int i = queue[head++];
            if (dist[i] > radius) continue;
            int x = i % Size, y = i / Size;
            for (int d = 0; d < 4; d++)
            {
                int xx = x + dx[d], yy = y + dy[d];
                if (xx < 0 || yy < 0 || xx >= Size || yy >= Size) continue;
                int j = yy * Size + xx;
                if (dist[j] != 0) continue;
                dist[j] = (byte)(dist[i] + 1);
                dst.Add(j); src.Add(i);
                queue[tail++] = j;
            }
        }
        p.padDst = dst.ToArray();
        p.padSrc = src.ToArray();
    }

    public static void ClearCache() { cache.Clear(); }

    // ------------------------------------------------------------------ preparación (una vez por modelo)
    static Prepared Prepare(SkinnedMeshRenderer smr)
    {
        Mesh mesh = smr.sharedMesh;
        Prepared p;
        if (cache.TryGetValue(mesh, out p)) return p;
        cache[mesh] = null;
        if (!mesh.isReadable)
        {
            Debug.LogError("KitPainter: el modelo del jugador no tiene Read/Write. Ejecuta Clasico > Animaciones > Reparar animaciones.");
            return null;
        }
        Material mat = smr.sharedMaterial;
        Texture src = null;
        foreach (string prop in new string[] { "_MainTex", "_BaseMap", "_EmissionMap" })
            if (mat != null && mat.HasProperty(prop) && mat.GetTexture(prop) != null) { src = mat.GetTexture(prop); break; }
        if (src == null) { Debug.LogError("KitPainter: el material del jugador no tiene textura."); return null; }

        p = new Prepared();
        p.baseTex = src;
        p.basePx = ReadTexture(src, Size);

        // ---- marco del cuerpo (en el espacio del mesh): caderas, arriba, derecha, frente
        Transform[] bones = smr.bones;
        Matrix4x4[] bind = mesh.bindposes;
        int[] cat = new int[bones.Length];
        Vector3 hips = Vector3.zero, neck = Vector3.up, lArm = Vector3.left, rArm = Vector3.right;
        bool hasHips = false, hasNeck = false, hasL = false, hasR = false;
        for (int i = 0; i < bones.Length && i < bind.Length; i++)
        {
            string n = bones[i] != null ? bones[i].name.ToLower() : "";
            cat[i] = Category(n);
            Vector3 o = bind[i].inverse.MultiplyPoint3x4(Vector3.zero);
            if (n == "hips" || n.EndsWith(":hips") || n.Contains("pelvis")) { hips = o; hasHips = true; }
            else if (n.Contains("neck") && !hasNeck) { neck = o; hasNeck = true; }
            else if ((n.Contains("leftarm") || n.Contains("upperarm_l") || n.Contains("l_upperarm")) && !n.Contains("fore")) { lArm = o; hasL = true; }
            else if ((n.Contains("rightarm") || n.Contains("upperarm_r") || n.Contains("r_upperarm")) && !n.Contains("fore")) { rArm = o; hasR = true; }
        }
        if (!hasHips || !hasNeck || !hasL || !hasR)
            Debug.LogWarning("KitPainter: no encontré todos los huesos (Hips/Neck/LeftArm/RightArm); el escudo y el número pueden quedar corridos.");
        float T = Mathf.Max(0.0001f, (neck - hips).magnitude);
        Vector3 up = (neck - hips) / T;
        Vector3 right = Vector3.ProjectOnPlane(rArm - lArm, up).normalized;
        Vector3 fwd = Vector3.Cross(right, up).normalized;

        // ---- región de cada vértice según sus huesos (y la altura, para la zona de la cintura)
        Vector3[] verts = mesh.vertices;
        Vector3[] norms = mesh.normals;
        Vector2[] uvs = mesh.uv;
        BoneWeight[] bw = mesh.boneWeights;
        if (uvs.Length != verts.Length || bw.Length != verts.Length) { Debug.LogError("KitPainter: el mesh no tiene UV o pesos."); return null; }
        byte[] vReg = new byte[verts.Length];
        float[] acc = new float[9];
        for (int v = 0; v < verts.Length; v++)
        {
            System.Array.Clear(acc, 0, acc.Length);
            Add(acc, cat, bw[v].boneIndex0, bw[v].weight0); Add(acc, cat, bw[v].boneIndex1, bw[v].weight1);
            Add(acc, cat, bw[v].boneIndex2, bw[v].weight2); Add(acc, cat, bw[v].boneIndex3, bw[v].weight3);
            int best = 0;
            for (int c = 1; c < acc.Length; c++) if (acc[c] > acc[best]) best = c;
            float h = Vector3.Dot(verts[v] - hips, up) / T;
            vReg[v] = RegionOf(best, h);
        }

        // ---- rasterizar los triángulos en el espacio de la textura
        p.region = new byte[Size * Size];
        short[] sx = new short[Size * Size], sy = new short[Size * Size];
        sbyte[] sf = new sbyte[Size * Size];
        int[] tris = mesh.triangles;
        for (int t = 0; t < tris.Length; t += 3)
        {
            int a = tris[t], b = tris[t + 1], c = tris[t + 2];
            byte r = Majority(vReg[a], vReg[b], vReg[c]);
            if (r == 0) r = (byte)Region.Other;
            Raster(p.region, sx, sy, sf, r, uvs[a], uvs[b], uvs[c], verts[a], verts[b], verts[c],
                   norms.Length == verts.Length ? (norms[a] + norms[b] + norms[c]).normalized : Vector3.zero,
                   hips, up, right, fwd, T);
        }

        List<int> cloth = new List<int>(), shirt = new List<int>();
        for (int i = 0; i < p.region.Length; i++)
        {
            if (p.region[i] == 0 || p.region[i] == (byte)Region.Other) continue;
            cloth.Add(i);
            if (p.region[i] == (byte)Region.Shirt) shirt.Add(i);
        }
        p.clothIdx = cloth.ToArray();
        p.shirtIdx = shirt.ToArray();
        p.shirtX = new float[shirt.Count]; p.shirtY = new float[shirt.Count]; p.shirtF = new float[shirt.Count];
        for (int i = 0; i < shirt.Count; i++)
        {
            int k = shirt[i];
            p.shirtX[i] = sx[k] / 1000f; p.shirtY[i] = sy[k] / 1000f; p.shirtF[i] = sf[k] / 100f;
        }
        BuildPadding(p, 12);
        cache[mesh] = p;
        return p;
    }

    // 0 nada, 1 torso, 2 brazo, 3 mano, 4 cabeza, 5 cuello, 6 cadera, 7 muslo, 8 pierna, (pie = 0)
    static int Category(string n)
    {
        if (n.Contains("armature") || n.Length == 0) return 0;
        if (n.Contains("hand") || n.Contains("finger") || n.Contains("thumb") || n.Contains("index") || n.Contains("pinky") || n.Contains("ring") || n.Contains("middle")) return 3;
        if (n.Contains("head") || n.Contains("eye") || n.Contains("jaw")) return 4;
        if (n.Contains("neck")) return 5;
        if (n.Contains("arm") || n.Contains("shoulder") || n.Contains("clavicle")) return 2;
        if (n.Contains("spine") || n.Contains("chest") || n.Contains("torso")) return 1;
        if (n.Contains("hips") || n.Contains("pelvis")) return 6;
        if (n.Contains("upleg") || n.Contains("thigh") || n.Contains("upperleg")) return 7;
        if (n.Contains("toe") || n.Contains("foot")) return 0;
        if (n.Contains("leg") || n.Contains("calf") || n.Contains("shin") || n.Contains("knee")) return 8;
        return 0;
    }

    static void Add(float[] acc, int[] cat, int bone, float w)
    {
        if (w <= 0f || bone < 0 || bone >= cat.Length) return;
        acc[cat[bone]] += w;
    }

    static byte RegionOf(int c, float h)
    {
        switch (c)
        {
            case 1: case 2: case 5: return (byte)Region.Shirt;
            case 6: return (byte)(h > 0.12f ? Region.Shirt : Region.Shorts);
            case 7: return (byte)Region.Shorts;
            case 8: return (byte)Region.Socks;
            default: return 0;
        }
    }

    static byte Majority(byte a, byte b, byte c)
    {
        if (a == b || a == c) return a;
        if (b == c) return b;
        return a;
    }

    static void Raster(byte[] reg, short[] sx, short[] sy, sbyte[] sf, byte r, Vector2 ua, Vector2 ub, Vector2 uc,
                       Vector3 pa, Vector3 pb, Vector3 pc, Vector3 n, Vector3 hips, Vector3 up, Vector3 right, Vector3 fwd, float T)
    {
        Vector2 a = ua * Size - new Vector2(0.5f, 0.5f), b = ub * Size - new Vector2(0.5f, 0.5f), c = uc * Size - new Vector2(0.5f, 0.5f);
        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))) - 1);
        int x1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))) + 1);
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))) - 1);
        int y1 = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))) + 1);
        float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
        if (Mathf.Abs(area) < 1e-6f) return;
        float inv = 1f / area;
        float facing = Vector3.Dot(n, fwd);
        // margen de ~1 texel para que no queden líneas sin pintar en las costuras
        float eps = 1.2f / Mathf.Sqrt(Mathf.Abs(area) + 1f);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float w0 = ((b.x - x) * (c.y - y) - (c.x - x) * (b.y - y)) * inv;
                float w1 = ((c.x - x) * (a.y - y) - (a.x - x) * (c.y - y)) * inv;
                float w2 = 1f - w0 - w1;
                if (w0 < -eps || w1 < -eps || w2 < -eps) continue;
                int k = y * Size + x;
                bool inside = w0 >= 0f && w1 >= 0f && w2 >= 0f;
                if (!inside && reg[k] != 0) continue;     // el margen no pisa lo que ya está pintado
                reg[k] = r;
                if (r == (byte)Region.Shirt)
                {
                    Vector3 P = pa * w0 + pb * w1 + pc * w2 - hips;
                    sx[k] = (short)Mathf.Clamp(Vector3.Dot(P, right) / T * 1000f, -32000f, 32000f);
                    sy[k] = (short)Mathf.Clamp(Vector3.Dot(P, up) / T * 1000f, -32000f, 32000f);
                    sf[k] = (sbyte)Mathf.Clamp(facing * 100f, -100f, 100f);
                }
            }
    }

    // ------------------------------------------------------------------ recolor de la tela
    static void Recolor(Prepared p, Color32[] px, KitDef kit)
    {
        Dictionary<int, int> shirtSlot = new Dictionary<int, int>(p.shirtIdx.Length);
        for (int i = 0; i < p.shirtIdx.Length; i++) shirtSlot[p.shirtIdx[i]] = i;
        for (int n = 0; n < p.clothIdx.Length; n++)
        {
            int k = p.clothIdx[n];
            Color32 c = px[k];
            float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
            float mx = Mathf.Max(r, Mathf.Max(g, b)), mn = Mathf.Min(r, Mathf.Min(g, b));
            float sat = mx > 0.001f ? (mx - mn) / mx : 0f;
            bool white = sat < 0.2f && mx > 0.42f;
            bool red = r > 0.3f && r > g * 1.8f && r > b * 1.8f;
            if (!white && !red) continue;                 // piel, pelo, guayos: intactos
            Region reg = (Region)p.region[k];
            int slot;
            if (reg != Region.Shirt || !shirtSlot.TryGetValue(k, out slot)) slot = -1;
            // el modelo trae un nombre y un número ROJOS estampados en el pecho/espalda: se borran (pasan a ser tela)
            if (red && slot >= 0 && Mathf.Abs(p.shirtF[slot]) > 0.25f && Mathf.Abs(p.shirtX[slot]) < 0.34f && p.shirtY[slot] > 0.06f && p.shirtY[slot] < 0.93f)
            {
                red = false; white = true; mx = Mathf.Clamp(r * 1.05f, 0.6f, 0.95f);
            }
            Color target;
            float shade;
            if (white)
            {
                shade = Mathf.Clamp(mx / 0.9f, 0.35f, 1.08f);
                if (reg == Region.Shirt)
                {
                    target = kit.shirt;
                    if (kit.stripes && slot >= 0)
                    {
                        float s = Mathf.Repeat(p.shirtX[slot] / 0.2f + 0.25f, 1f);
                        if (s < 0.5f) target = kit.stripe;
                    }
                }
                else if (reg == Region.Shorts) target = kit.shorts;
                else continue;                            // medias blancas: se quedan
            }
            else
            {
                shade = Mathf.Clamp(r / 0.8f, 0.35f, 1.15f);
                target = reg == Region.Shirt ? kit.trim : (reg == Region.Shorts ? kit.shortsTrim : kit.socks);
            }
            px[k] = new Color32((byte)Mathf.Clamp(target.r * shade * 255f, 0f, 255f), (byte)Mathf.Clamp(target.g * shade * 255f, 0f, 255f),
                                (byte)Mathf.Clamp(target.b * shade * 255f, 0f, 255f), c.a);
        }
    }

    // ------------------------------------------------------------------ escudo, nombre y número
    class Decal
    {
        public Color32[] px; public int w, h;
        public Color Sample(float u, float v)
        {
            if (u < 0f || u >= 1f || v < 0f || v >= 1f) return Color.clear;
            Color32 c = px[Mathf.Min(h - 1, (int)(v * h)) * w + Mathf.Min(w - 1, (int)(u * w))];
            return c;
        }
    }

    static readonly Dictionary<int, Decal> crestCache = new Dictionary<int, Decal>();

    static void Decals(Prepared p, Color32[] px, KitDef kit, int team, string number, string name)
    {
        Decal num = TextDecal(number, 256, 192, kit.numFill, kit.numOutline, 9);
        Decal nam = TextDecal(name, 384, 96, kit.numFill, kit.numOutline, 5);
        Decal crest;
        if (!crestCache.TryGetValue(team, out crest))
        {
            Texture2D ct = UIKit.Crest(team);
            crest = new Decal();
            crest.w = 128; crest.h = 128;
            crest.px = ReadTexture(ct, 128);
            crestCache[team] = crest;
        }
        // proporciones en largos de torso (caderas → cuello)
        float numH = 0.42f, numW = numH * 256f / 192f, numCy = 0.42f;
        float namH = 0.15f, namW = namH * 384f / 96f, namCy = 0.80f;
        float crH = 0.15f, crCx = -0.11f, crCy = 0.72f;

        for (int i = 0; i < p.shirtIdx.Length; i++)
        {
            float x = p.shirtX[i], y = p.shirtY[i], f = p.shirtF[i];
            Color d = Color.clear;
            if (f < -0.3f)                                // espalda: se ve con la derecha del jugador a la derecha
            {
                d = num.Sample(x / numW + 0.5f, (y - numCy) / numH + 0.5f);
                if (d.a < 0.01f) d = nam.Sample(x / namW + 0.5f, (y - namCy) / namH + 0.5f);
            }
            else if (f > 0.3f)                            // frente: el escudo en el pecho izquierdo
                d = crest.Sample(-(x - crCx) / crH + 0.5f, (y - crCy) / crH + 0.5f);
            if (d.a < 0.01f) continue;
            int k = p.shirtIdx[i];
            Color c = px[k];
            Color o = Color.Lerp(c, new Color(d.r, d.g, d.b, c.a), d.a);
            px[k] = o;
        }
    }

    static Decal TextDecal(string text, int w, int h, Color fill, Color outline, int outlinePx)
    {
        float[] mask = TextRaster.Render(text, w, h);
        Decal d = new Decal();
        d.w = w; d.h = h;
        d.px = new Color32[w * h];
        // contorno: dilatación del texto
        float[] ol = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float m = 0f;
                for (int dy = -outlinePx; dy <= outlinePx && m < 1f; dy++)
                {
                    int yy = y + dy; if (yy < 0 || yy >= h) continue;
                    for (int dx = -outlinePx; dx <= outlinePx; dx++)
                    {
                        int xx = x + dx; if (xx < 0 || xx >= w) continue;
                        if (dx * dx + dy * dy > outlinePx * outlinePx) continue;
                        float v = mask[yy * w + xx];
                        if (v > m) m = v;
                    }
                }
                ol[y * w + x] = m;
            }
        for (int i = 0; i < d.px.Length; i++)
        {
            Color c = Color.Lerp(outline, fill, mask[i]);
            c.a = Mathf.Max(mask[i], ol[i]);
            d.px[i] = c;
        }
        return d;
    }

    // copia cualquier textura (aunque no sea legible) a píxeles CPU del tamaño pedido
    static Color32[] ReadTexture(Texture src, int size)
    {
        RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.clear);
        Graphics.Blit(src, rt);
        RenderTexture.active = rt;
        Texture2D tmp = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tmp.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tmp.Apply(false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] px = tmp.GetPixels32();
        Object.Destroy(tmp);
        return px;
    }
}

// Dibuja un texto (Arial negrita) en una máscara 0..1 usando una cámara temporal y un TextMesh.
public static class TextRaster
{
    const int Layer = 31;

    public static float[] Render(string text, int w, int h)
    {
        float[] mask = new float[w * h];
        if (string.IsNullOrEmpty(text)) return mask;
        Vector3 origin = new Vector3(0f, -5000f, 0f);

        GameObject tg = new GameObject("_TextRaster");
        tg.layer = Layer;
        tg.transform.position = origin;
        TextMesh tm = tg.AddComponent<TextMesh>();
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        tm.font = font;
        tm.fontSize = 128;
        tm.characterSize = 0.1f;
        tm.fontStyle = FontStyle.Bold;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        tm.text = text;
        MeshRenderer mr = tg.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;

        GameObject cg = new GameObject("_TextRasterCam");
        Camera cam = cg.AddComponent<Camera>();
        cam.enabled = false;
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 1 << Layer;
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
        cg.transform.position = origin + new Vector3(0f, 0f, -5f);
        cg.transform.rotation = Quaternion.identity;

        RenderTexture rt = RenderTexture.GetTemporary(w, h, 16, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.aspect = w / (float)h;
        Bounds b = mr.bounds;
        float aspect = w / (float)h;
        cam.orthographicSize = Mathf.Max(b.extents.y, b.extents.x / aspect) * 1.12f + 0.001f;
        cg.transform.position = new Vector3(b.center.x, b.center.y, origin.z - 5f);
        cam.Render();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tmp.Apply(false);
        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] px = tmp.GetPixels32();
        for (int i = 0; i < mask.Length; i++) mask[i] = px[i].r / 255f;
        Object.Destroy(tmp);
        Object.Destroy(tg);
        Object.Destroy(cg);
        return mask;
    }
}
