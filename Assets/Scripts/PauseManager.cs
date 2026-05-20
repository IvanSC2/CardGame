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

        // 1. Contamos cuántos vivos quedan en toda la mesa (Humanos + Bots)
        int jugadoresVivos = 0;
        if (InteractionManager.Instance != null)
        {
            for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
            {
                if (InteractionManager.Instance.vidas[i] > 0) jugadoresVivos++;
            }
        }

        // 2. Contamos ESPECÍFICAMENTE cuántos HUMANOS quedan vivos
        int otrosHumanosVivos = 0;
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                int id = (int)clientId;
                // Si el cliente no soy yo, y además tiene vidas > 0, es un humano vivo
                if (id != (int)NetworkManager.Singleton.LocalClientId && InteractionManager.Instance.vidas[id] > 0)
                {
                    otrosHumanosVivos++;
                }
            }
        }

        // 3. Cambio de botones para el modo Game Over
        if (resumeButton != null) resumeButton.SetActive(false);

        //Solo se puede espectar si queda OTRO HUMANO REAL jugando
        if (spectateButton != null)
        {
            bool sePuedeEspectar = (otrosHumanosVivos > 0);
            spectateButton.SetActive(sePuedeEspectar);
        }

        // 4. Mostrar Estadísticas
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

        // MONETIZACIÓN: Reparto de premios al finalizar
        if (!GameConfig.prizeAwarded && GameConfig.currentFee > 0)
        {
            if (puesto == 1) // Ganador absoluto
            {
                TopBarUI.Instance.ActualizarMonedas(GameConfig.currentPrize);
                Debug.Log($"[ECONOMÍA] ¡Has ganado! Recibes {GameConfig.currentPrize} monedas.");
            }
            
            // Bono por hostear partida pública hasta el final
            if (!GameConfig.isPrivateMatch && GameConfig.isHostLobby)
            {
                TopBarUI.Instance.ActualizarMonedas(50);
                Debug.Log("[ECONOMÍA] Bono de 50 monedas por mantener el servidor público vivo.");
            }
            
            GameConfig.prizeAwarded = true;
        }

        // Encendemos el panel visual
        pausePanel.SetActive(true); 

        // 5. Congelamos el tiempo SOLO si ya no quedan humanos
        if (otrosHumanosVivos == 0)
        {
            Time.timeScale = 0f; // Congelo porque ya nadie real está jugando (solo bots)
        }
        else
        {
            Time.timeScale = 1f; // NO congelo para que puedas espectar la partida del otro
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


public async void QuitGame() 
{ 
    Time.timeScale = 1f;

    // MONETIZACIÓN: Penalización por abandono voluntario del Host en Privadas
    if (!GameConfig.prizeAwarded && GameConfig.currentFee > 0)
    {
        if (GameConfig.isPrivateMatch && GameConfig.isHostLobby)
        {
            // El Host de una privada huye: pierde la fianza de los demás (él ya pagó la suya)
            int penalty = GameConfig.currentFee * (GameConfig.nHumanPlayers - 1);
            if (penalty > 0)
            {
                TopBarUI.Instance.GastarMonedas(penalty);
                Debug.Log($"[ECONOMÍA] Penalización por abandonar hosteando: -{penalty} monedas.");
            }
        }
        GameConfig.prizeAwarded = true; 
    }

    // Si el gestor de red existe, cerramos la sesión de UGS y apagamos Netcode
    if (SessionNetworkManager.Instance != null)
    {
        // false porque aquí la conexión SÍ está viva y queremos avisar educadamente a UGS
        await SessionNetworkManager.Instance.AbandonarSala(false);
        
        // Esperamos a que los sockets se liberen antes de cambiar de escena
        await System.Threading.Tasks.Task.Delay(500);
    }

    UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
}

    private void SetPauseState(bool isPaused)
    {
        if (pausePanel != null) pausePanel.SetActive(isPaused);
        InteractionManager.Instance.isPaused = isPaused;
        InteractionManager.Instance.UpdateVisualStates();
        Time.timeScale = isPaused ? 0f : 1f;
    }
}