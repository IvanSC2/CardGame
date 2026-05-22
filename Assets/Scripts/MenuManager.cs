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

    private MenuLobbyUI _cachedMenuLobbyUI;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        MostrarHub();
        
        if(practicePlayers != null) CalcularPremioPractica();
        if(privatePlayers != null) CalcularPremioPrivada();

        // PERFIL: Si el jugador no tiene nickname, mostrar panel de bienvenida
        if (!PlayerPrefs.HasKey("Nickname") && welcomePanel != null)
        {
            welcomePanel.SetActive(true);
        }
    }

    // --- MÁQUINA DE ESTADOS ---

    public void MostrarHub() 
    { 
        GameConfig.gameStarted = false;
        ApagarTodosLosPaneles(); 
        if(panelHub != null) panelHub.SetActive(true); 
        if(pToolBar != null) pToolBar.SetActive(true);
        if(bBack != null) bBack.gameObject.SetActive(true);
        
        // Disparamos la limpieza global de la UI Privada
        if (_cachedMenuLobbyUI == null) _cachedMenuLobbyUI = Object.FindFirstObjectByType<MenuLobbyUI>();
        if (_cachedMenuLobbyUI != null)
        {
            _cachedMenuLobbyUI.ResetearLobbyCompleto();
        }
    }
    public void MostrarPerfil() { ApagarTodosLosPaneles(); if(panelProfile != null) panelProfile.SetActive(true); }
    public void MostrarTienda() { ApagarTodosLosPaneles(); if(panelShop != null) panelShop.SetActive(true); }

    public void IniciarFlujoPractica() { GameConfig.currentMatchMode = "practice"; ApagarTodosLosPaneles(); if(panelPractice != null) panelPractice.SetActive(true); }
    public void IniciarFlujoPublico() { GameConfig.currentMatchMode = "public"; ApagarTodosLosPaneles(); if(panelMatchmakingLobby != null) panelMatchmakingLobby.SetActive(true); if(bBack != null) bBack.gameObject.SetActive(false); if(pToolBar != null) pToolBar.SetActive(false); }
    
    // Al pulsar "Private" en el Hub, venimos aquí:
    public void IniciarFlujoPrivado() { GameConfig.currentMatchMode = "private"; ApagarTodosLosPaneles(); if(panelPrivateChoice != null) panelPrivateChoice.SetActive(true); }

    // Al pulsar "Join" en pPrivate, venimos aquí:
    public void MostrarPrivateJoin() { ApagarTodosLosPaneles(); if(panelPrivateJoin != null) panelPrivateJoin.SetActive(true); }
    
    // Al pulsar "Create" (tras cargar la red), venimos aquí:
    public void MostrarPrivateLobby() { ApagarTodosLosPaneles(); if(panelPrivateLobby != null) panelPrivateLobby.SetActive(true); if(bBack != null) bBack.gameObject.SetActive(false); if(pToolBar != null) pToolBar.SetActive(false); }
    
    // Al pulsar "Join" (tras buscar el código), venimos aquí:
    public void MostrarClientLobby() { ApagarTodosLosPaneles(); if(panelClientLobby != null) panelClientLobby.SetActive(true); if(bBack != null) bBack.gameObject.SetActive(false); if(pToolBar != null) pToolBar.SetActive(false); } 

    private void ApagarTodosLosPaneles()
    {
        if (panelHub != null) panelHub.SetActive(false);
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
        if (practicePlayers == null || textoPremioPractica == null) return;

        int premioBase = 50;
        int bonusJugadores = practicePlayers.ObtenerIndice() * 20;
        int idxTiempo = practiceTime.ObtenerIndice();
        int bonusTiempo = (2 - idxTiempo) * 30;
        int bonusIA = practiceBotAI.ObtenerIndice() * 50;

        int premioTotal = premioBase + bonusJugadores + bonusTiempo + bonusIA;
        textoPremioPractica.text = premioTotal.ToString();
    }

    // =======================================================
    // --- FLUJO MODO PRÁCTICA (HOST LOCAL OFFLINE) ---
    // =======================================================
    public void IniciarPartidaPractica()
    {
        // 1. Extraemos los valores de los selectores de la UI
        int maxJugadores = practicePlayers.ObtenerIndice() + 2; // +2 porque el índice 0 equivale a 1v1 (2 jugadores)
        int dificultadBot = practiceBotAI.ObtenerIndice();
        
        // implementar el tiempo por turno en Práctica en el futuro
        // string tiempoStr = practiceTime.opciones[practiceTime.ObtenerIndice()].Replace("s", "");
        // int turnTime = int.Parse(tiempoStr);

        // 2. Guardamos en el GameConfig (Memoria Persistente)
        GameConfig.nPlayers = maxJugadores;
        GameConfig.difficulty = dificultadBot;

        Debug.Log($"[PRÁCTICA] Iniciando Host Local: {maxJugadores} Jugadores, Dificultad IA: {dificultadBot}");

        // 3. Arrancamos el motor de red en modo HOST y cargamos la escena directamente
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            // Limpiamos la baraja por seguridad antes de cambiar de escena
            if (CardDatabase.deck != null) CardDatabase.deck.Clear();
            
            Unity.Netcode.NetworkManager.Singleton.StartHost();
            Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene("MainGame", UnityEngine.SceneManagement.LoadSceneMode.Single);
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
        int feeMatchmaking = 200; // El coste fijo por entrar a partidas públicas

        if (TopBarUI.Instance != null && !TopBarUI.Instance.TieneSuficientes(feeMatchmaking))
        {
            Debug.LogWarning("No tienes dinero suficiente para el Matchmaking.");
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

        int bonusTiempo = (2 - privateTime.ObtenerIndice()) * 15;
        int bonusIA = privateDifficulty.ObtenerIndice() * 20;

        return (cantidadJugadores * entryFee) + bonusTiempo + bonusIA;
    }

    public void CalcularPremioPrivada()
    {
        if (textoPrecioPrivada != null)
        {
            textoPrecioPrivada.text = ObtenerPremioPrivadaCalculado().ToString();
        }
    }
}