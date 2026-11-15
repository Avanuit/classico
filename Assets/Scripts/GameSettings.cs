using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ajustes guardados (volumen, música, efectos, ambiente, brillo, duración del partido).
public static class GameSettings
{
    static bool loaded;
    static float master = 1f, music = 0.6f, sfx = 1f, ambient = 1f, brightness = 1f;
    static int minutes = 5;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        master = PlayerPrefs.GetFloat("cl_master", 1f);
        music = PlayerPrefs.GetFloat("cl_music", 0.6f);
        sfx = PlayerPrefs.GetFloat("cl_sfx", 1f);
        ambient = PlayerPrefs.GetFloat("cl_ambient", 1f);
        brightness = PlayerPrefs.GetFloat("cl_brightness", 1f);
        minutes = PlayerPrefs.GetInt("cl_minutes", 5);
        Apply();
    }

    public static void Apply()
    {
        AudioListener.volume = master;
        GameAudio.AmbientLevel = ambient;
        GameAudio.SfxLevel = sfx;
        if (GameAudio.I != null) GameAudio.I.RefreshMusicVolume();
        ScreenOverlay.SetBrightness(brightness);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("cl_master", master);
        PlayerPrefs.SetFloat("cl_music", music);
        PlayerPrefs.SetFloat("cl_sfx", sfx);
        PlayerPrefs.SetFloat("cl_ambient", ambient);
        PlayerPrefs.SetFloat("cl_brightness", brightness);
        PlayerPrefs.SetInt("cl_minutes", minutes);
        PlayerPrefs.Save();
    }

    public static float Master { get { Load(); return master; } set { Load(); master = Mathf.Clamp01(value); AudioListener.volume = master; } }
    public static float Music { get { Load(); return music; } set { Load(); music = Mathf.Clamp01(value); if (GameAudio.I != null) GameAudio.I.RefreshMusicVolume(); } }
    public static float Sfx { get { Load(); return sfx; } set { Load(); sfx = Mathf.Clamp01(value); GameAudio.SfxLevel = sfx; } }
    public static float Ambient { get { Load(); return ambient; } set { Load(); ambient = Mathf.Clamp01(value); GameAudio.AmbientLevel = ambient; } }
    public static float Brightness { get { Load(); return brightness; } set { Load(); brightness = Mathf.Clamp(value, 0.5f, 1.5f); ScreenOverlay.SetBrightness(brightness); } }
    public static int MatchMinutes { get { Load(); return minutes; } set { Load(); minutes = Mathf.Clamp(value, 1, 30); } }

    public static void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

// Elección de equipo que viaja del menú principal (otra escena) a la escena del partido.
public static class GameSession
{
    public static bool HasChoice;
    public static int Team;                       // 0 Millonarios, 1 Nacional, -1 solo mirar
    public static string MenuScene = "MainMenu";
    public static bool MenuAvailable { get { return Application.CanStreamedLevelBeLoaded(MenuScene); } }
}

// Capa global (sobrevive a los cambios de escena): fundidos a negro, viñeta cinematográfica y brillo.
public class ScreenOverlay : MonoBehaviour
{
    static ScreenOverlay inst;
    float fade, fadeTarget, fadeSpeed = 2f;
    float brightness = 1f;
    Texture2D vignette;
    readonly Dictionary<Light, float> baseIntensity = new Dictionary<Light, float>();
    float baseAmbient = -1f;
    bool dirty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (inst != null) return;
        GameObject go = new GameObject("ScreenOverlay");
        inst = go.AddComponent<ScreenOverlay>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        inst = this;
        GameSettings.Load();
        SceneManager.sceneLoaded += OnLoaded;
    }

    void OnDestroy() { SceneManager.sceneLoaded -= OnLoaded; if (inst == this) inst = null; }

    void OnLoaded(Scene s, LoadSceneMode m)
    {
        baseIntensity.Clear();
        baseAmbient = RenderSettings.ambientIntensity;
        dirty = true;
        FadeFromBlack(0.7f);
    }

    public static void FadeFromBlack(float sec)
    {
        if (inst == null) return;
        inst.fade = 1f; inst.fadeTarget = 0f; inst.fadeSpeed = 1f / Mathf.Max(0.05f, sec);
    }

    public static void FadeToBlack(float sec)
    {
        if (inst == null) return;
        inst.fadeTarget = 1f; inst.fadeSpeed = 1f / Mathf.Max(0.05f, sec);
    }

    public static void SetBrightness(float b)
    {
        if (inst == null) return;
        inst.brightness = b;
        inst.dirty = true;
    }

    public static void LoadScene(string name)
    {
        if (inst == null) { SceneManager.LoadScene(name); return; }
        inst.StartCoroutine(inst.LoadRoutine(name));
    }

    IEnumerator LoadRoutine(string name)
    {
        FadeToBlack(0.45f);
        yield return new WaitForSecondsRealtime(0.5f);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(name);
    }

    void Update()
    {
        fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * Time.unscaledDeltaTime);
        if (dirty) { dirty = false; ApplyBrightness(); }
    }

    // por encima de 100% sube la intensidad de las luces; por debajo, oscurece con una capa negra
    void ApplyBrightness()
    {
        float b = Mathf.Max(1f, brightness);
        if (baseAmbient < 0f) baseAmbient = RenderSettings.ambientIntensity;
        foreach (Light l in FindObjectsOfType<Light>())
        {
            if (l == null) continue;
            float baseI;
            if (!baseIntensity.TryGetValue(l, out baseI)) { baseI = l.intensity; baseIntensity[l] = baseI; }
            l.intensity = baseI * b;
        }
        RenderSettings.ambientIntensity = baseAmbient * b;
    }

    void OnGUI()
    {
        GUI.depth = -1000;
        if (Event.current.type != EventType.Repaint) return;
        if (vignette == null) vignette = MakeVignette();
        Color old = GUI.color;
        Rect full = new Rect(0f, 0f, Screen.width, Screen.height);
        GUI.color = new Color(0f, 0f, 0f, 0.32f);
        GUI.DrawTexture(full, vignette);
        if (brightness < 1f)
        {
            GUI.color = new Color(0f, 0f, 0f, (1f - brightness) * 0.9f);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
        }
        if (fade > 0.001f)
        {
            GUI.color = new Color(0f, 0f, 0f, fade);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
        }
        GUI.color = old;
    }

    static Texture2D MakeVignette()
    {
        int n = 128;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        Color[] px = new Color[n * n];
        float c = (n - 1) * 0.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (c * 1.41f);
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Pow(d, 2.6f) * 1.5f));
            }
        t.SetPixels(px);
        t.Apply();
        return t;
    }
}
