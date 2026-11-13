using UnityEngine;

// Ponlo en el GameObject "MatchManager" (junto a MatchSetup).
// Centra el estadio en (0,0,0), lo orienta, lo escala a tamaño real, agrega colliders
// y crea los dos arcos de gol. MatchSetup lee de aquí el largo y ancho de la cancha.
[DefaultExecutionOrder(-100)]
public class StadiumFitter : MonoBehaviour
{
    public static bool Ready;
    public static float HalfLength = 52.5f;
    public static float HalfWidth = 34f;

    [Header("Objetos del estadio (arrástralos desde la Hierarchy)")]
    public Transform stadiumRoot;      // el objeto padre de todo el estadio
    public Transform grass;            // "cesped"
    public Transform pitchLines;       // "lineas de juego" (define el rectángulo real de la cancha)

    [Header("Tamaño real")]
    public bool fitToRealSize = true;
    public float pitchLength = 105f;   // largo entre líneas de fondo

    [Header("Colliders y arcos")]
    public string[] collideNames = { "cesped", "arco", "malla1", "malla2" };
    public bool createGoalTriggers = true;

    void Awake()
    {
        Fit();
        AddColliders();
        if (createGoalTriggers)
        {
            MakeGoal(+1f, 0);
            MakeGoal(-1f, 1);
        }
        Ready = true;
    }

    Bounds GetBounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    void Fit()
    {
        Transform refT = pitchLines != null ? pitchLines : grass;
        Bounds b = GetBounds(refT);

        // 1) que el lado largo quede en el eje X
        if (b.size.x < b.size.z)
        {
            stadiumRoot.RotateAround(b.center, Vector3.up, 90f);
            b = GetBounds(refT);
        }
        // 2) escala a tamaño real
        if (fitToRealSize)
        {
            float k = pitchLength / b.size.x;
            stadiumRoot.localScale *= k;
            b = GetBounds(refT);
        }
        // 3) centro de la cancha en el origen y césped a altura Y = 0
        float grassTop = GetBounds(grass).max.y;
        stadiumRoot.position += new Vector3(-b.center.x, -grassTop, -b.center.z);
        Physics.SyncTransforms();

        b = GetBounds(refT);
        HalfLength = b.size.x * 0.5f;
        HalfWidth = b.size.z * 0.5f;
        Debug.Log($"Cancha: {b.size.x:F1} x {b.size.z:F1} m");
    }

    void AddColliders()
    {
        foreach (var t in stadiumRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<MeshFilter>() == null || t.GetComponent<Collider>() != null) continue;
            foreach (var n in collideNames)
                if (t.name.ToLower() == n.ToLower()) { t.gameObject.AddComponent<MeshCollider>(); break; }
        }
    }

    void MakeGoal(float sign, int scoringTeam)
    {
        // +X: gol que anota Millonarios (ataca hacia +X). -X: gol que anota Nacional.
        var go = new GameObject(sign > 0 ? "Gol_anota_Millonarios" : "Gol_anota_Nacional");
        go.transform.position = new Vector3(sign * (HalfLength + 1f), 1.2f, 0f);
        var bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(2f, 2.44f, 7.32f);
        go.AddComponent<GoalTrigger>().scoringTeam = scoringTeam;
    }
}
