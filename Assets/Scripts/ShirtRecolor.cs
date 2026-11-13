using System.Collections.Generic;
using UnityEngine;

// Recolorea SOLO los píxeles de la textura que se parecen al color de la camiseta original.
// Sirve cuando el modelo tiene UN solo material/textura para todo el cuerpo.
// No requiere Read/Write en la textura (usa Blit), y cachea una textura por color (solo se generan 4).
public static class ShirtRecolor
{
    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    static readonly string[] texProps = { "_BaseMap", "_MainTex", "baseColorTexture" };

    public static void Apply(GameObject player, Color source, Color target, float tolerance)
    {
        foreach (var r in player.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.materials; // instancias propias de este jugador
            for (int i = 0; i < mats.Length; i++)
            {
                foreach (var prop in texProps)
                {
                    if (!mats[i].HasProperty(prop)) continue;
                    var tex = mats[i].GetTexture(prop);
                    if (tex == null) continue;

                    string key = tex.GetInstanceID() + "_" + ColorUtility.ToHtmlStringRGB(target);
                    if (!cache.TryGetValue(key, out var recolored))
                    {
                        recolored = Recolor(tex, source, target, tolerance);
                        cache[key] = recolored;
                    }
                    mats[i].SetTexture(prop, recolored);
                    break;
                }
            }
            r.materials = mats;
        }
    }

    static Texture2D Recolor(Texture src, Color source, Color target, float tol)
    {
        var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, true);
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        Color[] px = tex.GetPixels();
        float srcLum = Mathf.Max(0.05f, Lum(source));
        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            float d = Mathf.Sqrt((c.r - source.r) * (c.r - source.r) + (c.g - source.g) * (c.g - source.g) + (c.b - source.b) * (c.b - source.b));
            if (d < tol)
            {
                float k = Mathf.Clamp(Lum(c) / srcLum, 0f, 1.6f);   // conserva sombras y pliegues
                float blend = 1f - d / tol;                          // borde suave
                Color n = new Color(target.r * k, target.g * k, target.b * k, c.a);
                px[i] = Color.Lerp(c, n, Mathf.Clamp01(blend * 1.5f));
            }
        }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    static float Lum(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
}
