using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// ============================================================================================================
//  ANIMACIONES MIXAMO (sin Animator Controller: se mezclan por código con Playables)
//  Vínculo con los clips:  Assets/Resources/AnimationSet.asset  (lo crea  Clasico > Animaciones > Asistente).
//  Ajuste de tiempos: en el Inspector de ese asset o en vivo con F1 dentro del juego (AnimTunerHUD).
// ============================================================================================================
public enum AnimKey
{
    Idle, GkIdle, JogF, JogB, StrafeL, StrafeR,
    Kick, Kick2, PenaltyKick, ThrowIn, Header,
    GkCatch, GkDropKick, GkPass, GkThrow, GkPlace, GkDive, Fallen,
    Sprint, Tackle, Trip, Receive, StandUp          // nuevos: siempre al FINAL para no desordenar los anteriores
}

// Busca los clips: 1) en el AnimationSet vinculado  2) por nombre de archivo en Resources/Animations (respaldo)
public static class AnimLibrary
{
    static bool setLoaded, loaded;
    static AnimationSet set;
    static AnimationClip[] all;
    static readonly Dictionary<AnimKey, AnimationClip> cache = new Dictionary<AnimKey, AnimationClip>();
    static readonly HashSet<AnimKey> searched = new HashSet<AnimKey>();

    public static AnimationSet Set
    {
        get
        {
            if (!setLoaded) { setLoaded = true; set = Resources.Load<AnimationSet>("AnimationSet"); }
            return set;
        }
    }

    public static string[] FileNames(AnimKey k)
    {
        switch (k)
        {
            case AnimKey.Idle: return new string[] { "offensive idle" };
            case AnimKey.GkIdle: return new string[] { "goalkeeper idle", "goalkeeper idle (2)" };
            case AnimKey.JogF: return new string[] { "jog forward" };
            case AnimKey.JogB: return new string[] { "jog backward" };
            case AnimKey.StrafeL: return new string[] { "jog strafe left" };
            case AnimKey.StrafeR: return new string[] { "jog strafe right" };
            case AnimKey.Kick: return new string[] { "kick soccerball" };
            case AnimKey.Kick2: return new string[] { "kick soccerball (2)" };
            case AnimKey.PenaltyKick: return new string[] { "soccer penalty kick" };
            case AnimKey.ThrowIn: return new string[] { "throw in" };
            case AnimKey.Header: return new string[] { "header soccerball" };
            case AnimKey.GkCatch: return new string[] { "goalkeeper catch" };
            case AnimKey.GkDropKick: return new string[] { "goalkeeper drop kick" };
            case AnimKey.GkPass: return new string[] { "goalkeeper pass" };
            case AnimKey.GkThrow: return new string[] { "goalkeeper overhand throw" };
            case AnimKey.GkPlace: return new string[] { "goalkeeper placing ball" };
            case AnimKey.GkDive: return new string[] { "goalkeeper diving save" };
            case AnimKey.Sprint: return new string[] { "sprint", "fast run", "running", "run" };
            case AnimKey.Tackle: return new string[] { "soccer tackle", "soccer tackle (2)" };
            case AnimKey.Trip: return new string[] { "soccer trip" };
            case AnimKey.Receive: return new string[] { "receive soccerball" };
            case AnimKey.StandUp: return new string[] { "standing up" };
            default: return new string[] { "fallen idle" };
        }
    }

    public static string Norm(string s)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (char c in s.ToLower()) if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }

    public static AnimationClip Get(AnimKey k)
    {
        AnimationSet s = Set;
        if (s != null) { AnimationClip fromSet = s.GetClip(k); if (fromSet != null) return fromSet; }
        AnimationClip c;
        if (cache.TryGetValue(k, out c)) return c;
        if (searched.Contains(k)) return null;
        searched.Add(k);
        foreach (string f in FileNames(k))
        {
            AnimationClip[] arr = Resources.LoadAll<AnimationClip>("Animations/" + f);
            foreach (AnimationClip a in arr)
                if (a != null && !a.name.StartsWith("__preview__")) { cache[k] = a; return a; }
        }
        if (!loaded) { loaded = true; all = Resources.LoadAll<AnimationClip>(""); }
        foreach (string f in FileNames(k))
        {
            string n = Norm(f);
            foreach (AnimationClip a in all)
                if (a != null && Norm(a.name) == n) { cache[k] = a; return a; }
        }
        return null;
    }

    // tiempos de una acción: del AnimationSet, o valores por defecto si no hay asset
    public static ActionTiming Timing(AnimKey k)
    {
        AnimationSet s = Set;
        ActionTiming t = s != null ? s.Get(k) : null;
        if (t == null) t = new ActionTiming();
        if (t.clip == null) t.clip = Get(k);
        return t;
    }
}

[DefaultExecutionOrder(-10)]
public class MixamoAnimator : MonoBehaviour
{
    public static readonly List<MixamoAnimator> All = new List<MixamoAnimator>();
    public static string GlobalError = "";

    public string Status { get; private set; }
    public bool Ready { get { return ready; } }
    public bool Failed { get { return failed; } }

    PlayerAI ai;
    ProceduralRun run;
    Animator animator;
    PlayableGraph graph;
    AnimationMixerPlayable root, loco;
    const int N = 6;
    readonly AnimationClipPlayable[] clips = new AnimationClipPlayable[N];   // reposo, adelante, atrás, izquierda, derecha, sprint
    readonly AnimationClip[] src = new AnimationClip[N];
    readonly float[] weights = new float[N];
    readonly float[] target = new float[N];
    float stillTime, idleRate;
    AnimationClipPlayable action;
    ActionTiming current;
    bool ready, failed, graphPlaying, actionActive, holdAction, freezeAction, graphBuilt;
    float phase, idleTime, actionTime, actionLen, actionWeight, actionElapsed, actionSpeed;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); Status = "Esperando..."; }
    void OnDisable() { All.Remove(this); }

    public float MoveFactor
    {
        get
        {
            if (!ready || !actionActive || current == null) return 1f;
            if (holdAction) return 0f;
            return current.moveFactor;
        }
    }

    void Fail(string msg)
    {
        failed = true;
        Status = "ERROR: " + msg;
        if (string.IsNullOrEmpty(GlobalError))
        {
            GlobalError = msg;
            Debug.LogError("MixamoAnimator (" + name + "): " + msg + "  → se usa la animación por código. Pulsa F1 en el juego para ver el diagnóstico.");
        }
        enabled = false;
    }

    // ------------------------------------------------------------ preparación y bucle
    void Update()
    {
        if (failed) return;
        if (!ready) { TryInit(); if (!ready) return; }
        float dt = Time.deltaTime;

        bool dead = ai.IsDead;
        int stance = run != null ? run.stance : 0;
        // con arma o pose de héroe: las PIERNAS y el torso siguen con Mixamo (correr de verdad) y el código solo coloca los BRAZOS
        bool wantClips = !dead;
        bool wantProcedural = !dead && stance != 0;
        if (run != null) { run.armsOnly = true; run.enabled = wantProcedural; }
        if (stance != 0 && actionActive && !freezeAction) ClearAction();     // nada de patadas con el arma en la mano
        if (wantClips != graphPlaying) { graphPlaying = wantClips; if (wantClips) graph.Play(); else graph.Stop(); }
        if (!wantClips) { if (dead) { actionActive = false; holdAction = false; freezeAction = false; } return; }

        UpdateLocomotion(dt);
        UpdateAction(dt);
    }

    void TryInit()
    {
        if (ai == null) ai = GetComponent<PlayerAI>();
        if (run == null) run = GetComponent<ProceduralRun>();
        if (ai == null) return;
        if (run != null && !run.Ready) return;            // espera a que ProceduralRun guarde la pose base
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Fail("el modelo del jugador NO tiene componente Animator. Usa Clasico > Animaciones > Asistente para crear el prefab del personaje Humanoide.");
            return;
        }
        if (animator.avatar == null || !animator.avatar.isValid)
        {
            Fail("el Animator no tiene Avatar válido. El FBX del personaje debe importarse como Humanoid (Rig > Animation Type = Humanoid).");
            return;
        }

        src[0] = AnimLibrary.Get(ai.isGoalkeeper ? AnimKey.GkIdle : AnimKey.Idle);
        if (src[0] == null) src[0] = AnimLibrary.Get(AnimKey.Idle);
        src[1] = AnimLibrary.Get(AnimKey.JogF);
        src[2] = AnimLibrary.Get(AnimKey.JogB);
        src[3] = AnimLibrary.Get(AnimKey.StrafeL);
        src[4] = AnimLibrary.Get(AnimKey.StrafeR);
        src[5] = AnimLibrary.Get(AnimKey.Sprint);
        if (src[1] == null)
        {
            Fail("no hay clip de 'jog forward'. Crea el AnimationSet con Clasico > Animaciones > Asistente (o arrastra los clips a Assets/Resources/AnimationSet.asset).");
            return;
        }
        if (src[0] == null) src[0] = src[1];
        if (src[1].isHumanMotion && !animator.isHuman)
        {
            Fail("los clips son Humanoides pero el personaje NO es Humanoide. Importa el FBX del personaje con Rig > Animation Type = Humanoid.");
            return;
        }

        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        graph = PlayableGraph.Create("Mixamo_" + name);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        loco = AnimationMixerPlayable.Create(graph, N);
        for (int i = 0; i < N; i++)
        {
            if (src[i] == null) continue;
            clips[i] = AnimationClipPlayable.Create(graph, src[i]);
            clips[i].SetSpeed(0);                    // el tiempo lo controlamos nosotros (sincroniza los pasos)
            graph.Connect(clips[i], 0, loco, i);
            loco.SetInputWeight(i, i == 0 ? 1f : 0f);
        }
        weights[0] = 1f;
        root = AnimationMixerPlayable.Create(graph, 2);
        graph.Connect(loco, 0, root, 0);
        root.SetInputWeight(0, 1f);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Mixamo", animator);
        output.SetSourcePlayable(root);
        graph.Play();
        graphPlaying = true;
        graphBuilt = true;
        ready = true;
        Status = "OK (clips: " + CountClips() + ")";
    }

    string CountClips()
    {
        int n = 0;
        for (int i = 0; i < N; i++) if (src[i] != null) n++;
        return n + "/" + N + " de locomoción" + (src[5] == null ? " (sin sprint)" : "");
    }

    void OnDestroy() { if (graphBuilt && graph.IsValid()) graph.Destroy(); }

    // ------------------------------------------------------------ correr: mezcla reposo / adelante / atrás / lateral
    void UpdateLocomotion(float dt)
    {
        AnimationSet s = AnimLibrary.Set;
        float refSpeed = s != null ? s.jogRefSpeed : 3.8f;
        float sprintRef = s != null ? s.sprintRefSpeed : 6f;
        float maxPb = s != null ? s.maxPlaybackSpeed : 1.45f;
        float blend = s != null ? s.blendTime : 0.15f;
        float stopBlend = s != null ? s.stopBlendTime : 0.3f;
        float idleDelay = s != null ? s.idleDelay : 6.5f;

        Vector3 v = ai.Velocity; v.y = 0f;
        float speed = v.magnitude;
        float vF = Vector3.Dot(v, transform.forward), vR = Vector3.Dot(v, transform.right);
        float move = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 1.0f, speed));

        float aF = Mathf.Max(0f, vF), aB = Mathf.Max(0f, -vF), aR = Mathf.Max(0f, vR), aL = Mathf.Max(0f, -vR);
        float sum = aF + aB + aR + aL + 0.0001f;
        target[0] = 1f - move;
        target[1] = move * aF / sum;
        target[2] = move * aB / sum;
        target[3] = move * aL / sum;
        target[4] = move * aR / sum;
        target[5] = 0f;
        for (int i = 2; i < 5; i++) if (src[i] == null) { target[1] += target[i]; target[i] = 0f; }
        sprintAmount = Mathf.InverseLerp(refSpeed * 1.1f, Mathf.Max(refSpeed * 1.2f, sprintRef * 0.95f), speed);
        if (src[5] != null) { target[5] = target[1] * sprintAmount; target[1] -= target[5]; }

        float total = 0f;
        for (int i = 0; i < N; i++)
        {
            float bt = (i == 0 && target[0] > weights[0]) ? stopBlend : blend;   // al frenar, más suave
            weights[i] = Mathf.MoveTowards(weights[i], target[i], dt / Mathf.Max(0.02f, bt));
            total += weights[i];
        }
        if (total < 0.0001f) { weights[0] = 1f; total = 1f; }

        // un solo "reloj" de pasos para trote y sprint (así los pies no se cruzan al mezclarlos)
        float jogRate = Mathf.Clamp(speed / refSpeed, 0.5f, maxPb) / Mathf.Max(0.2f, src[1].length);
        float rate = jogRate;
        if (src[5] != null)
        {
            float sprRate = Mathf.Clamp(speed / sprintRef, 0.6f, 1.35f) / Mathf.Max(0.2f, src[5].length);
            rate = Mathf.Lerp(jogRate, sprRate, sprintAmount);
        }
        phase = (phase + dt * rate) % 1f;

        // reposo: primero se queda QUIETO (cuadro 0 del clip); tras idleDelay segundos empieza la animación de reposo
        if (move < 0.05f) stillTime += dt; else stillTime = 0f;
        idleRate = Mathf.MoveTowards(idleRate, stillTime >= idleDelay ? 1f : 0f, dt / 0.8f);
        if (stillTime == 0f && weights[0] < 0.02f) idleTime = 0f;
        idleTime = (idleTime + dt * idleRate) % Mathf.Max(0.2f, src[0].length);

        for (int i = 0; i < N; i++)
        {
            if (src[i] == null) continue;
            loco.SetInputWeight(i, weights[i] / total);
            clips[i].SetTime(i == 0 ? idleTime : phase * src[i].length);
        }
    }

    // Sin clip de sprint: al correr rápido el torso se inclina hacia delante (se nota que esprinta)
    float sprintAmount, lean;
    Transform spine, chest;
    void LateUpdate()
    {
        if (!ready || !graphPlaying || failed) return;
        float want = (src[5] == null && !actionActive) ? sprintAmount : 0f;
        lean = Mathf.MoveTowards(lean, want, Time.deltaTime * 3f);
        if (lean < 0.001f) return;
        if (spine == null) { spine = animator.GetBoneTransform(HumanBodyBones.Spine); chest = animator.GetBoneTransform(HumanBodyBones.Chest); }
        Vector3 axis = Vector3.Cross(Vector3.up, ai.FaceDir);
        if (spine != null) spine.rotation = Quaternion.AngleAxis(9f * lean, axis) * spine.rotation;
        if (chest != null) chest.rotation = Quaternion.AngleAxis(6f * lean, axis) * chest.rotation;
    }

    // ------------------------------------------------------------ acciones puntuales (patada, atajada...)
    // devuelve el retardo (s) con el que debe salir el balón; -1 si no se pudo reproducir
    float PlayAction(AnimKey key, bool freeze, float freezeTime, AnimationClip overrideClip = null)
    {
        if (!ready) return -1f;
        ActionTiming t = AnimLibrary.Timing(key);
        AnimationClip clip = overrideClip != null ? overrideClip : t.clip;
        if (clip == null) return -1f;
        if (actionActive) { graph.Disconnect(root, 1); action.Destroy(); }
        current = t;
        float start = freeze ? freezeTime : t.startOffset;
        start = Mathf.Clamp(start, 0f, clip.length * 0.95f);
        action = AnimationClipPlayable.Create(graph, clip);
        action.SetSpeed(0);
        action.SetTime(start);
        graph.Connect(action, 0, root, 1);
        actionActive = true;
        holdAction = t.holdAtEnd || freeze;
        freezeAction = freeze;
        actionTime = start;
        actionLen = clip.length;
        actionSpeed = freeze ? 0f : Mathf.Max(0.1f, t.speed);
        actionWeight = 0f;
        actionElapsed = 0f;
        return t.releaseDelay;
    }

    void UpdateAction(float dt)
    {
        if (!actionActive) return;
        actionElapsed += dt;
        actionTime += dt * actionSpeed;
        float endT = Mathf.Max(0.05f, actionLen - current.endTrim);
        float target = 1f;
        if (holdAction)
        {
            actionTime = Mathf.Min(actionTime, endT);
            if (!freezeAction)
            {
                MatchManager m = MatchManager.I;
                if (m == null || m.Holder != ai) holdAction = false;        // soltó el balón: vuelve a moverse
            }
        }
        else if (actionTime >= endT - current.fadeOut * actionSpeed) target = 0f;
        float rate = target > 0.5f ? Mathf.Max(0.01f, current.fadeIn) : Mathf.Max(0.01f, current.fadeOut);
        actionWeight = Mathf.MoveTowards(actionWeight, target, dt / rate);
        action.SetTime(Mathf.Min(actionTime, endT));
        root.SetInputWeight(0, 1f - actionWeight);
        root.SetInputWeight(1, actionWeight);
        if (!holdAction && target == 0f && actionWeight <= 0.001f && actionElapsed > 0.1f)
        {
            graph.Disconnect(root, 1);
            action.Destroy();
            actionActive = false;
            root.SetInputWeight(0, 1f);
        }
    }

    // ------------------------------------------------------------ eventos del juego (los llama PlayerAI)
    // Devuelve el retardo con el que debe salir el balón (para que coincida con el golpe)
    public float OnKick(Vector3 vel, int overrideKey)
    {
        if (!ready || !graphPlaying) return 0f;
        AnimKey key;
        if (overrideKey >= 0) key = (AnimKey)overrideKey;
        else if (ai.isGoalkeeper) key = (vel.y > 3f || vel.magnitude > 18f) ? AnimKey.GkDropKick : AnimKey.GkPass;
        else key = AnimKey.Kick;
        if (AnimLibrary.Timing(key).clip == null) key = AnimKey.Kick;
        float d = PlayAction(key, false, 0f);
        return d < 0f ? 0f : d;
    }

    // el portero atrapa el balón y lo sostiene (se congela al final del clip) hasta que lo suelte
    public void OnCatch() { if (ready && graphPlaying) PlayAction(AnimKey.GkCatch, false, 0f); }

    // acción suelta (barrida, tropiezo, recibir, cabezazo, levantarse). Devuelve la duración en segundos (0 si no hay clip)
    public float Play(AnimKey key)
    {
        if (!ready || !graphPlaying) return 0f;
        if (PlayAction(key, false, 0f) < 0f) return 0f;
        return ActionDuration();
    }

    // estirada del portero hacia worldDir (usa el clip original o su espejo según el lado)
    public float OnDive(Vector3 worldDir)
    {
        if (!ready || !graphPlaying) return 0f;
        AnimationSet s = AnimLibrary.Set;
        float side = Mathf.Sign(Vector3.Dot(worldDir, Vector3.Cross(Vector3.up, ai.FaceDir)));   // +1 = a su derecha
        AnimationClip clip = null;
        if (s != null && s.gkDiveMirror != null && side != Mathf.Sign(s.gkDiveSide)) clip = s.gkDiveMirror;
        if (PlayAction(AnimKey.GkDive, false, 0f, clip) < 0f) return 0f;
        return ActionDuration();
    }

    float ActionDuration()
    {
        if (current == null) return 0f;
        return Mathf.Max(0.1f, (actionLen - current.endTrim - actionTime) / Mathf.Max(0.1f, actionSpeed));
    }

    public bool ActionPlaying { get { return actionActive; } }

    // ---- para el panel de ajuste (F1) y el menú principal
    public bool Preview(AnimKey key) { return ready && PlayAction(key, false, 0f) >= 0f; }

    // deja al personaje CONGELADO en un cuadro del clip (poses del menú principal). time en segundos
    public bool FreezePose(AnimKey key, float time) { return ready && PlayAction(key, true, time) >= 0f; }

    public void ClearAction()
    {
        if (ready && actionActive) { graph.Disconnect(root, 1); action.Destroy(); actionActive = false; root.SetInputWeight(0, 1f); }
        holdAction = false; freezeAction = false;
    }
}
