using System.Collections.Generic;
using UnityEngine;

// Granada con física real (rebota y rueda) y explosión con daño por distancia, empuje al balón y luz.
public class Grenade : MonoBehaviour
{
    PlayerCombat owner;
    Rigidbody rb;
    float fuse = 2.6f;
    const float Radius = 6.5f;
    const float MaxDamage = 100f;

    public static void Throw(Vector3 pos, Vector3 vel, PlayerCombat owner)
    {
        Transform mz;
        GameObject go = WeaponModels.Build(ItemType.Grenade, out mz);
        go.transform.position = pos;
        SphereCollider sc = go.AddComponent<SphereCollider>();
        sc.radius = 0.06f;
        PhysicMaterial pm = new PhysicMaterial("Granada");
        pm.bounciness = 0.35f; pm.dynamicFriction = 0.6f; pm.staticFriction = 0.6f;
        pm.bounceCombine = PhysicMaterialCombine.Maximum;
        sc.material = pm;
        Rigidbody r = go.AddComponent<Rigidbody>();
        r.mass = 0.4f; r.drag = 0.05f; r.angularDrag = 0.3f;
        r.collisionDetectionMode = CollisionDetectionMode.Continuous;
        r.velocity = vel;
        r.angularVelocity = Random.insideUnitSphere * 8f;
        Grenade g = go.AddComponent<Grenade>();
        g.owner = owner; g.rb = r;
        CharacterController occ = owner.GetComponent<CharacterController>();
        if (occ != null) Physics.IgnoreCollision(sc, occ);
    }

    void Update()
    {
        fuse -= Time.deltaTime;
        if (fuse <= 0f) Explode();
    }

    void Explode()
    {
        Vector3 p = transform.position;
        Fx.Explosion(p, Radius);
        if (GameAudio.I != null) GameAudio.I.Explosion(p);

        Collider[] cols = Physics.OverlapSphere(p, Radius, ~0, QueryTriggerInteraction.Ignore);
        HashSet<PlayerCombat> done = new HashSet<PlayerCombat>();
        for (int i = 0; i < cols.Length; i++)
        {
            Collider c = cols[i];
            PlayerCombat pc = c.GetComponent<PlayerCombat>();
            if (pc != null) { if (done.Add(pc)) Hurt(pc, p); continue; }
            Rigidbody r = c.attachedRigidbody;
            if (r == null || r == rb || r.isKinematic) continue;
            bool ball = MatchManager.I != null && r == MatchManager.I.BallRb;
            r.AddExplosionForce(ball ? 16f : 6f, p, Radius * 1.3f, 0.8f, ForceMode.Impulse);
        }
        Destroy(gameObject);
    }

    void Hurt(PlayerCombat pc, Vector3 p)
    {
        if (pc.IsDead) return;
        float f = Mathf.Clamp01(1f - Vector3.Distance(pc.ChestPos(), p) / Radius);
        if (f <= 0.02f) return;
        float mult = 1f;
        if (owner != null)
        {
            if (pc == owner) mult = 0.4f;                       // te lastimas menos a ti mismo
            else if (pc.TeamId == owner.TeamId) mult = 0f;      // sin daño a compañeros
        }
        if (mult <= 0f) return;
        pc.TakeDamage(MaxDamage * f * mult, owner, false, "GRANADA");
    }
}
