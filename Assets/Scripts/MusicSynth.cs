using UnityEngine;

public enum MusicTrack { None = 0, Menu = 1, Intro = 2, Match = 3, Victory = 4 }

// Música generada por código (salsa-latin para el menú y el partido, pad cinematográfico para la intro, fanfarria para el podio).
// Es un sustituto: para música de verdad pon archivos en Assets/Resources/Audio/ llamados
//   music_menu, music_intro, music_match, music_victory   (.mp3 / .ogg / .wav) y se usarán solos.
public static class MusicSynth
{
    const int Rate = 22050;
    const float Tau = 6.2831853f;
    static float[] sinTab;
    static uint seed = 2463534242u;

    static float Sin(float cycles)
    {
        if (sinTab == null)
        {
            sinTab = new float[4096];
            for (int i = 0; i < 4096; i++) sinTab[i] = Mathf.Sin(Tau * i / 4096f);
        }
        float x = cycles - Mathf.Floor(cycles);
        float f = x * 4096f;
        int i0 = (int)f;
        float fr = f - i0;
        i0 &= 4095;
        int i1 = (i0 + 1) & 4095;
        return sinTab[i0] + (sinTab[i1] - sinTab[i0]) * fr;
    }

    static float Noise()
    {
        seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
        return (seed & 0xFFFF) / 32768f - 1f;
    }

    static float Hz(int midi) { return 440f * Mathf.Pow(2f, (midi - 69) / 12f); }

    // ------------------------------------------------------------ instrumentos (suman en el buffer con vuelta circular)
    static void Bass(float[] b, float t0, float dur, int midi, float vol)
    {
        int s0 = (int)(t0 * Rate), n = (int)((dur + 0.15f) * Rate);
        float inc = Hz(midi) / Rate, ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Min(t / 0.006f, 1f) * Mathf.Exp(-t * 3f) * (t < dur ? 1f : Mathf.Exp(-(t - dur) * 40f));
            float s = Sin(ph) + 0.45f * Sin(ph * 2f) + 0.15f * Sin(ph * 3f);
            ph += inc;
            b[(s0 + i) % b.Length] += s * env * vol;
        }
    }

    static void Piano(float[] b, float t0, float dur, int midi, float vol)
    {
        int s0 = (int)(t0 * Rate), n = (int)((dur + 0.25f) * Rate);
        float inc = Hz(midi) / Rate, ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Min(t / 0.004f, 1f) * (t < dur ? 1f : Mathf.Exp(-(t - dur) * 25f));
            float s = Sin(ph) * Mathf.Exp(-t * 4.5f) + 0.5f * Sin(ph * 2f) * Mathf.Exp(-t * 8f) + 0.25f * Sin(ph * 3f) * Mathf.Exp(-t * 12f);
            ph += inc;
            b[(s0 + i) % b.Length] += s * env * vol;
        }
    }

    static void Pad(float[] b, float t0, float dur, int midi, float vol)
    {
        int s0 = (int)(t0 * Rate), n = (int)((dur + 1.2f) * Rate);
        float inc = Hz(midi) / Rate, ph = 0f, ph2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Min(t / 0.7f, 1f) * (t < dur ? 1f : Mathf.Exp(-(t - dur) * 3f));
            float s = Sin(ph) + 0.8f * Sin(ph2) + 0.35f * Sin(ph * 0.5f);
            ph += inc; ph2 += inc * 1.005f;
            b[(s0 + i) % b.Length] += s * env * vol;
        }
    }

    static void Brass(float[] b, float t0, float dur, int midi, float vol)
    {
        int s0 = (int)(t0 * Rate), n = (int)((dur + 0.3f) * Rate);
        float inc = Hz(midi) / Rate, ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Min(t / 0.03f, 1f) * (t < dur ? 1f : Mathf.Exp(-(t - dur) * 14f));
            float s = 0f;
            for (int k = 1; k <= 6; k++) s += Sin(ph * k) / k;
            ph += inc;
            b[(s0 + i) % b.Length] += s * env * vol * (0.85f + 0.15f * Sin(t * 5.5f));
        }
    }

    static void Conga(float[] b, float t0, float vol, bool open)
    {
        int s0 = (int)(t0 * Rate), n = (int)(0.3f * Rate);
        float ph = 0f, baseHz = open ? 215f : 300f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            ph += baseHz * (1f + 0.6f * Mathf.Exp(-t * 45f)) / Rate;
            float s = Sin(ph) * Mathf.Exp(-t * (open ? 11f : 24f)) + Noise() * Mathf.Exp(-t * 120f) * 0.4f;
            b[(s0 + i) % b.Length] += s * vol;
        }
    }

    static void Clave(float[] b, float t0, float vol)
    {
        int s0 = (int)(t0 * Rate), n = (int)(0.1f * Rate);
        float p1 = 0f, p2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            p1 += 2500f / Rate; p2 += 3700f / Rate;
            b[(s0 + i) % b.Length] += (Sin(p1) + 0.4f * Sin(p2)) * Mathf.Exp(-t * 70f) * vol;
        }
    }

    static void Cowbell(float[] b, float t0, float vol)
    {
        int s0 = (int)(t0 * Rate), n = (int)(0.25f * Rate);
        float p1 = 0f, p2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            p1 += 562f / Rate; p2 += 845f / Rate;
            b[(s0 + i) % b.Length] += (Sin(p1) + Sin(p2)) * 0.5f * Mathf.Exp(-t * 13f) * vol;
        }
    }

    static void Shaker(float[] b, float t0, float vol)
    {
        int s0 = (int)(t0 * Rate), n = (int)(0.06f * Rate);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float nz = Noise();
            float hp = nz - prev; prev = nz;
            b[(s0 + i) % b.Length] += hp * Mathf.Exp(-t * 60f) * vol;
        }
    }

    static void Kick(float[] b, float t0, float vol)
    {
        int s0 = (int)(t0 * Rate), n = (int)(0.35f * Rate);
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            ph += Mathf.Lerp(48f, 120f, Mathf.Exp(-t * 18f)) / Rate;
            b[(s0 + i) % b.Length] += Sin(ph) * Mathf.Exp(-t * 9f) * vol;
        }
    }

    static void Snare(float[] b, float t0, float vol)
    {
        int s0 = (int)(t0 * Rate), n = (int)(0.2f * Rate);
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            ph += 190f / Rate;
            b[(s0 + i) % b.Length] += (Noise() * 0.8f + Sin(ph) * 0.5f) * Mathf.Exp(-t * 22f) * vol;
        }
    }

    // ------------------------------------------------------------ canciones
    public static AudioClip Make(MusicTrack t)
    {
        float[] buf;
        switch (t)
        {
            case MusicTrack.Menu: buf = Salsa(100f, false); break;
            case MusicTrack.Match: buf = Salsa(118f, true); break;
            case MusicTrack.Intro: buf = Cinematic(); break;
            default: buf = Fanfare(); break;
        }
        float peak = 0.0001f;
        for (int i = 0; i < buf.Length; i++) { float a = Mathf.Abs(buf[i]); if (a > peak) peak = a; }
        float k = 0.8f / peak;
        for (int i = 0; i < buf.Length; i++) buf[i] *= k;
        AudioClip c = AudioClip.Create("music_" + t, buf.Length, 1, Rate, false);
        c.SetData(buf, 0);
        return c;
    }

    static readonly int[][] chordsMenu =
    {
        new int[] { 57, 60, 64, 67 }, new int[] { 62, 65, 69, 72 }, new int[] { 55, 59, 62, 65 }, new int[] { 60, 64, 67, 71 },
        new int[] { 57, 60, 64, 67 }, new int[] { 62, 65, 69, 72 }, new int[] { 64, 68, 71, 74 }, new int[] { 57, 60, 64, 67 }
    };
    static readonly int[] rootsMenu = { 45, 50, 43, 48, 45, 50, 52, 45 };
    static readonly int[][] chordsMatch =
    {
        new int[] { 57, 60, 64 }, new int[] { 53, 57, 60 }, new int[] { 60, 64, 67 }, new int[] { 55, 59, 62 },
        new int[] { 57, 60, 64 }, new int[] { 53, 57, 60 }, new int[] { 60, 64, 67 }, new int[] { 55, 59, 62 }
    };
    static readonly int[] rootsMatch = { 45, 41, 48, 43, 45, 41, 48, 43 };

    // salsa: tumbao de bajo, montuno de piano, clave 3-2, congas, campana y shaker (8 compases)
    static float[] Salsa(float bpm, bool drive)
    {
        float beat = 60f / bpm;
        int bars = 8;
        float[] buf = new float[(int)(bars * 4 * beat * Rate)];
        for (int bar = 0; bar < bars; bar++)
        {
            float b0 = bar * 4 * beat;
            int root = drive ? rootsMatch[bar] : rootsMenu[bar];
            int[] chord = drive ? chordsMatch[bar] : chordsMenu[bar];
            // bajo
            if (drive)
                for (int i = 0; i < 8; i++) Bass(buf, b0 + i * 0.5f * beat, 0.4f * beat, i % 4 == 3 ? root + 7 : root, 0.5f);
            else
            {
                Bass(buf, b0, 1.3f * beat, root, 0.55f);
                Bass(buf, b0 + 1.5f * beat, 0.5f * beat, root + 7, 0.5f);
                Bass(buf, b0 + 2.5f * beat, 0.5f * beat, root, 0.5f);
                Bass(buf, b0 + 3.5f * beat, 0.5f * beat, root + 7, 0.45f);
            }
            // piano montuno
            float[] hits = drive ? new float[] { 0f, 0.75f, 1.5f, 2f, 2.75f, 3.5f } : new float[] { 0.5f, 1f, 1.5f, 2.5f, 3f, 3.5f };
            for (int h = 0; h < hits.Length; h++)
            {
                int n1 = chord[h % chord.Length], n2 = chord[(h + 2) % chord.Length];
                Piano(buf, b0 + hits[h] * beat, 0.35f * beat, n1, 0.16f);
                Piano(buf, b0 + hits[h] * beat, 0.35f * beat, n2, 0.12f);
            }
            // clave 3-2
            float[] clave = (bar % 2 == 0) ? new float[] { 0f, 1.5f, 3f } : new float[] { 1f, 2f };
            for (int c = 0; c < clave.Length; c++) Clave(buf, b0 + clave[c] * beat, 0.22f);
            // congas
            Conga(buf, b0 + 0.5f * beat, 0.2f, false);
            Conga(buf, b0 + 1.5f * beat, 0.3f, false);
            Conga(buf, b0 + 2f * beat, 0.32f, true);
            Conga(buf, b0 + 2.5f * beat, 0.22f, false);
            Conga(buf, b0 + 3.5f * beat, 0.3f, false);
            // campana y shaker
            for (int q = 0; q < 4; q++) Cowbell(buf, b0 + q * beat, drive ? 0.12f : 0.08f);
            int sh = drive ? 16 : 8;
            for (int s = 0; s < sh; s++) Shaker(buf, b0 + s * (4f * beat / sh), drive ? 0.1f : 0.06f);
            if (drive) { Kick(buf, b0, 0.4f); Kick(buf, b0 + 2f * beat, 0.3f); Snare(buf, b0 + beat, 0.12f); Snare(buf, b0 + 3f * beat, 0.12f); }
        }
        return buf;
    }

    // intro cinematográfica: pad menor lento, latido grave y arpegio suave
    static float[] Cinematic()
    {
        float bpm = 76f, beat = 60f / bpm;
        int bars = 8;
        float[] buf = new float[(int)(bars * 4 * beat * Rate)];
        int[][] pads = { new int[] { 45, 52, 57, 60 }, new int[] { 41, 48, 53, 57 }, new int[] { 38, 45, 50, 53 }, new int[] { 40, 47, 52, 56 } };
        for (int c = 0; c < 4; c++)
        {
            float t0 = c * 8f * beat;
            for (int n = 0; n < pads[c].Length; n++) Pad(buf, t0, 8f * beat, pads[c][n], 0.12f);
            for (int s = 0; s < 16; s++) Piano(buf, t0 + s * 0.5f * beat, 0.6f * beat, pads[c][s % pads[c].Length] + 12, 0.06f);
        }
        for (int bar = 0; bar < bars; bar++)
        {
            float b0 = bar * 4 * beat;
            Kick(buf, b0, 0.45f); Kick(buf, b0 + 2f * beat, 0.3f);
            if (bar >= 4) { Kick(buf, b0 + beat * 1.5f, 0.2f); Conga(buf, b0 + 3.5f * beat, 0.2f, true); }
        }
        return buf;
    }

    // fanfarria de victoria en do mayor (4 compases en bucle)
    static float[] Fanfare()
    {
        float bpm = 104f, beat = 60f / bpm;
        int bars = 4;
        float[] buf = new float[(int)(bars * 4 * beat * Rate)];
        int[][] chords = { new int[] { 60, 64, 67, 72 }, new int[] { 60, 65, 69, 72 }, new int[] { 59, 62, 67, 71 }, new int[] { 60, 64, 67, 72 } };
        int[] roots = { 36, 41, 43, 36 };
        int[][] mel = { new int[] { 72, 76, 79, 84 }, new int[] { 77, 76, 72, 77 }, new int[] { 79, 83, 86, 83 }, new int[] { 84, 79, 76, 72 } };
        for (int bar = 0; bar < bars; bar++)
        {
            float b0 = bar * 4 * beat;
            for (int n = 0; n < chords[bar].Length; n++) Brass(buf, b0, 3.6f * beat, chords[bar][n], 0.1f);
            Bass(buf, b0, 1.8f * beat, roots[bar], 0.5f);
            Bass(buf, b0 + 2f * beat, 1.8f * beat, roots[bar] + 7, 0.45f);
            for (int m = 0; m < 4; m++) Brass(buf, b0 + m * beat, 0.9f * beat, mel[bar][m], 0.14f);
            Kick(buf, b0, 0.45f); Kick(buf, b0 + 2f * beat, 0.4f);
            Snare(buf, b0 + beat, 0.2f); Snare(buf, b0 + 3f * beat, 0.2f);
            for (int q = 0; q < 8; q++) Shaker(buf, b0 + q * 0.5f * beat, 0.07f);
            Cowbell(buf, b0 + 0.5f * beat, 0.1f);
        }
        return buf;
    }
}
