using UnityEngine;

// Puente: el menú principal vive ahora DENTRO de la escena del partido (con el estadio y los jugadores de fondo).
// Si todavía tienes la escena MainMenu en Build Settings, este script solo carga la escena del partido.
public class MainMenu : MonoBehaviour
{
    public string gameSceneName = "SampleScene";

    void Start()
    {
        GameSession.HasChoice = false;
        if (Application.CanStreamedLevelBeLoaded(gameSceneName)) ScreenOverlay.LoadScene(gameSceneName);
        else Debug.LogWarning("MainMenu: la escena '" + gameSceneName + "' no está en Build Settings.");
    }
}
