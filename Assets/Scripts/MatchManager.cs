using System.Collections.Generic;
using UnityEngine;

public enum MatchState { Menu, Intro, Playing, Goal, SetPiece, Finished }
public enum SetPieceType { KickOff, ThrowIn, Corner, GoalKick }

// Árbitro del partido: posesión, goles, fuera de juego (saques de banda, esquinas, saques de meta),
// saque de centro, tiempo y roles de presión. Lo crea MatchSetup automáticamente.
public class MatchManager : MonoBehaviour
{
    public static MatchManager I;

    public Transform Ball;
    public Rigidbody BallRb;
    public List<PlayerAI>[] Teams = new List<PlayerAI>[2];
    public string[] TeamNames = { "MILLONARIOS", "NACIONAL" };
    public Color[] TeamColors = { new Color(0.05f, 0.25f, 0.8f), new Color(0f, 0.6f, 0.2f) };
    public int[] Score = new int[2];

    public float HL = 52.5f, HW = 34f;
    public float goalHalfWidth = 3.66f, goalHeight = 2.44f;
    public float matchLengthSeconds = 360f;   // duración real de los 90 minutos
    public float keeperSaveSkill = 0.65f;     // 0 = nunca ataja, 1 = ataja todo

    public MatchState State;
    public SetPieceType SetType;
    public int SetTeam;
    public Vector3 SetSpot;
    public PlayerAI Taker;
    public bool Paused;

    // ---- sistema de puntos y combate (para el HUD)
    public class FeedItem { public string text; public float time; public bool user; }
    public int UserPoints, UserKills, UserDeaths;
    public int[] TeamKills = new int[2];
    public readonly System.Collections.Generic.List<FeedItem> Feed = new System.Collections.Generic.List<FeedItem>();
    public string NoticeText = "";
    public float NoticeTime = -99f;
    public float UserDamageTime = -99f, UserHitTime = -99f;
    public bool UserHitKill, UserHitHead;
    public Vector3 UserDamageFrom;
    public string PointsText = ""; public float PointsTextTime = -99f;
    [Tooltip("Cada saque de centro lo hace un equipo al azar (si es false: saca quien recibió el gol)")]
    public bool randomKickoff = true;
    Vector3 ballScale;
    bool ballScaled;

    // jugador humano
    public int UserTeam = -1;                 // -1 = solo mirar
    public bool UserTakesSetPiece;
    public float setPieceChoiceSeconds = 8f;
    float userDeadline;
    public float UserSetPieceRemaining { get { return Mathf.Max(0f, userDeadline - Time.time); } }

    public PlayerAI Holder;
    public float HolderSince;
    public PlayerAI PassTarget;
    public int LastTouchTeam;
    public PlayerAI[] Chaser = new PlayerAI[2];
    public PlayerAI[] Cover = new PlayerAI[2];
    public float MatchTime;

    // ---- intro de lore, cuenta atrás y final de partido
    public const float CountdownStart = 14.8f, IntroEnd = 17.8f;
    public float IntroTime { get { return Time.time - introStart; } }
    public int CountdownNumber;
    public float FinishTime;
    public float TimeLeft { get { return Mathf.Max(0f, matchLengthSeconds - MatchTime); } }
    float introStart, slowUntil;
    int lastCountdown = -1, lastTimeWarn = -1;

    float passUntil, nextRoles, goalTimer, setStart, arrivedAt;
    int concedingTeam;

    public int AttackingTeam { get { return Holder != null ? Holder.teamId : LastTouchTeam; } }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    public void Init(List<PlayerAI> a, List<PlayerAI> b, Transform ball, float hl, float hw)
    {
        I = this;
        Teams[0] = a; Teams[1] = b;
        Ball = ball; BallRb = ball.GetComponent<Rigidbody>();
        HL = hl; HW = hw;
        GameSettings.Load();
        GameSettings.Apply();
        matchLengthSeconds = GameSettings.MatchMinutes * 60f;      // el tiempo límite se elige en Ajustes
        if (GetComponent<MatchHUD>() == null) gameObject.AddComponent<MatchHUD>();
        if (GetComponent<Fx>() == null) gameObject.AddComponent<Fx>();
        if (GetComponent<WeaponTuning>() == null) gameObject.AddComponent<WeaponTuning>();
        if (GetComponent<PickupSpawner>() == null) gameObject.AddComponent<PickupSpawner>();
        if (GetComponent<AnimTunerHUD>() == null) gameObject.AddComponent<AnimTunerHUD>();
        if (GameSession.HasChoice) StartMatch(GameSession.Team); else ShowMenu();
    }

    void OnDestroy() { Time.timeScale = 1f; AudioListener.pause = false; if (I == this) I = null; }

    public void ResetMatch()
    {
        CleanupPodium();
        RestoreBall();
        Score[0] = 0; Score[1] = 0; MatchTime = 0f;
        ResetCombat();
        BeginKickOff(PickKickoffTeam(0));
        if (GameAudio.I != null) GameAudio.I.PlayMusic(MusicTrack.Match);
    }

    // ---------------------------------------------------------------- bucle
    void Update()
    {
        if (Ball == null) return;
        if (slowUntil > 0f && Time.unscaledTime > slowUntil) { slowUntil = 0f; if (!Paused) Time.timeScale = 1f; }
        if (Input.GetKeyDown(KeyCode.Escape) && State != MatchState.Menu && State != MatchState.Intro && State != MatchState.Finished) SetPaused(!Paused);
        if (Paused) return;
        if (Input.GetKeyDown(KeyCode.M) && State != MatchState.Menu && State != MatchState.Intro) { GoToMainMenu(); return; }
        if (Input.GetKeyDown(KeyCode.F5) && State != MatchState.Menu && State != MatchState.Intro && State != MatchState.Finished) { ResetMatch(); return; }

        switch (State)
        {
            case MatchState.Playing: UpdatePlaying(); break;
            case MatchState.SetPiece: UpdateSetPiece(); break;
            case MatchState.Intro: UpdateIntro(); break;
            case MatchState.Menu: break;
            case MatchState.Goal:
                goalTimer -= Time.deltaTime;
                if (goalTimer <= 0f) BeginKickOff(PickKickoffTeam(concedingTeam));
                break;
        }
        if (Time.time >= nextRoles) { nextRoles = Time.time + 0.15f; AssignRoles(); }
    }

    void FixedUpdate()
    {
        if (State == MatchState.Playing && Ball != null) CheckControl();
    }

    void UpdatePlaying()
    {
        MatchTime += Time.deltaTime;
        if (MatchTime >= matchLengthSeconds) { Finish(); return; }
        int left = Mathf.CeilToInt(TimeLeft);
        if (left <= 10 && left > 0 && left != lastTimeWarn)
        {
            lastTimeWarn = left;
            if (GameAudio.I != null) GameAudio.I.CountdownTick(false);
        }

        Vector3 b = Ball.position;
        if (Mathf.Abs(b.x) > HL) { OnGoalLine(b); return; }
        if (Mathf.Abs(b.z) > HW) { OnSideLine(b); return; }
        if (b.y < -5f) { BeginKickOff(1 - LastTouchTeam); return; }

        if (PassTarget != null && Time.time > passUntil) PassTarget = null;
        if (Holder != null && Flat(Holder.transform.position - b).magnitude > 2.4f) Holder = null;
        if (Holder != null && (Holder.IsDead || !Holder.CanTouchBall)) Holder = null;
    }

    void Finish()
    {
        State = MatchState.Finished;
        Holder = null; PassTarget = null; UserTakesSetPiece = false;
        FinishTime = Time.time;
        Time.timeScale = 1f; slowUntil = 0f;
        if (GameAudio.I != null) { GameAudio.I.Whistle(3, false); GameAudio.I.PlayMusic(MusicTrack.Victory, 1.6f); }
        MatchHUD.Show("", Color.black, 0f);
        Podium.Build(this);
        ScreenOverlay.FadeFromBlack(0.9f);
    }

    // ---------------------------------------------------------------- posesión
    void CheckControl()
    {
        if (Holder != null) return;
        PlayerAI best = null;
        float bestD = 99f;
        for (int t = 0; t < 2; t++)
        {
            List<PlayerAI> list = Teams[t];
            for (int i = 0; i < list.Count; i++)
            {
                PlayerAI p = list[i];
                if (Time.time < p.noControlUntil || p.IsDead || !p.CanTouchBall) continue;
                float radius = p.isGoalkeeper ? (p.IsDiving ? 2.3f : 1.9f) : 1.0f;     // estirado llega más lejos
                float maxH = p.isGoalkeeper ? (p.IsDiving ? 2.6f : 2.4f) : 1.2f;
                float dist = Flat(p.transform.position - Ball.position).magnitude;
                if (dist < radius && Ball.position.y < maxH && dist < bestD) { best = p; bestD = dist; }
            }
        }
        if (best != null) TryControl(best);
    }

    void TryControl(PlayerAI p)
    {
        Vector3 v = BallRb.velocity;
        bool shotAtMe = p.isGoalkeeper && v.magnitude > 11f && v.x * -p.attackDirection > 0f;
        if (shotAtMe)
        {
            float r = Random.value;
            float beaten = 0.55f * (1f - keeperSaveSkill) + 0.15f;
            if (p.IsDiving) beaten *= 0.5f;        // la estirada ataja más (y más rechaces)
            if (r < beaten)                       // batido: no la toca
            {
                p.noControlUntil = Time.time + 0.9f;
                return;
            }
            if (r < beaten + 0.25f)               // rechace
            {
                p.noControlUntil = Time.time + 0.9f;
                BallRb.velocity = new Vector3(-v.x * 0.25f, 1.5f, v.z * 0.4f + Random.Range(-3f, 3f));
                LastTouchTeam = p.teamId;
                return;
            }
        }
        GiveBall(p);
    }

    void GiveBall(PlayerAI p)
    {
        Holder = p;
        HolderSince = Time.time;
        LastTouchTeam = p.teamId;
        PassTarget = null;
        float incoming = BallRb.velocity.magnitude;
        BallRb.velocity = Vector3.ClampMagnitude(BallRb.velocity, 4f) * 0.3f;
        p.OnGotBall(incoming);
    }

    // barrida (tecla E o IA): 75% se lleva el balón y el rival tropieza
    public bool SlideTackle(PlayerAI tackler)
    {
        if (Holder == null || Holder.teamId == tackler.teamId || Holder.isGoalkeeper) return false;
        if (Random.value > 0.75f) return false;
        PlayerAI loser = Holder;
        loser.noControlUntil = Time.time + 1.4f;
        loser.Trip();
        Holder = null; PassTarget = null;
        LastTouchTeam = tackler.teamId;
        tackler.noControlUntil = Time.time + 0.25f;
        BallRb.velocity = tackler.FaceDir * 5f + Vector3.up * 0.5f;
        if (GameAudio.I != null) GameAudio.I.Kick(6f, Ball.position);
        return true;
    }

    public void NotifyKick(PlayerAI p)
    {
        if (Holder == p) Holder = null;
        LastTouchTeam = p.teamId;
        p.noControlUntil = Time.time + 0.5f;
    }

    public void SetPassTarget(PlayerAI p) { PassTarget = p; passUntil = Time.time + 2.5f; }

    public bool TryTackle(PlayerAI tackler)
    {
        if (Holder == null || Holder.teamId == tackler.teamId || Holder.isGoalkeeper) return false;
        if (Time.time - HolderSince < 0.35f) return false;
        if (Random.value < 0.5f)
        {
            PlayerAI loser = Holder;
            loser.noControlUntil = Time.time + 1.0f;
            GiveBall(tackler);
            return true;
        }
        return false;
    }

    void AssignRoles()
    {
        for (int t = 0; t < 2; t++)
        {
            Chaser[t] = null; Cover[t] = null;
            if (State != MatchState.Playing) continue;
            if (Holder != null && Holder.teamId == t) continue;
            if (Holder == null && PassTarget != null && PassTarget.teamId == t) continue;
            PlayerAI c1 = null, c2 = null;
            float d1 = 1e9f, d2 = 1e9f;
            List<PlayerAI> list = Teams[t];
            for (int i = 1; i < list.Count; i++)
            {
                PlayerAI p = list[i];
                if (Time.time < p.noControlUntil || p.userControlled || p.IsDead) continue;
                float d = Flat(p.transform.position - Ball.position).magnitude;
                if (d < d1) { c2 = c1; d2 = d1; c1 = p; d1 = d; }
                else if (d < d2) { c2 = p; d2 = d; }
            }
            Chaser[t] = c1; Cover[t] = c2;
        }
    }

    // ---------------------------------------------------------------- goles y fuera de juego
    void OnGoalLine(Vector3 b)
    {
        float sideSign = Mathf.Sign(b.x);
        int defTeam = sideSign > 0f ? 1 : 0;      // Millonarios defiende -X, Nacional +X
        int attTeam = 1 - defTeam;

        if (Mathf.Abs(b.z) < goalHalfWidth && b.y < goalHeight) { Goal(attTeam); return; }

        if (LastTouchTeam == defTeam)
        {
            Vector3 spot = new Vector3(sideSign * (HL - 0.4f), 0.12f, Mathf.Sign(b.z) * (HW - 0.4f));
            BeginSetPiece(SetPieceType.Corner, attTeam, spot, "TIRO DE ESQUINA", null);
        }
        else
        {
            Vector3 spot = new Vector3(sideSign * (HL - 5.5f), 0.12f, 0f);
            BeginSetPiece(SetPieceType.GoalKick, defTeam, spot, "SAQUE DE META", null);
        }
    }

    void OnSideLine(Vector3 b)
    {
        int team = 1 - LastTouchTeam;
        Vector3 spot = new Vector3(Mathf.Clamp(b.x, -HL + 4f, HL - 4f), 0.12f, Mathf.Sign(b.z) * (HW - 0.2f));
        BeginSetPiece(SetPieceType.ThrowIn, team, spot, "SAQUE DE BANDA", null);
    }

    void Goal(int attTeam)
    {
        Score[attTeam]++;
        if (UserTeam == attTeam) AddPoints(300, "¡GOL!");
        if (GameAudio.I != null) GameAudio.I.Goal();
        State = MatchState.Goal;
        goalTimer = 4.5f;
        Time.timeScale = 0.35f; slowUntil = Time.unscaledTime + 1.4f;                    // cámara lenta en el gol
        for (int i = 0; i < Teams[attTeam].Count; i++)
        {
            PlayerCombat cel = Teams[attTeam][i].Combat;
            if (cel != null && !cel.IsDead) cel.heroStance = 4;                          // celebran con los brazos arriba
        }
        Fx.Confetti(new Vector3(Mathf.Sign(Ball.position.x) * HL, 3f, 0f), 140);
        if (PlayerController.I != null) PlayerController.I.Shake(0.8f);
        concedingTeam = 1 - attTeam;
        Holder = null; PassTarget = null;
        MatchHUD.Show("¡GOOOL DE " + TeamNames[attTeam] + "!\n" + Score[0] + " - " + Score[1],
                      TeamColors[attTeam], 4.3f);
    }

    // ---------------------------------------------------------------- jugadas a balón parado
    PlayerAI PlaceKickoffFormation(int team)
    {
        for (int t = 0; t < 2; t++)
        {
            List<PlayerAI> list = Teams[t];
            for (int i = 0; i < list.Count; i++)
            {
                PlayerAI p = list[i];
                float ownX = -p.attackDirection * HL;
                float maxD = (t == team) ? 0.47f : 0.40f;
                float depth = Mathf.Min(p.slotDepth, maxD);
                Vector3 pos = new Vector3(ownX + p.attackDirection * depth * 2f * HL, 0f, p.slotLateral * HW * 0.85f);
                if (p.Combat != null) p.Combat.ReviveForKickoff();
                p.Teleport(pos, new Vector3(p.attackDirection, 0f, 0f));
            }
        }
        int tn = Teams[team].Count;
        int ti = Random.value < 0.5f ? tn - 1 : tn - 2;               // delantero que saca: al azar
        PlayerAI taker = Teams[team][ti];
        float dir = taker.attackDirection;
        taker.Teleport(new Vector3(-dir * 0.9f, 0f, 0f), new Vector3(dir, 0f, 0f));
        Teams[team][(2 * tn - 3) - ti].Teleport(new Vector3(-dir * 1.6f, 0f, Random.value < 0.5f ? 6f : -6f), new Vector3(dir, 0f, 0f));
        return taker;
    }

    int PickKickoffTeam(int rule) { return randomKickoff ? Random.Range(0, 2) : rule; }

    void BeginKickOff(int team)
    {
        PlayerAI kickTaker = PlaceKickoffFormation(team);
        BeginSetPiece(SetPieceType.KickOff, team, new Vector3(0f, 0.12f, 0f), "SAQUE DE CENTRO\n" + TeamNames[team], kickTaker);
    }

    // Pantalla de selección de equipo (la dibuja MatchHUD)
    public void ShowMenu()
    {
        CleanupPodium();
        if (GameAudio.I != null) GameAudio.I.PlayMusic(MusicTrack.Menu, 1.2f);
        SetPaused(false);
        State = MatchState.Menu;
        UserTeam = -1; UserTakesSetPiece = false;
        Score[0] = 0; Score[1] = 0; MatchTime = 0f;
        ResetCombat();
        Holder = null; PassTarget = null; Taker = null;
        PlaceKickoffFormation(0);
        BallRb.isKinematic = false;
        BallRb.velocity = Vector3.zero;
        BallRb.angularVelocity = Vector3.zero;
        BallRb.isKinematic = true;
        Ball.position = new Vector3(0f, 0.12f, 0f);
        MenuShowcase.Begin(this);                          // poses congeladas + cámara que recorre a los jugadores
        MainMenuUI.OnEnterMenu();
        MatchHUD.Show("", Color.black, 0f);
    }

    void RestoreBall()
    {
        if (!ballScaled || Ball == null) return;
        Ball.localScale = ballScale;
        Ball.rotation = Quaternion.identity;
        ballScaled = false;
    }

    // true cuando el humano es quien saca y ya está junto al balón: no puede moverse (evita errores en el saque)
    public bool UserTakerLocked(PlayerAI p)
    {
        return State == MatchState.SetPiece && UserTakesSetPiece && Taker == p &&
               Flat(p.transform.position - SetSpot).magnitude < 1.8f;
    }

    // ============================================================ combate, puntos y respawn
    static string Label(PlayerAI p)
    {
        if (PlayerController.I != null && PlayerController.I.Controlled == p) return "TÚ";
        return p.DisplayName;
    }

    bool IsUser(PlayerAI p) { return PlayerController.I != null && PlayerController.I.Controlled == p; }

    public void AddPoints(int pts, string why)
    {
        UserPoints += pts;
        PointsText = "+" + pts + "  " + why;
        PointsTextTime = Time.time;
    }

    public void Notice(string s) { NoticeText = s; NoticeTime = Time.time; }

    public void ReleaseBall(PlayerAI p)
    {
        if (Holder != p) return;
        Holder = null;
        if (!BallRb.isKinematic) BallRb.velocity = p.FaceDir * 1.5f;
        p.noControlUntil = Time.time + 0.6f;
    }

    public Vector3 RespawnPoint(PlayerAI p)
    {
        float ownX = -p.attackDirection * HL;
        float d = Random.Range(0.10f, 0.38f);                 // en la mitad de la cancha de su propio equipo
        return new Vector3(ownX + p.attackDirection * d * 2f * HL, 0f, Random.Range(-0.8f, 0.8f) * HW);
    }

    void ResetCombat()
    {
        UserPoints = 0; UserKills = 0; UserDeaths = 0;
        TeamKills[0] = 0; TeamKills[1] = 0;
        Feed.Clear();
        PickupSpawner.ClearAll();
        for (int t = 0; t < 2; t++)
            for (int i = 0; i < Teams[t].Count; i++)
                if (Teams[t][i].Combat != null) Teams[t][i].Combat.ResetAll();
    }

    public void OnPlayerHurt(PlayerCombat victim, PlayerCombat attacker, bool head)
    {
        if (IsUser(victim.AI))
        {
            UserDamageTime = Time.time;
            UserDamageFrom = attacker != null ? attacker.transform.position : victim.transform.position;
        }
        if (attacker != null && IsUser(attacker.AI))
        {
            UserHitTime = Time.time; UserHitKill = false; UserHitHead = head;
            if (GameAudio.I != null) GameAudio.I.HitTick(false);
        }
    }

    public void OnPlayerDied(PlayerCombat victim, PlayerCombat killer, bool headshot, string weapon)
    {
        PlayerAI v = victim.AI;
        if (Holder == v) Holder = null;
        if (PassTarget == v) PassTarget = null;
        bool userKiller = killer != null && killer != victim && IsUser(killer.AI);
        bool userVictim = IsUser(v);
        string k = (killer != null && killer != victim) ? Label(killer.AI) : "ACCIDENTE";
        FeedItem f = new FeedItem();
        f.text = k + "   [" + weapon + (headshot ? " · CABEZA" : "") + "]   " + Label(v);
        f.time = Time.time; f.user = userKiller || userVictim;
        Feed.Add(f);
        if (Feed.Count > 6) Feed.RemoveAt(0);
        if (killer != null && killer != victim) TeamKills[killer.TeamId]++;
        if (userKiller)
        {
            UserKills++;
            AddPoints(100 + (headshot ? 50 : 0), headshot ? "BAJA · CABEZA" : "BAJA");
            UserHitTime = Time.time; UserHitKill = true; UserHitHead = headshot;
            if (GameAudio.I != null) GameAudio.I.HitTick(true);
        }
        if (userVictim) UserDeaths++;
    }

    public void OnPlayerRespawned(PlayerCombat pc)
    {
        if (IsUser(pc.AI)) Notice("¡Reapareciste en tu campo!");
    }

    public void OnPickup(PlayerCombat pc, ItemType t)
    {
        if (!IsUser(pc.AI)) return;
        int slot = t == ItemType.Glock ? 2 : (t == ItemType.AK47 ? 3 : 4);
        Notice("Recogiste " + Items.Name(t) + " · tecla " + slot);
        AddPoints(10, "OBJETO");
        if (GameAudio.I != null) GameAudio.I.Pickup();
    }

    public void SetPaused(bool paused)
    {
        Paused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // team = 0 Millonarios, 1 Nacional, -1 solo mirar (IA vs IA)
    public void StartMatch(int team)
    {
        UserTeam = team;
        BeginIntro();
    }

    // ---- intro cinematográfica (lore) + cuenta atrás 3-2-1
    public void BeginIntro()
    {
        MenuShowcase.End();
        matchLengthSeconds = GameSettings.MatchMinutes * 60f;        // la duración se elige al darle JUGAR
        SetPaused(false);
        CleanupPodium();
        RestoreBall();
        Score[0] = 0; Score[1] = 0; MatchTime = 0f;
        ResetCombat();
        Holder = null; PassTarget = null; Taker = null; UserTakesSetPiece = false;
        PlaceKickoffFormation(0);
        BallRb.isKinematic = false;
        BallRb.velocity = Vector3.zero;
        BallRb.angularVelocity = Vector3.zero;
        BallRb.isKinematic = true;
        Ball.position = new Vector3(0f, 0.12f, 0f);
        MatchHUD.Show("", Color.black, 0f);
        State = MatchState.Intro;
        introStart = Time.time;
        CountdownNumber = 0; lastCountdown = -1; lastTimeWarn = -1;
        if (GameAudio.I != null) GameAudio.I.PlayMusic(MusicTrack.Intro, 1.5f);
        ScreenOverlay.FadeFromBlack(1f);
    }

    void UpdateIntro()
    {
        float t = IntroTime;
        if (t < CountdownStart && t > 0.5f && Input.anyKeyDown) { introStart = Time.time - CountdownStart; t = CountdownStart; }   // saltar
        if (t >= CountdownStart)
        {
            CountdownNumber = Mathf.Clamp(3 - Mathf.FloorToInt(t - CountdownStart), 1, 3);
            if (CountdownNumber != lastCountdown)
            {
                lastCountdown = CountdownNumber;
                if (GameAudio.I != null) GameAudio.I.CountdownTick(false);
            }
        }
        if (t >= IntroEnd)
        {
            CountdownNumber = 0;
            if (GameAudio.I != null) { GameAudio.I.CountdownTick(true); GameAudio.I.PlayMusic(MusicTrack.Match, 1.2f); }
            ScreenOverlay.FadeFromBlack(0.5f);
            BeginKickOff(PickKickoffTeam(0));
        }
    }

    // ---- final: revancha, cambiar de equipo, menú principal
    void CleanupPodium()
    {
        Podium.Clear();
        if (!Paused && Time.timeScale == 0f) Time.timeScale = 1f;
    }

    public void Rematch()
    {
        ScreenOverlay.FadeFromBlack(0.6f);
        ResetMatch();
    }

    public void ChangeTeam() { ShowMenu(); }

    // el menú principal vive en esta misma escena (con el estadio real de fondo)
    public void GoToMainMenu()
    {
        CleanupPodium();
        GameSession.HasChoice = false;
        ShowMenu();
    }

    void BeginSetPiece(SetPieceType type, int team, Vector3 spot, string label, PlayerAI takerOverride)
    {
        State = MatchState.SetPiece;
        SetType = type; SetTeam = team; SetSpot = spot;
        Holder = null; PassTarget = null; LastTouchTeam = team;

        BallRb.isKinematic = false;
        BallRb.velocity = Vector3.zero;
        BallRb.angularVelocity = Vector3.zero;
        BallRb.isKinematic = true;
        Ball.position = spot;

        Taker = takerOverride != null ? takerOverride : PickTaker(type, team, spot);
        setStart = Time.time;
        arrivedAt = -1f;

        // si el saque es de MI equipo y el pateador está cerca, el jugador humano decide si saca (con tiempo límite)
        UserTakesSetPiece = false;
        if (UserTeam == team && Taker != null && !Taker.isGoalkeeper)
        {
            float dTaker = Flat(Taker.transform.position - spot).magnitude;
            if (dTaker <= 22f || type == SetPieceType.KickOff || type == SetPieceType.GoalKick)
            {
                UserTakesSetPiece = true;
                userDeadline = Time.time + setPieceChoiceSeconds;
            }
        }
        MatchHUD.Show(label, TeamColors[team], 2.2f);
        if (GameAudio.I != null && type != SetPieceType.KickOff) GameAudio.I.Whistle(1, false);
    }

    PlayerAI PickTaker(SetPieceType type, int team, Vector3 spot)
    {
        List<PlayerAI> list = Teams[team];
        if (type == SetPieceType.GoalKick) return list[0];
        PlayerAI best = null;
        float bd = 1e9f;
        for (int i = 1; i < list.Count; i++)
        {
            if (list[i].IsDead) continue;
            float d = Flat(list[i].transform.position - spot).magnitude;
            if (d < bd) { bd = d; best = list[i]; }
        }
        return best;
    }

    void UpdateSetPiece()
    {
        if (Taker == null) { State = MatchState.Playing; return; }
        if (Taker.IsDead)
        {
            Taker = PickTaker(SetType, SetTeam, SetSpot);
            if (Taker == null || Taker.IsDead) { State = MatchState.Playing; BallRb.isKinematic = false; UserTakesSetPiece = false; return; }
        }
        if (UserTakesSetPiece)
        {
            if (Time.time < userDeadline) return;          // esperando al humano
            UserTakesSetPiece = false;                      // se acabó el tiempo: saca la IA
            setStart = Time.time;
            arrivedAt = -1f;
        }
        Vector3 d = Flat(Taker.transform.position - SetSpot);
        float wait = SetType == SetPieceType.KickOff ? 2.2f : (SetType == SetPieceType.GoalKick ? 1.6f : 1.4f);

        if (arrivedAt < 0f && d.magnitude < 1.5f) arrivedAt = Time.time;
        if (arrivedAt < 0f && Time.time - setStart > 12f)
        {
            Vector3 dirOut = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.right;
            Taker.Teleport(SetSpot + dirOut * 1.0f, -dirOut);
            arrivedAt = Time.time;
        }
        if (arrivedAt >= 0f && Time.time - arrivedAt > wait) ExecuteSetPiece();
    }

    void ExecuteSetPiece()
    {
        State = MatchState.Playing;
        BallRb.isKinematic = false;
        if (SetType == SetPieceType.KickOff && GameAudio.I != null) GameAudio.I.Whistle(1, true);
        GiveBall(Taker);
        Taker.ExecuteSetPieceKick(SetType);
    }

    // El humano saca (clic): dir = hacia donde apunta la mira, power 0..1
    public void UserExecuteSetPiece(Vector3 dir, float power, bool aerial)
    {
        if (State != MatchState.SetPiece || !UserTakesSetPiece || Taker == null) return;
        if (Flat(Taker.transform.position - SetSpot).magnitude > 2.5f) return;
        UserTakesSetPiece = false;
        State = MatchState.Playing;
        BallRb.isKinematic = false;
        if (SetType == SetPieceType.KickOff && GameAudio.I != null) GameAudio.I.Whistle(1, true);
        GiveBall(Taker);
        bool lob = aerial || SetType == SetPieceType.ThrowIn || SetType == SetPieceType.Corner;
        if (SetType == SetPieceType.ThrowIn) Taker.nextKickAnim = (int)AnimKey.ThrowIn;
        if (SetType == SetPieceType.ThrowIn) power = Mathf.Min(power, 0.45f);   // un saque de banda no llega a 60 m
        Taker.UserKick(dir, power, lob);
    }
}
