using System.Collections.Generic;
using UnityEngine;

// Control del jugador humano.
//  - Fútbol (casilla 1): clic izq. = tiro raso (con >50% de fuerza va a la mira), clic der. = tiro aéreo; mantén hasta 2 s.
//  - Armas (casillas 2 y 3): clic izq. dispara, clic der. apunta (ADS), R recarga.
//  - Granadas (casilla 4): mantén el clic para dar fuerza y suelta para lanzar (se dibuja la trayectoria).
//  - Casillas con 1-4 o con la rueda del mouse. V = primera persona (vista FPS). Z / X = zoom de la cámara.
//  - Cámara sobre el hombro, libre con el mouse. El control pasa solo al compañero más cercano al balón
//    mientras no lleves armas (nunca al portero). Si te eliminan, reapareces a los 5 s en tu campo.
public class PlayerController : MonoBehaviour
{
    public static PlayerController I;

    [Header("Cámara sobre el hombro")]
    public float mouseSensitivity = 3f;
    public bool invertY = false;
    public float cameraDistance = 6.5f;
    public float minDistance = 3.5f, maxDistance = 18f;
    public float shoulderOffset = 0.9f;
    public float pivotHeight = 1.6f;
    public float startPitch = 12f;
    public float minPitch = -40f;
    public float maxPitch = 28f;
    public float minCameraHeight = 0.5f;
    public float fieldOfView = 62f;
    public float fpsFieldOfView = 75f;

    [Header("Tiro")]
    public float maxChargeSeconds = 2f;

    public PlayerAI Controlled { get; private set; }
    public float ChargePower { get; private set; }
    public bool Charging { get { return chargingButton >= 0; } }
    public bool ChargingAerial { get { return chargingButton == 1; } }
    public bool Ads { get; private set; }
    public bool FirstPerson { get { return firstPerson; } }
    public Vector3 AimDir { get; private set; }
    public Vector3 AimPoint { get; private set; }

    public PlayerCombat Combat { get { return Controlled != null ? Controlled.Combat : null; } }
    public ItemType Equipped { get { PlayerCombat c = Combat; return c != null ? c.equipped : ItemType.Ball; } }

    // tiro raso con más de 50% de fuerza: va exactamente al centro de la mira
    public bool AimAssistActive
    {
        get
        {
            return Charging && !ChargingAerial && ChargePower > 0.5f && Equipped == ItemType.Ball &&
                   MatchManager.I != null && MatchManager.I.State == MatchState.Playing;
        }
    }

    Camera cam;
    float yaw = 90f, pitch = 12f;
    int chargingButton = -1;
    float chargeStart, lastSwitch, menuAngle, lastWheel;
    bool hasSmooth, firstPerson;
    int lastUserTeam = -2;
    Vector3 smoothPivot;
    GameObject marker, landMarker, viewModel;
    Transform viewMuzzle;
    ItemType viewType = (ItemType)(-1);
    LineRenderer trail;
    readonly List<Vector3> pts = new List<Vector3>();
    readonly RaycastHit[] rayHits = new RaycastHit[16];

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void Awake() { I = this; AimDir = Vector3.right; }
    void OnDestroy() { if (I == this) I = null; }

    void Start()
    {
        cam = Camera.main;
        if (cam == null) cam = FindObjectOfType<Camera>();
        if (cam == null)
        {
            GameObject go = new GameObject("Camera");
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, 600f);
        cam.nearClipPlane = 0.1f;
        cam.fieldOfView = fieldOfView;
    }

    public bool IsUser(PlayerCombat pc) { return pc != null && Controlled != null && pc.AI == Controlled; }

    public void AddRecoil(float deg)
    {
        pitch = Mathf.Clamp(pitch - deg, minPitch, maxPitch);
        yaw += Random.Range(-0.3f, 0.3f) * deg;
    }

    Vector3 YawForward()
    {
        float r = yaw * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r));
    }

    void SetCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // ============================================================ entrada
    void Update()
    {
        MatchManager m = MatchManager.I;
        if (m == null) return;

        if (m.State == MatchState.Menu || m.UserTeam < 0)
        {
            SetCursor(false);
            ReleaseControl();
            chargingButton = -1; ChargePower = 0f; Ads = false; firstPerson = false;
            lastUserTeam = -2;
            HidePreview();
            return;
        }
        if (lastUserTeam != m.UserTeam)
        {
            lastUserTeam = m.UserTeam;
            yaw = m.UserTeam == 0 ? 90f : -90f;
            pitch = startPitch;
            hasSmooth = false;
            firstPerson = false;
        }

        if (m.Paused)
        {
            SetCursor(false);
            chargingButton = -1; ChargePower = 0f; Ads = false;
            Drive(Vector3.zero, false, Vector3.zero);
            HidePreview();
            return;
        }
        SetCursor(true);
        UpdateControlled(m);

        // cámara libre
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity * (invertY ? -1f : 1f);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        float zoom = (Input.GetKey(KeyCode.Z) ? 1f : 0f) - (Input.GetKey(KeyCode.X) ? 1f : 0f);
        cameraDistance = Mathf.Clamp(cameraDistance + zoom * 6f * Time.deltaTime, minDistance, maxDistance);

        PlayerCombat cb = Combat;
        bool dead = cb != null && cb.IsDead;
        if (dead)
        {
            Drive(Vector3.zero, false, Vector3.zero);
            chargingButton = -1; ChargePower = 0f; Ads = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.V)) firstPerson = !firstPerson;
        HandleInventory(cb, m);
        ItemType eq = cb != null ? cb.equipped : ItemType.Ball;

        // movimiento relativo a la cámara
        Vector3 fwd = YawForward();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 dir = fwd * Input.GetAxisRaw("Vertical") + right * Input.GetAxisRaw("Horizontal");
        if (dir.sqrMagnitude > 1f) dir.Normalize();
        bool sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool locked = Controlled != null && m.UserTakerLocked(Controlled);
        bool armedStance = eq != ItemType.Ball;
        Drive(dir, sprint, (chargingButton >= 0 || locked || armedStance) ? AimDir : Vector3.zero);

        if (cb != null)
        {
            cb.aimPoint = AimPoint;
            cb.aimActive = armedStance;
        }

        Ads = false;
        if (eq == ItemType.Ball) HandleFootball(m);
        else if (eq == ItemType.Glock || eq == ItemType.AK47) { chargingButton = -1; ChargePower = 0f; HandleGun(m, cb); }
        else HandleGrenade(m, cb);
        if (cb != null) cb.spreadMul = Ads ? 0.5f : 1f;
    }

    void HandleInventory(PlayerCombat cb, MatchManager m)
    {
        if (cb == null || chargingButton >= 0) return;
        if (m.State == MatchState.SetPiece && m.UserTakesSetPiece) { cb.Equip(ItemType.Ball); return; }   // para sacar hace falta el balón
        if (Input.GetKeyDown(KeyCode.Alpha1)) TryEquip(cb, ItemType.Ball, m);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TryEquip(cb, ItemType.Glock, m);
        if (Input.GetKeyDown(KeyCode.Alpha3)) TryEquip(cb, ItemType.AK47, m);
        if (Input.GetKeyDown(KeyCode.Alpha4)) TryEquip(cb, ItemType.Grenade, m);

        float w = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(w) > 0.01f && Time.time - lastWheel > 0.12f)
        {
            lastWheel = Time.time;
            int dir = w > 0f ? -1 : 1;
            int cur = (int)cb.equipped;
            for (int k = 1; k <= 4; k++)
            {
                int idx = ((cur + dir * k) % 4 + 4) % 4;
                if (cb.HasItem((ItemType)idx)) { cb.Equip((ItemType)idx); break; }
            }
        }
    }

    void TryEquip(PlayerCombat cb, ItemType t, MatchManager m)
    {
        if (!cb.HasItem(t)) { m.Notice("No tienes " + Items.Name(t)); return; }
        cb.Equip(t);
    }

    // ---- fútbol
    void HandleFootball(MatchManager m)
    {
        if (chargingButton < 0)
        {
            ChargePower = 0f;
            if (CanShoot(m))
            {
                if (Input.GetMouseButtonDown(0)) { chargingButton = 0; chargeStart = Time.time; }
                else if (Input.GetMouseButtonDown(1)) { chargingButton = 1; chargeStart = Time.time; }
            }
        }
        else
        {
            ChargePower = Mathf.Clamp01((Time.time - chargeStart) / maxChargeSeconds);
            if (Input.GetMouseButtonUp(chargingButton))
            {
                Fire(m, ChargePower, chargingButton == 1);
                chargingButton = -1; ChargePower = 0f;
            }
            else if (!CanShoot(m)) { chargingButton = -1; ChargePower = 0f; }
        }
    }

    bool CanShoot(MatchManager m)
    {
        if (Controlled == null) return false;
        if (m.State == MatchState.SetPiece)
            return m.UserTakesSetPiece && m.Taker == Controlled &&
                   Flat(Controlled.transform.position - m.SetSpot).magnitude < 2.5f;
        if (m.State != MatchState.Playing) return false;
        if (m.Holder == Controlled) return true;
        if (m.Holder == null)
            return Flat(Controlled.transform.position - m.Ball.position).magnitude < 1.6f && m.Ball.position.y < 1.5f;
        return false;
    }

    void Fire(MatchManager m, float power, bool aerial)
    {
        if (!CanShoot(m)) return;
        if (m.State == MatchState.SetPiece) { m.UserExecuteSetPiece(AimDir, power, aerial); return; }
        if (!aerial && power > 0.5f) Controlled.UserKickAt(AimPoint, power);
        else Controlled.UserKick(AimDir, power, aerial);
    }

    // ---- armas de fuego
    void HandleGun(MatchManager m, PlayerCombat cb)
    {
        if (cb == null || m.State != MatchState.Playing) return;
        Ads = Input.GetMouseButton(1);
        bool fire = cb.equipped == ItemType.AK47 ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
        if (fire) cb.TryFire(AimPoint, 0f);
        if (Input.GetKeyDown(KeyCode.R)) cb.Reload();
    }

    // ---- granadas
    Vector3 GrenadeVelocity(float power)
    {
        float v = Mathf.Lerp(7f, 24f, power);
        float ang = 38f * Mathf.Deg2Rad;
        Vector3 vel = AimDir * (v * Mathf.Cos(ang));
        vel.y = v * Mathf.Sin(ang);
        return vel;
    }

    void HandleGrenade(MatchManager m, PlayerCombat cb)
    {
        if (cb == null || m.State != MatchState.Playing) { chargingButton = -1; ChargePower = 0f; return; }
        if (chargingButton < 0)
        {
            ChargePower = 0f;
            if (cb.grenades > 0)
            {
                if (Input.GetMouseButtonDown(0)) { chargingButton = 0; chargeStart = Time.time; }
                else if (Input.GetMouseButtonDown(1)) { chargingButton = 1; chargeStart = Time.time; }
            }
        }
        else
        {
            ChargePower = Mathf.Clamp01((Time.time - chargeStart) / maxChargeSeconds);
            if (Input.GetMouseButtonUp(chargingButton))
            {
                cb.ThrowGrenade(GrenadeVelocity(ChargePower));
                chargingButton = -1; ChargePower = 0f;
            }
        }
    }

    void Drive(Vector3 dir, bool sprint, Vector3 face)
    {
        if (Controlled == null) return;
        Controlled.userDir = dir;
        Controlled.userSprint = sprint;
        Controlled.userFace = face;
    }

    // ============================================================ a quién controlo
    void UpdateControlled(MatchManager m)
    {
        if (Controlled != null && Controlled.IsDead) return;      // eliminado: la cámara sigue su cuerpo hasta reaparecer
        PlayerAI want = Controlled;
        int team = m.UserTeam;
        if (want != null && want.isGoalkeeper) want = null;

        if (m.State == MatchState.SetPiece && m.UserTakesSetPiece && m.Taker != null && !m.Taker.isGoalkeeper && !m.Taker.IsDead) want = m.Taker;
        else if (chargingButton >= 0 && Controlled != null && !Controlled.isGoalkeeper) want = Controlled;
        else if (m.Holder != null && m.Holder.teamId == team && !m.Holder.isGoalkeeper) want = m.Holder;
        else
        {
            PlayerAI near = NearestToBall(m, team);
            if (want == null || Input.GetKeyDown(KeyCode.Tab)) want = near;
            else if (near != null && near != want && Time.time - lastSwitch > 0.6f)
            {
                float dc = Flat(want.transform.position - m.Ball.position).magnitude;
                float dn = Flat(near.transform.position - m.Ball.position).magnitude;
                if (dn + 3f < dc) want = near;
            }
        }

        // con armas o granadas en el inventario NO se cambia de jugador solo (el inventario es de este jugador)
        PlayerCombat cc = Controlled != null ? Controlled.Combat : null;
        bool armed = cc != null && (cc.HasGun || cc.grenades > 0 || cc.equipped != ItemType.Ball);
        bool takesSetPiece = m.State == MatchState.SetPiece && m.UserTakesSetPiece;
        if (armed && !takesSetPiece && !Input.GetKeyDown(KeyCode.Tab) && Controlled != null) want = Controlled;

        if (want != null && want != Controlled) SetControlled(want);
        else if (want == null && Controlled != null) ReleaseControl();
    }

    PlayerAI NearestToBall(MatchManager m, int team)
    {
        PlayerAI best = null;
        float bd = 1e9f;
        List<PlayerAI> list = m.Teams[team];
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].isGoalkeeper || list[i].IsDead) continue;
            float d = Flat(list[i].transform.position - m.Ball.position).magnitude;
            if (d < bd) { bd = d; best = list[i]; }
        }
        return best;
    }

    static GameObject MakeDisc(string name, Color color, float diameter)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        Renderer r = go.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        go.transform.localScale = new Vector3(diameter, 0.01f, diameter);
        return go;
    }

    void ClearBodyFlags()
    {
        PlayerCombat c = Combat;
        if (c != null) { c.hideBody = false; c.hideWeaponVisual = false; c.useOverrideMuzzle = false; c.aimActive = false; }
    }

    void SetControlled(PlayerAI p)
    {
        if (Controlled != null) { ClearBodyFlags(); Controlled.userControlled = false; Controlled.userDir = Vector3.zero; }
        Controlled = p;
        p.userControlled = true;
        lastSwitch = Time.time;
        if (marker == null) marker = MakeDisc("UserMarker", new Color(1f, 0.9f, 0.1f, 0.8f), 1.3f);
        marker.SetActive(true);
    }

    void ReleaseControl()
    {
        if (Controlled != null) { ClearBodyFlags(); Controlled.userControlled = false; Controlled.userDir = Vector3.zero; Controlled = null; }
        if (marker != null) marker.SetActive(false);
        DestroyViewModel();
    }

    // ============================================================ cámara
    void LateUpdate()
    {
        MatchManager m = MatchManager.I;
        if (cam == null || m == null) return;
        float dt = Time.deltaTime;

        if (marker != null && marker.activeSelf && Controlled != null)
        {
            Vector3 p = Controlled.transform.position;
            p.y = Controlled.LastGroundY + 0.04f;
            marker.transform.position = p;
            marker.SetActive(!firstPerson || Combat == null || Combat.IsDead);
        }

        if (m.State == MatchState.Menu) { OrbitMenu(m, dt); return; }
        if (m.UserTeam < 0 || Controlled == null) { Spectator(m, dt); return; }

        PlayerCombat cb = Combat;
        bool dead = cb != null && cb.IsDead;
        bool fps = firstPerson && !dead;

        if (cb != null)
        {
            cb.hideBody = fps;
            cb.hideWeaponVisual = fps;
            cb.useOverrideMuzzle = fps;
        }

        float wantFov = fps ? (Ads ? 52f : fpsFieldOfView) : (Ads ? 48f : fieldOfView);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, wantFov, Mathf.Clamp01(12f * dt));

        if (fps)
        {
            Vector3 head = Controlled.transform.position + Vector3.up * (Controlled.playerHeight * 0.93f);
            Quaternion r = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.position = head + r * Vector3.forward * 0.12f;
            cam.transform.rotation = r;
            UpdateViewModel(cb, true);
            if (cb != null)
                cb.overrideMuzzle = viewMuzzle != null ? viewMuzzle.position : cam.transform.position + cam.transform.forward * 0.6f;
        }
        else
        {
            UpdateViewModel(cb, false);
            Vector3 pivot = Controlled.transform.position;
            pivot.y = Controlled.LastGroundY + pivotHeight;
            if (!hasSmooth) { smoothPivot = pivot; hasSmooth = true; }
            smoothPivot = Vector3.Lerp(smoothPivot, pivot, Mathf.Clamp01(16f * dt));

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            float off = Ads ? shoulderOffset * 0.6f : shoulderOffset;
            Vector3 camPos = smoothPivot + rot * Vector3.back * cameraDistance + rot * Vector3.right * off;
            float minY = Controlled.LastGroundY + minCameraHeight;
            if (camPos.y < minY) camPos.y = minY;
            cam.transform.position = camPos;
            cam.transform.rotation = rot;
        }

        AimPoint = ComputeAimPoint();
        AimDir = ComputeAimDir();
        UpdatePreview(m);
    }

    // arma en primera persona (modelo pegado a la cámara)
    void UpdateViewModel(PlayerCombat cb, bool show)
    {
        ItemType eq = cb != null ? cb.equipped : ItemType.Ball;
        if (!show || eq == ItemType.Ball || cb == null || !cb.HasItem(eq)) { DestroyViewModel(); return; }
        if (viewModel == null || viewType != eq)
        {
            DestroyViewModel();
            viewModel = WeaponModels.Build(eq, out viewMuzzle);
            viewModel.transform.SetParent(cam.transform, false);
            viewType = eq;
        }
        Vector3 hip = eq == ItemType.AK47 ? new Vector3(0.22f, -0.2f, 0.5f)
                    : (eq == ItemType.Glock ? new Vector3(0.16f, -0.14f, 0.38f) : new Vector3(0.18f, -0.18f, 0.38f));
        Vector3 aim = eq == ItemType.Grenade ? hip : new Vector3(0f, -0.115f, 0.46f);
        viewModel.transform.localPosition = Vector3.Lerp(viewModel.transform.localPosition, Ads ? aim : hip, Mathf.Clamp01(14f * Time.deltaTime));
        viewModel.transform.localRotation = Quaternion.Euler(WeaponModels.Euler(eq));
    }

    void DestroyViewModel()
    {
        if (viewModel != null) Destroy(viewModel);
        viewModel = null; viewMuzzle = null; viewType = (ItemType)(-1);
    }

    // Punto 3D al que apunta el centro de la pantalla: primer objeto sólido que corta la mira.
    Vector3 ComputeAimPoint()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        int n = Physics.RaycastNonAlloc(ray, rayHits, 150f, ~0, QueryTriggerInteraction.Ignore);
        float best = 1e9f;
        Vector3 point = ray.origin + ray.direction * 60f;
        for (int i = 0; i < n; i++)
        {
            Collider c = rayHits[i].collider;
            if (c is CharacterController || c.attachedRigidbody != null) continue;
            if (rayHits[i].distance < best) { best = rayHits[i].distance; point = rayHits[i].point; }
        }
        return point;
    }

    Vector3 ComputeAimDir()
    {
        Vector3 d = Flat(AimPoint - Controlled.transform.position);
        if (d.magnitude < 1.5f) d = YawForward();
        return d.normalized;
    }

    void OrbitMenu(MatchManager m, float dt)
    {
        menuAngle += 14f * dt;
        float a = menuAngle * Mathf.Deg2Rad;
        Vector3 c = m.Ball.position;
        cam.fieldOfView = fieldOfView;
        cam.transform.position = c + new Vector3(Mathf.Sin(a) * 3.4f, 0.35f + Mathf.Sin(a * 0.7f) * 0.25f, Mathf.Cos(a) * 3.4f);
        cam.transform.rotation = Quaternion.LookRotation(c - cam.transform.position, Vector3.up);
    }

    void Spectator(MatchManager m, float dt)
    {
        Vector3 b = m.Ball.position;
        cam.fieldOfView = fieldOfView;
        Vector3 desired = new Vector3(b.x * 0.8f, 26f, -42f);
        cam.transform.position = Vector3.Lerp(cam.transform.position, desired, Mathf.Clamp01(2f * dt));
        Quaternion want = Quaternion.LookRotation(b - cam.transform.position, Vector3.up);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, want, Mathf.Clamp01(4f * dt));
    }

    // ============================================================ trayectoria (balón y granada)
    void HidePreview()
    {
        if (trail != null) trail.enabled = false;
        if (landMarker != null) landMarker.SetActive(false);
    }

    void UpdatePreview(MatchManager m)
    {
        ItemType eq = Equipped;
        bool football = eq == ItemType.Ball && Charging && Controlled != null && CanShoot(m);
        bool grenade = eq == ItemType.Grenade && Charging && Controlled != null && m.State == MatchState.Playing;
        if (!(football || grenade)) { HidePreview(); return; }

        if (trail == null)
        {
            GameObject go = new GameObject("TrayectoriaTiro");
            trail = go.AddComponent<LineRenderer>();
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.useWorldSpace = true;
            trail.numCapVertices = 4;
            trail.numCornerVertices = 2;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }
        if (landMarker == null) landMarker = MakeDisc("TrayectoriaFin", new Color(1f, 1f, 1f, 0.35f), 0.9f);

        float power = ChargePower;
        float gy = Controlled.LastGroundY;
        Vector3 dir = AimDir;
        pts.Clear();
        bool assist = false, aerial = false;
        float markerDia;

        if (grenade)
        {
            Vector3 start = Combat.ChestPos() + dir * 0.5f + Vector3.up * 0.15f;
            Vector3 vel = GrenadeVelocity(power);
            Vector3 p = start;
            pts.Add(p);
            for (int i = 0; i < 120; i++)
            {
                float step = 0.04f;
                vel += Physics.gravity * step;
                vel *= 1f / (1f + 0.05f * step);
                p += vel * step;
                if (p.y <= gy + 0.1f) { p.y = gy + 0.06f; pts.Add(p); break; }
                pts.Add(p);
            }
            markerDia = 13f;   // radio de la explosión
        }
        else
        {
            aerial = ChargingAerial;
            if (m.State == MatchState.SetPiece)
            {
                if (m.SetType == SetPieceType.ThrowIn) { aerial = true; power = Mathf.Min(power, 0.45f); }
                else if (m.SetType == SetPieceType.Corner) aerial = true;
            }
            Vector3 start = m.Ball.position;
            assist = !aerial && power > 0.5f && m.State == MatchState.Playing;
            if (assist)
            {
                Vector3 avel = PlayerAI.AimVelocity(start, AimPoint, PlayerAI.UserShotSpeed(power), dir);
                Vector3 flatTo = AimPoint - start; flatTo.y = 0f;
                float targetDist = flatTo.magnitude;
                if (avel.y < 0.05f)
                {
                    Vector3 s0 = start; s0.y = gy + 0.06f;
                    Vector3 e0 = start + dir * Mathf.Min(targetDist, 70f); e0.y = gy + 0.06f;
                    for (int i = 0; i <= 20; i++) pts.Add(Vector3.Lerp(s0, e0, i / 20f));
                }
                else
                {
                    Vector3 pp = start, vv = avel;
                    float travelled = 0f;
                    pts.Add(pp);
                    for (int i = 0; i < 90; i++)
                    {
                        float step = 0.05f;
                        Vector3 prev = pp;
                        vv += Physics.gravity * step;
                        vv *= 1f / (1f + 0.2f * step);
                        pp += vv * step;
                        travelled += Mathf.Sqrt((pp.x - prev.x) * (pp.x - prev.x) + (pp.z - prev.z) * (pp.z - prev.z));
                        if (pp.y <= gy + 0.11f) { pp.y = gy + 0.06f; pts.Add(pp); break; }
                        pts.Add(pp);
                        if (travelled >= targetDist) break;
                    }
                }
            }
            else if (aerial)
            {
                Vector3 vel = PlayerAI.UserKickVelocity(dir, power, true);
                Vector3 p = start;
                pts.Add(p);
                for (int i = 0; i < 90; i++)
                {
                    float step = 0.05f;
                    vel += Physics.gravity * step;
                    vel *= 1f / (1f + 0.2f * step);
                    p += vel * step;
                    if (p.y <= gy + 0.11f) { p.y = gy + 0.06f; pts.Add(p); break; }
                    pts.Add(p);
                }
            }
            else
            {
                float len = Mathf.Lerp(4f, 42f, power);
                int n = 24;
                for (int i = 0; i <= n; i++)
                {
                    Vector3 q = start + dir * (len * i / n);
                    q.y = gy + 0.06f;
                    if (Mathf.Abs(q.x) > m.HL + 0.5f || Mathf.Abs(q.z) > m.HW + 0.5f) break;
                    pts.Add(q);
                }
                if (pts.Count < 2) pts.Add(start + dir * 0.6f);
            }
            markerDia = (aerial || assist ? 1.0f : 0.6f) * Mathf.Lerp(0.8f, 1.4f, power);
        }

        Color c = grenade ? Color.Lerp(new Color(1f, 0.9f, 0.3f), new Color(1f, 0.3f, 0.2f), power)
                          : Color.Lerp(new Color(0.65f, 1f, 0.85f), new Color(1f, 0.55f, 0.3f), power);
        Gradient g = new Gradient();
        g.SetKeys(new GradientColorKey[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                  new GradientAlphaKey[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0.12f, 1f) });
        trail.colorGradient = g;
        trail.widthMultiplier = Mathf.Lerp(0.10f, 0.22f, power);
        trail.positionCount = pts.Count;
        trail.SetPositions(pts.ToArray());
        trail.enabled = true;

        Vector3 end = pts[pts.Count - 1];
        end.y = gy + 0.05f;
        landMarker.SetActive(true);
        landMarker.transform.position = end;
        landMarker.transform.localScale = new Vector3(markerDia, 0.01f, markerDia);
        Color mc = c; mc.a = grenade ? 0.16f : 0.38f;
        landMarker.GetComponent<Renderer>().sharedMaterial.color = mc;
    }
}
