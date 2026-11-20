using UnityEngine;
using UnityEngine.Rendering;

// Iluminación ambiente del estadio SIN depender de datos horneados.
// Cuando la escena del partido se carga desde otra escena (o en el .exe), Unity no trae la luz ambiente/reflejos
// "auto-generados" del editor: las gradas, el techo y la sombra quedan NEGROS. Aquí se fija una luz ambiente de
// tres colores (cielo / horizonte / suelo) y se captura una vez un reflection probe en tiempo real.
public static class StadiumLighting
{
    public static Color Sky = new Color(0.62f, 0.67f, 0.78f);
    public static Color Equator = new Color(0.46f, 0.47f, 0.48f);
    public static Color Ground = new Color(0.26f, 0.29f, 0.22f);

    public static void Apply()
    {
        // solo en la escena del partido (MatchSetup ya existe al cargar; MatchManager se crea después, en Start)
        if (Object.FindObjectOfType<MatchSetup>() == null && Object.FindObjectOfType<MatchManager>() == null) return;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Sky;
        RenderSettings.ambientEquatorColor = Equator;
        RenderSettings.ambientGroundColor = Ground;
        RenderSettings.ambientIntensity = 1f;
        DynamicGI.UpdateEnvironment();

        // reflejos: un probe grande que se renderiza UNA vez (sin él, los materiales con brillo/metal se ven negros)
        GameObject go = GameObject.Find("_StadiumReflection");
        ReflectionProbe rp = go != null ? go.GetComponent<ReflectionProbe>() : null;
        if (rp == null)
        {
            go = new GameObject("_StadiumReflection");
            go.transform.position = new Vector3(0f, 12f, 0f);
            rp = go.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Realtime;
            rp.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            rp.size = new Vector3(400f, 150f, 400f);
            rp.resolution = 128;
            rp.importance = 0;
            rp.intensity = 0.8f;
            rp.clearFlags = ReflectionProbeClearFlags.Skybox;
        }
        rp.RenderProbe();
    }
}
