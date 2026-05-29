using UnityEngine;
using TMPro; 
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;

    [Header("1. HUB Central")]
    public GameObject panelHub;
    public GameObject pToolBar;
    public Button bBack; //

    [Header("Extras Hub")]
    [Tooltip("Imagen u objeto externo al panelHub que se activa/desactiva junto a él")]
    public GameObject extraHubOverlay;
    
    [Header("2. Perfil & Stats")]
    public GameObject panelProfile;

    [Header("Panel de Bienvenida (primera vez)")]
    [Tooltip("Arrastra aquí el GameObject del WelcomePanel. Se activa si no hay Nickname.")]
    public GameObject welcomePanel;
    
    [Header("3. Tienda (Shop)")]
    public GameObject panelShop;

    [Header("4A. Modo Práctica")]
    public GameObject panelPractice;
    public OptionController practicePlayers;
    public OptionController practiceTime;
    public OptionController practiceBotAI;
    public TMP_Text textoPremioPractica; 

    [Header("4B. Modo Público (Matchmaking)")]
    public GameObject panelMatchmakingLobby;
    public Button bLeave;

    [Header("4C. Modo Privado (Private)")]
    public GameObject panelPrivateChoice;  
    public GameObject panelPrivateJoin;   
    public GameObject panelPrivateLobby;   
    public GameObject panelClientLobby;    
    
    [Header("Selectores Modo Privado")]
    public OptionController privatePlayers;
    public OptionController privateTime;
    public OptionController privateDifficulty;
    public OptionController privateEntryFee;
    public TMP_Text textoPrecioPrivada; 

    [Header("Sistema de Popup Global")]
    public GameObject panelInfoPopup;
    public TMP_Text txtPopupMessage;
    public Button btnPopupClose;
    public Button btnPopupGoToShop;

    private MenuLobbyUI _cachedMenuLobbyUI;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // =======================================================
    // POPUP GLOBAL
    // =======================================================
    /// <summary>
    /// Muestra un mensaje en el Popup Global. Si esErrorDinero=true, activa el botón de ir a la Tienda.
    /// </summary>
    public void MostrarPopupInfo(string mensaje, bool esErrorDinero = false)
    {
        if (panelInfoPopup == null) { Debug.LogWarning("[POPUP] Panel no asignado: " + mensaje); return; }

        if (txtPopupMessage != null) txtPopupMessage.text = mensaje;

        // Botón de Tienda: solo visible si es error de dinero
        if (btnPopupGoToShop != null) btnPopupGoToShop.gameObject.SetActive(esErrorDinero);

        panelInfoPopup.SetActive(true);
    }

    public void CerrarPopup()
    {
        if (panelInfoPopup != null) panelInfoPopup.SetActive(false);
    }

    private void ConfigurarPopupBotones()
    {
        if (btnPopupClose != null)
        {
            btnPopupClose.onClick.RemoveAllListeners();
            btnPopupClose.onClick.AddListener(CerrarPopup);
        }
        if (btnPopupGoToShop != null)
        {
            btnPopupGoToShop.onClick.RemoveAllListeners();
            btnPopupGoToShop.onClick.AddListener(() =>
            {
                CerrarPopup();
                MostrarTienda();
            });
        }
    }

    private void Start()
    {
        AudioManager.Instance?.PlayMenuMusic();
        MostrarHubSilencioso();
        ConfigurarPopupBotones();
        if (panelInfoPopup != null) panelInfoPopup.SetActive(false);
        
        // Opciones de Tiempo (4 opciones)
        if(practiceTime != null) { practiceTime.opciones = new string[] { "5s", "10s", "15s", "20s" }; practiceTime.ResetearComponente(); }
        if(privateTime != null) { privateTime.opciones = new string[] { "5s", "10s", "15s", "20s" }; privateTime.ResetearComponente(); }
        
        // Opciones de Dificultad IA (7 niveles)
        string[] diffOpciones = { "Ultra Facil", "Facil", "Normal", "Dificil", "Muy Dificil", "Experto", "Imposible" };
        if(practiceBotAI != null) { practiceBotAI.opciones = diffOpciones; practiceBotAI.ResetearComponente(); }
        if(privateDifficulty != null) { privateDifficulty.opciones = diffOpciones; privateDifficulty.ResetearComponente(); }
        
        if(practicePlayers != null) CalcularPremioPractica();
        if(privatePlayers != null) CalcularPremioPrivada();

        // PERFIL: El WelcomePanel ya no se dispara aquí, sino en SessionNetworkManager 
        // una vez que se ha descargado el perfil real de la nube.

        // ANALÍTICAS: Evento app_opened (Funnel 1, paso 1)
        if (AnalyticsManager.Instance != null) AnalyticsManager.Instance.EventoAppOpened();
    }

    // --- MÁQUINA DE ESTADOS ---

    public void MostrarHub() 
    { 
        MostrarHubInterno(true);
    }

    public void MostrarHubSilencioso()
    {
        MostrarHubInterno(false);
    }

    private void MostrarHubInterno(bool playSound)
    {
        if (playSound) AudioManager.Instance?.PlayButtonGeneric();
        GameConfig.gameStarted = false;
        ApagarTodosLosPaneles(); 
        if(panelHub != null) panelHub.SetActive(true); 
        if(extraHubOverlay != null) extraHubOverlay.SetActive(true);
        if(pToolBar != null) pToolBar.SetActive(true);
        if(bBack != null) bBack.gameObject.SetActive(true);
        
        // Disparamos la limpieza global de la UI Privada
        if (_cachedMenuLobbyUI == null) _cachedMenuLobbyUI = Object.FindFirstObjectByType<MenuLobbyUI>();
        if (_cachedMenuLobbyUI != null)
        {
            _cachedMenuLobbyUI.ResetearLobbyCompleto();
        }
    }
    public void MostrarPerfil() { AudioManager.Instance?.PlayButtonGeneric(); ApagarTodosLosPaneles(); if(panelProfile != null) panelProfile.SetActive(true); }
    public void MostrarTienda() 
    { 
        AudioManager.Instance?.PlayButtonGeneric();
        ApagarTodosLosPaneles(); 
        if(panelShop != null) panelShop.SetActive(true);
        // ANALÍTICAS: Evento shop_opened (Funnel 2, paso 2)
        if (AnalyticsManager.Instance != null) AnalyticsManager.Instance.EventoShopOpened();
    }

    public void IniciarFlujoPractica() { AudioManager.Instance?.PlayButtonGeneric(); GameConfig.currentMatchMode = "practice"; ApagarTodosLosPaneles(); if(panelPractice != null) panelPractice.SetActive(true); }
    public void IniciarFlujoPublico() 
    { 
        AudioManager.Instance?.PlayButtonGeneric();
        GameConfig.currentMatchMode = "public"; 
        ApagarTodosLosPaneles(); 
        if (_cachedMenuLobbyUI != null) _cachedMenuLobbyUI.ResetearLobbyCompleto();
        if(panelMatchmakingLobby != null) panelMatchmakingLobby.SetActive(true); 
        if(bBack != null) bBack.gameObject.SetActive(false); 
        if(pToolBar != null) pToolBar.SetActive(false); 
    }
    
    // Al pulsar "Private" en el Hub, venimos aquí:
    public void IniciarFlujoPrivado() 
    { 
        AudioManager.Instance?.PlayButtonGeneric();
        GameConfig.currentMatchMode = "private"; 
        ApagarTodosLosPaneles(); 
        if (_cachedMenuLobbyUI != null) _cachedMenuLobbyUI.ResetearLobbyCompleto();
        if(panelPrivateChoice != null) panelPrivateChoice.SetActive(true);
        // ANALÍTICAS: Evento matchmaking_started (Funnel 1, paso 2)
        if (AnalyticsManager.Instance != null) AnalyticsManager.Instance.EventoMatchmakingStarted("private");
    }

    // Al pulsar "Join" en pPrivate, venimos aquí:
    public void MostrarPrivateJoin() 
    { 
        AudioManager.Instance?.PlayButtonGeneric();
        ApagarTodosLosPaneles(); 
        if (_cachedMenuLobbyUI != null) _cachedMenuLobbyUI.ResetearLobbyCompleto();
        if(panelPrivateJoin != null) panelPrivateJoin.SetActive(true); 
    }
    
    // Al pulsar "Create" (tras cargar la red), venimos aquí:
    public void MostrarPrivateLobby() { AudioManager.Instance?.PlayButtonGeneric(); ApagarTodosLosPaneles(); if(panelPrivateLobby != null) panelPrivateLobby.SetActive(true); if(bBack != null) bBack.gameObject.SetActive(false); if(pToolBar != null) pToolBar.SetActive(false); }
    
    // Al pulsar "Join" (tras buscar el código), venimos aquí:
    public void MostrarClientLobby() { AudioManager.Instance?.PlayButtonGeneric(); ApagarTodosLosPaneles(); if(panelClientLobby != null) panelClientLobby.SetActive(true); if(bBack != null) bBack.gameObject.SetActive(false); if(pToolBar != null) pToolBar.SetActive(false); } 

    private void ApagarTodosLosPaneles()
    {
        if (panelHub != null) panelHub.SetActive(false);
        if (extraHubOverlay != null) extraHubOverlay.SetActive(false);
        if (panelProfile != null) panelProfile.SetActive(false);
        if (panelShop != null) panelShop.SetActive(false);
        if (panelPractice != null) panelPractice.SetActive(false);
        if (panelMatchmakingLobby != null) panelMatchmakingLobby.SetActive(false);
        if (panelPrivateChoice != null) panelPrivateChoice.SetActive(false);
        if (panelPrivateJoin != null) panelPrivateJoin.SetActive(false);
        if (panelPrivateLobby != null) panelPrivateLobby.SetActive(false);
        if (panelClientLobby != null) panelClientLobby.SetActive(false);
    }

    // --- ECONOMÍA DINÁMICA ---

    public void CalcularPremioPractica()
    {
        if (practicePlayers == null || practiceTime == null || practiceBotAI == null || textoPremioPractica == null) return;

        int premioBase = 50;
        
        // Bonus por Jugadores: 2 jugadores (+0), 3 jugadores (+35), 4 (+70), 5 (+105), 6 (+140)
        int bonusJugadores = practicePlayers.ObtenerIndice() * 35;
        
        // Bonus por Tiempo de Turno: 5s (+80), 10s (+40), 15s (0), 20s (-30)
        int idxTiempo = practiceTime.ObtenerIndice();
        int bonusTiempo = idxTiempo switch
        {
            0 => 80,  // 5s
            1 => 40,  // 10s
            2 => 0,   // 15s
            3 => -30, // 20s
            _ => 0
        };
        
        // Bonus por Dificultad de la IA: escala no lineal y progresiva de bot
        int dificultadBot = practiceBotAI.ObtenerIndice();
        int bonusIA = dificultadBot switch
        {
            0 => 0,   // Ultra Easy
            1 => 30,  // Easy
            2 => 80,  // Normal
            3 => 180, // Difficult
            4 => 320, // Hard
            5 => 500, // UltraHard
            6 => 750, // Impossible
            _ => 0
        };

        int premioTotal = premioBase + bonusJugadores + bonusTiempo + bonusIA;
        textoPremioPractica.text = premioTotal.ToString();
    }

    // =======================================================
    // --- FLUJO MODO PRÁCTICA (HOST LOCAL OFFLINE) ---
    // =======================================================
    public void IniciarPartidaPractica()
    {
        AudioManager.Instance?.PlayButtonAction();
        // 1. Extraemos los valores de los selectores de la UI
        int maxJugadores = practicePlayers.ObtenerIndice() + 2; // +2 porque el índice 0 equivale a 1v1 (2 jugadores)
        int dificultadBot = practiceBotAI.ObtenerIndice();
        
        string tiempoStr = practiceTime.opciones[practiceTime.ObtenerIndice()].Replace("s", "");
        int turnTime = int.Parse(tiempoStr);

        // 2. Guardamos en el GameConfig (Memoria Persistente)
        GameConfig.nPlayers = maxJugadores;
        GameConfig.difficulty = dificultadBot;
        GameConfig.turnTime = turnTime;

        // --- CORRECCIÓN DE ECONOMÍA DE PRÁCTICA Y PREMIO ---
        int premioBase = 50;
        int bonusJugadores = practicePlayers.ObtenerIndice() * 35;
        
        int idxTiempo = practiceTime.ObtenerIndice();
        int bonusTiempo = idxTiempo switch
        {
            0 => 80,  // 5s
            1 => 40,  // 10s
            2 => 0,   // 15s
            3 => -30, // 20s
            _ => 0
        };

        int bonusIA = dificultadBot switch
        {
            0 => 0,   // Ultra Easy
            1 => 30,  // Easy
            2 => 80,  // Normal
            3 => 180, // Difficult
            4 => 320, // Hard
            5 => 500, // UltraHard
            6 => 750, // Impossible
            _ => 0
        };

        GameConfig.currentPrize = premioBase + bonusJugadores + bonusTiempo + bonusIA;
        GameConfig.currentFee = 0; // Sin coste de entrada en Práctica
        GameConfig.currentMatchMode = "practice";
        GameConfig.isPrivateMatch = false;
        GameConfig.isHostLobby = false;
        GameConfig.prizeAwarded = false;

        Debug.Log($"[PRÁCTICA] Iniciando Host Local: {maxJugadores} Jugadores, Dificultad IA: {dificultadBot}, Premio Calculado: {GameConfig.currentPrize} Monedas");

        // 3. Arrancamos el motor de red en modo HOST y cargamos la escena directamente
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            // Limpiamos la baraja por seguridad antes de cambiar de escena
            if (CardDatabase.deck != null) CardDatabase.deck.Clear();
            
            LoadingManager.Instance?.MostrarCargando("Cargando partida...");
            
            Unity.Netcode.NetworkManager.Singleton.StartHost();
            Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene("MainGame", UnityEngine.SceneManagement.LoadSceneMode.Single);

            // ANALÍTICAS: Evento match_started (Funnel 1, paso 3)
            GameConfig.matchStartTime = Time.realtimeSinceStartup;
            if (AnalyticsManager.Instance != null) AnalyticsManager.Instance.EventoMatchStarted("practice", maxJugadores);
        }
        else
        {
            Debug.LogError("Error: NetworkManager.Singleton no existe. Asegúrate de que el NetworkManager está en la escena Hub.");
        }
    }
    // =======================================================
    // --- FLUJO MODO PÚBLICO (MATCHMAKING) ---
    // =======================================================
    public async void IntentarMatchmaking()
    {
        AudioManager.Instance?.PlayButtonAction();
        int feeMatchmaking = 200; // El coste fijo por entrar a partidas públicas

        if (TopBarUI.Instance != null && !TopBarUI.Instance.TieneSuficientes(feeMatchmaking))
        {
            MostrarPopupInfo("No tienes monedas suficientes para jugar en público. \n\nNecesitas 200 ♠.", esErrorDinero: true);
            return;
        }

        Debug.Log("[MATCHMAKING] Saldo validado. Abriendo panel inmediatamente...");
        
        // 1. ABRIMOS EL PANEL AL INSTANTE (Antes de tocar la red)
        IniciarFlujoPublico();
        
        
        
        if (_cachedMenuLobbyUI == null) _cachedMenuLobbyUI = Object.FindFirstObjectByType<MenuLobbyUI>();
        if (_cachedMenuLobbyUI != null && _cachedMenuLobbyUI.txtJugadoresMatchmaking != null)
        {
            _cachedMenuLobbyUI.txtJugadoresMatchmaking.text = "Buscando...";
            if (_cachedMenuLobbyUI.txtListaNombresMatchmaking != null) _cachedMenuLobbyUI.txtListaNombresMatchmaking.text = "";
        }

        //GameConfig.nPlayers = 4; 
        GameConfig.difficulty = 0; 
        GameConfig.turnTime = 10f; 

        if (SessionNetworkManager.Instance != null)
        {
            // 2. Ejecutamos la búsqueda pesada en segundo plano
            await SessionNetworkManager.Instance.IniciarMatchmakingPublico(feeMatchmaking);
        }
    }
    public int ObtenerPremioPrivadaCalculado()
    {
        if (privateEntryFee == null || privatePlayers == null) return 0;

        string textoFee = privateEntryFee.opciones[privateEntryFee.ObtenerIndice()];
        int entryFee = int.Parse(textoFee);
        int cantidadJugadores = privatePlayers.ObtenerIndice() + 2;

        // Eliminamos el bonusTiempo y bonusIA para evitar inflación e inyección de dinero gratis.
        // El premio estimado en el creador es Fee * (Max Jugadores de la sala)
        // El premio real se recalculará justo al arrancar basándose en los humanos reales.
        return (cantidadJugadores * entryFee);
    }

    public void CalcularPremioPrivada()
    {
        if (textoPrecioPrivada != null)
        {
            textoPrecioPrivada.text = ObtenerPremioPrivadaCalculado().ToString();
        }
    }
}