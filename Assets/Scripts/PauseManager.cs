using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class PauseManager : NetworkBehaviour
{
    public static PauseManager Instance;

    [Header("UI References")]
    public GameObject pausePanel;
    public TMP_Text titleText;
    public GameObject resumeButton;
    public GameObject spectateButton;

    [Header("Control de Estados")]
    public bool isGameOver = false;
    public bool isSpectating = false;
    public TMP_Text statsText;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;

        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (isGameOver) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (isGameOver) return;

        bool currentState = InteractionManager.Instance.isPaused;

        // Si estamos a punto de pausar normalmente (no por morir)
        if (!currentState)
        {
            if (titleText != null) titleText.text = "PAUSA";
            if (resumeButton != null) resumeButton.SetActive(true);
            if (spectateButton != null) spectateButton.SetActive(false);
            if (statsText != null) statsText.gameObject.SetActive(false); // Ocultamos las stats en pausa normal
        }

        SetPauseState(!currentState);
    }

    public void ResumeGame()
    {
        if (isGameOver) return;
        SetPauseState(false);
    }

    //  RECIBE EL PUESTO NUMÉRICO 
    public void TriggerGameOver(int puesto)
    {
        isGameOver = true;

        if (titleText != null)
            titleText.text = "GAMEOVER";

        // 1. Contamos cuántos vivos quedan en toda la mesa
        int jugadoresVivos = 0;
        if (InteractionManager.Instance != null)
        {
            for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
            {
                if (InteractionManager.Instance.vidas[i] > 0) jugadoresVivos++;
            }
        }

        // 2. Cambio de botones para el modo Game Over
        if (resumeButton != null) resumeButton.SetActive(false);

        // Activamos "Espectar" si quedan al menos 2 personas para jugar.
        if (spectateButton != null)
        {
            bool sePuedeEspectar = (jugadoresVivos > 1);
            spectateButton.SetActive(sePuedeEspectar);
        }

        if (statsText != null)
        {
            statsText.gameObject.SetActive(true);

            var im = InteractionManager.Instance;
            int rondas = im.rondasJugadasTotales;

            string stats =
                $"Puesto No {puesto}\n" +
                $"Apuestas Cumplidas: <b>{im.apuestasAcertadasTotales[0]}</b> / {rondas}\n" +
                $"Bazas Ganadas: <b>{im.bazasTotales[0]}</b>\n";

            statsText.text = stats;
            statsText.alignment = TextAlignmentOptions.Center;
        }

        // Encendemos el panel visual directamente sin usar el método antiguo
        pausePanel.SetActive(true); // O el nombre que tenga tu panel principal

        
        int otrosHumanosVivos = 0;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            int id = (int)clientId;
            if (id != (int)NetworkManager.Singleton.LocalClientId && InteractionManager.Instance.vidas[id] > 0)
            {
                otrosHumanosVivos++;
            }
        }

        if (otrosHumanosVivos == 0)
        {
            Time.timeScale = 0f; // Congelo porque ya nadie real está jugando
        }
        else
        {
            Time.timeScale = 1f; // NO congelo
        }
    }

    // MODO ESPECTADOR 
    public void SpectateGame()
    {
        isGameOver = false; // Ya no estamos en la pantalla final
        isSpectating = true; // Pero somos un fantasma
        SetPauseState(false); // Quitamos la pausa y escondemos el panel

        InteractionManager.Instance.SetInfoMessage("MODO ESPECTADOR: Viendo jugar a los bots.");
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        isGameOver = false;
        isSpectating = false;
        SetPauseState(false);

        if (CardDatabase.deck != null) CardDatabase.deck.Clear();

        InteractionManager.Instance.StartNewGame();
        if (TableZone.Instance != null) TableZone.Instance.ResetStats();
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        if (NetworkManager.Singleton != null)
        {
            // Al llamar a Shutdown, Netcode corta los cables.
            // Los clientes recibirán el evento 'OnClientDisconnectCallback' inmediatamente.
            NetworkManager.Singleton.Shutdown();
        }

        // 2. Volvemos al menú
        SceneManager.LoadScene("MainMenu");
    }

    private void SetPauseState(bool isPaused)
    {
        if (pausePanel != null) pausePanel.SetActive(isPaused);
        InteractionManager.Instance.isPaused = isPaused;
        InteractionManager.Instance.UpdateVisualStates();
        Time.timeScale = isPaused ? 0f : 1f;
    }
}