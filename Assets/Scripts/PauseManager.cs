using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;
using System.Collections.Generic;

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

        // ANALÍTICAS: Registrar el inicio de la partida
        GameConfig.matchStartTime = Time.realtimeSinceStartup;
        if (AnalyticsManager.Instance != null)
            AnalyticsManager.Instance.EventoMatchStarted(GameConfig.currentMatchMode, GameConfig.nPlayers);
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
        // Guard: evita que el Host registre el historial N veces (1 por jugador)
        if (isGameOver) return;
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
            int miAsiento = im.MySeatIndex;

            string stats =
                $"Puesto No {puesto}\n" +
                $"Apuestas Cumplidas: <b>{im.apuestasAcertadasTotales[miAsiento]}</b> / {rondas}\n" +
                $"Bazas Ganadas: <b>{im.bazasTotales[miAsiento]}</b>\n";

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

        // PERFIL: Registrar resultado de la partida en estadísticas + historial
        // Esto lo ejecuta CADA cliente de forma independiente en su propia máquina.
        // NO depende del Host. Cada uno sabe su propio puesto y calcula su propio dinero.
        if (ProfileManager.Instance != null)
        {
            int totalJugadores = InteractionManager.Instance != null 
                ? InteractionManager.Instance.totalPlayers 
                : GameConfig.nPlayers;

            int dineroPartida = 0;
            if (puesto == 1) dineroPartida += GameConfig.currentPrize;
            if (!GameConfig.isPrivateMatch && GameConfig.isHostLobby) dineroPartida += 50;
            dineroPartida -= GameConfig.currentFee;

            // Recopilar nombres de todos los jugadores para el historial
            List<string> nombres = ObtenerNombresParaHistorial();

            ProfileManager.Instance.RegistrarResultadoPartida(
                GameConfig.currentMatchMode,
                puesto,
                totalJugadores,
                dineroPartida,
                nombres,
                GameConfig.difficulty
            );
        }

        // Encendemos el panel visual
        pausePanel.SetActive(true); 

        // ANALÍTICAS: Evento match_completed (Funnel 1, paso 4)
        if (AnalyticsManager.Instance != null)
        {
            int duracion = Mathf.RoundToInt(Time.realtimeSinceStartup - GameConfig.matchStartTime);
            int dineroAnalytics = 0;
            if (puesto == 1) dineroAnalytics += GameConfig.currentPrize;
            dineroAnalytics -= GameConfig.currentFee;
            AnalyticsManager.Instance.EventoMatchCompleted(puesto, duracion, dineroAnalytics, GameConfig.currentMatchMode);
        }

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
    isGameOver = true; // Prevenir que TriggerGameOver salte a la vez por la desconexión

    // MONETIZACIÓN y ESTADÍSTICAS: Penalización por abandono voluntario
    if (!GameConfig.prizeAwarded && GameConfig.currentFee >= 0)
    {
        // 1. Calculamos vivos para saber en qué puesto te vas
        int vivos = 1;
        if (InteractionManager.Instance != null && InteractionManager.Instance.vidas != null)
        {
            vivos = 0;
            foreach (int v in InteractionManager.Instance.vidas) if (v > 0) vivos++;
        }
        if (vivos < 1) vivos = 1;

        int dineroHistorial = -GameConfig.currentFee; // Por defecto pierdes el fee

        if (GameConfig.isPrivateMatch && GameConfig.isHostLobby && GameConfig.currentFee > 0)
        {
            // El Host de una privada huye: pierde la fianza de los demás
            int penalty = GameConfig.currentFee * (GameConfig.nHumanPlayers - 1);
            if (penalty > 0)
            {
                TopBarUI.Instance.GastarMonedas(penalty);
                Debug.Log($"[ECONOMÍA] Penalización por abandonar hosteando privada: -{penalty} monedas.");
            }
        }
        else if (!GameConfig.isPrivateMatch)
        {
            // PÚBLICA: Nadie pierde dinero si el host se va — reembolso del fee
            TopBarUI.Instance.ActualizarMonedas(GameConfig.currentFee);
            dineroHistorial = 0; // En el historial aparece 0
            Debug.Log($"[ECONOMÍA] Matchmaking público: reembolso de {GameConfig.currentFee} monedas.");
        }

        // Registrar en el historial como "Abandonada"
        if (ProfileManager.Instance != null)
        {
            List<string> nombres = ObtenerNombresParaHistorial();

            ProfileManager.Instance.RegistrarResultadoPartida(
                GameConfig.currentMatchMode,
                vivos,
                GameConfig.nPlayers,
                dineroHistorial,
                nombres, 
                GameConfig.difficulty,
                "Abandonada"
            );
        }

        GameConfig.prizeAwarded = true; 
    }

    // ANALÍTICAS: Evento match_abandoned
    if (AnalyticsManager.Instance != null)
    {
        int duracion = Mathf.RoundToInt(Time.realtimeSinceStartup - GameConfig.matchStartTime);
        AnalyticsManager.Instance.EventoMatchAbandoned(duracion, GameConfig.isHostLobby, GameConfig.currentMatchMode);
    }

    // Si el gestor de red existe, cerramos la sesión de UGS y apagamos Netcode
    if (SessionNetworkManager.Instance != null)
    {
        await SessionNetworkManager.Instance.AbandonarSala(false);
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

    /// <summary>
    /// Recopila los nombres de todos los jugadores para guardarlos en el historial.
    /// </summary>
    private List<string> ObtenerNombresParaHistorial()
    {
        List<string> nombres = new List<string>();
        if (InteractionManager.Instance != null)
        {
            for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
            {
                string nombre = InteractionManager.Instance.GetPlayerName(i);
                nombres.Add(nombre);
            }
        }
        return nombres;
    }
}