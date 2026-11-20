using UnityEngine;

// Objetos del inventario (estilo Fortnite): casilla 1 = balón (fútbol), 2 = Glock, 3 = AK-47, 4 = granadas.
public enum ItemType { Ball = 0, Glock = 1, AK47 = 2, Grenade = 3 }

public class WeaponStats
{
    public string name;
    public float damage, fireRate, range, spread, bloomPerShot, maxBloom, reloadTime;
    public int mag, reserve, maxReserve;
    public bool auto;
    public Color rarity;
}

public static class Items
{
    public static readonly WeaponStats Glock = new WeaponStats
    {
        name = "GLOCK", damage = 20f, fireRate = 5.5f, range = 60f, spread = 0.5f, bloomPerShot = 0.7f, maxBloom = 3f,
        reloadTime = 1.4f, mag = 15, reserve = 45, maxReserve = 120, auto = false, rarity = new Color(0.3f, 0.6f, 1f)
    };
    public static readonly WeaponStats AK47 = new WeaponStats
    {
        name = "AK-47", damage = 18f, fireRate = 9f, range = 90f, spread = 0.8f, bloomPerShot = 0.45f, maxBloom = 4.5f,
        reloadTime = 2.2f, mag = 30, reserve = 60, maxReserve = 150, auto = true, rarity = new Color(1f, 0.6f, 0.1f)
    };
    public const int MaxGrenades = 5;
    public static readonly Color GrenadeColor = new Color(0.4f, 1f, 0.45f);
    public static readonly Color BallColor = new Color(0.85f, 0.85f, 0.9f);

    public static WeaponStats Stats(ItemType t) { return t == ItemType.AK47 ? AK47 : Glock; }

    public static string Name(ItemType t)
    {
        switch (t)
        {
            case ItemType.Glock: return "GLOCK";
            case ItemType.AK47: return "AK-47";
            case ItemType.Grenade: return "GRANADA";
            default: return "BALÓN";
        }
    }

    public static Color Rarity(ItemType t)
    {
        switch (t)
        {
            case ItemType.Glock: return Glock.rarity;
            case ItemType.AK47: return AK47.rarity;
            case ItemType.Grenade: return GrenadeColor;
            default: return BallColor;
        }
    }
}

// Ajustes visibles en el Inspector (objeto MatchManager) para tus propios modelos de arma.
// Pon tus prefabs en Assets/Resources/Weapons/ con los nombres: ak47, glock, grenade
// y ajusta la rotación si el arma sale girada (el "frente" del modelo debe apuntar a +Z).
public class WeaponTuning : MonoBehaviour
{
    public static WeaponTuning I;
    public Vector3 akEuler, glockEuler, grenadeEuler;
    public Vector3 akHandOffset = new Vector3(0f, 0.02f, 0.22f);
    public Vector3 glockHandOffset = new Vector3(0f, 0.03f, 0.08f);
    public Vector3 grenadeHandOffset = new Vector3(0f, 0.02f, 0.05f);
    public float akLength = 0.85f, glockLength = 0.2f, grenadeSize = 0.09f;
    [Tooltip("Actívalo si el arma aparece en la mano izquierda")]
    public bool leftHand = false;
    [Header("Agarre (IK): giro de cada mano sobre su eje, en grados. Ajústalo en Play si el dorso queda al revés")]
    [Range(-180f, 180f)] public float rightHandRoll = 0f;
    [Range(-180f, 180f)] public float leftHandRoll = 0f;
    void Awake() { I = this; }
}

// Modelos de arma: usa tu prefab de Resources/Weapons si existe; si no, el modelo low-poly de WeaponMeshes.
public static class WeaponModels
{
    public static string ResName(ItemType t)
    {
        return t == ItemType.AK47 ? "ak47" : (t == ItemType.Glock ? "glock" : "grenade");
    }

    public static GameObject Build(ItemType t, out Transform muzzle)
    {
        GameObject root = new GameObject("Arma_" + Items.Name(t));
        muzzle = null;
        WeaponTuning tune = WeaponTuning.I;
        float want = t == ItemType.AK47 ? (tune != null ? tune.akLength : 0.85f)
                   : t == ItemType.Glock ? (tune != null ? tune.glockLength : 0.2f)
                   : (tune != null ? tune.grenadeSize : 0.09f);

        GameObject prefab = Resources.Load<GameObject>("Weapons/" + ResName(t));
        if (prefab != null)
        {
            GameObject inst = Object.Instantiate(prefab, root.transform);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            foreach (Collider c in inst.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            foreach (Rigidbody r in inst.GetComponentsInChildren<Rigidbody>()) Object.Destroy(r);
            Renderer[] rs = inst.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (longest > 0.0001f) inst.transform.localScale *= want / longest;
                b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                inst.transform.position += root.transform.position - b.center;     // centra el modelo
            }
            if (t != ItemType.Grenade)
            {
                GameObject mz = new GameObject("Muzzle");
                mz.transform.SetParent(root.transform, false);
                mz.transform.localPosition = new Vector3(0f, 0f, want * 0.5f);
                muzzle = mz.transform;
            }
            return root;
        }

        // modelos low-poly detallados hechos por código (ver WeaponMeshes.cs)
        Transform p = root.transform;
        WeaponMeshes.Create(t, p);
        if (t == ItemType.AK47)
        {
            GameObject mz = new GameObject("Muzzle"); mz.transform.SetParent(p, false); mz.transform.localPosition = new Vector3(0f, 0.012f, 0.595f); muzzle = mz.transform;
        }
        else if (t == ItemType.Glock)
        {
            GameObject mz = new GameObject("Muzzle"); mz.transform.SetParent(p, false); mz.transform.localPosition = new Vector3(0f, 0.032f, 0.1f); muzzle = mz.transform;
        }
        return root;
    }

    public static Vector3 Euler(ItemType t)
    {
        WeaponTuning w = WeaponTuning.I;
        if (w == null) return Vector3.zero;
        return t == ItemType.AK47 ? w.akEuler : (t == ItemType.Glock ? w.glockEuler : w.grenadeEuler);
    }

    public static Vector3 HandOffset(ItemType t)
    {
        WeaponTuning w = WeaponTuning.I;
        if (w == null) return new Vector3(0f, 0.02f, 0.15f);
        return t == ItemType.AK47 ? w.akHandOffset : (t == ItemType.Glock ? w.glockHandOffset : w.grenadeHandOffset);
    }
}
