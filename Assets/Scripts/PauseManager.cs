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
        if (isGameOver && !isSpectating) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (!isSpectating) TogglePause();
        }
    }

    public void TogglePause()
    {
        AudioManager.Instance?.PlayButtonGeneric();
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
        AudioManager.Instance?.PlayButtonGeneric();
        if (isGameOver) return;
        SetPauseState(false);
    }

    //  RECIBE EL PUESTO NUMÉRICO 
   public void TriggerGameOver(int puesto)
    {
        if (isGameOver) return;
        isGameOver = true;
        isSpectating = false;

        // Forzar apertura del panel aunque el jugador estuviera en pausa o espectando
        if (pausePanel != null) pausePanel.SetActive(true);
        if (InteractionManager.Instance != null) InteractionManager.Instance.isPaused = true;
        AudioManager.Instance?.SetMusicLowVolume(true);

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
        if (InteractionManager.Instance != null)
        {
            for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
            {
                if (i != InteractionManager.Instance.MySeatIndex && 
                    InteractionManager.Instance.vidas[i] > 0 && 
                    InteractionManager.Instance.IsPlayerConnectedAndHuman(i))
                {
                    otrosHumanosVivos++;
                }
            }
        }

        // 3. Cambio de botones para el modo Game Over
        if (resumeButton != null) resumeButton.SetActive(false);

        // Espectar SOLO si:
        //  - Has PERDIDO (puesto > 1)
        //  - Quedan 2 o más jugadores activos (jugadoresVivos > 1): si solo queda 1, la partida ha terminado para todos
        //  - Al menos uno de esos jugadores es humano real (otrosHumanosVivos > 0)
        if (spectateButton != null)
        {
            bool sePuedeEspectar = (puesto > 1) && (jugadoresVivos > 1) && (otrosHumanosVivos > 0);
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

        // MONETIZACIÓN Y TROFEOS: Se aplica a públicas/privadas (con Fee > 0) y a modo Práctica
        if (!GameConfig.prizeAwarded && (GameConfig.currentFee > 0 || GameConfig.currentMatchMode == "practice"))
        {
            int monedaDelta = 0;
            if (puesto == 1)
            {
                monedaDelta += GameConfig.currentPrize;
                Debug.Log($"[ECONOMÍA] ¡Has ganado en modo {GameConfig.currentMatchMode}! Recibes {GameConfig.currentPrize} monedas.");
            }
            if (GameConfig.currentMatchMode == "public" && GameConfig.isHostLobby)
            {
                monedaDelta += 50;
                Debug.Log("[ECONOMÍA] Bono de 50 monedas por mantener el servidor público vivo.");
            }
            if (monedaDelta != 0) TopBarUI.QueuePendingDelta(0, monedaDelta);
            GameConfig.prizeAwarded = true;
        }

        // TROFEOS: Solo se aplica en partidas públicas de MatchMaking (Suma Cero Dinámica)
        if (!GameConfig.trophyAwarded && GameConfig.currentMatchMode == "public")
        {
            int totalPlayers = InteractionManager.Instance != null ? InteractionManager.Instance.totalPlayers : GameConfig.nPlayers;
            int delta = 0;

            bool esLoser = puesto > totalPlayers / 2;
            if (esLoser)
            {
                int distanciaUltimo = totalPlayers - puesto;
                if (distanciaUltimo == 0) delta = -20;
                else if (distanciaUltimo == 1) delta = -15;
                else delta = -10;
            }
            else
            {
                if (puesto == 1)
                {
                    if (totalPlayers == 2)
                        delta = 20;
                    else
                    {
                        int premioSegundo = Mathf.RoundToInt(20f * 0.25f);
                        delta = 20 - premioSegundo;
                    }
                }
                else if (puesto == 2 && totalPlayers >= 4)
                {
                    delta = Mathf.RoundToInt(20f * 0.25f);
                }
            }

            if (GameConfig.isHostLobby) delta += 5;

            TopBarUI.QueuePendingDelta(delta);
            GameConfig.trophyAwarded = true;
            GameConfig.trophyBote = 0;

            string signo = delta >= 0 ? "+" : "";
            Debug.Log($"Trofeos aplicados: puesto {puesto}/{totalPlayers} → {signo}{delta}");
        }

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

            // Recuperamos el delta de trofeos que acabamos de calcular arriba (solo para públicas)
            // GameConfig.trophyBote ya fue reseteado a 0, así que guardamos el delta en una var local
            int trofeosDelta = 0;
            if (GameConfig.currentMatchMode == "public")
            {
                // Recalculamos el delta igual que en el bloque de trofeos de arriba
                int tp = totalJugadores;
                bool esLoser2 = puesto > tp / 2;
                if (esLoser2)
                {
                    int dist = tp - puesto;
                    if (dist == 0) trofeosDelta = -20;
                    else if (dist == 1) trofeosDelta = -15;
                    else trofeosDelta = -10;
                }
                else
                {
                    if (puesto == 1)
                    {
                        if (tp == 2) trofeosDelta = 20;
                        else { int p2 = Mathf.RoundToInt(20 * 0.25f); trofeosDelta = 20 - p2; }
                    }
                    else if (puesto == 2 && tp >= 4)
                        trofeosDelta = Mathf.RoundToInt(20 * 0.25f);
                    else
                        trofeosDelta = 0;
                }
                if (GameConfig.isHostLobby) trofeosDelta += 5;
            }

            ProfileManager.Instance.RegistrarResultadoPartida(
                GameConfig.currentMatchMode,
                puesto,
                totalJugadores,
                dineroPartida,
                nombres,
                GameConfig.difficulty,
                "",
                trofeosDelta
            );
        }

        // Limpiamos la bandera anti-ragequit (el final fue legal)
        PlayerPrefs.SetInt("PartidaEnCurso", 0);
        PlayerPrefs.Save();

        // Encendemos el panel visual
        pausePanel.SetActive(true); 
        AudioManager.Instance?.SetMusicLowVolume(true);

        // ANALÍTICAS: Evento match_completed (Funnel 1, paso 4)
        if (AnalyticsManager.Instance != null)
        {
            int duracion = Mathf.RoundToInt(Time.realtimeSinceStartup - GameConfig.matchStartTime);
            int dineroAnalytics = 0;
            if (puesto == 1) dineroAnalytics += GameConfig.currentPrize;
            dineroAnalytics -= GameConfig.currentFee;
            AnalyticsManager.Instance.EventoMatchCompleted(puesto, duracion, dineroAnalytics, GameConfig.currentMatchMode);
        }

        // TEST A/B: Lógica de Fricción Publicitaria
        if (AdManager.Instance != null && RemoteConfigManager.Instance != null)
        {
            string estrategia = RemoteConfigManager.Instance.adStrategy;
            if (estrategia == "aggressive") 
            {
                // Grupo A: Anuncio SIEMPRE (ganes o pierdas)
                AdManager.Instance.MostrarAnuncioIntersticial();
            } 
            else if (estrategia == "punitive" && puesto > 1) 
            {
                // Grupo B: Anuncio SOLO si has perdido (puesto 2, 3 o 4)
                AdManager.Instance.MostrarAnuncioIntersticial();
            }
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
        AudioManager.Instance?.PlayButtonGeneric();
        isGameOver = false; // Ya no estamos en la pantalla final
        isSpectating = true; // Pero somos un fantasma
        SetPauseState(false); // Quitamos la pausa y escondemos el panel

        // Marcamos prizeAwarded para que QuitGame no registre una segunda entrada "Abandonada"
        // si el espectador decide salir mientras observa.
        GameConfig.prizeAwarded = true;

        InteractionManager.Instance.SetInfoMessage("<color=#AAAAAA>Has sido eliminado. Ahora eres espectador.</color>", 9999f);
    }

    /// <summary>
    /// Llamado cuando la partida termina definitivamente (quedan ≤1 jugadores vivos).
    /// Muestra al espectador la pantalla final con solo el botón de Salir.
    /// También sobrescribe el menú de Pausa si algún jugador lo tenía abierto.
    /// </summary>
    public void NotificarFinPartidaDefinitivo()
    {
        if (!isSpectating) return;

        isSpectating = false;
        isGameOver = true;
        Time.timeScale = 0f;

        if (pausePanel != null) pausePanel.SetActive(true);
        if (InteractionManager.Instance != null) InteractionManager.Instance.isPaused = true;
        AudioManager.Instance?.SetMusicLowVolume(true);

        if (resumeButton != null) resumeButton.SetActive(false);
        if (spectateButton != null) spectateButton.SetActive(false);
        if (titleText != null) titleText.text = "PARTIDA FINALIZADA";
        if (statsText != null)
        {
            statsText.gameObject.SetActive(true);
            statsText.text = "La partida ha concluido.\nFuiste eliminado.\n\n<size=70%>Tus stats ya fueron registradas al ser eliminado.</size>";
            statsText.alignment = TMPro.TextAlignmentOptions.Center;
        }
    }

    public void RestartGame()
    {
        AudioManager.Instance?.PlayButtonGeneric();
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
    AudioManager.Instance?.PlayButtonAction();
    Time.timeScale = 1f;
    LoadingManager.Instance?.MostrarCargando("Volviendo al menú...");

    // ¿El juego ya terminó legítimamente (GameOver normal o espectador)?
    // isGameOver = true significa que TriggerGameOver ya procesó el resultado correctamente.
    // prizeAwarded = true significa que el premio/castigo ya se aplicó (incluso en modo espectador).
    bool yaTerminadaLegalmente = isGameOver || isSpectating || GameConfig.prizeAwarded;

    if (!yaTerminadaLegalmente)
    {
        // Abandono real durante la partida: aplicar penalización y registrar en historial
        AplicarPenalizacionAbandono();

        int vivos = 1;
        if (InteractionManager.Instance != null && InteractionManager.Instance.vidas != null)
        {
            vivos = 0;
            foreach (int v in InteractionManager.Instance.vidas) if (v > 0) vivos++;
        }
        if (vivos < 1) vivos = 1;

        int dineroHistorial = -GameConfig.currentFee; 
        if (!GameConfig.isPrivateMatch) dineroHistorial = 0;

        // Castigo extra al Host que abandona una pública
        if (!GameConfig.isPrivateMatch && GameConfig.isHostLobby && AdManager.Instance != null)
        {
            AdManager.Instance.MostrarAnuncioIntersticial();
        }

        if (ProfileManager.Instance != null)
        {
            List<string> nombres = ObtenerNombresParaHistorial();
            ProfileManager.Instance.RegistrarResultadoPartida(
                GameConfig.currentMatchMode, vivos, GameConfig.nPlayers,
                dineroHistorial, nombres, GameConfig.difficulty, "Abandonada");
        }

        if (AnalyticsManager.Instance != null)
        {
            int duracion = Mathf.RoundToInt(Time.realtimeSinceStartup - GameConfig.matchStartTime);
            AnalyticsManager.Instance.EventoMatchAbandoned(duracion, GameConfig.isHostLobby, GameConfig.currentMatchMode);
        }
    }
    else
    {
        Debug.Log("Salida legal, no se registra como Abandonada.");
    }

    isGameOver = true;

    // Si el host abandona en mitad de la partida, notifica a los supervivientes
    // antes de desconectar para que reciban su GameOver y sus premios.
    if (!yaTerminadaLegalmente && IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
    {
        var im = InteractionManager.Instance;
        if (im != null && im.vidas != null)
            AvisarSupervivientesGanadoresClientRpc(im.vidas);
        await System.Threading.Tasks.Task.Delay(700);
    }

    if (SessionNetworkManager.Instance != null)
    {
        await SessionNetworkManager.Instance.AbandonarSala(false);
        await System.Threading.Tasks.Task.Delay(500);
    }

    UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
}

    [Rpc(SendTo.NotServer)]
    private void AvisarSupervivientesGanadoresClientRpc(int[] vidasSnapshot)
    {
        if (InteractionManager.Instance == null) return;
        int localId = InteractionManager.Instance.MySeatIndex;
        if (localId < 0 || localId >= vidasSnapshot.Length) return;

        bool sigueVivo = vidasSnapshot[localId] > 0;
        if (!isGameOver && !isSpectating && sigueVivo)
        {
            int misVidas = vidasSnapshot[localId];
            int puesto = 1;
            for (int i = 0; i < vidasSnapshot.Length; i++)
            {
                if (i != localId && vidasSnapshot[i] > misVidas) puesto++;
            }
            TriggerGameOver(puesto);
        }
    }

    // =======================================================
    // GESTIÓN DE CIERRES ABRUPTOS (Alt+F4 o Cerrar Ventana)
    // =======================================================
    private void OnApplicationQuit()
    {
        AplicarPenalizacionAbandono();
    }

    private void AplicarPenalizacionAbandono()
    {
        // Si la partida ya se ha acabado por cauces normales o ya hemos penalizado, no hacemos nada
        if (isGameOver || GameConfig.prizeAwarded || GameConfig.currentFee < 0) return;

        int vivos = 1;
        if (InteractionManager.Instance != null && InteractionManager.Instance.vidas != null)
        {
            vivos = 0;
            foreach (int v in InteractionManager.Instance.vidas) if (v > 0) vivos++;
        }
        if (vivos < 1) vivos = 1;

        if (GameConfig.isPrivateMatch && GameConfig.isHostLobby && GameConfig.currentFee > 0)
        {
            int penalty = GameConfig.currentFee;
            TopBarUI.QueuePendingDelta(0, -penalty);
        }
        else if (GameConfig.currentMatchMode == "public")
        {
            if (!GameConfig.trophyAwarded)
            {
                int indice = Mathf.Clamp(vivos - 1, 0, GameConfig.trophyDeltaByRank.Length - 1);
                int deltaTrofeos = GameConfig.trophyDeltaByRank[indice];
                if (deltaTrofeos > 0) deltaTrofeos = 0;
                TopBarUI.QueuePendingDelta(deltaTrofeos);
                GameConfig.trophyAwarded = true;
            }
        }

        // Marcamos como procesado para no duplicar si pasa Quit y Pause a la vez
        GameConfig.prizeAwarded = true;
        
        // Limpiamos la bandera anti-ragequit (ya aplicamos el castigo o reembolso en esta sesión)
        PlayerPrefs.SetInt("PartidaEnCurso", 0);
        PlayerPrefs.Save();
    }

    public void UpdateAntiRageQuitPenalty()
    {
        if (GameConfig.currentMatchMode != "public" || GameConfig.prizeAwarded) return;

        int vivos = 1;
        if (InteractionManager.Instance != null && InteractionManager.Instance.vidas != null)
        {
            vivos = 0;
            foreach (int v in InteractionManager.Instance.vidas) if (v > 0) vivos++;
        }
        if (vivos < 1) vivos = 1;

        int indice = Mathf.Clamp(vivos - 1, 0, GameConfig.trophyDeltaByRank.Length - 1);
        int penalizacionMax = Mathf.Min(0, GameConfig.trophyDeltaByRank[indice]);
        
        PlayerPrefs.SetInt("RageQuit_Trophies", penalizacionMax);
        PlayerPrefs.Save();
    }

    private void SetPauseState(bool isPaused)
    {
        if (pausePanel != null) pausePanel.SetActive(isPaused);
        InteractionManager.Instance.isPaused = isPaused;
        InteractionManager.Instance.UpdateVisualStates();
        Time.timeScale = isPaused ? 0f : 1f;

        AudioManager.Instance?.SetMusicLowVolume(isPaused);
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