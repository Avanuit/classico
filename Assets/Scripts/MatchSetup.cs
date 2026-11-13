using System.Collections.Generic;
using UnityEngine;

public enum ColorMethod { BonesAuto, TextureColor }

// Pon este script en el GameObject "MatchManager" (junto a StadiumFitter).
// Asigna playerPrefab y ballPrefab. Crea los 22 jugadores, la pelota, el árbitro (MatchManager) y el HUD.
public class MatchSetup : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject ballPrefab;

    [Header("Tamaños reales")]
    public float playerHeight = 1.8f;
    public float ballDiameter = 0.22f;
    [Tooltip("Si los pies flotan sobre el césped baja este valor (ej: -0.03); si se hunden, súbelo")]
    public float footOffset = -0.01f;

    [Header("Colores")]
    public Color millonariosColor = new Color(0.05f, 0.25f, 0.8f);   // azul
    public Color nacionalColor    = new Color(0.0f, 0.6f, 0.2f);     // verde
    public Color gkMillonarios    = new Color(0.95f, 0.8f, 0.1f);    // portero
    public Color gkNacional       = new Color(0.9f, 0.4f, 0.0f);

    [Header("Camiseta")]
    [Tooltip("BonesAuto: separa torso y brazos por huesos (recomendado). TextureColor: repinta píxeles parecidos al color original")]
    public ColorMethod colorMethod = ColorMethod.BonesAuto;
    public Color originalShirtColor = Color.white;       // solo para TextureColor
    [Range(0.05f, 0.6f)] public float colorTolerance = 0.25f;

    [Header("Modo de juego")]
    [Tooltip("Jugadores por equipo, portero incluido (6 = 6 contra 6 con armas; 11 = fútbol completo)")]
    [Range(4, 11)] public int teamSize = 6;

    [Header("Ritmo de juego")]
    [Tooltip("Velocidad de los jugadores: 1 = muy rápido, 0.7 = más realista, 0.5 = lento")]
    [Range(0.3f, 1.2f)] public float speedMultiplier = 0.7f;

    [Header("Modelo")]
    [Tooltip("Gira el modelo si en el juego corre de lado o de espaldas (prueba 90, 180 o -90)")]
    public float modelYawOffset = 0f;
    [Tooltip("Actívalo si el modelo está en T-pose (brazos abiertos) para bajarlos al correr")]
    public bool tPoseArmsDown = false;

    [Header("Partido")]
    [Tooltip("Minutos reales que duran los 90 minutos del partido")]
    public float matchMinutesReal = 6f;
    [Range(0f, 1f)] public float keeperSaveSkill = 0.65f;

    [Header("Cancha (si usas StadiumFitter se rellena solo)")]
    public float halfLength = 52.5f;
    public float halfWidth = 34f;

    void Start()
    {
        if (StadiumFitter.Ready)
        {
            halfLength = StadiumFitter.HalfLength;
            halfWidth = StadiumFitter.HalfWidth;
        }
        List<PlayerAI>[] teams = new List<PlayerAI>[2];
        teams[0] = SpawnTeam(0);
        teams[1] = SpawnTeam(1);
        Transform ball = SpawnBall();

        // la pelota no choca con los jugadores (el control del balón lo maneja MatchManager)
        Collider bc = ball.GetComponentInChildren<Collider>();
        for (int t = 0; t < 2; t++)
            foreach (PlayerAI ai in teams[t])
            {
                CharacterController c = ai.GetComponent<CharacterController>();
                if (bc != null && c != null) Physics.IgnoreCollision(bc, c);
            }

        gameObject.AddComponent<GameAudio>();
        gameObject.AddComponent<PlayerController>();
        MatchManager mm = gameObject.AddComponent<MatchManager>();
        mm.TeamColors = new Color[] { millonariosColor, nacionalColor };
        mm.matchLengthSeconds = matchMinutesReal * 60f;
        mm.keeperSaveSkill = keeperSaveSkill;
        mm.Init(teams[0], teams[1], ball, halfLength, halfWidth);
    }

    List<PlayerAI> SpawnTeam(int team)
    {
        List<PlayerAI> list = new List<PlayerAI>();
        float attackDir = team == 0 ? 1f : -1f;
        Color teamColor = team == 0 ? millonariosColor : nacionalColor;
        Color gkColor = team == 0 ? gkMillonarios : gkNacional;

        for (int i = 0; i < teamSize; i++)
        {
            Vector3 pos = new Vector3(-attackDir * halfLength * 0.5f, 0f, (i - teamSize / 2f) * 4f);
            GameObject p = Instantiate(playerPrefab, pos, Quaternion.LookRotation(new Vector3(attackDir, 0f, 0f)));
            p.name = (team == 0 ? "Millonarios_" : "Nacional_") + i;

            NormalizePlayer(p);
            PaintShirt(p, i == 0 ? gkColor : teamColor);

            if (p.GetComponent<PlayerCombat>() == null) p.AddComponent<PlayerCombat>();
            PlayerAI ai = p.GetComponent<PlayerAI>();
            if (ai == null) ai = p.AddComponent<PlayerAI>();
            ai.playerHeight = playerHeight;
            ai.footOffset = footOffset;
            ai.speedMultiplier = speedMultiplier;
            ai.Setup(team, attackDir, i, modelYawOffset, teamSize);

            ProceduralRun run = p.GetComponent<ProceduralRun>();
            if (run == null) run = p.AddComponent<ProceduralRun>();
            run.ai = ai;
            run.tPoseArmsDown = tPoseArmsDown;
            list.Add(ai);
        }
        return list;
    }

    void PaintShirt(GameObject p, Color color)
    {
        bool done = false;
        if (colorMethod == ColorMethod.BonesAuto) done = ShirtMeshSplit.Apply(p, color);
        if (!done) ShirtRecolor.Apply(p, originalShirtColor, color, colorTolerance);
    }

    static Bounds GetBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // Escala aproximada a 1.8 m; el ajuste fino (altura exacta y suelas sobre el piso) lo hace PlayerAI.Calibrate.
    void NormalizePlayer(GameObject p)
    {
        foreach (SkinnedMeshRenderer r in p.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
        Bounds b = GetBounds(p);
        if (b.size.y > 0.0001f) p.transform.localScale *= playerHeight / b.size.y;
        b = GetBounds(p);
        p.transform.position += Vector3.up * (-b.min.y);
    }

    Transform SpawnBall()
    {
        GameObject go = Instantiate(ballPrefab, new Vector3(0f, 0.5f, 0f), Quaternion.identity);
        Bounds b = GetBounds(go);
        float d = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (d > 0.0001f) go.transform.localScale *= ballDiameter / d;

        b = GetBounds(go);
        if (go.GetComponent<Collider>() == null)
        {
            SphereCollider sc = go.AddComponent<SphereCollider>();
            sc.center = go.transform.InverseTransformPoint(b.center);
            sc.radius = (ballDiameter * 0.5f) / go.transform.lossyScale.x;
        }
        if (go.GetComponent<BallSetup>() == null) go.AddComponent<BallSetup>();
        if (go.GetComponent<BallSfx>() == null) go.AddComponent<BallSfx>();
        return go.transform;
    }

    [ContextMenu("Listar huesos del jugador")]
    void ListBones()
    {
        if (playerPrefab == null) return;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (Transform t in playerPrefab.GetComponentsInChildren<Transform>(true)) sb.AppendLine(t.name);
        Debug.Log("Huesos/objetos del jugador:\n" + sb);
    }
}
