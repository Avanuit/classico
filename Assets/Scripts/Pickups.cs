using System.Collections.Generic;
using UnityEngine;

// Objeto de loot en la cancha (con rayo de luz y anillo, como en Fortnite). Lo recoge quien pase cerca.
public class WeaponPickup : MonoBehaviour
{
    public static readonly List<WeaponPickup> All = new List<WeaponPickup>();
    const float Lifetime = 35f;

    public ItemType type;
    float born;
    Transform model;

    public static WeaponPickup Create(ItemType type, Vector3 groundPos)
    {
        GameObject go = new GameObject("Loot_" + Items.Name(type));
        go.transform.position = groundPos;
        WeaponPickup p = go.AddComponent<WeaponPickup>();
        p.Init(type);
        return p;
    }

    static Material Glow(Color c)
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = c;
        return mat;
    }

    void Init(ItemType t)
    {
        type = t; born = Time.time;
        Color c = Items.Rarity(t);

        Transform mz;
        GameObject w = WeaponModels.Build(t, out mz);
        w.transform.SetParent(transform, false);
        w.transform.localScale = Vector3.one * (t == ItemType.Grenade ? 2.2f : 1.4f);
        model = w.transform;

        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(beam.GetComponent<Collider>());
        beam.transform.SetParent(transform, false);
        beam.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        beam.transform.localScale = new Vector3(0.35f, 1.6f, 0.35f);
        Renderer br = beam.GetComponent<Renderer>();
        br.sharedMaterial = Glow(new Color(c.r, c.g, c.b, 0.16f));
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        ring.transform.localScale = new Vector3(1.6f, 0.01f, 1.6f);
        Renderer rr = ring.GetComponent<Renderer>();
        rr.sharedMaterial = Glow(new Color(c.r, c.g, c.b, 0.4f));
        rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameObject lg = new GameObject("luz");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = 5f; l.intensity = 1.6f;

        All.Add(this);
    }

    void OnDestroy() { All.Remove(this); }
    public void Remove() { All.Remove(this); Destroy(gameObject); }

    public static WeaponPickup Nearest(Vector3 pos, float maxDist)
    {
        WeaponPickup best = null;
        float bd = maxDist;
        for (int i = 0; i < All.Count; i++)
        {
            Vector3 d = All[i].transform.position - pos; d.y = 0f;
            if (d.magnitude < bd) { bd = d.magnitude; best = All[i]; }
        }
        return best;
    }

    void Update()
    {
        MatchManager m = MatchManager.I;
        if (model == null) return;
        float age = Time.time - born;
        model.localRotation = Quaternion.Euler(0f, age * 90f, 0f);
        model.localPosition = new Vector3(0f, 0.95f + Mathf.Sin(age * 2.2f) * 0.08f, 0f);
        bool blink = age > Lifetime - 5f && ((int)(Time.time * 6f) % 2 == 0);
        model.gameObject.SetActive(!blink);
        if (age > Lifetime) { Remove(); return; }
        if (m == null || (m.State != MatchState.Playing && m.State != MatchState.SetPiece)) return;

        for (int t = 0; t < 2; t++)
        {
            List<PlayerAI> list = m.Teams[t];
            for (int i = 0; i < list.Count; i++)
            {
                PlayerAI p = list[i];
                if (p.IsDead) continue;
                Vector3 d = p.transform.position - transform.position; d.y = 0f;
                if (d.magnitude > 1.5f) continue;
                PlayerCombat pc = p.Combat;
                if (pc != null && pc.PickUp(type))
                {
                    m.OnPickup(pc, type);
                    Remove();
                    return;
                }
            }
        }
    }
}

// Hace aparecer armas y granadas periódicamente en la cancha mientras se juega.
public class PickupSpawner : MonoBehaviour
{
    public float interval = 10f;
    public int maxActive = 4;
    float next;

    void Update()
    {
        MatchManager m = MatchManager.I;
        if (m == null) return;
        if (m.State != MatchState.Playing) { next = Time.time + 6f; return; }
        if (Time.time < next) return;
        next = Time.time + interval * Random.Range(0.8f, 1.2f);
        if (WeaponPickup.All.Count >= maxActive) return;
        Spawn(m);
    }

    void Spawn(MatchManager m)
    {
        float r = Random.value;
        ItemType t = r < 0.35f ? ItemType.Glock : (r < 0.55f ? ItemType.AK47 : ItemType.Grenade);
        Vector3 pos = Vector3.zero;
        for (int tries = 0; tries < 12; tries++)
        {
            pos = new Vector3(Random.Range(-m.HL + 12f, m.HL - 12f), 0f, Random.Range(-m.HW + 6f, m.HW - 6f));
            bool ok = true;
            for (int i = 0; i < WeaponPickup.All.Count; i++)
            {
                Vector3 d = WeaponPickup.All[i].transform.position - pos; d.y = 0f;
                if (d.magnitude < 9f) { ok = false; break; }
            }
            if (ok) break;
        }
        RaycastHit h;
        pos.y = Physics.Raycast(new Vector3(pos.x, 8f, pos.z), Vector3.down, out h, 20f, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : 0f;
        WeaponPickup.Create(t, pos);
        Fx.Flash(pos + Vector3.up * 0.6f, 0.9f);
        m.Notice("¡Nueva arma en la cancha!");
    }

    public static void ClearAll()
    {
        foreach (WeaponPickup p in new List<WeaponPickup>(WeaponPickup.All)) if (p != null) p.Remove();
    }
}
