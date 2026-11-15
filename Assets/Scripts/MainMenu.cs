using UnityEngine;

// Escena del MENÚ PRINCIPAL (se arma sola en tiempo de ejecución, por eso la escena queda vacía):
// atardecer caleño con torres de luz azules y verdes, balón girando, brasas, confeti y botones animados con marco.
// Crea la escena con el menú de Unity:  Clasico > Crear escena de Menú Principal.
public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "SampleScene";

    enum Page { Home, Team, Settings, Controls, Credits }
    Page page = Page.Home;
    float creditsScroll, startTime, nextConfetti = 3f;
    Camera cam;
    Transform ball;
    readonly Transform[] beams = new Transform[4];
    Vector3 camBase = new Vector3(0f, 1.6f, -7.5f);
    ParticleSystem confetti;
    int tipIdx;
    float tipTime;
    bool sceneMissing;

    static readonly string[] tips =
    {
        "Las granadas también empujan el balón.",
        "Un disparo raso con más de 50% de fuerza va directo a la mira.",
        "Con un arma en la mano no puedes controlar el balón.",
        "Los centros de esquina caen al área: ¡cabecea con el portero!",
        "Si te eliminan, reapareces en tu campo a los 5 segundos.",
        "Pulsa V para jugar en primera persona."
    };

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameSettings.Load();
        GameSession.HasChoice = false;
        BuildScene();
        if (GameAudio.I == null) gameObject.AddComponent<GameAudio>();
        GameAudio.I.PlayMusic(MusicTrack.Menu, 1.5f);
        startTime = Time.time;
        sceneMissing = !Application.CanStreamedLevelBeLoaded(gameSceneName);
    }

    // ============================================================ escenario 3D
    static Material Unlit(Texture2D tex, Color c)
    {
        Material m = new Material(Shader.Find("Sprites/Default"));
        m.color = c;
        if (tex != null) m.mainTexture = tex;
        return m;
    }

    GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(t);
        g.name = name;
        Destroy(g.GetComponent<Collider>());
        g.transform.position = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    void BuildScene()
    {
        cam = Camera.main;
        if (cam == null)
        {
            GameObject cg = new GameObject("Main Camera");
            cg.tag = "MainCamera";
            cam = cg.AddComponent<Camera>();
            cg.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.03f, 0.08f);
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 200f;
        cam.transform.position = camBase;
        cam.transform.rotation = Quaternion.LookRotation(new Vector3(1.4f, 1.4f, 0f) - camBase);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.14f, 0.22f);

        // cielo de atardecer
        Prim(PrimitiveType.Quad, "Cielo", new Vector3(0f, 9f, 42f), new Vector3(120f, 66f, 1f), Unlit(SkyTexture(), Color.white));

        // horizonte: gradas oscuras con lucecitas
        Material dark = Unlit(null, new Color(0.03f, 0.04f, 0.09f));
        Material win = Unlit(null, new Color(1f, 0.85f, 0.5f, 0.85f));
        for (float x = -48f; x <= 48f; x += 2.1f)
        {
            float h = Random.Range(2f, 8f);
            Prim(PrimitiveType.Cube, "Grada", new Vector3(x, h * 0.5f - 0.5f, 30f), new Vector3(2f, h, 1f), dark);
            if (Random.value < 0.6f)
                Prim(PrimitiveType.Quad, "Luz", new Vector3(x + Random.Range(-0.6f, 0.6f), Random.Range(0.5f, h - 0.5f), 29.4f), new Vector3(0.2f, 0.2f, 1f), win);
        }

        // torres de luz con haces azules (Millonarios) y verdes (Nacional)
        Texture2D beamTex = BeamTexture();
        float[] tx = { -34f, -14f, 14f, 34f };
        for (int i = 0; i < 4; i++)
        {
            Prim(PrimitiveType.Cube, "Torre", new Vector3(tx[i], 7f, 28f), new Vector3(0.5f, 14f, 0.5f), dark);
            Prim(PrimitiveType.Quad, "Foco", new Vector3(tx[i], 14.4f, 27.8f), new Vector3(4f, 1.2f, 1f), Unlit(null, new Color(1f, 0.95f, 0.8f, 0.95f)));
            GameObject pivot = new GameObject("Haz" + i);
            pivot.transform.position = new Vector3(tx[i], 14.2f, 27.5f);
            Color bc = i < 2 ? new Color(0.35f, 0.58f, 1f, 0.28f) : new Color(0.3f, 1f, 0.5f, 0.24f);
            GameObject q = Prim(PrimitiveType.Quad, "HazQuad", Vector3.zero, new Vector3(5f, 40f, 1f), Unlit(beamTex, bc));
            q.transform.SetParent(pivot.transform, false);
            q.transform.localPosition = new Vector3(0f, -20f, 0f);
            beams[i] = pivot.transform;
        }

        // piso oscuro brillante
        Material fm = new Material(Shader.Find("Standard"));
        fm.color = new Color(0.02f, 0.03f, 0.05f);
        fm.SetFloat("_Glossiness", 0.85f);
        fm.SetFloat("_Metallic", 0.5f);
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Destroy(floor.GetComponent<Collider>());
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(8f, 1f, 8f);
        floor.GetComponent<Renderer>().sharedMaterial = fm;

        // balón con textura de gajos
        Material bm = new Material(Shader.Find("Standard"));
        bm.mainTexture = BallTexture();
        bm.SetFloat("_Glossiness", 0.6f);
        ball = Prim(PrimitiveType.Sphere, "Balon", new Vector3(1.8f, 1.45f, 0f), Vector3.one * 2.2f, bm).transform;

        // luces: azul a un lado, verde al otro
        MakeLight(LightType.Point, new Vector3(-4f, 2.6f, -3f), new Color(0.2f, 0.45f, 1f), 3.2f, 15f);
        MakeLight(LightType.Point, new Vector3(7f, 2.6f, -3f), new Color(0.15f, 0.9f, 0.4f), 3.2f, 15f);
        MakeLight(LightType.Point, new Vector3(1.8f, 5f, 2f), new Color(1f, 0.9f, 0.8f), 1.3f, 14f);

        BuildParticles();
    }

    static void MakeLight(LightType t, Vector3 pos, Color c, float intensity, float range)
    {
        GameObject g = new GameObject("Luz");
        g.transform.position = pos;
        Light l = g.AddComponent<Light>();
        l.type = t; l.color = c; l.intensity = intensity; l.range = range;
    }

    void BuildParticles()
    {
        // brasas que suben
        GameObject eg = new GameObject("Brasas");
        eg.transform.position = new Vector3(0f, 0.2f, 4f);
        eg.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        ParticleSystem ep = eg.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule em = ep.main;
        em.loop = true;
        em.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        em.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        em.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        em.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f, 0.9f), new Color(1f, 0.45f, 0.2f, 0.8f));
        em.maxParticles = 300;
        em.gravityModifier = -0.04f;
        em.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule ee = ep.emission;
        ee.rateOverTime = 34f;
        ParticleSystem.ShapeModule es = ep.shape;
        es.shapeType = ParticleSystemShapeType.Box;
        es.scale = new Vector3(26f, 8f, 0.2f);
        ParticleSystem.ColorOverLifetimeModule ec = ep.colorOverLifetime;
        ec.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        ec.color = g;
        eg.GetComponent<ParticleSystemRenderer>().sharedMaterial = Unlit(UIKit.CircleTex, Color.white);

        // confeti (se lanza a ráfagas)
        GameObject cg = new GameObject("Confeti");
        cg.transform.position = new Vector3(1.8f, 9f, 3f);
        cg.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        confetti = cg.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule cm = confetti.main;
        cm.loop = false; cm.playOnAwake = false;
        cm.startLifetime = new ParticleSystem.MinMaxCurve(5f, 7f);
        cm.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.5f);
        cm.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        cm.gravityModifier = 0.25f;
        cm.maxParticles = 400;
        cm.simulationSpace = ParticleSystemSimulationSpace.World;
        Gradient cg2 = new Gradient();
        cg2.SetKeys(new GradientColorKey[]
        {
            new GradientColorKey(new Color(0.2f, 0.45f, 1f), 0f), new GradientColorKey(new Color(0.25f, 0.9f, 0.45f), 0.33f),
            new GradientColorKey(Color.white, 0.66f), new GradientColorKey(new Color(1f, 0.82f, 0.25f), 1f)
        }, new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        ParticleSystem.MinMaxGradient mg = new ParticleSystem.MinMaxGradient(cg2);
        mg.mode = ParticleSystemGradientMode.RandomColor;
        cm.startColor = mg;
        ParticleSystem.EmissionModule ce = confetti.emission;
        ce.rateOverTime = 0f;
        ParticleSystem.ShapeModule cs = confetti.shape;
        cs.shapeType = ParticleSystemShapeType.Box;
        cs.scale = new Vector3(9f, 3f, 0.2f);
        ParticleSystem.RotationOverLifetimeModule cr = confetti.rotationOverLifetime;
        cr.enabled = true;
        cr.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        cg.GetComponent<ParticleSystemRenderer>().sharedMaterial = Unlit(null, Color.white);
    }

    // ============================================================ texturas procedurales
    static Texture2D SkyTexture()
    {
        Texture2D t = new Texture2D(1, 128, TextureFormat.RGB24, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] stops = { new Color(1f, 0.55f, 0.25f), new Color(0.85f, 0.25f, 0.45f), new Color(0.27f, 0.10f, 0.38f), new Color(0.02f, 0.04f, 0.14f) };
        float[] at = { 0f, 0.22f, 0.52f, 1f };
        for (int i = 0; i < 128; i++)
        {
            float v = i / 127f;
            int k = 0;
            while (k < 2 && v > at[k + 1]) k++;
            float u = Mathf.InverseLerp(at[k], at[k + 1], v);
            t.SetPixel(0, i, Color.Lerp(stops[k], stops[k + 1], u));
        }
        t.Apply();
        return t;
    }

    static Texture2D BeamTexture()
    {
        Texture2D t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Mathf.Abs(x - 31.5f) / 31.5f;
                float a = Mathf.Pow(1f - d, 2.2f) * Mathf.Lerp(1f, 0.15f, 1f - y / 63f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    // balón de gajos: 12 pentágonos negros y 20 hexágonos blancos (icosaedro truncado)
    static Texture2D BallTexture()
    {
        int W = 512, H = 256;
        Texture2D t = new Texture2D(W, H, TextureFormat.RGB24, true);
        float phi = (1f + Mathf.Sqrt(5f)) * 0.5f, ip = 1f / phi;
        Vector3[] pent = new Vector3[12];
        Vector3[] hex = new Vector3[20];
        int pi = 0, hi = 0;
        for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                pent[pi++] = new Vector3(0f, a, b * phi).normalized;
                pent[pi++] = new Vector3(a, b * phi, 0f).normalized;
                pent[pi++] = new Vector3(b * phi, 0f, a).normalized;
                hex[hi++] = new Vector3(0f, a * ip, b * phi).normalized;
                hex[hi++] = new Vector3(a * ip, b * phi, 0f).normalized;
                hex[hi++] = new Vector3(b * phi, 0f, a * ip).normalized;
                for (int c = -1; c <= 1; c += 2) hex[hi++] = new Vector3(a, b, c).normalized;
            }
        Color[] px = new Color[W * H];
        Color black = new Color(0.07f, 0.07f, 0.09f), white = new Color(0.97f, 0.97f, 0.97f), seamC = new Color(0.3f, 0.3f, 0.35f);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float lon = ((x + 0.5f) / W - 0.5f) * Mathf.PI * 2f, lat = ((y + 0.5f) / H - 0.5f) * Mathf.PI;
                Vector3 d = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
                float dp = -1f, h1 = -1f, h2 = -1f;
                for (int i = 0; i < 12; i++) { float v = Vector3.Dot(d, pent[i]); if (v > dp) dp = v; }
                for (int j = 0; j < 20; j++)
                {
                    float v = Vector3.Dot(d, hex[j]);
                    if (v > h1) { h2 = h1; h1 = v; } else if (v > h2) h2 = v;
                }
                float aP = Mathf.Acos(Mathf.Clamp(dp, -1f, 1f)) / 0.295f, aH = Mathf.Acos(Mathf.Clamp(h1, -1f, 1f)) / 0.358f;
                Color c = aP < aH ? black : white;
                if (Mathf.Abs(aP - aH) < 0.04f || (aP >= aH && h1 - h2 < 0.012f)) c = Color.Lerp(c, seamC, 0.85f);
                px[y * W + x] = c;
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    // ============================================================ animación 3D
    void Update()
    {
        float dt = Time.deltaTime, time = Time.time;
        if (ball != null)
        {
            ball.Rotate(new Vector3(8f, 28f, 3f) * dt, Space.World);
            Vector3 p = ball.position; p.y = 1.45f + Mathf.Sin(time * 1.4f) * 0.08f; ball.position = p;
        }
        for (int i = 0; i < 4; i++)
            if (beams[i] != null)
            {
                float dir = i < 2 ? 1f : -1f;
                beams[i].rotation = Quaternion.Euler(0f, 0f, dir * (18f + 8f * Mathf.Sin(time * 0.5f + i)));
            }
        if (cam != null)
        {
            Vector2 m = new Vector2(Input.mousePosition.x / Screen.width - 0.5f, Input.mousePosition.y / Screen.height - 0.5f);
            Vector3 tp = camBase + new Vector3(m.x * 0.8f, m.y * 0.4f, 0f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, tp, Mathf.Clamp01(3f * dt));
            Vector3 look = new Vector3(1.4f + m.x * 0.3f, 1.4f, 0f);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look - cam.transform.position), Mathf.Clamp01(4f * dt));
        }
        if (time - startTime > nextConfetti && confetti != null)
        {
            nextConfetti = time - startTime + 7f;
            confetti.transform.position = new Vector3(Random.Range(-2f, 5f), 9f, 3f);
            confetti.Emit(70);
        }
        if (Time.time - tipTime > 5f) { tipTime = Time.time; tipIdx = (tipIdx + 1) % tips.Length; }
        if (Input.GetKeyDown(KeyCode.Escape) && page != Page.Home) page = Page.Home;
    }

    // ============================================================ interfaz
    static float Ease(float x) { return 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f); }

    void OnGUI()
    {
        UIKit.Begin();
        float S = UIKit.S, W = Screen.width, H = Screen.height;
        float t = Time.time - startTime;

        DrawTitle(t, S, W);

        if (page == Page.Home) DrawHome(t, S, W, H);
        else if (page == Page.Team)
        {
            int r = MenuScreens.TeamSelect(true);
            if (r == 0 || r == 1 || r == 2) StartGame(r);
            else if (r == -2) page = Page.Home;
        }
        else if (page == Page.Settings) { if (MenuScreens.Settings()) page = Page.Home; }
        else if (page == Page.Controls) { if (MenuScreens.Controls()) page = Page.Home; }
        else if (page == Page.Credits) { if (MenuScreens.Credits(ref creditsScroll)) page = Page.Home; }
    }

    void DrawTitle(float t, float S, float W)
    {
        float e = Ease(t / 1.1f);
        float y = 18f * S - (1f - e) * 80f * S;
        float bob = Mathf.Sin(Time.time * 1.6f) * 4f * S;
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, e);
        UIKit.DrawCrest(new Rect(W * 0.5f - 400f * S, y + 6f * S + bob, 130f * S, 130f * S), 0, 1f);
        UIKit.DrawCrest(new Rect(W * 0.5f + 270f * S, y + 6f * S - bob, 130f * S, 130f * S), 1, 1f);
        UIKit.Outline(new Rect(0f, y, W, 120f * S), "CLÁSICO", 104, Color.white, new Color(0.04f, 0.06f, 0.18f));
        UIKit.Text(new Rect(0f, y + 112f * S, W, 34f * S), "NACIONAL   vs   MILLONARIOS", 26, UIKit.Gold);
        UIKit.Text(new Rect(0f, y + 142f * S, W, 26f * S), "NOCHE EN CALI", 17, new Color(1f, 1f, 1f, 0.75f));
        GUI.color = old;
    }

    void DrawHome(float t, float S, float W, float H)
    {
        string[] labels = { "JUGAR", "AJUSTES", "CONTROLES", "CRÉDITOS", "SALIR" };
        Color[] cols = { UIKit.Green, UIKit.Blue, UIKit.Slate, UIKit.Purple, UIKit.Red };
        float bw = 340f * S, bh = 60f * S, gap = 18f * S;
        float y0 = H * 0.34f;
        for (int i = 0; i < labels.Length; i++)
        {
            float e = Ease((t - 0.55f - i * 0.1f) / 0.55f);
            float x = Mathf.Lerp(-bw - 20f * S, 70f * S, e);
            if (UIKit.Button(new Rect(x, y0 + i * (bh + gap), bw, bh), labels[i], cols[i], i == 0 ? 30 : 24))
            {
                if (i == 0) page = Page.Team;
                else if (i == 1) page = Page.Settings;
                else if (i == 2) page = Page.Controls;
                else if (i == 3) { page = Page.Credits; creditsScroll = 0f; }
                else GameSettings.Quit();
            }
        }
        UIKit.Text(new Rect(0f, H - 48f * S, W, 30f * S), "Consejo: " + tips[tipIdx], 17, new Color(1f, 1f, 1f, 0.85f));
        UIKit.Text(new Rect(W - 330f * S, H - 34f * S, 316f * S, 24f * S), "v1.0 · Unity 2019.4", 13, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleRight, false);
        if (sceneMissing)
            UIKit.Text(new Rect(0f, H - 84f * S, W, 30f * S), "Falta la escena '" + gameSceneName + "' en Build Settings (menú Clasico > Crear escena de Menú Principal).", 15, new Color(1f, 0.6f, 0.5f));
    }

    void StartGame(int r)
    {
        GameSession.HasChoice = true;
        GameSession.Team = r == 2 ? -1 : r;
        GameSettings.Save();
        ScreenOverlay.LoadScene(gameSceneName);
    }
}
