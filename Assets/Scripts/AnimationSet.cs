using System;
using UnityEngine;

// Tiempos de UNA acción (patada, atajada...). Todo se puede ajustar en el Inspector o en vivo con F1 (AnimTunerHUD).
[Serializable]
public class ActionTiming
{
    public AnimationClip clip;
    [Tooltip("Segundos del clip que se saltan al empezar (adelanta el golpe)")]
    public float startOffset = 0.20f;
    [Tooltip("Velocidad de reproducción (1 = normal)")]
    public float speed = 1.2f;
    [Tooltip("Segundos DESPUÉS de empezar la animación en que el balón sale disparado (0 = al instante)")]
    public float releaseDelay = 0.12f;
    public float fadeIn = 0.08f;
    public float fadeOut = 0.15f;
    [Tooltip("Segundos que se recortan del final del clip")]
    public float endTrim = 0.05f;
    [Tooltip("Se congela en el último cuadro mientras el jugador siga con el balón (portero atajando)")]
    public bool holdAtEnd;
    [Tooltip("Velocidad de movimiento mientras dura la acción (1 = no frena)")]
    [Range(0f, 1f)] public float moveFactor = 0.5f;

    public ActionTiming() { }
    public ActionTiming(float startOffset, float speed, float releaseDelay, bool hold, float moveFactor)
    {
        this.startOffset = startOffset; this.speed = speed; this.releaseDelay = releaseDelay;
        this.holdAtEnd = hold; this.moveFactor = moveFactor;
    }
}

// Aquí se VINCULAN las animaciones con el juego. Se crea con  Clasico > Animaciones > Asistente  (rellena todo solo)
// o a mano: clic derecho en Project > Create > Clasico > Animation Set y arrastra los clips.
// Debe quedar en  Assets/Resources/AnimationSet.asset  para que el juego lo encuentre.
[CreateAssetMenu(fileName = "AnimationSet", menuName = "Clasico/Animation Set")]
public class AnimationSet : ScriptableObject
{
    [Tooltip("Avatar humanoide del personaje (lo rellena el asistente; solo informativo)")]
    public Avatar characterAvatar;
    [HideInInspector] public int importVersion;   // versión del importador que configuró los clips (2 = pose de descanso del personaje)

    [Header("Locomoción (se mezclan según la velocidad y la dirección)")]
    public AnimationClip idle;
    public AnimationClip goalkeeperIdle;
    public AnimationClip jogForward;
    public AnimationClip jogBackward;
    public AnimationClip strafeLeft;
    public AnimationClip strafeRight;
    [Tooltip("Opcional: 'Fast Run' de Mixamo guardado como sprint.fbx. Si no hay, se acelera el trote")]
    public AnimationClip sprint;
    [Tooltip("Velocidad (m/s) a la que el trote se ve natural. Si los pies patinan, cámbiala")]
    public float jogRefSpeed = 3.8f;
    [Tooltip("Velocidad (m/s) a la que el sprint se ve natural")]
    public float sprintRefSpeed = 6.0f;
    public float maxPlaybackSpeed = 1.45f;
    [Tooltip("Mezcla al empezar a moverse")]
    public float blendTime = 0.15f;
    [Tooltip("Mezcla al frenar (más largo = se detiene más suave)")]
    public float stopBlendTime = 0.3f;
    [Tooltip("Segundos quieto antes de que empiece la animación de reposo (antes se queda inmóvil)")]
    public float idleDelay = 6.5f;

    [Header("Jugador de campo")]
    public ActionTiming kick = new ActionTiming(0.20f, 1.25f, 0.12f, false, 0.5f);
    public ActionTiming kick2 = new ActionTiming(0.20f, 1.25f, 0.12f, false, 0.5f);
    public ActionTiming penaltyKick = new ActionTiming(0.30f, 1.1f, 0.2f, false, 0.2f);
    public ActionTiming throwIn = new ActionTiming(0.25f, 1.1f, 0.25f, false, 0.2f);
    public ActionTiming header = new ActionTiming(0.15f, 1.2f, 0.1f, false, 0.6f);
    [Tooltip("Barrida (tecla E)")]
    public ActionTiming tackle = new ActionTiming(0.1f, 1.2f, 0f, false, 0f);
    [Tooltip("El que pierde el balón en una barrida")]
    public ActionTiming trip = new ActionTiming(0f, 1.15f, 0f, false, 0f);
    [Tooltip("Recibir un pase fuerte")]
    public ActionTiming receive = new ActionTiming(0.1f, 1.3f, 0f, false, 0.6f);
    [Tooltip("Levantarse (después de la estirada o la barrida)")]
    public ActionTiming standUp = new ActionTiming(0.2f, 1.4f, 0f, false, 0f);

    [Header("Portero")]
    public ActionTiming gkCatch = new ActionTiming(0f, 1.2f, 0f, true, 0f);
    public ActionTiming gkDropKick = new ActionTiming(0.35f, 1.2f, 0.45f, false, 0.2f);
    public ActionTiming gkPass = new ActionTiming(0.30f, 1.2f, 0.3f, false, 0.3f);
    public ActionTiming gkThrow = new ActionTiming(0.30f, 1.2f, 0.35f, false, 0.2f);
    public ActionTiming gkPlace = new ActionTiming(0f, 1f, 0f, false, 0f);
    public ActionTiming gkDive = new ActionTiming(0f, 1.1f, 0f, false, 0f);
    [Tooltip("Copia en espejo de la estirada (la crea el asistente)")]
    public AnimationClip gkDiveMirror;
    [Tooltip("+1 si la estirada original va a la derecha del portero, -1 si va a la izquierda (lo detecta el asistente)")]
    public float gkDiveSide = 1f;

    [Header("Otros")]
    public ActionTiming fallen = new ActionTiming(0f, 1f, 0f, true, 0f);

    // ------------------------------------------------------------ acceso por clave
    public ActionTiming Get(AnimKey k)
    {
        switch (k)
        {
            case AnimKey.Kick: return kick;
            case AnimKey.Kick2: return kick2;
            case AnimKey.PenaltyKick: return penaltyKick;
            case AnimKey.ThrowIn: return throwIn;
            case AnimKey.Header: return header;
            case AnimKey.GkCatch: return gkCatch;
            case AnimKey.GkDropKick: return gkDropKick;
            case AnimKey.GkPass: return gkPass;
            case AnimKey.GkThrow: return gkThrow;
            case AnimKey.GkPlace: return gkPlace;
            case AnimKey.GkDive: return gkDive;
            case AnimKey.Fallen: return fallen;
            case AnimKey.Tackle: return tackle;
            case AnimKey.Trip: return trip;
            case AnimKey.Receive: return receive;
            case AnimKey.StandUp: return standUp;
            default: return null;
        }
    }

    public AnimationClip GetClip(AnimKey k)
    {
        switch (k)
        {
            case AnimKey.Idle: return idle;
            case AnimKey.GkIdle: return goalkeeperIdle;
            case AnimKey.JogF: return jogForward;
            case AnimKey.JogB: return jogBackward;
            case AnimKey.StrafeL: return strafeLeft;
            case AnimKey.StrafeR: return strafeRight;
            case AnimKey.Sprint: return sprint;
            default: { ActionTiming t = Get(k); return t != null ? t.clip : null; }
        }
    }

    public void SetClip(AnimKey k, AnimationClip c)
    {
        switch (k)
        {
            case AnimKey.Idle: idle = c; break;
            case AnimKey.GkIdle: goalkeeperIdle = c; break;
            case AnimKey.JogF: jogForward = c; break;
            case AnimKey.JogB: jogBackward = c; break;
            case AnimKey.StrafeL: strafeLeft = c; break;
            case AnimKey.StrafeR: strafeRight = c; break;
            case AnimKey.Sprint: sprint = c; break;
            default: { ActionTiming t = Get(k); if (t != null) t.clip = c; break; }
        }
    }
}
