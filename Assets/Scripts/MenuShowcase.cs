using System.Collections.Generic;
using UnityEngine;

// Fondo del MENÚ PRINCIPAL: dos filas de jugadores (Millonarios y Nacional) cara a cara en la cancha, congelados en poses
// (apuntando con AK-47, con pistola, con granada, celebrando, pateando, portero atajando...) mientras la cámara
// recorre el pasillo entre ambos equipos pasando de jugador en jugador, en bucle. Sirve de adelanto del juego.
public static class MenuShowcase
{
    class Shot { public Vector3 camPos, look; public float fov; }
    class Pending { public PlayerAI p; public AnimKey key; public bool atRelease; }

    static readonly List<Shot> shots = new List<Shot>();
    static readonly List<PlayerAI> staged = new List<PlayerAI>();
    static readonly List<Pending> pending = new List<Pending>();
    static bool active, first;
    static int idx;
    static float shotStart;
    static Vector3 fromPos, fromLook;
    static float fromFov;
    const float Dwell = 4.2f, Move = 1.6f;

    public static void Begin(MatchManager m)
    {
        End();
        active = true; first = true;
        float rowX = 3.4f;
        for (int t = 0; t < 2; t++)
        {
            List<PlayerAI> list = m.Teams[t];
            int n = list.Count;
            for (int i = 0; i < n; i++)
            {
                PlayerAI p = list[i];
                float z = (i - (n - 1) * 0.5f) * 2.7f;
                Vector3 face = new Vector3(t == 0 ? 1f : -1f, 0f, 0f);
                PlayerCombat cb = p.Combat;
                if (cb != null) cb.ResetAll();
                p.Teleport(new Vector3(t == 0 ? -rowX : rowX, 0f, z), face);
                staged.Add(p);
                AssignPose(p, t, i);
            }
        }
        BuildShots(m);
        idx = 0;
        shotStart = Time.time;
    }

    public static void End()
    {
        if (!active) return;
        active = false;
        for (int i = 0; i < staged.Count; i++)
        {
            PlayerAI p = staged[i];
            if (p == null) continue;
            MixamoAnimator mx = p.GetComponent<MixamoAnimator>();
            if (mx != null) mx.ClearAction();
            PlayerCombat cb = p.Combat;
            if (cb != null) cb.ResetAll();
        }
        staged.Clear(); shots.Clear(); pending.Clear();
    }

    static void Give(PlayerCombat cb, ItemType t)
    {
        if (cb == null) return;
        if (t == ItemType.AK47) { cb.hasAK = true; cb.akMag = 30; cb.akReserve = 60; }
        else if (t == ItemType.Glock) { cb.hasGlock = true; cb.glockMag = 15; cb.glockReserve = 45; }
        else cb.grenades = 3;
        cb.Equip(t);
    }

    static void AssignPose(PlayerAI p, int team, int i)
    {
        PlayerCombat cb = p.Combat;
        int kind = (team * 3 + i) % 8;
        if (p.isGoalkeeper) kind = 7;
        switch (kind)
        {
            case 0: Give(cb, ItemType.AK47); break;
            case 1: Give(cb, ItemType.Glock); break;
            case 2: Give(cb, ItemType.Grenade); break;
            case 3: if (cb != null) cb.heroStance = 4; break;
            case 4: if (cb != null) cb.heroStance = 5; break;
            case 5: if (cb != null) cb.heroStance = 6; break;
            case 6: Queue(p, AnimKey.Kick, true, 5); break;
            default: Queue(p, AnimKey.GkCatch, false, 5); break;
        }
    }

    // pose de animación Mixamo congelada (si no hay animaciones, queda una pose por código)
    static void Queue(PlayerAI p, AnimKey key, bool atRelease, int fallbackHero)
    {
        Pending pe = new Pending(); pe.p = p; pe.key = key; pe.atRelease = atRelease;
        pending.Add(pe);
        PlayerCombat cb = p.Combat;
        if (cb != null) cb.heroStance = 0;
    }

    static void ApplyPending()
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            Pending pe = pending[i];
            MixamoAnimator mx = pe.p != null ? pe.p.GetComponent<MixamoAnimator>() : null;
            if (mx == null || mx.Failed)
            {
                if (pe.p != null && pe.p.Combat != null) pe.p.Combat.heroStance = 5;     // sin animaciones: pose de victoria por código
                pending.RemoveAt(i);
                continue;
            }
            if (!mx.Ready) continue;
            ActionTiming t = AnimLibrary.Timing(pe.key);
            float time = t.clip == null ? 0f : (pe.atRelease ? t.startOffset + t.releaseDelay : t.clip.length * 0.9f);
            if (!mx.FreezePose(pe.key, time) && pe.p.Combat != null) pe.p.Combat.heroStance = 5;
            pending.RemoveAt(i);
        }
    }

    static void BuildShots(MatchManager m)
    {
        shots.Clear();
        int k = 0;
        int max = Mathf.Max(m.Teams[0].Count, m.Teams[1].Count);
        for (int i = 0; i < max; i++)
            for (int t = 0; t < 2; t++)
            {
                if (i >= m.Teams[t].Count) continue;
                PlayerAI p = m.Teams[t][i];
                Vector3 pp = p.transform.position;
                Vector3 f = new Vector3(t == 0 ? 1f : -1f, 0f, 0f);
                Vector3 right = Vector3.Cross(Vector3.up, f);
                float side = (k % 2 == 0) ? 1f : -1f;
                Shot s = new Shot();
                s.camPos = pp + f * 2.7f + right * (0.9f * side) + Vector3.up * (1.15f + 0.2f * ((k / 2) % 3));
                s.look = pp + Vector3.up * 1.3f;
                s.fov = 34f + (k % 3) * 3f;
                shots.Add(s);
                k++;
            }
    }

    // la llama PlayerController cada cuadro mientras el estado es Menu
    public static void UpdateCamera(Camera cam, float dt)
    {
        if (!active || shots.Count == 0 || cam == null) return;
        ApplyPending();
        if (first)
        {
            first = false;
            fromPos = cam.transform.position;
            fromLook = cam.transform.position + cam.transform.forward * 8f;
            fromFov = cam.fieldOfView;
            shotStart = Time.time;
        }
        Shot s = shots[idx];
        float t = Time.time - shotStart;
        float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Move));
        Vector3 dirToLook = (s.look - s.camPos).normalized;
        Vector3 drift = Vector3.Cross(Vector3.up, dirToLook) * Mathf.Sin(Time.time * 0.45f) * 0.45f + Vector3.up * Mathf.Sin(Time.time * 0.3f) * 0.07f;
        Vector3 pos = Vector3.Lerp(fromPos, s.camPos + drift, u);
        Vector3 look = Vector3.Lerp(fromLook, s.look, u);
        float fov = Mathf.Lerp(fromFov, s.fov, u);
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
        cam.fieldOfView = fov;
        if (t > Move + Dwell)
        {
            idx = (idx + 1) % shots.Count;
            shotStart = Time.time;
            fromPos = pos; fromLook = look; fromFov = fov;
        }
    }
}
