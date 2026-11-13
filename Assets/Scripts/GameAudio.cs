using System.Collections;
using UnityEngine;

// Audio del partido generado por código (no hace falta ningún archivo): patadas, pasos, rebotes, murmullo de grada,
// viento MUY suave, grito de gol, pito de árbitro y choque de escudos en la intro.
//
// SONIDOS REALES: si quieres grabaciones de verdad, ponlas en Assets/Resources/Audio/ con estos nombres y se usan solas
// (sustituyen al sonido generado):  kick, step, bounce, wind, crowd, goal, whistle, whistle_long, impact,
//   ak47, glock, explosion, pickup, hit, reload, hurt
// Formatos: .wav / .mp3 / .ogg. Bancos gratuitos: freesound.org (filtra por CC0), pixabay.com/sound-effects, mixkit.co.
public class GameAudio : MonoBehaviour
{
    public static GameAudio I;
    public static float AmbientLevel = 1f;          // slider "Ambiente" del menú (0 = sin viento ni grada)

    const int Rate = 22050;
    const float TwoPi = 6.2831853f;
    const float WindBase = 0.03f;                   // viento: apenas perceptible
    const float CrowdBase = 0.08f;                  // murmullo de fondo

    AudioClip kick, stepA, stepB, stepC, wind, crowd, roar, whistleShort, whistleLong, bounce, impact;
    AudioClip shotAK, shotGlock, explosion, pickup, hitTick, killTick, reload, hurt;
    float whistleShortLen = 0.38f, whistleLongLen = 0.8f;
    readonly AudioSource[] pool = new AudioSource[16];
    readonly AudioSource[] stepPool = new AudioSource[8];
    int poolIdx, stepIdx;
    AudioSource windSrc, crowdSrc;
    float lastStep, crowdBoostUntil;

    static float N() { return Random.value * 2f - 1f; }

    void Awake()
    {
        I = this;
        kick = Load("kick", MakeKick());
        AudioClip so = Resources.Load<AudioClip>("Audio/step");
        stepA = so != null ? so : MakeStep(0.20f);
        stepB = so != null ? so : MakeStep(0.27f);
        stepC = so != null ? so : MakeStep(0.23f);
        bounce = Load("bounce", MakeBounce());
        wind = Load("wind", MakeLoop("wind", 8f, false, 1, 2));
        crowd = Load("crowd", MakeLoop("crowd", 8f, true, 2, 3));
        roar = Load("goal", MakeRoar());
        AudioClip ws = Resources.Load<AudioClip>("Audio/whistle");
        AudioClip wl = Resources.Load<AudioClip>("Audio/whistle_long");
        whistleShort = ws != null ? ws : MakeWhistle(whistleShortLen, 2900f);
        whistleLong = wl != null ? wl : MakeWhistle(whistleLongLen, 2800f);
        if (ws != null) whistleShortLen = ws.length;
        if (wl != null) whistleLongLen = wl.length;
        impact = Load("impact", MakeImpact());
        shotAK = Load("ak47", MakeGunshot(38f, 105f, 1.0f));
        shotGlock = Load("glock", MakeGunshot(70f, 150f, 0.8f));
        explosion = Load("explosion", MakeExplosion());
        pickup = Load("pickup", MakeChime());
        hitTick = Load("hit", MakeTick(1800f));
        killTick = MakeTick(1200f);
        reload = Load("reload", MakeReload());
        hurt = Load("hurt", MakeHurt());

        for (int i = 0; i < pool.Length; i++) pool[i] = MakeSource("sfx" + i);
        for (int i = 0; i < stepPool.Length; i++) stepPool[i] = MakeSource("step" + i);

        windSrc = MakeSource("wind"); windSrc.loop = true; windSrc.spatialBlend = 0f; windSrc.clip = wind; windSrc.volume = WindBase; windSrc.Play();
        crowdSrc = MakeSource("crowd"); crowdSrc.loop = true; crowdSrc.spatialBlend = 0f; crowdSrc.clip = crowd; crowdSrc.volume = CrowdBase; crowdSrc.Play();
    }

    void OnDestroy() { if (I == this) I = null; }

    void Update()
    {
        windSrc.volume = WindBase * AmbientLevel;
        float target = (Time.time < crowdBoostUntil ? 0.30f : CrowdBase) * AmbientLevel;
        crowdSrc.volume = Mathf.Lerp(crowdSrc.volume, target, 3f * Time.unscaledDeltaTime);
    }

    // ------------------------------------------------------------ API
    public void Kick(float speed, Vector3 pos)
    {
        float k = Mathf.Clamp01(speed / 30f);
        Play(Next(pool, ref poolIdx), kick, pos, Mathf.Lerp(0.28f, 0.85f, k), Random.Range(0.93f, 1.07f), 0.85f);
    }

    public void Step(Vector3 pos, float vol)
    {
        if (Time.time - lastStep < 0.025f) return;
        Camera cam = Camera.main;
        if (cam != null && (cam.transform.position - pos).sqrMagnitude > 40f * 40f) return;
        lastStep = Time.time;
        int r = Random.Range(0, 3);
        AudioClip c = r == 0 ? stepA : (r == 1 ? stepB : stepC);
        Play(Next(stepPool, ref stepIdx), c, pos, 0.16f * vol, Random.Range(0.9f, 1.12f), 1f);
    }

    public void Bounce(float impactSpeed, Vector3 pos)
    {
        if (impactSpeed < 1.8f) return;
        Play(Next(pool, ref poolIdx), bounce, pos, Mathf.Clamp01(impactSpeed / 12f) * 0.65f, Random.Range(0.95f, 1.08f), 0.85f);
    }

    public void Goal()
    {
        Play(Next(pool, ref poolIdx), roar, Vector3.zero, 0.9f, 1f, 0f);
        crowdBoostUntil = Time.time + 4.5f;
        Whistle(1, true, 1.1f);
    }

    // ---- combate
    public void Shot(ItemType t, Vector3 pos)
    {
        Play(Next(pool, ref poolIdx), t == ItemType.AK47 ? shotAK : shotGlock, pos, t == ItemType.AK47 ? 0.8f : 0.7f, Random.Range(0.96f, 1.04f), 0.9f);
    }
    public void Explosion(Vector3 pos) { Play(Next(pool, ref poolIdx), explosion, pos, 1f, Random.Range(0.95f, 1.05f), 0.6f); }
    public void Pickup() { Play(Next(pool, ref poolIdx), pickup, Vector3.zero, 0.6f, 1f, 0f); }
    public void HitTick(bool kill) { Play(Next(pool, ref poolIdx), kill ? killTick : hitTick, Vector3.zero, 0.7f, 1f, 0f); }
    public void Reload(Vector3 pos) { Play(Next(pool, ref poolIdx), reload, pos, 0.6f, 1f, 0.8f); }
    public void Hurt(Vector3 pos) { Play(Next(pool, ref poolIdx), hurt, pos, 0.55f, Random.Range(0.9f, 1.1f), 0.8f); }

    public void Impact() { Play(Next(pool, ref poolIdx), impact, Vector3.zero, 0.85f, 1f, 0f); }

    public void Whistle(int blasts, bool longBlast, float delay = 0f)
    {
        StartCoroutine(WhistleRoutine(blasts, longBlast, delay));
    }

    IEnumerator WhistleRoutine(int blasts, bool longBlast, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        AudioClip c = longBlast ? whistleLong : whistleShort;
        float len = longBlast ? whistleLongLen : whistleShortLen;
        for (int i = 0; i < blasts; i++)
        {
            Play(Next(pool, ref poolIdx), c, Vector3.zero, 0.7f, 1f, 0f);
            yield return new WaitForSecondsRealtime(len + 0.18f);
        }
    }

    // ------------------------------------------------------------ fuentes
    AudioSource MakeSource(string n)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(transform);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 3f;
        s.maxDistance = 60f;
        s.dopplerLevel = 0f;
        return s;
    }

    static AudioSource Next(AudioSource[] arr, ref int idx)
    {
        for (int k = 0; k < arr.Length; k++)
        {
            int j = (idx + k) % arr.Length;
            if (!arr[j].isPlaying) { idx = (j + 1) % arr.Length; return arr[j]; }
        }
        return null;
    }

    static void Play(AudioSource s, AudioClip c, Vector3 pos, float vol, float pitch, float spatial)
    {
        if (s == null || c == null) return;
        s.transform.position = pos;
        s.clip = c;
        s.volume = Mathf.Clamp01(vol);
        s.pitch = pitch;
        s.spatialBlend = spatial;
        s.Play();
    }

    static AudioClip Load(string name, AudioClip fallback)
    {
        AudioClip c = Resources.Load<AudioClip>("Audio/" + name);
        return c != null ? c : fallback;
    }

    // ------------------------------------------------------------ síntesis
    static AudioClip Build(string name, float[] d, float norm)
    {
        float peak = 0.0001f;
        for (int i = 0; i < d.Length; i++) { float a = Mathf.Abs(d[i]); if (a > peak) peak = a; }
        float k = norm / peak;
        for (int i = 0; i < d.Length; i++) d[i] *= k;
        AudioClip c = AudioClip.Create(name, d.Length, 1, Rate, false);
        c.SetData(d, 0);
        return c;
    }

    // tail = segundos extra para la reverberación; echo = rebotes de la grada (eco de estadio)
    static AudioClip Make(string name, float seconds, System.Func<float, float> gen, float tail = 0f, bool echo = false)
    {
        int n = Mathf.Max(1, (int)((seconds + tail) * Rate));
        float[] d = new float[n];
        for (int i = 0; i < n; i++) d[i] = gen(i / (float)Rate);
        if (echo) AddEcho(d, new float[] { 0.09f, 0.19f, 0.33f, 0.52f }, new float[] { 0.30f, 0.18f, 0.10f, 0.05f });
        return Build(name, d, 0.9f);
    }

    static void AddEcho(float[] d, float[] taps, float[] gains)
    {
        float[] src = (float[])d.Clone();
        for (int k = 0; k < taps.Length; k++)
        {
            int delay = (int)(taps[k] * Rate);
            for (int i = delay; i < d.Length; i++) d[i] += src[i - delay] * gains[k];
        }
    }

    // balón de cuero: golpe seco grave + chasquido corto
    static AudioClip MakeKick()
    {
        float phase = 0f, lp = 0f;
        return Make("kick", 0.3f, t =>
        {
            float f = Mathf.Lerp(60f, 150f, Mathf.Exp(-t * 26f));
            phase += TwoPi * f / Rate;
            float body = Mathf.Sin(phase) * Mathf.Exp(-t * 17f);
            lp += 0.25f * (N() - lp);
            float slap = lp * Mathf.Exp(-t * 45f) * 1.4f;
            float click = N() * Mathf.Exp(-t * 160f) * 0.35f;
            return body * 0.9f + slap + click;
        });
    }

    // pisada sobre césped: soplido corto y blando
    static AudioClip MakeStep(float bright)
    {
        float lp = 0f, lp2 = 0f;
        return Make("step", 0.12f, t =>
        {
            float n = N();
            lp += bright * (n - lp);
            lp2 += 0.06f * (n - lp2);
            float env = Mathf.Clamp01(t / 0.006f) * Mathf.Exp(-t * 38f);
            return ((lp - lp2) * 1.6f + lp2 * 1.2f) * env;
        });
    }

    static AudioClip MakeBounce()
    {
        float phase = 0f, lp = 0f;
        return Make("bounce", 0.2f, t =>
        {
            phase += TwoPi * Mathf.Lerp(70f, 120f, Mathf.Exp(-t * 30f)) / Rate;
            lp += 0.22f * (N() - lp);
            return Mathf.Sin(phase) * Mathf.Exp(-t * 26f) + lp * Mathf.Exp(-t * 70f) * 0.5f;
        });
    }

    // Ambientes en bucle sin costuras. Viento: soplo grave con ráfagas lentas. Grada: murmullo de muchas voces.
    static AudioClip MakeLoop(string name, float seconds, bool crowdType, int c1, int c2)
    {
        int fade = (int)(0.5f * Rate), total = (int)(seconds * Rate);
        float[] raw = new float[total + fade];
        float a1 = 0f, a2 = 0f, a3 = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)Rate;
            float n = N();
            float sample, amp;
            if (crowdType)
            {
                a1 += 0.22f * (n - a1);
                a2 += 0.02f * (n - a2);
                sample = a1 - a2;
                float swell = 0.5f + 0.5f * Mathf.Sin(TwoPi * c1 * t / seconds);
                amp = 0.55f + 0.45f * swell * (0.7f + 0.3f * Mathf.Sin(TwoPi * c2 * t / seconds + 1f));
            }
            else
            {
                float cut = Mathf.Lerp(0.05f, 0.16f, 0.5f + 0.5f * Mathf.Sin(TwoPi * t / seconds));
                a1 += cut * (n - a1);
                a2 += cut * (a1 - a2);
                a3 += cut * (a2 - a3);
                sample = a3;
                float gust = 0.5f + 0.5f * Mathf.Sin(TwoPi * c1 * t / seconds);
                amp = 0.25f + 0.75f * gust * gust * (0.75f + 0.25f * Mathf.Sin(TwoPi * c2 * t / seconds + 0.8f));
            }
            raw[i] = sample * amp;
        }
        float[] d = new float[total];
        for (int i = 0; i < total; i++) d[i] = raw[i];
        for (int i = 0; i < fade; i++) { float w = i / (float)fade; d[i] = raw[i] * w + raw[total + i] * (1f - w); }
        return Build(name, d, 0.9f);
    }

    static AudioClip MakeRoar()
    {
        float a1 = 0f, a2 = 0f, p1 = 0f, p2 = 0f, p3 = 0f;
        return Make("goal", 4.2f, t =>
        {
            float n = N();
            a1 += 0.24f * (n - a1); a2 += 0.035f * (n - a2);
            float band = a1 - a2;
            float env = Mathf.Clamp01(t / 0.35f) * (t > 2.6f ? Mathf.Exp(-(t - 2.6f) * 2.2f) : 1f);
            float flutter = 0.8f + 0.2f * Mathf.Sin(TwoPi * 6.5f * t) * Mathf.Sin(TwoPi * 2.3f * t + 0.7f);
            float vib = 1f + 0.015f * Mathf.Sin(TwoPi * 5f * t);
            p1 += TwoPi * 196f * vib / Rate; p2 += TwoPi * 294f * vib / Rate; p3 += TwoPi * 392f * vib / Rate;
            float hum = (Mathf.Sin(p1) + 0.7f * Mathf.Sin(p2) + 0.5f * Mathf.Sin(p3)) * 0.10f;
            return (band * 3.2f * flutter + hum) * env;
        });
    }

    // Pito de árbitro tipo Fox 40 (sin bolita): dos tonos agudos que baten, soplido turbulento, ligera subida al atacar
    // y eco de estadio.
    static AudioClip MakeWhistle(float seconds, float baseHz)
    {
        float p1 = 0f, p2 = 0f, p3 = 0f, lpA = 0f, lpB = 0f;
        return Make("whistle", seconds, t =>
        {
            float press = 1f + 0.012f * Mathf.Sin(TwoPi * 4.1f * t) + 0.008f * Mathf.Sin(TwoPi * 8.3f * t + 1f);
            float scoop = 1f - 0.07f * Mathf.Exp(-t * 30f);
            float f = baseHz * press * scoop;
            p1 += TwoPi * f / Rate;
            p2 += TwoPi * f * 1.24f / Rate;
            p3 += TwoPi * f * 0.79f / Rate;
            float n = N();
            lpA += 0.5f * (n - lpA);
            lpB += 0.2f * (n - lpB);
            float turb = lpA - lpB;
            float beat = 0.85f + 0.15f * Mathf.Sin(p1 - p2);
            float tone = (Mathf.Sin(p1) + 0.75f * Mathf.Sin(p2) + 0.45f * Mathf.Sin(p3) + 0.12f * Mathf.Sin(2f * p1)) * beat;
            float env = Mathf.Clamp01(t / 0.025f) * Mathf.Clamp01((seconds - t) / 0.08f);
            return (tone * 0.3f + turb * 0.9f) * env;
        }, 0.7f, true);
    }

    // disparo: chasquido de banda ancha + golpe grave + eco de estadio
    static AudioClip MakeGunshot(float decay, float thumpHz, float crack)
    {
        float lp = 0f, phase = 0f;
        return Make("shot", 0.3f, t =>
        {
            float n = N();
            lp += 0.35f * (n - lp);
            float crackS = (n * 0.6f + lp * 0.8f) * Mathf.Exp(-t * decay) * crack;
            phase += TwoPi * Mathf.Lerp(thumpHz * 1.8f, thumpHz, Mathf.Exp(-t * 40f)) / Rate;
            return crackS + Mathf.Sin(phase) * Mathf.Exp(-t * 22f) * 0.8f;
        }, 0.5f, true);
    }

    static AudioClip MakeExplosion()
    {
        float lp = 0f, phase = 0f;
        return Make("explosion", 1.6f, t =>
        {
            float n = N();
            lp += 0.06f * (n - lp);
            phase += TwoPi * Mathf.Lerp(26f, 60f, Mathf.Exp(-t * 3f)) / Rate;
            float crackle = n * Mathf.Exp(-t * 18f) * 0.6f;
            return lp * 4f * Mathf.Exp(-t * 2.2f) + Mathf.Sin(phase) * Mathf.Exp(-t * 2.8f) + crackle;
        }, 1.0f, true);
    }

    static AudioClip MakeChime()
    {
        float p1 = 0f;
        return Make("pickup", 0.3f, t =>
        {
            float f = t < 0.09f ? 740f : 1110f;
            p1 += TwoPi * f / Rate;
            return Mathf.Sin(p1) * Mathf.Exp(-(t % 0.09f) * 22f) * Mathf.Exp(-t * 6f);
        });
    }

    static AudioClip MakeTick(float hz)
    {
        float p = 0f;
        return Make("tick", 0.07f, t =>
        {
            p += TwoPi * hz / Rate;
            return Mathf.Sin(p) * Mathf.Exp(-t * 60f);
        });
    }

    static AudioClip MakeReload()
    {
        float lp = 0f, p = 0f;
        return Make("reload", 0.5f, t =>
        {
            float n = N();
            lp += 0.4f * (n - lp);
            float c1 = Mathf.Exp(-Mathf.Abs(t - 0.03f) * 160f);
            float c2 = Mathf.Exp(-Mathf.Max(0f, t - 0.32f) * 120f) * (t > 0.32f ? 1f : 0f);
            p += TwoPi * 1250f / Rate;
            return (lp * (c1 + c2) + Mathf.Sin(p) * 0.15f * (c1 + c2)) * 0.8f;
        });
    }

    static AudioClip MakeHurt()
    {
        float lp = 0f, p = 0f;
        return Make("hurt", 0.2f, t =>
        {
            lp += 0.15f * (N() - lp);
            p += TwoPi * 140f / Rate;
            return (lp * 2f + Mathf.Sin(p) * 0.5f) * Mathf.Exp(-t * 14f) * Mathf.Clamp01(t / 0.005f);
        });
    }

    static AudioClip MakeImpact()
    {
        float phase = 0f, lp = 0f, r1 = 0f, r2 = 0f;
        return Make("impact", 1.2f, t =>
        {
            phase += TwoPi * Mathf.Lerp(32f, 95f, Mathf.Exp(-t * 5f)) / Rate;
            float boom = Mathf.Sin(phase) * Mathf.Exp(-t * 3.2f);
            lp += 0.2f * (N() - lp);
            float crash = lp * Mathf.Exp(-t * 9f) * 1.5f;
            r1 += TwoPi * 880f / Rate; r2 += TwoPi * 1320f / Rate;
            float ring = (Mathf.Sin(r1) + 0.6f * Mathf.Sin(r2)) * 0.12f * Mathf.Exp(-t * 7f);
            return boom * 1.1f + crash + ring;
        });
    }
}

// Rebote de la pelota contra el piso / postes
public class BallSfx : MonoBehaviour
{
    void OnCollisionEnter(Collision c)
    {
        if (GameAudio.I != null) GameAudio.I.Bounce(c.relativeVelocity.magnitude, transform.position);
    }
}
