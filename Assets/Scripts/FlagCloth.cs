using System.Collections.Generic;
using UnityEngine;

public enum FlagAttach { Left, Right, Top, Bottom }
public enum FlagStyle { Pole, Spectator, Banner }
public enum FlagFit
{
    [Tooltip("La bandera toma el ancho que le toca por la proporción de su textura (alto = Flag Height)")] MatchTexture,
    [Tooltip("Cabe dentro del plano sin deformarse (puede quedar más pequeña que el plano)")] FitInsidePlane,
    [Tooltip("Llena todo el plano recortando la textura, sin deformarla")] CropToPlane
}

// ===========================================================================================================
//  BANDERAS DE TELA (Cloth).  Pon este script en un PLANO o QUAD con la textura de la bandera (el que colocaste a mano).
//  Al darle Play, el plano se reemplaza por una tela simulada que:
//    · ondea con viento constante y ráfagas,
//    · reacciona a los disparos (cualquier arma), a las explosiones y a los balonazos que pasen cerca,
//    · respeta la PROPORCIÓN de la textura (no se deforma), según el modo "Fit".
//  Usos:  Pole (en un poste: borde izquierdo fijo) · Spectator (la sostiene un hincha: además se agita) ·
//         Banner (colgada de lo alto del estadio: borde superior fijo; cambia Attach a Top).
// ===========================================================================================================
public class FlagCloth : MonoBehaviour
{
    public static readonly List<FlagCloth> All = new List<FlagCloth>();

    [Header("Textura")]
    [Tooltip("Opciones de bandera. Si hay varias se elige una al azar (o la de 'Texture Index'). Vacío = usa la textura del material del plano")]
    public Texture2D[] textures;
    [Tooltip("-1 = al azar entre las opciones")] public int textureIndex = -1;
    public FlagFit fit = FlagFit.MatchTexture;
    [Tooltip("Alto de la bandera en metros (solo con Fit = MatchTexture). 0 = usar el alto del plano")]
    public float flagHeight = 1.2f;
    public bool flipX, flipY;
    [Range(0.5f, 1.2f)] public float brightness = 0.95f;

    [Header("Sujeción")]
    public FlagStyle style = FlagStyle.Pole;
    [Tooltip("Borde que queda fijo al poste o a la pared. Left = poste a la izquierda")]
    public FlagAttach attach = FlagAttach.Left;
    [Range(6, 40)] public int segmentsU = 20;
    [Range(4, 30)] public int segmentsV = 12;

    [Header("Viento")]
    public float windStrength = 1f;
    [Tooltip("Cuánto cambia el viento (ráfagas)")] public float gustiness = 1f;
    [Tooltip("Vacío = dirección global (FlagWindSettings)")] public Vector3 windDirectionOverride;

    [Header("Hincha agitando (solo Spectator)")]
    public float waveAmplitude = 22f;
    public float waveFrequency = 1.1f;

    [Header("Rendimiento")]
    [Tooltip("Más lejos que esto la tela deja de simularse")] public float simulateDistance = 70f;

    // ---- viento global (lo edita FlagWindSettings)
    public static Vector3 GlobalWindDirection = new Vector3(1f, 0.05f, 0.35f);
    public static float GlobalWindStrength = 7f;

    GameObject clothGo;
    Cloth cloth;
    SkinnedMeshRenderer smr;
    Quaternion baseRot;
    Vector3 center, impulse;
    float phase, nextLod, noiseSeed;
    Vector3 waveAxis;
    Transform cam;

    void Start()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        Renderer orig = GetComponent<Renderer>();
        if (mf == null || mf.sharedMesh == null || orig == null)
        {
            Debug.LogWarning("FlagCloth: '" + name + "' necesita MeshFilter + MeshRenderer (usa un Plane o un Quad).");
            enabled = false;
            return;
        }
        phase = Random.value * 6.28f;
        noiseSeed = Random.value * 100f;
        Build(mf, orig);
        All.Add(this);
    }

    void OnDestroy() { All.Remove(this); if (clothGo != null) Destroy(clothGo); }

    // ------------------------------------------------------------ construcción
    void Build(MeshFilter mf, Renderer orig)
    {
        Bounds lb = mf.sharedMesh.bounds;
        Vector3 size = lb.size;
        // eje fino = normal; U = ancho (x), V = el otro eje grande
        int thin = size.x <= size.y && size.x <= size.z ? 0 : (size.y <= size.z ? 1 : 2);
        int au = thin == 0 ? 2 : 0;
        int av = thin == 1 ? 2 : (thin == 0 ? 1 : 1);
        Vector3 lu = Vector3.zero, lv = Vector3.zero;
        lu[au] = 1f; lv[av] = 1f;
        Vector3 ls = transform.lossyScale;
        float wU = Mathf.Max(0.05f, size[au] * Mathf.Abs(ls[au]));
        float wV = Mathf.Max(0.05f, size[av] * Mathf.Abs(ls[av]));
        Vector3 uW = transform.TransformDirection(lu).normalized;
        Vector3 vW = transform.TransformDirection(lv).normalized;
        Vector3 centerW = transform.TransformPoint(lb.center);

        // textura
        Texture2D tex = null;
        if (textures != null && textures.Length > 0)
        {
            int i = textureIndex >= 0 ? Mathf.Clamp(textureIndex, 0, textures.Length - 1) : Random.Range(0, textures.Length);
            tex = textures[i];
        }
        if (tex == null && orig.sharedMaterial != null) tex = orig.sharedMaterial.mainTexture as Texture2D;
        float aspect = (tex != null && tex.height > 0) ? tex.width / (float)tex.height : wU / wV;
        if (tex != null) tex.wrapMode = TextureWrapMode.Clamp;

        // tamaño final sin deformar la textura
        float width, height;
        Vector2 uvMin = Vector2.zero, uvMax = Vector2.one;
        if (fit == FlagFit.MatchTexture)
        {
            height = flagHeight > 0.01f ? flagHeight : wV;
            width = height * aspect;
        }
        else if (fit == FlagFit.FitInsidePlane)
        {
            width = Mathf.Min(wU, wV * aspect);
            height = width / aspect;
        }
        else
        {
            width = wU; height = wV;
            float quadAspect = wU / wV;
            if (aspect > quadAspect) { float k = quadAspect / aspect; uvMin.x = (1f - k) * 0.5f; uvMax.x = 1f - uvMin.x; }
            else { float k = aspect / quadAspect; uvMin.y = (1f - k) * 0.5f; uvMax.y = 1f - uvMin.y; }
        }

        // el borde de sujeción se queda donde estaba en el plano
        Vector3 anchor, newCenter;
        switch (attach)
        {
            case FlagAttach.Left: anchor = centerW - uW * (wU * 0.5f); newCenter = anchor + uW * (width * 0.5f); break;
            case FlagAttach.Right: anchor = centerW + uW * (wU * 0.5f); newCenter = anchor - uW * (width * 0.5f); break;
            case FlagAttach.Top: anchor = centerW + vW * (wV * 0.5f); newCenter = anchor - vW * (height * 0.5f); break;
            default: anchor = centerW - vW * (wV * 0.5f); newCenter = anchor + vW * (height * 0.5f); break;
        }

        // malla en rejilla
        int su = segmentsU, sv = segmentsV;
        Vector3[] verts = new Vector3[(su + 1) * (sv + 1)];
        Vector2[] uvs = new Vector2[verts.Length];
        Vector3[] nrm = new Vector3[verts.Length];
        for (int j = 0; j <= sv; j++)
            for (int i = 0; i <= su; i++)
            {
                float u = i / (float)su, v = j / (float)sv;
                int k = j * (su + 1) + i;
                verts[k] = new Vector3((u - 0.5f) * width, (v - 0.5f) * height, 0f);
                float tu = flipX ? 1f - u : u, tv = flipY ? 1f - v : v;
                uvs[k] = new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, tu), Mathf.Lerp(uvMin.y, uvMax.y, tv));
                nrm[k] = Vector3.back;
            }
        int[] tris = new int[su * sv * 6];
        int t = 0;
        for (int j = 0; j < sv; j++)
            for (int i = 0; i < su; i++)
            {
                int a = j * (su + 1) + i, b = a + 1, c = a + su + 1, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
        Mesh mesh = new Mesh();
        mesh.name = "FlagMesh";
        mesh.vertices = verts; mesh.uv = uvs; mesh.normals = nrm; mesh.triangles = tris;
        mesh.RecalculateBounds();

        // objeto de tela (en la raíz del mundo, escala 1: la tela no admite escalas raras)
        clothGo = new GameObject(name + "_Cloth");
        baseRot = Quaternion.LookRotation(Vector3.Cross(uW, vW), vW);
        clothGo.transform.SetPositionAndRotation(newCenter, baseRot);
        center = newCenter;
        waveAxis = vW;

        Material mat = new Material(Shader.Find("Sprites/Default"));
        if (tex != null) mat.mainTexture = tex;
        mat.color = new Color(brightness, brightness, brightness, 1f);
        smr = clothGo.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.sharedMaterial = mat;
        smr.updateWhenOffscreen = true;
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cloth = clothGo.AddComponent<Cloth>();

        // coeficientes: el borde de sujeción queda fijo y el resto se libera poco a poco (evita pliegues bruscos)
        ClothSkinningCoefficient[] co = new ClothSkinningCoefficient[verts.Length];
        float free = Mathf.Max(width, height) * 1.6f;
        for (int j = 0; j <= sv; j++)
            for (int i = 0; i <= su; i++)
            {
                float u = i / (float)su, v = j / (float)sv;
                float edgeDist;
                switch (attach)
                {
                    case FlagAttach.Left: edgeDist = u; break;
                    case FlagAttach.Right: edgeDist = 1f - u; break;
                    case FlagAttach.Top: edgeDist = 1f - v; break;
                    default: edgeDist = v; break;
                }
                int k = j * (su + 1) + i;
                co[k].maxDistance = edgeDist <= 0.0001f ? 0f : free * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edgeDist / 0.30f));
                co[k].collisionSphereDistance = 0f;
            }
        cloth.coefficients = co;

        // propiedades según el uso
        cloth.useGravity = true;
        cloth.friction = 0.25f;
        cloth.sleepThreshold = 0f;
        cloth.worldVelocityScale = 0.4f;
        cloth.worldAccelerationScale = 0.3f;
        switch (style)
        {
            case FlagStyle.Spectator: cloth.stretchingStiffness = 0.9f; cloth.bendingStiffness = 0.15f; cloth.damping = 0.10f; break;
            case FlagStyle.Banner: cloth.stretchingStiffness = 0.95f; cloth.bendingStiffness = 0.45f; cloth.damping = 0.25f; break;
            default: cloth.stretchingStiffness = 0.95f; cloth.bendingStiffness = 0.25f; cloth.damping = 0.12f; break;
        }

        orig.enabled = false;                                   // se oculta el plano original
        foreach (Collider col in GetComponents<Collider>()) col.enabled = false;
    }

    // ------------------------------------------------------------ perturbaciones (disparos, explosiones, balón)
    public static void Disturb(Vector3 pos, Vector3 dir, float strength, float radius)
    {
        for (int i = 0; i < All.Count; i++)
        {
            FlagCloth f = All[i];
            if (f == null || f.cloth == null) continue;
            float d = Vector3.Distance(f.center, pos);
            if (d > radius) continue;
            Vector3 push = dir.normalized * strength * (1f - d / radius) + Vector3.up * strength * 0.25f * (1f - d / radius)
                           + Random.insideUnitSphere * strength * 0.2f;
            f.impulse = Vector3.ClampMagnitude(f.impulse + push, 140f);
        }
    }

    // un disparo que pasa cerca de la bandera (cualquier arma)
    public static void Shot(Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
        for (int i = 0; i < All.Count; i++)
        {
            FlagCloth f = All[i];
            if (f == null || f.cloth == null) continue;
            float t = Mathf.Clamp01(Vector3.Dot(f.center - a, ab) / len2);
            Vector3 closest = a + ab * t;
            float d = Vector3.Distance(f.center, closest);
            if (d > 4f) continue;
            float fall = 1f - d / 4f;
            f.impulse = Vector3.ClampMagnitude(f.impulse + ab.normalized * 60f * fall + Random.insideUnitSphere * 12f * fall, 140f);
        }
    }

    public static void Explosion(Vector3 p, float radius)
    {
        for (int i = 0; i < All.Count; i++)
        {
            FlagCloth f = All[i];
            if (f == null || f.cloth == null) continue;
            Vector3 dir = f.center - p;
            float d = dir.magnitude;
            if (d > radius * 2.5f) continue;
            f.impulse = Vector3.ClampMagnitude(f.impulse + dir.normalized * 110f * (1f - d / (radius * 2.5f)) + Vector3.up * 30f, 160f);
        }
    }

    // ------------------------------------------------------------ viento y rendimiento
    void Update()
    {
        if (cloth == null) return;
        float dt = Time.deltaTime;

        // rendimiento: lejos de la cámara no se simula
        if (Time.time >= nextLod)
        {
            nextLod = Time.time + 0.4f;
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            if (cam != null)
            {
                float d = Vector3.Distance(cam.position, center);
                cloth.enabled = d < simulateDistance;
                smr.enabled = d < simulateDistance * 2.5f;
            }
        }
        if (!cloth.enabled) return;

        // viento constante + ráfagas
        Vector3 dir = windDirectionOverride.sqrMagnitude > 0.001f ? windDirectionOverride.normalized : GlobalWindDirection.normalized;
        float t = Time.time * 0.6f + noiseSeed;
        float gust = Mathf.PerlinNoise(t, noiseSeed) * 0.9f + Mathf.PerlinNoise(t * 3.1f, noiseSeed + 7f) * 0.25f;
        float strength = GlobalWindStrength * windStrength * (0.55f + gust * gustiness);

        // balonazo: si el balón pasa rápido cerca, la bandera se agita
        MatchManager m = MatchManager.I;
        if (m != null && m.BallRb != null && !m.BallRb.isKinematic)
        {
            Vector3 bv = m.BallRb.velocity;
            float sp = bv.magnitude;
            if (sp > 9f)
            {
                float d = Vector3.Distance(center, m.Ball.position);
                if (d < 4.5f) impulse = Vector3.ClampMagnitude(impulse + bv.normalized * Mathf.Min(sp * 2.2f, 55f) * (1f - d / 4.5f) * dt * 20f, 140f);
            }
        }

        cloth.externalAcceleration = dir * strength + impulse;
        cloth.randomAcceleration = new Vector3(Mathf.PerlinNoise(t * 2f, 1f) - 0.5f, Mathf.PerlinNoise(t * 2f, 5f) - 0.5f, Mathf.PerlinNoise(t * 2f, 9f) - 0.5f) * strength * 0.7f * gustiness;
        impulse = Vector3.MoveTowards(impulse, Vector3.zero, 150f * dt);

        // el hincha agita su bandera
        if (style == FlagStyle.Spectator && clothGo != null)
            clothGo.transform.rotation = Quaternion.AngleAxis(Mathf.Sin(Time.time * waveFrequency * 6.28f + phase) * waveAmplitude, waveAxis) * baseRot;
    }
}

// Opcional: ponlo en cualquier objeto de la escena para controlar el viento de TODAS las banderas desde el Inspector.
public class FlagWindSettings : MonoBehaviour
{
    public Vector3 direction = new Vector3(1f, 0.05f, 0.35f);
    [Tooltip("Fuerza del viento (aceleración en m/s²). 4 = suave, 7 = normal, 12 = temporal")]
    public float strength = 7f;

    void Update()
    {
        FlagCloth.GlobalWindDirection = direction;
        FlagCloth.GlobalWindStrength = strength;
    }
}
