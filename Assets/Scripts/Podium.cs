using System.Collections.Generic;
using UnityEngine;

// Podio final: los 3 jugadores con más bajas en el centro de la cancha, sobre tarimas, con poses de héroe,
// focos de color y confeti. La cámara orbita lento (PlayerController) y el HUD pone sus etiquetas.
public class Podium : MonoBehaviour
{
    public static Podium I;

    public class Entry
    {
        public PlayerAI ai;
        public int place, kills, deaths;
        public string label;
    }

    public readonly List<Entry> Entries = new List<Entry>();
    public Vector3 Center;
    readonly List<GameObject> props = new List<GameObject>();
    readonly List<PlayerCombat> hidden = new List<PlayerCombat>();
    readonly List<PlayerCombat> everyone = new List<PlayerCombat>();
    ParticleSystem confetti;

    public static Podium Build(MatchManager m)
    {
        Clear();
        GameObject go = new GameObject("Podio");
        Podium p = go.AddComponent<Podium>();
        I = p;
        p.Setup(m);
        return p;
    }

    public static void Clear()
    {
        if (I == null) return;
        I.Restore();
        Destroy(I.gameObject);
        I = null;
    }

    static Material Mat(Color c)
    {
        Shader sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.6f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.4f);
        return m;
    }

    void Setup(MatchManager m)
    {
        for (int t = 0; t < 2; t++)
            for (int i = 0; i < m.Teams[t].Count; i++)
            {
                PlayerCombat c = m.Teams[t][i].Combat;
                if (c != null) everyone.Add(c);
            }
        everyone.Sort((a, b) =>
        {
            if (a.kills != b.kills) return b.kills.CompareTo(a.kills);
            return a.deaths.CompareTo(b.deaths);
        });

        float gy = m.Teams[0][0].LastGroundY;
        Center = new Vector3(0f, gy, 0f);
        float[] heights = { 0.85f, 0.55f, 0.30f };
        float[] zs = { 0f, -2.2f, 2.2f };
        Color[] colors = { new Color(1f, 0.8f, 0.2f), new Color(0.82f, 0.85f, 0.9f), new Color(0.82f, 0.52f, 0.28f) };
        Color[] spots = { new Color(1f, 0.85f, 0.4f), new Color(0.75f, 0.85f, 1f), new Color(1f, 0.65f, 0.35f) };
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        for (int i = 0; i < 3 && i < everyone.Count; i++)
        {
            PlayerCombat pc = everyone[i];
            PlayerAI ai = pc.AI;

            // tarima
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Tarima" + (i + 1);
            block.transform.position = new Vector3(Center.x, gy + heights[i] * 0.5f, zs[i]);
            block.transform.localScale = new Vector3(1.7f, heights[i], 1.7f);
            block.GetComponent<Renderer>().sharedMaterial = Mat(colors[i]);
            props.Add(block);

            // número en el frente de la tarima
            GameObject tg = new GameObject("Numero" + (i + 1));
            tg.transform.position = new Vector3(Center.x - 0.87f, gy + heights[i] * 0.5f, zs[i]);
            tg.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            TextMesh tm = tg.AddComponent<TextMesh>();
            tm.text = (i + 1).ToString();
            tm.fontSize = 100;
            tm.characterSize = 0.1f;
            tg.transform.localScale = Vector3.one * (heights[i] * 0.55f);
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.1f, 0.07f, 0.02f);
            if (font != null) { tm.font = font; tg.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
            props.Add(tg);

            // foco de color
            GameObject lg = new GameObject("Foco" + (i + 1));
            lg.transform.position = new Vector3(Center.x + 2.5f, gy + 7f, zs[i]);
            lg.transform.rotation = Quaternion.LookRotation(new Vector3(Center.x, gy + 1.4f, zs[i]) - lg.transform.position);
            Light l = lg.AddComponent<Light>();
            l.type = LightType.Spot; l.spotAngle = 36f; l.range = 22f; l.intensity = 7f; l.color = spots[i];
            props.Add(lg);

            // el jugador sube a su tarima y posa
            pc.ReviveForKickoff();
            pc.invulnerableUntil = 0f;
            pc.equipped = ItemType.Ball;
            pc.hideBody = false; pc.hideWeaponVisual = true;
            ai.Teleport(new Vector3(Center.x, gy, zs[i]), new Vector3(-1f, 0f, 0f));
            pc.heroStance = i == 0 ? 4 : (i == 1 ? 5 : 6);

            Entry e = new Entry();
            e.ai = ai; e.place = i + 1; e.kills = pc.kills; e.deaths = pc.deaths;
            e.label = (ai.teamId == 0 ? "MILLONARIOS" : "NACIONAL") + " #" + ai.index;
            Entries.Add(e);
        }

        // el resto del equipo se oculta para una toma limpia
        for (int i = 3; i < everyone.Count; i++)
        {
            PlayerCombat pc = everyone[i];
            pc.ReviveForKickoff();
            pc.invulnerableUntil = 0f;
            pc.hideBody = true; pc.hideWeaponVisual = true;
            hidden.Add(pc);
        }

        // balón de adorno delante del podio
        m.BallRb.isKinematic = false;
        m.BallRb.velocity = Vector3.zero;
        m.BallRb.angularVelocity = Vector3.zero;
        m.BallRb.isKinematic = true;
        m.Ball.position = new Vector3(Center.x - 1.7f, gy + 0.12f, 0f);

        confetti = Fx.CreateConfetti(new Vector3(Center.x, gy + 9f, 0f), new Vector3(10f, 8f, 0.2f), 90f, 7f);
    }

    void Restore()
    {
        for (int i = 0; i < props.Count; i++) if (props[i] != null) Destroy(props[i]);
        props.Clear();
        for (int i = 0; i < everyone.Count; i++)
            if (everyone[i] != null) { everyone[i].heroStance = 0; everyone[i].hideBody = false; everyone[i].hideWeaponVisual = false; }
        hidden.Clear();
        if (confetti != null) Destroy(confetti.gameObject);
    }
}
