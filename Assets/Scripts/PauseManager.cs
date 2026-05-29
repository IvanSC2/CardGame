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
                // Es perdedor: pierde trofeos según su posición desde el final
                int distanciaUltimo = totalPlayers - puesto;
                if (distanciaUltimo == 0) delta = -20;
                else if (distanciaUltimo == 1) delta = -15;
                else delta = -10;
            }
            else
            {
                // Es ganador (mitad superior)
                if (puesto == 1)
                {
                    // El ganador se lleva la gran mayoría del bote
                    if (totalPlayers == 2)
                    {
                        delta = GameConfig.trophyBote; // 100% del bote (20 trofeos)
                    }
                    else
                    {
                        int premioSegundo = Mathf.RoundToInt(GameConfig.trophyBote * 0.25f);
                        delta = GameConfig.trophyBote - premioSegundo; // 75% del bote (garantiza suma cero perfecta)
                    }
                }
                else if (puesto == 2 && totalPlayers >= 4)
                {
                    // El segundo lugar se lleva el 25% del bote
                    delta = Mathf.RoundToInt(GameConfig.trophyBote * 0.25f);
                }
                else
                {
                    // Tercer lugar en mesa de 6 no gana ni pierde (0 trofeos)
                    delta = 0;
                }
            }

            if (GameConfig.isHostLobby) delta += 5; // Pequeño bono de host por mantener la sala

            TopBarUI.QueuePendingDelta(delta); // Guardado seguro en PlayerPrefs
            GameConfig.trophyAwarded = true;
            GameConfig.trophyBote = 0;

            string signo = delta >= 0 ? "+" : "";
            Debug.Log($"[TROFEOS SUMA-CERO] Partida finalizada. Puesto {puesto}/{totalPlayers}. Delta encolado: {signo}{delta}");
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

        InteractionManager.Instance.SetInfoMessage("<color=#AAAAAA>Has sido eliminado. Ahora eres espectador.</color>", 9999f);
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

    AplicarPenalizacionAbandono();

    isGameOver = true; // Prevenir que TriggerGameOver salte a la vez por la desconexión

    int vivos = 1;
    if (InteractionManager.Instance != null && InteractionManager.Instance.vidas != null)
    {
        vivos = 0;
        foreach (int v in InteractionManager.Instance.vidas) if (v > 0) vivos++;
    }
    if (vivos < 1) vivos = 1;

    int dineroHistorial = -GameConfig.currentFee; 
    if (!GameConfig.isPrivateMatch) dineroHistorial = 0; // Reembolso en pública

    // PENALIZACIÓN ADICIONAL: Si el que se va de la pública es el Host, se traga un anuncio sí o sí
    if (!GameConfig.isPrivateMatch && GameConfig.isHostLobby && AdManager.Instance != null)
    {
        Debug.Log("[CASTIGO] El Host ha abandonado la partida pública. Mostrando anuncio de castigo...");
        AdManager.Instance.MostrarAnuncioIntersticial();
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
            // Penalización fija: fee×1 extra (total para el host = fee×2, independiente del nº de jugadores)
            int penalty = GameConfig.currentFee;
            TopBarUI.QueuePendingDelta(0, -penalty);
            Debug.Log($"[ECONOMÍA] Abandono privada como host: -{penalty} monedas de penalización (total perdido: {GameConfig.currentFee * 2}).");
        }
        else if (GameConfig.currentMatchMode == "public")
        {
            // El fee NO se reembolsa en abandono voluntario: se mantiene en el bote del ganador
            Debug.Log($"[ECONOMÍA] Abandono voluntario en pública: fee ({GameConfig.currentFee}) no reembolsado.");

            // TROFEOS: Solo en partidas públicas
            if (!GameConfig.trophyAwarded)
            {
                int indice = Mathf.Clamp(vivos - 1, 0, GameConfig.trophyDeltaByRank.Length - 1);
                int deltaTrofeos = GameConfig.trophyDeltaByRank[indice];
                if (deltaTrofeos > 0) deltaTrofeos = 0; 
                TopBarUI.QueuePendingDelta(deltaTrofeos);
                GameConfig.trophyAwarded = true;
                Debug.Log($"[TROFEOS] Abandono pública en puesto {vivos}. Delta encolado: {deltaTrofeos}");
            }
        }

        // Marcamos como procesado para no duplicar si pasa Quit y Pause a la vez
        GameConfig.prizeAwarded = true;
        
        // Limpiamos la bandera anti-ragequit (ya aplicamos el castigo o reembolso en esta sesión)
        PlayerPrefs.SetInt("PartidaEnCurso", 0);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Actualiza el castigo offline por ragequit en base al número de jugadores que siguen vivos.
    /// Se llama cada vez que se resuelve una ronda y las vidas cambian.
    /// </summary>
    public void UpdateAntiRageQuitPenalty()
    {
        // Solo aplica en públicas y si la partida sigue en curso
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
        Debug.Log($"[ANTI-RAGEQUIT] Actualizado castigo local a: {penalizacionMax} trofeos (quedan {vivos} vivos).");
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