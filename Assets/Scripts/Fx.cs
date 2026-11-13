using System.Collections.Generic;
using UnityEngine;

// Efectos visuales simples hechos por código: trazadoras de bala, fogonazo, chispas e explosión (con luz).
public class Fx : MonoBehaviour
{
    public static Fx I;

    class Item
    {
        public GameObject go; public Renderer r; public LineRenderer lr; public Light light;
        public float born, life, s0, s1, light0, alpha0; public Color color; public Vector3 vel; public bool flat;
    }

    readonly List<Item> items = new List<Item>();
    Material mat;
    MaterialPropertyBlock mpb;

    void Awake()
    {
        I = this;
        mat = new Material(Shader.Find("Sprites/Default"));
        mpb = new MaterialPropertyBlock();
    }

    void OnDestroy() { if (I == this) I = null; }

    static Item NewShape(PrimitiveType type, Vector3 pos, float s0, float s1, Color c, float life, Vector3 vel)
    {
        Item it = new Item();
        it.go = GameObject.CreatePrimitive(type);
        Object.Destroy(it.go.GetComponent<Collider>());
        it.r = it.go.GetComponent<Renderer>();
        it.r.sharedMaterial = I.mat;
        it.r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        it.r.receiveShadows = false;
        it.go.transform.position = pos;
        it.born = Time.time; it.life = life; it.s0 = s0; it.s1 = s1; it.color = c; it.alpha0 = c.a; it.vel = vel;
        I.items.Add(it);
        return it;
    }

    public static void Tracer(Vector3 a, Vector3 b, Color c)
    {
        if (I == null) return;
        Item it = new Item();
        it.go = new GameObject("tracer");
        it.lr = it.go.AddComponent<LineRenderer>();
        it.lr.sharedMaterial = I.mat;
        it.lr.positionCount = 2;
        it.lr.SetPosition(0, a); it.lr.SetPosition(1, b);
        it.lr.startWidth = 0.035f; it.lr.endWidth = 0.012f;
        it.lr.startColor = c; it.lr.endColor = new Color(c.r, c.g, c.b, 0f);
        it.lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        it.born = Time.time; it.life = 0.07f; it.color = c; it.alpha0 = c.a;
        I.items.Add(it);
    }

    public static void Flash(Vector3 p, float size)
    {
        if (I == null) return;
        NewShape(PrimitiveType.Sphere, p, size, size * 0.3f, new Color(1f, 0.85f, 0.4f, 0.95f), 0.06f, Vector3.zero);
    }

    public static void Impact(Vector3 p, Vector3 normal, Color c)
    {
        if (I == null) return;
        for (int i = 0; i < 4; i++)
        {
            Vector3 v = (normal + Random.insideUnitSphere * 0.8f) * Random.Range(1.2f, 3f);
            NewShape(PrimitiveType.Sphere, p, 0.07f, 0.02f, c, 0.22f, v);
        }
    }

    public static void Explosion(Vector3 p, float radius)
    {
        if (I == null) return;
        Item fire = NewShape(PrimitiveType.Sphere, p, 0.4f, radius * 1.7f, new Color(1f, 0.55f, 0.15f, 0.85f), 0.45f, Vector3.zero);
        GameObject lg = new GameObject("luz");
        lg.transform.SetParent(fire.go.transform, false);
        fire.light = lg.AddComponent<Light>();
        fire.light.type = LightType.Point; fire.light.color = new Color(1f, 0.6f, 0.25f);
        fire.light.range = radius * 3f; fire.light.intensity = 0f; fire.light0 = 6f;
        Item ring = NewShape(PrimitiveType.Cylinder, p + Vector3.up * 0.05f, 0.5f, radius * 3.4f, new Color(1f, 1f, 1f, 0.35f), 0.35f, Vector3.zero);
        ring.flat = true;
        for (int i = 0; i < 7; i++)
        {
            Vector3 v = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(1.5f, 3.2f), Random.Range(-1.2f, 1.2f));
            NewShape(PrimitiveType.Sphere, p + Random.insideUnitSphere * 0.6f, 0.7f, radius * 0.9f, new Color(0.22f, 0.22f, 0.22f, 0.5f), Random.Range(1.0f, 1.6f), v);
        }
    }

    void Update()
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            Item it = items[i];
            float u = (Time.time - it.born) / it.life;
            if (u >= 1f)
            {
                if (it.go != null) Destroy(it.go);
                items.RemoveAt(i);
                continue;
            }
            float a = it.alpha0 * (1f - u);
            if (it.lr != null)
            {
                Color c = it.color; c.a = a;
                it.lr.startColor = c; it.lr.endColor = new Color(c.r, c.g, c.b, 0f);
                continue;
            }
            float e = 1f - (1f - u) * (1f - u);
            float s = Mathf.Lerp(it.s0, it.s1, e);
            it.go.transform.localScale = it.flat ? new Vector3(s, 0.01f, s) : new Vector3(s, s, s);
            it.go.transform.position += it.vel * Time.deltaTime;
            Color col = it.color; col.a = a;
            mpb.SetColor("_Color", col);
            it.r.SetPropertyBlock(mpb);
            if (it.light != null) it.light.intensity = it.light0 * (1f - u);
        }
    }
}
