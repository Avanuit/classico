using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Role { GK, DEF, MID, FWD }

// Cerebro de cada jugador. El trabajo en equipo sale de tres ideas:
//  1) Forma táctica 4-4-2 que sube/baja y se desplaza con el balón y con la posesión.
//  2) Quien no tiene el balón busca el hueco libre (lejos de rivales y con línea de pase limpia).
//  3) El portador elige entre disparar, pasar al compañero mejor ubicado o seguir conduciendo.
[RequireComponent(typeof(CharacterController))]
public class PlayerAI : MonoBehaviour
{
    // 4-4-2 : profundidad (0 = arco propio, 1 = arco rival) y lateral (-1..1)
    static readonly float[] SlotDepth   = { 0.02f, 0.20f, 0.18f, 0.18f, 0.20f, 0.40f, 0.38f, 0.38f, 0.40f, 0.58f, 0.58f };
    static readonly float[] SlotLateral = { 0f, -0.75f, -0.25f, 0.25f, 0.75f, -0.75f, -0.25f, 0.25f, 0.75f, -0.25f, 0.25f };
    // 6 contra 6 (portero + 2 defensas + 2 medios + 1 delantero)
    static readonly float[] SlotDepth6   = { 0.02f, 0.22f, 0.22f, 0.42f, 0.42f, 0.60f };
    static readonly float[] SlotLateral6 = { 0f, -0.5f, 0.5f, -0.55f, 0.55f, 0f };
    static readonly Vector3[] cand = new Vector3[8];
    static readonly RaycastHit[] hits = new RaycastHit[12];

    public int teamId;
    public int index;
    public Role role;
    public bool isGoalkeeper;
    public float attackDirection = 1f;
    public float speed = 6.5f;
    [Tooltip("Multiplica la velocidad de todos los jugadores (1 = rápido, 0.7 = más realista)")]
    public float speedMultiplier = 0.7f;
    public float userJogFactor = 0.8f;
    public float userSprintFactor = 1.3f;
    public float yawOffset;
    public float slotDepth, slotLateral;
    [Header("Suelo y tamaño")]
    public float playerHeight = 1.8f;
    [Tooltip("Sube (+) o baja (-) el modelo en metros. Si los pies flotan, usa valores negativos como -0.02")]
    public float footOffset = -0.01f;
    [HideInInspector] public float pivotToSole;
    [HideInInspector] public float LastGroundY;

    // control del jugador humano (lo escribe PlayerController)
    [HideInInspector] public bool userControlled;
    [HideInInspector] public Vector3 userDir;
    [HideInInspector] public bool userSprint;
    [HideInInspector] public Vector3 userFace;

    [HideInInspector] public float noControlUntil;
    public Vector3 Velocity { get; private set; }
    [HideInInspector] public Vector3 aiFace;          // la IA de combate gira el cuerpo hacia su objetivo
    PlayerCombat combat;
    public PlayerCombat Combat { get { if (combat == null) combat = GetComponent<PlayerCombat>(); return combat; } }
    public bool IsDead { get { PlayerCombat c = Combat; return c != null && c.IsDead; } }
    public void OnDied() { Velocity = Vector3.zero; }
    // con un arma o granada en las manos no se controla el balón
    public bool CanTouchBall { get { PlayerCombat c = Combat; return c == null || c.equipped == ItemType.Ball; } }
    public Vector3 FaceDir { get; private set; }

    CharacterController cc;
    MatchManager m;
    Animator anim;
    bool hasSpeedParam;
    float nextTackle, nextThink, nextEval, holdTime = 1f, stepDist;
    Vector3 supportTarget;

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false;
            if (anim.runtimeAnimatorController != null)
                foreach (AnimatorControllerParameter p in anim.parameters)
                    if (p.name == "Speed") hasSpeedParam = true;
        }
    }

    public void Setup(int team, float attackDir, int idx, float yaw, int teamSize = 11)
    {
        teamId = team; attackDirection = attackDir; index = idx; yawOffset = yaw;
        bool six = teamSize <= 6;
        slotDepth = six ? SlotDepth6[idx] : SlotDepth[idx];
        slotLateral = six ? SlotLateral6[idx] : SlotLateral[idx];
        isGoalkeeper = (idx == 0);
        if (six) role = idx == 0 ? Role.GK : (idx <= 2 ? Role.DEF : (idx <= 4 ? Role.MID : Role.FWD));
        else role = idx == 0 ? Role.GK : (idx <= 4 ? Role.DEF : (idx <= 8 ? Role.MID : Role.FWD));
        FaceDir = new Vector3(attackDir, 0f, 0f);
        speed = (isGoalkeeper ? 7f : 6.5f + Random.Range(-0.4f, 0.6f)) * speedMultiplier;
        ConfigureCC();
    }

    void Start() { StartCoroutine(Calibrate()); }

    public void Teleport(Vector3 pos, Vector3 face)
    {
        Physics.SyncTransforms();
        pos.y = GroundY(pos) + pivotToSole + footOffset;
        cc.enabled = false;
        transform.position = pos;
        cc.enabled = true;
        Velocity = Vector3.zero;
        if (face.sqrMagnitude > 0.01f) { FaceDir = face.normalized; ApplyRotation(1f); }
    }

    public void OnGotBall() { holdTime = Random.Range(0.6f, 1.8f); }

    void ApplyRotation(float t)
    {
        Quaternion target = Quaternion.LookRotation(FaceDir, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Mathf.Clamp01(t));
    }

    // ------------------------------------------------------------------ bucle
    void Update()
    {
        if (m == null) m = MatchManager.I;
        if (m == null || m.Ball == null) return;
        float dt = Time.deltaTime;

        if (IsDead) { Velocity = Vector3.zero; UpdateAnimator(); return; }

        if (m.State == MatchState.Goal || m.State == MatchState.Finished || m.State == MatchState.Menu)
        {
            MoveTo(transform.position, 0f, dt);
            UpdateAnimator();
            return;
        }

        bool userActive = userControlled && (m.State == MatchState.Playing ||
            (m.State == MatchState.SetPiece && m.Taker == this && m.UserTakesSetPiece));
        if (userActive)
        {
            bool locked = m.UserTakerLocked(this);          // sacando: sin movimiento
            if (locked) Velocity = Vector3.zero;
            Vector3 desired = locked ? Vector3.zero : userDir * speed * (userSprint ? userSprintFactor : userJogFactor);
            ApplyVelocity(desired, dt, userFace);
        }
        else
        {
            if (Combat != null) Combat.AIThink();
            float sp;
            Vector3 target = ComputeTarget(out sp);
            if (!(m.State == MatchState.SetPiece && m.Taker == this)) target = ClampToPitch(target);
            MoveTo(target, sp, dt);
        }

        if (m.State == MatchState.Playing && m.Holder != null && m.Holder.teamId != teamId
            && !isGoalkeeper && Time.time >= nextTackle)
        {
            if (Flat(m.Holder.transform.position - transform.position).magnitude < 1.1f)
            {
                nextTackle = Time.time + 0.8f;
                m.TryTackle(this);
            }
        }
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        if (m == null || m.Holder != this || m.State != MatchState.Playing) return;
        Rigidbody rb = m.BallRb;
        if (rb.isKinematic) return;
        float reach = isGoalkeeper ? 0.7f : 0.85f;
        Vector3 front = transform.position + FaceDir * reach;
        front.y = isGoalkeeper ? 1.0f : 0.12f;
        Vector3 v = (front - rb.position) * 14f;
        if (!isGoalkeeper) v.y = rb.velocity.y;
        rb.velocity = v;
        rb.angularVelocity = Vector3.Cross(Vector3.up, new Vector3(v.x, 0f, v.z)) / 0.11f;
    }

    void UpdateAnimator()
    {
        if (anim != null && hasSpeedParam) anim.SetFloat("Speed", Velocity.magnitude);
    }

    void MoveTo(Vector3 target, float sp, float dt)
    {
        Vector3 pos = transform.position;
        Vector3 d = target - pos; d.y = 0f;
        float dist = d.magnitude;
        Vector3 desired = Vector3.zero;
        if (dist > 0.4f && sp > 0.01f)
        {
            float arrive = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(dist / 3f));
            desired = d / dist * speed * sp * arrive;
        }
        ApplyVelocity(desired, dt, aiFace);
        aiFace = Vector3.zero;
    }

    // Mueve horizontalmente con el CharacterController y fija la altura con un raycast al césped:
    // así las suelas quedan exactamente sobre el piso, sin depender del skinWidth del CharacterController.
    void ApplyVelocity(Vector3 desired, float dt, Vector3 faceOverride)
    {
        Velocity = Vector3.MoveTowards(Velocity, desired, 14f * dt);
        Vector3 pos = transform.position;
        float targetY = GroundY(pos) + pivotToSole + footOffset;
        float dy = Mathf.Clamp(targetY - pos.y, -0.3f, 0.3f);
        cc.Move(new Vector3(Velocity.x * dt, dy, Velocity.z * dt));

        float spd = Velocity.magnitude;                       // pasos
        if (spd > 1.2f && GameAudio.I != null)
        {
            stepDist += spd * dt;
            if (stepDist >= (spd > 5f ? 2.3f : 1.7f))
            {
                stepDist = 0f;
                GameAudio.I.Step(transform.position, Mathf.Clamp01(spd / 7f) * (userControlled ? 1.2f : 1f));
            }
        }

        Vector3 look;
        if (faceOverride.sqrMagnitude > 0.01f) look = faceOverride;
        else look = Velocity.magnitude > 0.6f ? Velocity : Flat(m.Ball.position - pos);
        if (look.sqrMagnitude > 0.01f)
        {
            FaceDir = Vector3.Slerp(FaceDir, look.normalized, Mathf.Clamp01(8f * dt));
            ApplyRotation(10f * dt);
        }
    }

    float GroundY(Vector3 p)
    {
        Vector3 o = new Vector3(p.x, p.y + 4f, p.z);
        int n = Physics.RaycastNonAlloc(o, Vector3.down, hits, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = -1000f;
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c is CharacterController || c.attachedRigidbody != null) continue;
            float y = hits[i].point.y;
            if (y > p.y + 1f) continue;
            if (y > best) best = y;
        }
        if (best > -500f) LastGroundY = best;
        return LastGroundY;
    }

    void ConfigureCC()
    {
        if (cc == null) return;
        float s = Mathf.Max(0.0001f, transform.lossyScale.y);
        float radius = 0.3f;
        float height = Mathf.Max(playerHeight - 0.3f, radius * 2f + 0.05f);
        float centerWorld = -pivotToSole + 0.3f + height * 0.5f;   // la cápsula flota 30 cm sobre la suela
        cc.skinWidth = 0.02f;
        cc.radius = radius / s;
        cc.height = height / s;
        cc.center = new Vector3(0f, centerWorld / s, 0f);
        cc.stepOffset = Mathf.Min(0.1f / s, cc.height * 0.5f);
    }

    bool TightBounds(out Bounds b)
    {
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        b = new Bounds(transform.position, Vector3.zero);
        if (rs.Length == 0) return false;
        b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return true;
    }

    // Con updateWhenOffscreen los bounds del SkinnedMesh son los reales (no los inflados del importador),
    // así se mide la altura y la distancia pivote→suela exactas.
    IEnumerator Calibrate()
    {
        foreach (SkinnedMeshRenderer r in GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
        for (int i = 0; i < 3; i++) yield return null;
        Bounds b;
        if (!TightBounds(out b)) yield break;
        if (b.size.y > 0.1f && Mathf.Abs(b.size.y - playerHeight) > 0.03f)
        {
            transform.localScale *= playerHeight / b.size.y;
            for (int i = 0; i < 3; i++) yield return null;
            if (!TightBounds(out b)) yield break;
        }
        pivotToSole = transform.position.y - b.min.y;
        ConfigureCC();
    }

    // Velocidad inicial de un tiro del humano (la usa también la trayectoria dibujada en pantalla).
    public static Vector3 UserKickVelocity(Vector3 dir, float power, bool aerial)
    {
        dir.y = 0f;
        dir.Normalize();
        float p = Mathf.Clamp01(power);
        if (aerial)
        {
            float v = Mathf.Lerp(8f, 28f, p);
            float ang = 35f * Mathf.Deg2Rad;
            Vector3 vel = dir * (v * Mathf.Cos(ang));
            vel.y = v * Mathf.Sin(ang);
            return vel;
        }
        return dir * Mathf.Lerp(6f, 32f, p);
    }

    // Tiro hacia un punto 3D (el centro de la mira): calcula la velocidad vertical para llegar a esa altura.
    // Si el punto está a ras de piso el balón va rodando recto.
    public static Vector3 AimVelocity(Vector3 from, Vector3 target, float hSpeed, Vector3 fallbackDir)
    {
        Vector3 flat = new Vector3(target.x - from.x, 0f, target.z - from.z);
        float dist = flat.magnitude;
        if (dist < 2f)
        {
            fallbackDir.y = 0f;
            return fallbackDir.normalized * hSpeed;
        }
        Vector3 dir = flat / dist;
        float ty = Mathf.Clamp(target.y, 0.1f, 5f);
        if (ty < 0.5f) return dir * hSpeed;

        float t0 = dist / (hSpeed * 0.92f);
        float vy = (ty - from.y + 4.905f * t0 * t0) / t0;
        for (int it = 0; it < 4; it++)
        {
            float t;
            float y = HeightAtDistance(from.y, hSpeed, vy, dist, out t);
            vy += (ty - y) / Mathf.Max(t, 0.1f);
        }
        vy = Mathf.Clamp(vy, -2f, 12f);
        Vector3 vel = dir * hSpeed;
        vel.y = vy;
        return vel;
    }

    static float HeightAtDistance(float y0, float hs, float vy, float dist, out float t)
    {
        float dt = 0.02f, x = 0f, y = y0, vx = hs;
        t = 0f;
        for (int i = 0; i < 300; i++)
        {
            vy -= 9.81f * dt;
            float damp = 1f / (1f + 0.2f * dt);
            vx *= damp; vy *= damp;
            x += vx * dt; y += vy * dt; t += dt;
            if (x >= dist) return y;
        }
        return y;
    }

    public static float UserShotSpeed(float power) { return Mathf.Lerp(6f, 32f, Mathf.Clamp01(power)); }

    // Disparo raso con más de 50% de fuerza: va al centro de la mira.
    public void UserKickAt(Vector3 target, float power)
    {
        Kick(AimVelocity(m.Ball.position, target, UserShotSpeed(power), FaceDir));
    }

    // Disparo del jugador humano. power 0..1 (carga del clic).
    public void UserKick(Vector3 dir, float power, bool aerial)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = FaceDir;
        Kick(UserKickVelocity(dir, power, aerial));
    }

    Vector3 ClampToPitch(Vector3 t)
    {
        t.x = Mathf.Clamp(t.x, -m.HL + 1f, m.HL - 1f);
        t.z = Mathf.Clamp(t.z, -m.HW + 1f, m.HW - 1f);
        t.y = 0f;
        return t;
    }

    // ------------------------------------------------------------------ qué hago ahora
    Vector3 ComputeTarget(out float sp)
    {
        sp = 1f;
        Vector3 ball = Flat(m.Ball.position);
        Vector3 ballVel = Flat(m.BallRb.velocity);

        if (m.State == MatchState.SetPiece) return SetPieceTarget(out sp);
        if (isGoalkeeper && m.Holder != this) return GoalkeeperTarget(out sp);
        if (m.Holder == this) return CarrierTarget(out sp);

        if (m.PassTarget == this && m.Holder == null) { sp = 1.25f; return ball + ballVel * 0.3f; }
        Vector3 pickupPos;
        if (Combat != null && Combat.WantsPickup(out pickupPos)) { sp = 1.1f; return pickupPos; }

        bool myTeamAttacks = m.AttackingTeam == teamId;
        if (m.Holder != null && m.Holder.teamId == teamId) return SupportTarget();

        if (m.Chaser[teamId] == this)
        {
            sp = 1.2f;
            if (m.Holder != null) return Flat(m.Holder.transform.position);
            return ball + ballVel * 0.25f;
        }
        if (m.Cover[teamId] == this && !myTeamAttacks)
        {
            Vector3 ownGoal = new Vector3(-attackDirection * m.HL, 0f, 0f);
            return Vector3.Lerp(ball, ownGoal, 0.2f);
        }
        if (myTeamAttacks) return FormationTarget(true, ball);
        return DefendTarget(ball);
    }

    // Posición táctica: la línea sube/baja con el balón y con la posesión, y se cierra hacia el balón.
    Vector3 FormationTarget(bool attacking, Vector3 refBall)
    {
        float ownX = -attackDirection * m.HL;
        float bd = Mathf.Clamp01(((refBall.x - ownX) * attackDirection) / (2f * m.HL));
        float follow, atk, def;
        switch (role)
        {
            case Role.DEF: follow = 0.50f; atk = 0.07f; def = -0.03f; break;
            case Role.MID: follow = 0.65f; atk = 0.10f; def = -0.05f; break;
            case Role.FWD: follow = 0.55f; atk = 0.13f; def = -0.04f; break;
            default: follow = 0.10f; atk = 0f; def = 0f; break;
        }
        float d = slotDepth + (bd - 0.5f) * follow + (attacking ? atk : def);
        d = Mathf.Clamp(d, 0.03f, 0.93f);
        float x = ownX + attackDirection * d * 2f * m.HL;
        float z = slotLateral * m.HW * (attacking ? 0.85f : 0.70f);
        z = Mathf.Lerp(z, refBall.z, 0.3f);
        return new Vector3(x, 0f, z);
    }

    // Defensa: mantiene la forma y marca por el lado del arco al rival más cercano a su zona.
    Vector3 DefendTarget(Vector3 ball)
    {
        Vector3 t = FormationTarget(false, ball);
        PlayerAI mark = NearestOpponentTo(t, 12f);
        if (mark != null)
        {
            Vector3 mp = Flat(mark.transform.position);
            Vector3 goal = new Vector3(-attackDirection * m.HL, 0f, 0f);
            Vector3 goalSide = mp + (goal - mp).normalized * 2f;
            t = Vector3.Lerp(t, goalSide, 0.6f);
        }
        return t;
    }

    // Ataque sin balón: medios y delanteros buscan el mejor hueco para recibir un pase.
    Vector3 SupportTarget()
    {
        Vector3 hp = Flat(m.Holder.transform.position);
        Vector3 baseT = FormationTarget(true, hp);
        if (role == Role.DEF || role == Role.GK) return baseT;
        if (Time.time >= nextEval)
        {
            nextEval = Time.time + 0.4f + Random.value * 0.2f;
            supportTarget = BestSpot(baseT, hp);
        }
        return supportTarget;
    }

    Vector3 BestSpot(Vector3 baseT, Vector3 hp)
    {
        float dir = attackDirection;
        cand[0] = baseT;
        cand[1] = baseT + new Vector3(0f, 0f, 7f);
        cand[2] = baseT + new Vector3(0f, 0f, -7f);
        cand[3] = baseT + new Vector3(dir * 7f, 0f, 0f);
        cand[4] = baseT + new Vector3(dir * 7f, 0f, 6f);
        cand[5] = baseT + new Vector3(dir * 7f, 0f, -6f);
        cand[6] = hp + new Vector3(dir * 6f, 0f, 9f);
        cand[7] = hp + new Vector3(dir * 6f, 0f, -9f);
        float bestS = -1000f;
        Vector3 best = baseT;
        for (int i = 0; i < cand.Length; i++)
        {
            Vector3 c = ClampToPitch(cand[i]);
            float s = ScoreSpot(c, hp, baseT);
            if (s > bestS) { bestS = s; best = c; }
        }
        return best;
    }

    float ScoreSpot(Vector3 c, Vector3 hp, Vector3 baseT)
    {
        float open = Mathf.Min(MinOppDist(c), 10f);
        float lane = Mathf.Min(MinOppToSegment(hp, c, true), 6f);
        float fwd = Mathf.Clamp((c.x - hp.x) * attackDirection, -5f, 25f);
        float spacing = Mathf.Min(MinMateDist(c), 10f);
        float dBase = Vector3.Distance(c, baseT);
        float dHold = Vector3.Distance(c, hp);
        float s = open * 1.5f + lane * 2f + fwd * 0.25f + spacing * 0.8f - dBase * 0.25f;
        if (dHold < 6f) s -= 4f;
        if (dHold > 32f) s -= (dHold - 32f) * 0.5f;
        return s;
    }

    // ------------------------------------------------------------------ portador del balón
    Vector3 CarrierTarget(out float sp)
    {
        sp = 0.92f;
        Vector3 pos = Flat(transform.position);
        Vector3 goal = new Vector3(attackDirection * m.HL, 0f, 0f);
        float heldFor = Time.time - m.HolderSince;

        if (isGoalkeeper)
        {
            sp = 0f;
            if (heldFor > 1.4f) GoalkeeperDistribute(pos);
            return pos;
        }

        float pressure = MinOppDist(pos);
        if (Time.time >= nextThink && heldFor > 0.35f)
        {
            nextThink = Time.time + 0.25f;
            Decide(pos, goal, pressure, heldFor > holdTime || pressure < 3f);
        }
        if (m.Holder != this) return pos;   // ya pateó

        Vector3 dir = (goal - pos).normalized;
        List<PlayerAI> opps = m.Teams[1 - teamId];
        for (int i = 0; i < opps.Count; i++)
        {
            if (opps[i].IsDead) continue;
            Vector3 o = Flat(opps[i].transform.position) - pos;
            float d = o.magnitude;
            if (d < 7f && d > 0.01f && Vector3.Dot(o / d, dir) > 0.2f)
            {
                float w = (7f - d) / 7f;
                dir -= (o / d) * w * 0.8f;
                float side = o.z > 0f ? -1f : 1f;
                dir += new Vector3(0f, 0f, side) * w * 1.2f;
            }
        }
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = (goal - pos).normalized;
        dir.Normalize();
        return pos + dir * 6f;
    }

    void Decide(Vector3 pos, Vector3 goal, float pressure, bool allowPass)
    {
        float distGoal = Vector3.Distance(pos, goal);
        bool lane = MinOppToSegment(pos, goal, false) > 1.8f;
        if (lane && (distGoal < 22f || (distGoal < 30f && Random.value < 0.3f))) { Shoot(pos, goal); return; }
        if (!allowPass) return;

        float score;
        PlayerAI mate = ChoosePass(pos, 5f, 40f, out score);
        if (mate == null) return;
        float dribble = FreeSpaceAhead(pos, (goal - pos).normalized) + 4f;
        if (pressure < 3f || score > dribble + 6f) PassTo(mate, pos);
    }

    PlayerAI ChoosePass(Vector3 pos, float minD, float maxD, out float best)
    {
        best = -1000f;
        PlayerAI bm = null;
        List<PlayerAI> mates = m.Teams[teamId];
        for (int i = 0; i < mates.Count; i++)
        {
            PlayerAI mate = mates[i];
            if (mate == this || mate.IsDead) continue;
            Vector3 mp = Flat(mate.transform.position);
            float dist = Vector3.Distance(mp, pos);
            if (dist < minD || dist > maxD) continue;
            float lane = MinOppToSegment(pos, mp, true);
            if (lane < 1.6f) continue;
            float open = Mathf.Min(MinOppDist(mp), 8f);
            float fwd = (mp.x - pos.x) * attackDirection;
            float s = fwd * 0.6f + Mathf.Min(lane, 6f) * 2f + open * 1.2f - dist * 0.15f;
            if (mate.isGoalkeeper) s -= 12f;
            if (mate.role == Role.FWD) s += 2f;
            if (s > best) { best = s; bm = mate; }
        }
        return bm;
    }

    PlayerAI NearestMate(Vector3 pos)
    {
        PlayerAI best = null;
        float bd = 1e9f;
        List<PlayerAI> l = m.Teams[teamId];
        for (int i = 0; i < l.Count; i++)
        {
            if (l[i] == this || l[i].IsDead) continue;
            float d = Vector3.Distance(Flat(l[i].transform.position), pos);
            if (d < bd) { bd = d; best = l[i]; }
        }
        return best;
    }

    void PassTo(PlayerAI mate, Vector3 pos)
    {
        Vector3 mp = ClampToPitch(Flat(mate.transform.position) + mate.Velocity * 0.5f);
        float dist = Vector3.Distance(mp, pos);
        if (dist > 26f) KickLofted(mp, 20f, 0.8f);
        else KickGround(mp, Mathf.Clamp(dist * 0.9f + 5f, 7f, 20f));
        m.SetPassTarget(mate);
    }

    void Shoot(Vector3 pos, Vector3 goal)
    {
        PlayerAI gk = m.Teams[1 - teamId][0];
        float side = gk.transform.position.z > 0f ? -1f : 1f;
        float tz = side * Random.Range(1.3f, 3.1f) + Random.Range(-0.6f, 0.6f);
        Vector3 target = new Vector3(goal.x + attackDirection * 0.4f, 0f, tz);
        float dist = Vector3.Distance(pos, target);
        float v = Mathf.Clamp(dist * 0.5f + 15f, 17f, 26f);
        KickLofted(target, v, Random.Range(0.4f, 1.7f));
    }

    void GoalkeeperDistribute(Vector3 pos)
    {
        float s;
        PlayerAI mate = ChoosePass(pos, 8f, 50f, out s);
        if (mate == null)
        {
            KickLofted(new Vector3(attackDirection * m.HL * 0.2f, 0f, Random.Range(-15f, 15f)), 22f, 0.8f);
            return;
        }
        Vector3 mp = ClampToPitch(Flat(mate.transform.position) + mate.Velocity * 0.5f);
        float dist = Vector3.Distance(mp, pos);
        if (dist > 22f) KickLofted(mp, 22f, 0.8f);
        else KickGround(mp, Mathf.Clamp(dist * 0.9f + 5f, 8f, 20f));
        m.SetPassTarget(mate);
    }

    public void ExecuteSetPieceKick(SetPieceType type)
    {
        Vector3 pos = Flat(m.Ball.position);
        if (type == SetPieceType.Corner)
        {
            Vector3 target = new Vector3(attackDirection * (m.HL - 9f), 0f, Random.Range(-4f, 4f));
            KickLofted(target, 15f, 2.0f);
            return;
        }
        if (type == SetPieceType.GoalKick) { GoalkeeperDistribute(pos); return; }

        float s;
        PlayerAI mate = ChoosePass(pos, type == SetPieceType.KickOff ? 3f : 6f, 25f, out s);
        if (mate == null) mate = NearestMate(pos);
        if (mate == null) return;
        Vector3 mp = ClampToPitch(Flat(mate.transform.position) + mate.Velocity * 0.4f);
        if (type == SetPieceType.ThrowIn) KickLofted(mp, 11f, 1.2f);
        else KickGround(mp, 9f);
        m.SetPassTarget(mate);
    }

    // ------------------------------------------------------------------ patadas
    void KickGround(Vector3 target, float spd)
    {
        Vector3 d = Flat(target - m.Ball.position);
        if (d.sqrMagnitude < 0.01f) d = FaceDir;
        Kick(d.normalized * spd);
    }

    void KickLofted(Vector3 target, float hSpeed, float arrivalHeight)
    {
        hSpeed *= 1.08f;   // compensa el rozamiento del aire
        Vector3 from = m.Ball.position;
        Vector3 d = Flat(target - from);
        float dist = Mathf.Max(d.magnitude, 1f);
        float t = dist / hSpeed;
        float vy = (arrivalHeight - from.y + 4.905f * t * t) / t;
        Vector3 vel = d.normalized * hSpeed;
        vel.y = vy;
        Kick(vel);
    }

    void Kick(Vector3 vel)
    {
        Rigidbody rb = m.BallRb;
        rb.isKinematic = false;
        rb.velocity = vel;
        rb.angularVelocity = Vector3.Cross(Vector3.up, Flat(vel)) / 0.11f * 0.7f;
        if (GameAudio.I != null) GameAudio.I.Kick(vel.magnitude, rb.position);
        m.NotifyKick(this);
    }

    // ------------------------------------------------------------------ portero
    // Portero: 1) va al punto por donde cruzará un tiro a puerta, 2) sale a los centros altos al área,
    // 3) sale a por balones sueltos si llega primero, 4) si no, se coloca sobre la línea balón-centro del arco
    // y se adelanta cuanto más cerca esté el balón para cerrar el ángulo.
    Vector3 GoalkeeperTarget(out float sp)
    {
        sp = 1f;
        float ownX = -attackDirection * m.HL;
        Vector3 b = m.Ball.position;
        Vector3 bv = m.BallRb.velocity;
        Vector3 ballFlat = Flat(b);
        Vector3 pos = Flat(transform.position);
        float goalHalf = m.goalHalfWidth;
        bool playing = m.State == MatchState.Playing;

        // 1) tiro a puerta
        float towardGoal = bv.x * -attackDirection;
        if (playing && m.Holder == null && towardGoal > 7f)
        {
            float t = (ownX - b.x) / bv.x;
            if (t > 0f && t < 2.5f)
            {
                float yAt = b.y + bv.y * t - 4.905f * t * t;
                float zAt = b.z + bv.z * t;
                if (yAt < m.goalHeight + 0.4f && Mathf.Abs(zAt) < goalHalf + 0.8f)
                {
                    sp = 1.8f;
                    return new Vector3(ownX + attackDirection * 0.8f, 0f, Mathf.Clamp(zAt, -goalHalf, goalHalf));
                }
            }
        }

        // 2) balón alto hacia el área
        if (playing && m.Holder == null && b.y > 1.2f && bv.magnitude > 6f)
        {
            float h = b.y - 0.11f;
            float disc = bv.y * bv.y + 2f * 9.81f * h;
            if (disc > 0f)
            {
                float tl = (bv.y + Mathf.Sqrt(disc)) / 9.81f;
                Vector3 land = ballFlat + Flat(bv) * tl;
                bool inBox = Mathf.Abs(land.x - ownX) < 16f && Mathf.Abs(land.z) < 20f;
                if (inBox && Vector3.Distance(land, pos) < 11f) { sp = 1.4f; return land; }
            }
        }

        // 3) balón suelto en el área y llego primero
        float distBall = Vector3.Distance(ballFlat, pos);
        bool ballInBox = Mathf.Abs(b.x - ownX) < 18f && Mathf.Abs(b.z) < 22f;
        if (playing && m.Holder == null && ballInBox && Flat(bv).magnitude < 6f)
        {
            if (distBall < MinOppDist(ballFlat) - 1.0f) { sp = 1.3f; return ballFlat; }
        }

        // 4) posición base sobre la línea balón → centro del arco
        Vector3 goalCenter = new Vector3(ownX, 0f, 0f);
        Vector3 toBall = ballFlat - goalCenter;
        float dBall = toBall.magnitude;
        Vector3 dir = dBall > 0.1f ? toBall / dBall : new Vector3(attackDirection, 0f, 0f);
        float depth = Mathf.Lerp(5.5f, 1.3f, Mathf.Clamp01(dBall / 38f));
        if (m.Holder != null && m.Holder.teamId != teamId && dBall < 16f) depth = Mathf.Min(depth + 1f, 6.5f);
        Vector3 spot = goalCenter + dir * depth;
        spot.z = Mathf.Clamp(spot.z, -goalHalf - 1f, goalHalf + 1f);
        float dx = (spot.x - ownX) * attackDirection;
        if (dx < 1f) spot.x = ownX + attackDirection * 1f;
        sp = 1.1f;
        spot.y = 0f;
        return spot;
    }

    // ------------------------------------------------------------------ balón parado
    Vector3 SetPieceTarget(out float sp)
    {
        sp = 1.1f;
        Vector3 spot = m.SetSpot;
        if (m.Taker == this) return spot;
        if (isGoalkeeper) return GoalkeeperTarget(out sp);

        bool mine = (m.SetTeam == teamId);
        if (m.SetType == SetPieceType.Corner) return CornerTarget(mine);

        Vector3 t = FormationTarget(mine, spot);
        if (mine && (role == Role.MID || role == Role.FWD))
        {
            if (Time.time >= nextEval) { nextEval = Time.time + 0.5f; supportTarget = BestSpot(t, spot); }
            t = supportTarget;
        }
        float ownX = -attackDirection * m.HL;
        float dd = (t.x - ownX) * attackDirection / (2f * m.HL);
        float maxD = 1f;
        if (m.SetType == SetPieceType.KickOff) maxD = mine ? 0.47f : 0.40f;
        else if (m.SetType == SetPieceType.GoalKick && !mine) maxD = 0.80f;
        if (dd > maxD) t.x = ownX + attackDirection * maxD * 2f * m.HL;

        if (!mine)
        {
            Vector3 away = Flat(t - spot);
            if (away.magnitude < 9f)
            {
                if (away.sqrMagnitude < 0.01f) away = new Vector3(attackDirection, 0f, 0f);
                t = spot + away.normalized * 9f;
            }
        }
        return t;
    }

    Vector3 CornerTarget(bool mine)
    {
        if (mine)
        {
            if (role == Role.DEF) return FormationTarget(true, m.SetSpot);
            float gx = attackDirection * m.HL;
            float x = gx - attackDirection * (7f + (index % 3) * 3.5f);
            float z = ((index % 4) - 1.5f) * 4.5f;
            return new Vector3(x, 0f, z);
        }
        if (role == Role.FWD) return FormationTarget(false, m.SetSpot);
        float dgx = -attackDirection * m.HL;
        float dx = dgx + attackDirection * (6f + (index % 3) * 3f);
        float dz = ((index % 4) - 1.5f) * 4f;
        return new Vector3(dx, 0f, dz);
    }

    // ------------------------------------------------------------------ utilidades de lectura del campo
    PlayerAI NearestOpponentTo(Vector3 p, float maxD)
    {
        PlayerAI best = null;
        float bd = maxD;
        List<PlayerAI> opps = m.Teams[1 - teamId];
        for (int i = 1; i < opps.Count; i++)
        {
            if (opps[i].IsDead) continue;
            float d = Vector3.Distance(Flat(opps[i].transform.position), p);
            if (d < bd) { bd = d; best = opps[i]; }
        }
        return best;
    }

    float MinOppDist(Vector3 p)
    {
        float best = 99f;
        List<PlayerAI> opps = m.Teams[1 - teamId];
        for (int i = 0; i < opps.Count; i++)
        {
            if (opps[i].IsDead) continue;
            float d = Vector3.Distance(Flat(opps[i].transform.position), p);
            if (d < best) best = d;
        }
        return best;
    }

    float MinMateDist(Vector3 p)
    {
        float best = 99f;
        List<PlayerAI> mates = m.Teams[teamId];
        for (int i = 1; i < mates.Count; i++)
        {
            if (mates[i] == this || mates[i].IsDead) continue;
            float d = Vector3.Distance(Flat(mates[i].transform.position), p);
            if (d < best) best = d;
        }
        return best;
    }

    float MinOppToSegment(Vector3 a, Vector3 b, bool includeGK)
    {
        float best = 99f;
        List<PlayerAI> opps = m.Teams[1 - teamId];
        for (int i = includeGK ? 0 : 1; i < opps.Count; i++)
        {
            if (opps[i].IsDead) continue;
            Vector3 op = Flat(opps[i].transform.position);
            if (Vector3.Distance(op, a) < 2f) continue;   // el que me presiona no bloquea el pase
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(op - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            float d = Vector3.Distance(op, a + ab * t);
            if (d < best) best = d;
        }
        return best;
    }

    float FreeSpaceAhead(Vector3 pos, Vector3 dir)
    {
        float best = 12f;
        List<PlayerAI> opps = m.Teams[1 - teamId];
        for (int i = 0; i < opps.Count; i++)
        {
            if (opps[i].IsDead) continue;
            Vector3 o = Flat(opps[i].transform.position) - pos;
            float d = o.magnitude;
            if (d < best && d > 0.01f && Vector3.Dot(o / d, dir) > 0.5f) best = d;
        }
        return best;
    }
}
