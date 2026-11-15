using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Combate de cada jugador (humano e IA): vida, inventario tipo Fortnite, disparo, granadas, muerte y respawn.
[DefaultExecutionOrder(100)]
public class PlayerCombat : MonoBehaviour
{
    public const float RespawnSeconds = 5f;

    public float maxHealth = 100f;
    public float health = 100f;
    public bool IsDead { get; private set; }
    public ItemType equipped = ItemType.Ball;
    public bool hasGlock, hasAK;
    public int glockMag, glockReserve, akMag, akReserve, grenades;
    public float respawnAt, invulnerableUntil;
    public int kills, deaths;
    [HideInInspector] public int heroStance;      // 4/5/6 = poses de podio o celebración (lo escribe MatchManager/Podium)

    // apuntado (lo escribe PlayerController o la IA)
    [HideInInspector] public Vector3 aimPoint;
    [HideInInspector] public bool aimActive;
    [HideInInspector] public bool useOverrideMuzzle;
    [HideInInspector] public Vector3 overrideMuzzle;
    [HideInInspector] public bool hideWeaponVisual, hideBody;
    [HideInInspector] public float spreadMul = 1f;

    PlayerAI ai;
    CharacterController cc;
    ProceduralRun run;
    Transform hand, muzzleT;
    GameObject weaponObj;
    ItemType shownType = ItemType.Ball;
    Renderer[] renderers;
    bool reloading;
    float nextFire, reloadUntil, bloom, aggression;
    static readonly RaycastHit[] hits = new RaycastHit[24];

    // IA
    PlayerCombat aiTarget;
    float aiNextScan, aiNextThrow, aiReactionAt, aiBurstPause, aiLastTarget;
    int aiBurstLeft;

    public PlayerAI AI { get { if (ai == null) ai = GetComponent<PlayerAI>(); return ai; } }
    public int TeamId { get { return AI.teamId; } }
    public bool IsReloading { get { return reloading; } }
    public float ReloadProgress
    {
        get
        {
            if (!reloading) return 1f;
            float t = Items.Stats(equipped).reloadTime;
            return Mathf.Clamp01(1f - (reloadUntil - Time.time) / t);
        }
    }
    public bool HasGun { get { return (hasAK && akMag + akReserve > 0) || (hasGlock && glockMag + glockReserve > 0); } }
    public Vector3 ChestPos() { return transform.position + Vector3.up * (AI.playerHeight * 0.72f); }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void Start()
    {
        cc = GetComponent<CharacterController>();
        run = GetComponent<ProceduralRun>();
        renderers = GetComponentsInChildren<Renderer>();
        aggression = Random.Range(0.2f, 1f);
        FindHand();
    }

    void FindHand()
    {
        Vector3 right = Vector3.Cross(Vector3.up, AI.FaceDir);
        bool left = WeaponTuning.I != null && WeaponTuning.I.leftHand;
        Transform best = null;
        float bestScore = -1e9f;
        foreach (Transform t in GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLower();
            if (!n.Contains("hand")) continue;
            if (n.Contains("index") || n.Contains("thumb") || n.Contains("middle") || n.Contains("ring") ||
                n.Contains("pinky") || n.Contains("finger")) continue;
            float s = Vector3.Dot(t.position - transform.position, right);
            if (n.Contains("right")) s += 2f;
            if (n.Contains("left")) s -= 2f;
            if (left) s = -s;
            if (s > bestScore) { bestScore = s; best = t; }
        }
        hand = best;
    }

    // ------------------------------------------------------------ inventario
    public bool HasItem(ItemType t)
    {
        switch (t)
        {
            case ItemType.Glock: return hasGlock;
            case ItemType.AK47: return hasAK;
            case ItemType.Grenade: return grenades > 0;
            default: return true;
        }
    }

    public int MagOf(ItemType t) { return t == ItemType.AK47 ? akMag : glockMag; }
    public int ReserveOf(ItemType t) { return t == ItemType.AK47 ? akReserve : glockReserve; }

    public void Equip(ItemType t)
    {
        if (IsDead || !HasItem(t) || t == equipped) return;
        equipped = t;
        reloading = false;
        if (t != ItemType.Ball && MatchManager.I != null) MatchManager.I.ReleaseBall(AI);   // manos ocupadas: suelta el balón
    }

    // devuelve false si no sirve de nada (inventario lleno)
    public bool PickUp(ItemType t)
    {
        if (IsDead) return false;
        switch (t)
        {
            case ItemType.Glock:
                if (!hasGlock) { hasGlock = true; glockMag = Items.Glock.mag; glockReserve = Items.Glock.reserve; return true; }
                if (glockReserve >= Items.Glock.maxReserve) return false;
                glockReserve = Mathf.Min(Items.Glock.maxReserve, glockReserve + 30);
                return true;
            case ItemType.AK47:
                if (!hasAK) { hasAK = true; akMag = Items.AK47.mag; akReserve = Items.AK47.reserve; return true; }
                if (akReserve >= Items.AK47.maxReserve) return false;
                akReserve = Mathf.Min(Items.AK47.maxReserve, akReserve + 60);
                return true;
            case ItemType.Grenade:
                if (grenades >= Items.MaxGrenades) return false;
                grenades = Mathf.Min(Items.MaxGrenades, grenades + 2);
                return true;
        }
        return false;
    }

    public void ResetAll()
    {
        StopAllCoroutines();
        IsDead = false; health = maxHealth; equipped = ItemType.Ball;
        hasGlock = hasAK = false; glockMag = glockReserve = akMag = akReserve = grenades = 0;
        reloading = false; invulnerableUntil = 0f; kills = 0; deaths = 0;
        heroStance = 0; hideBody = false; hideWeaponVisual = false;
        if (cc != null) cc.enabled = true;
    }

    // en cada saque de centro todos vuelven con vida completa (conservan lo que llevan)
    public void ReviveForKickoff()
    {
        if (IsDead) { StopAllCoroutines(); IsDead = false; if (cc != null) cc.enabled = true; }
        health = maxHealth;
        heroStance = 0;
        invulnerableUntil = Time.time + 1f;
    }

    // ------------------------------------------------------------ disparo
    Vector3 Muzzle()
    {
        if (useOverrideMuzzle) return overrideMuzzle;
        if (weaponObj != null && muzzleT != null) return muzzleT.position;
        return ChestPos() + AI.FaceDir * 0.5f;
    }

    public bool TryFire(Vector3 target, float extraSpread)
    {
        if (IsDead || (equipped != ItemType.Glock && equipped != ItemType.AK47)) return false;
        WeaponStats st = Items.Stats(equipped);
        if (reloading) return false;
        if (Time.time < nextFire) return false;
        int mag = MagOf(equipped);
        if (mag <= 0) { Reload(); return false; }

        nextFire = Time.time + 1f / st.fireRate;
        if (equipped == ItemType.AK47) akMag--; else glockMag--;
        bloom = Mathf.Min(bloom + st.bloomPerShot, st.maxBloom);

        Vector3 origin = Muzzle();
        Vector3 dir = (target - origin).normalized;
        float spread = (st.spread + bloom + extraSpread) * spreadMul;
        dir = (dir + Random.insideUnitSphere * Mathf.Tan(spread * Mathf.Deg2Rad)).normalized;

        Fx.Flash(origin, equipped == ItemType.AK47 ? 0.2f : 0.14f);
        if (GameAudio.I != null) GameAudio.I.Shot(equipped, origin);
        FireRay(origin, dir, st);
        if (PlayerController.I != null && PlayerController.I.IsUser(this)) PlayerController.I.AddRecoil(equipped == ItemType.AK47 ? 0.55f : 0.9f);
        return true;
    }

    void FireRay(Vector3 origin, Vector3 dir, WeaponStats st)
    {
        int n = Physics.RaycastNonAlloc(origin, dir, hits, st.range, ~0, QueryTriggerInteraction.Ignore);
        float best = st.range;
        Vector3 end = origin + dir * st.range, normal = -dir;
        PlayerCombat victim = null;
        Rigidbody ballHit = null;
        bool head = false;
        MatchManager m = MatchManager.I;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = hits[i];
            if (h.distance >= best) continue;
            Collider c = h.collider;
            PlayerCombat pc = c.GetComponent<PlayerCombat>();
            if (pc == this) continue;
            if (pc != null && (pc.IsDead || pc.TeamId == TeamId)) continue;          // atraviesa compañeros y caídos
            Rigidbody rb = c.attachedRigidbody;
            bool isBall = rb != null && m != null && rb == m.BallRb;
            if (pc == null && rb != null && !isBall) continue;                       // granadas y otros objetos sueltos
            best = h.distance; end = h.point; normal = h.normal;
            victim = pc;
            ballHit = isBall ? rb : null;
            if (pc != null) head = (h.point.y - pc.AI.LastGroundY) > pc.AI.playerHeight * 0.82f;
        }
        Fx.Tracer(origin, end, new Color(1f, 0.9f, 0.55f, 0.9f));
        if (victim != null)
        {
            Fx.Impact(end, normal, new Color(0.8f, 0.1f, 0.1f, 0.9f));
            victim.TakeDamage(st.damage * (head ? 2f : 1f), this, head, st.name);
        }
        else
        {
            Fx.Impact(end, normal, new Color(0.7f, 0.65f, 0.5f, 0.8f));
            if (ballHit != null && !ballHit.isKinematic) ballHit.AddForceAtPosition(dir * st.damage * 0.12f, end, ForceMode.Impulse);
        }
    }

    public void Reload()
    {
        if (IsDead || reloading) return;
        if (equipped != ItemType.Glock && equipped != ItemType.AK47) return;
        WeaponStats st = Items.Stats(equipped);
        if (MagOf(equipped) >= st.mag || ReserveOf(equipped) <= 0) return;
        reloading = true;
        reloadUntil = Time.time + st.reloadTime;
        if (GameAudio.I != null) GameAudio.I.Reload(transform.position);
    }

    void FinishReload()
    {
        reloading = false;
        if (equipped != ItemType.Glock && equipped != ItemType.AK47) return;
        WeaponStats st = Items.Stats(equipped);
        int need = st.mag - MagOf(equipped);
        int take = Mathf.Min(need, ReserveOf(equipped));
        if (equipped == ItemType.AK47) { akMag += take; akReserve -= take; }
        else { glockMag += take; glockReserve -= take; }
    }

    public bool ThrowGrenade(Vector3 vel)
    {
        if (IsDead || grenades <= 0) return false;
        grenades--;
        Vector3 dir = Flat(vel).normalized;
        Grenade.Throw(ChestPos() + dir * 0.5f + Vector3.up * 0.15f, vel, this);
        if (grenades <= 0 && equipped == ItemType.Grenade) equipped = ItemType.Ball;
        return true;
    }

    // ------------------------------------------------------------ daño, muerte y respawn
    public void TakeDamage(float amount, PlayerCombat attacker, bool headshot, string weapon)
    {
        if (IsDead || Time.time < invulnerableUntil) return;
        health -= amount;
        MatchManager m = MatchManager.I;
        if (m != null) m.OnPlayerHurt(this, attacker, headshot);
        if (GameAudio.I != null) GameAudio.I.Hurt(transform.position);
        if (health <= 0f) { health = 0f; Die(attacker, headshot, weapon); }
    }

    void Die(PlayerCombat killer, bool headshot, string weapon)
    {
        IsDead = true;
        deaths++;
        respawnAt = Time.time + RespawnSeconds;
        equipped = ItemType.Ball;
        hasGlock = hasAK = false; glockMag = glockReserve = akMag = akReserve = grenades = 0;
        reloading = false;
        if (killer != null && killer != this) killer.kills++;
        if (cc != null) cc.enabled = false;
        AI.OnDied();
        if (MatchManager.I != null) MatchManager.I.OnPlayerDied(this, killer, headshot, weapon);
        StartCoroutine(FallRoutine());
    }

    IEnumerator FallRoutine()
    {
        Quaternion start = transform.rotation;
        Vector3 right = Vector3.Cross(Vector3.up, AI.FaceDir);
        Quaternion end = Quaternion.AngleAxis(-88f, right) * start;     // cae de espaldas
        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(start, end, Mathf.Clamp01(t / 0.45f));
            yield return null;
        }
    }

    void Respawn()
    {
        MatchManager m = MatchManager.I;
        Vector3 p = m.RespawnPoint(AI);
        IsDead = false;
        health = maxHealth;
        invulnerableUntil = Time.time + 2f;
        AI.Teleport(p, new Vector3(AI.attackDirection, 0f, 0f));      // reactiva el CharacterController y endereza el cuerpo
        m.OnPlayerRespawned(this);
    }

    // ------------------------------------------------------------ bucle
    void Update()
    {
        MatchManager m = MatchManager.I;
        if (m == null) return;
        bloom = Mathf.MoveTowards(bloom, 0f, 5f * Time.deltaTime);
        if (reloading && Time.time >= reloadUntil) FinishReload();
        if (IsDead)
        {
            if (Time.time >= respawnAt && (m.State == MatchState.Playing || m.State == MatchState.SetPiece)) Respawn();
            return;
        }
        if (hasAK && akMag + akReserve <= 0) hasAK = false;
        if (hasGlock && glockMag + glockReserve <= 0) hasGlock = false;
        if (equipped != ItemType.Ball && !HasItem(equipped)) equipped = ItemType.Ball;
    }

    // ------------------------------------------------------------ IA de combate
    PlayerCombat FindTarget()
    {
        float maxRange = 0f;
        if (hasAK && akMag + akReserve > 0) maxRange = 50f;
        if (hasGlock && glockMag + glockReserve > 0) maxRange = Mathf.Max(maxRange, 32f);
        if (grenades > 0) maxRange = Mathf.Max(maxRange, 24f);
        if (maxRange <= 0f) return null;
        PlayerCombat best = null;
        float bd = maxRange;
        List<PlayerAI> opp = MatchManager.I.Teams[1 - TeamId];
        for (int i = 0; i < opp.Count; i++)
        {
            PlayerCombat oc = opp[i].Combat;
            if (oc == null || oc.IsDead) continue;
            float d = Flat(opp[i].transform.position - transform.position).magnitude;
            if (d < bd) { bd = d; best = oc; }
        }
        return best;
    }

    ItemType ChooseGun(float dist)
    {
        bool ak = hasAK && akMag + akReserve > 0 && dist <= 50f;
        bool gl = hasGlock && glockMag + glockReserve > 0 && dist <= 32f;
        if (ak && gl) return dist > 14f ? ItemType.AK47 : ItemType.Glock;
        if (ak) return ItemType.AK47;
        if (gl) return ItemType.Glock;
        return ItemType.Ball;
    }

    // La IA recoge armas, apunta al rival más cercano, dispara en ráfagas y lanza granadas.
    public void AIThink()
    {
        if (AI.userControlled) return;           // el humano maneja su propio combate
        aimActive = false;
        if (IsDead) return;
        MatchManager m = MatchManager.I;
        if (m == null || m.State != MatchState.Playing)
        {
            if (equipped != ItemType.Ball) equipped = ItemType.Ball;
            return;
        }
        if (aggression < 0.25f) return;
        bool carrying = m.Holder == AI;

        if (Time.time >= aiNextScan) { aiNextScan = Time.time + 0.3f; aiTarget = FindTarget(); }
        if (aiTarget != null && aiTarget.IsDead) aiTarget = null;

        if (aiTarget == null || carrying)
        {
            if (equipped != ItemType.Ball && Time.time - aiLastTarget > 1.2f) Equip(ItemType.Ball);
            return;
        }

        Vector3 tChest = aiTarget.ChestPos() + aiTarget.AI.Velocity * 0.15f;
        Vector3 flat = Flat(tChest - transform.position);
        float dist = flat.magnitude;
        aiLastTarget = Time.time;
        if (flat.sqrMagnitude > 0.01f) AI.aiFace = flat.normalized;

        // granada a media distancia
        if (grenades > 0 && dist > 9f && dist < 24f && Time.time >= aiNextThrow)
        {
            if (equipped != ItemType.Grenade) { Equip(ItemType.Grenade); aiReactionAt = Time.time + 0.5f; }
            aimPoint = tChest; aimActive = true;
            if (Time.time >= aiReactionAt)
            {
                ThrowAt(aiTarget.transform.position);
                aiNextThrow = Time.time + Random.Range(7f, 11f);
            }
            return;
        }

        ItemType gun = ChooseGun(dist);
        if (gun == ItemType.Ball) return;
        if (equipped != gun) { Equip(gun); aiReactionAt = Time.time + 0.45f; }
        aimPoint = tChest; aimActive = true;

        if (Time.time < aiReactionAt || Vector3.Angle(AI.FaceDir, flat) > 25f) return;
        float extra = 2f + dist * 0.06f;
        if (aiBurstLeft <= 0 && Time.time >= aiBurstPause) aiBurstLeft = equipped == ItemType.AK47 ? Random.Range(3, 7) : 1;
        if (aiBurstLeft > 0 && TryFire(aimPoint, extra))
        {
            aiBurstLeft--;
            if (aiBurstLeft <= 0) aiBurstPause = Time.time + (equipped == ItemType.AK47 ? Random.Range(0.5f, 1.1f) : Random.Range(0.25f, 0.5f));
        }
    }

    void ThrowAt(Vector3 targetPos)
    {
        Vector3 from = ChestPos();
        Vector3 flat = Flat(targetPos - from);
        float d = Mathf.Max(flat.magnitude, 2f);
        float ang = 40f * Mathf.Deg2Rad;
        float v = Mathf.Sqrt(9.81f * d / Mathf.Sin(2f * ang)) * Random.Range(0.95f, 1.05f);
        Vector3 vel = flat.normalized * (v * Mathf.Cos(ang));
        vel.y = v * Mathf.Sin(ang);
        ThrowGrenade(vel);
    }

    // ¿debe esta IA ir a recoger un arma? (solo si es de las más cercanas y el balón no está encima)
    public bool WantsPickup(out Vector3 pos)
    {
        pos = Vector3.zero;
        MatchManager m = MatchManager.I;
        if (IsDead || AI.isGoalkeeper || m == null || m.State != MatchState.Playing || AI.userControlled) return false;
        if (aggression < 0.35f) return false;
        if (hasAK && hasGlock && grenades >= 2) return false;
        if (Flat(m.Ball.position - transform.position).magnitude < 10f) return false;
        WeaponPickup w = WeaponPickup.Nearest(transform.position, 26f);
        if (w == null) return false;
        float mine = Flat(w.transform.position - transform.position).magnitude;
        List<PlayerAI> mates = m.Teams[TeamId];
        int closer = 0;
        for (int i = 1; i < mates.Count; i++)
        {
            if (mates[i] == AI || mates[i].IsDead) continue;
            if (Flat(w.transform.position - mates[i].transform.position).magnitude < mine) closer++;
        }
        if (closer >= 2) return false;
        pos = w.transform.position;
        return true;
    }

    // ------------------------------------------------------------ visual del arma (se actualiza después de ProceduralRun)
    void LateUpdate()
    {
        if (renderers != null)
        {
            bool blink = !IsDead && Time.time < invulnerableUntil && ((int)(Time.time * 10f) % 2 == 0);
            bool vis = !(hideBody && !IsDead) && !blink;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].enabled != vis) renderers[i].enabled = vis;
        }

        ItemType want = equipped;
        if (IsDead || !HasItem(want)) want = ItemType.Ball;
        if (want != shownType) RebuildWeapon(want);
        if (run != null) run.stance = heroStance > 0 ? heroStance : (want == ItemType.Ball ? 0 : (want == ItemType.AK47 ? 1 : (want == ItemType.Glock ? 2 : 3)));
        if (weaponObj != null)
        {
            bool show = !hideWeaponVisual;
            weaponObj.SetActive(show);
            if (show) PositionWeapon(want);
        }
    }

    void RebuildWeapon(ItemType want)
    {
        if (weaponObj != null) Destroy(weaponObj);
        weaponObj = null; muzzleT = null;
        shownType = want;
        if (want == ItemType.Ball) return;
        weaponObj = WeaponModels.Build(want, out muzzleT);
    }

    void PositionWeapon(ItemType t)
    {
        Vector3 chest = ChestPos();
        Vector3 dir = aimActive ? (aimPoint - chest) : new Vector3(AI.FaceDir.x, -0.05f, AI.FaceDir.z);
        if (dir.sqrMagnitude < 0.01f) dir = AI.FaceDir;
        dir.Normalize();
        Quaternion aimRot = Quaternion.LookRotation(dir, Vector3.up);
        Vector3 handPos;
        if (hand != null) handPos = hand.position;
        else handPos = chest + Vector3.Cross(Vector3.up, dir).normalized * 0.22f + Vector3.down * 0.1f;
        weaponObj.transform.position = handPos + aimRot * WeaponModels.HandOffset(t);
        weaponObj.transform.rotation = aimRot * Quaternion.Euler(WeaponModels.Euler(t));
    }

    void OnDestroy() { if (weaponObj != null) Destroy(weaponObj); }
}
