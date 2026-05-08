using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Netcode;

public class MenuLobbyUI : MonoBehaviour
{
    [Header("Panel: PRIVATE LOBBY (HOST)")]
    public TMP_Text txtCodigoGeneradoHost;
    public TMP_Text txtJugadoresHost;
    public TMP_Text txtPrecioLobbyHost;
    public TMP_Text txtPremioLobbyHost;
    public TMP_Text txtListaNombresHost;
    public Button btnStartHost;
    public Button btnLeaveHost;

    [Header("Panel: PRIVATE JOIN (Buscador)")]
    public TMP_InputField inputCodigoAmigo;

    [Header("Buscador - Información Lateral")]
    public GameObject panelInfoPartida;
    public TMP_Text txtDetalleJugadores;
    public TMP_Text txtDetalleFee;
    public TMP_Text txtDetallePrize;

    [Header("Panel: MATCHMAKING LOBBY")]
    public TMP_Text txtJugadoresMatchmaking;
    public TMP_Text txtPrecioLobbyMatchmaking;
    public TMP_Text txtPremioLobbyMatchmaking;
    public TMP_Text txtListaNombresMatchmaking;
    public Button btnLeaveMatchmaking;

    [Header("Buscador - Botones")]
    public Button btnSearch;
    public Button btnJoin;

    [Header("Panel: CLIENT LOBBY (Esperando)")]
    public TMP_Text txtJugadoresCliente;
    public TMP_Text txtPrecioLobbyCliente;
    public TMP_Text txtPremioLobbyCliente;
    public TMP_Text txtListaNombresCliente;
    public Button btnLeaveCliente;
    public GameObject pToolBar;
    public Button bBack;
    public Button bCreate;
    public Button bJoin;

    private int feeSalaActual = 0;
    private int premioSalaActual = 0;

    private void Start()
    {
        btnSearch.gameObject.SetActive(true);
        btnJoin.gameObject.SetActive(false);
        if (panelInfoPartida != null) panelInfoPartida.SetActive(false);

        btnSearch.onClick.AddListener(OnSearchClicked);
        btnJoin.onClick.AddListener(OnJoinConfirmed);

        btnLeaveHost.onClick.AddListener(AbandonarLobby);
        btnLeaveCliente.onClick.AddListener(AbandonarLobby);

        inputCodigoAmigo.onValueChanged.AddListener((texto) =>
        {
            if (btnJoin.gameObject.activeSelf)
            {
                ResetearBuscadorSinBorrarTexto();
            }
        });

        btnStartHost.onClick.AddListener(() =>
        {
            if (NetworkManager.Singleton.SceneManager == null)
            {
                Debug.LogError("Error: El SceneManager sigue siendo NULL.");
                return;
            }

            if (NetworkManager.Singleton.IsServer)
            {
                if (CardDatabase.deck != null) CardDatabase.deck.Clear();
                NetworkManager.Singleton.SceneManager.LoadScene("MainGame", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        });
        
        if (btnLeaveMatchmaking != null) btnLeaveMatchmaking.onClick.AddListener(AbandonarLobby);
    }

    private void OnEnable()
    {
        // Suscripción temprana al evento de conexión física de Netcode
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += EvaluacionCondicionTransicion;
        }
    }

    private void OnDisable()
    {
        // Limpieza de eventos para no dejar basura en memoria
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= EvaluacionCondicionTransicion;
        }
    }

    // El Callback Oficial del Host para cambiar de escena
    private void EvaluacionCondicionTransicion(ulong clientId)
    {
        // Solo el Host tiene autoridad para transicionar la escena
        if (!NetworkManager.Singleton.IsHost) return;

        if (SessionNetworkManager.Instance != null && SessionNetworkManager.Instance.currentSession != null)
        {
            var session = SessionNetworkManager.Instance.currentSession;
            
            // Verificamos que estamos en Matchmaking público
            if (!session.IsPrivate)
            {
                int conexionesFisicas = NetworkManager.Singleton.ConnectedClientsList.Count;
                Debug.Log($"[MATCHMAKING] Nodo {clientId} validado en red. Ocupación física: {conexionesFisicas}/{session.MaxPlayers}");

                // Si se llena físicamente la sala, mandamos a todos al juego
                if (conexionesFisicas == session.MaxPlayers)
                {
                    Debug.Log("[MATCHMAKING] ¡Cuórum topológico absoluto alcanzado! Viajando a MainGame...");
                    if (CardDatabase.deck != null) CardDatabase.deck.Clear();
                    
                    NetworkManager.Singleton.SceneManager.LoadScene("MainGame", UnityEngine.SceneManagement.LoadSceneMode.Single);
                }
            }
        }
    }

    // Método auxiliar para el listener
    private void ResetearBuscadorSinBorrarTexto()
    {
        btnSearch.gameObject.SetActive(true);
        btnSearch.interactable = true;
        btnJoin.gameObject.SetActive(false);
        if (panelInfoPartida != null) panelInfoPartida.SetActive(false);
    }

   private void Update()
    {
        if (SessionNetworkManager.Instance != null && SessionNetworkManager.Instance.currentSession != null)
        {
            var session = SessionNetworkManager.Instance.currentSession;
            int conectadosNube = session.Players.Count;
            int maxJugadores = session.MaxPlayers;

            // Actualización visual de textos
            string textoContador = $"{conectadosNube}/{maxJugadores}";
            if (txtJugadoresHost != null) txtJugadoresHost.text = textoContador;
            if (txtJugadoresCliente != null) txtJugadoresCliente.text = textoContador;
            if (txtJugadoresMatchmaking != null) txtJugadoresMatchmaking.text = textoContador;

            // Actualización de lista de nombres
            string listaGenerada = "";
            for (int i = 0; i < conectadosNube; i++)
            {
                if (i == 0) listaGenerada += session.IsHost ? "TÚ (Host)\n" : "HOST\n";
                else listaGenerada += $"JUGADOR {i}\n";
            }
            if (txtListaNombresMatchmaking != null) txtListaNombresMatchmaking.text = listaGenerada;

            // AutoStar del MatchMaking
            if (session.IsHost && !session.IsPrivate)
            {
                int conectadosRed = NetworkManager.Singleton.ConnectedClientsList.Count;

                // LOG DE DIAGNÓSTICO
                if (Time.frameCount % 120 == 0) 
                {
                    Debug.Log($"[DIAGNÓSTICO MM] Nube: {conectadosNube}/{maxJugadores} | Red NGO: {conectadosRed}/{maxJugadores}");
                }

                // Condición de victoria: Si por RED ya estamos todos, arrancamos sin mirar a la nube si hace falta
                if (conectadosRed >= maxJugadores)
                {
                    Debug.Log($"[MATCHMAKING] ¡CONDICIÓN CUMPLIDA! Red NGO detecta {conectadosRed} jugadores. Lanzando partida...");
                    
                    if (NetworkManager.Singleton.IsServer)
                    {
                        if (CardDatabase.deck != null) CardDatabase.deck.Clear();
                        
                        // Verificamos si el SceneManager está listo
                        if (NetworkManager.Singleton.SceneManager != null)
                        {
                            Debug.Log("Cargando escena MainGame...");
                            NetworkManager.Singleton.SceneManager.LoadScene("MainGame", UnityEngine.SceneManagement.LoadSceneMode.Single);
                            this.enabled = false; // Desactivamos este Update para evitar doble carga
                        }
                        else
                        {
                            Debug.LogError("ERROR CRÍTICO: SceneManager es NULL en el NetworkManager.");
                        }
                    }
                }
            }
        }
    }

    // =======================================================
    // 1. EL HOST CREA LA SALA
    // =======================================================
    public async void CrearSalaPrivadaDesdeUI()
    {
        if (btnStartHost != null) btnStartHost.interactable = false;

        pToolBar.SetActive(false);
        bBack.gameObject.SetActive(false);


        int maxJugadores = MenuManager.Instance.privatePlayers.ObtenerIndice() + 2;
        int entryFee = int.Parse(MenuManager.Instance.privateEntryFee.opciones[MenuManager.Instance.privateEntryFee.ObtenerIndice()]);
        int prizeTotal = MenuManager.Instance.ObtenerPremioPrivadaCalculado();

        int difficulty = MenuManager.Instance.privateDifficulty.ObtenerIndice();

        string tiempoStr = MenuManager.Instance.privateTime.opciones[MenuManager.Instance.privateTime.ObtenerIndice()].Replace("s", "");
        int turnTime = int.Parse(tiempoStr);

        GameConfig.difficulty = difficulty;
        GameConfig.nPlayers = maxJugadores;

        string codigo = await SessionNetworkManager.Instance.CrearSalaPrivada(maxJugadores, entryFee, prizeTotal, difficulty, turnTime);

        if (!string.IsNullOrEmpty(codigo))
        {
            if (txtCodigoGeneradoHost != null) txtCodigoGeneradoHost.text = $"CODE: {codigo}";

            if (txtPrecioLobbyHost != null) txtPrecioLobbyHost.text = $"Fee: {entryFee} M";
            if (txtPremioLobbyHost != null) txtPremioLobbyHost.text = $"Prize: {prizeTotal} M";

            MenuManager.Instance.MostrarPrivateLobby();

            if (btnStartHost != null) btnStartHost.interactable = true;
        }
        else
        {
            Debug.LogError("Error en la UI: Falló la creación de la sala.");
            if (btnStartHost != null) btnStartHost.interactable = true;
            pToolBar.SetActive(true); // Restaura la barra si falla la creación
            bBack.gameObject.SetActive(true);
        }
    }
    // =======================================================
    // 2. CLIENTE FASE BÚSQUEDA (Botón Search)
    // =======================================================
    private async void OnSearchClicked()
    {
        string codigo = inputCodigoAmigo.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(codigo)) return;

        btnSearch.interactable = false;
        if (txtDetalleFee != null) txtDetalleFee.text = "Buscando...";

        var (feeEncontrado, premioEncontrado) = await SessionNetworkManager.Instance.PrevisualizarSalaExterna(codigo);

        if (feeEncontrado >= 0)
        {
            feeSalaActual = feeEncontrado;
            premioSalaActual = premioEncontrado;
            var session = SessionNetworkManager.Instance.currentSession;

            if (panelInfoPartida != null) panelInfoPartida.SetActive(true);

            // if (txtDetalleJugadores != null) txtDetalleJugadores.text = $"Jugadores: {session.Players.Count}/{session.MaxPlayers}";
            if (txtDetalleFee != null) txtDetalleFee.text = $"Entrada: {feeSalaActual} M";
            if (txtDetallePrize != null) txtDetallePrize.text = $"Premio: {premioSalaActual} M"; // Pintamos el premio extraído de la nube

            if (TopBarUI.Instance.TieneSuficientes(feeSalaActual))
            {
                btnSearch.gameObject.SetActive(false);
                btnJoin.gameObject.SetActive(true);
                btnJoin.interactable = true;
            }
            else
            {
                if (txtDetalleFee != null) txtDetalleFee.text = "<color=red>¡DINERO INSUFICIENTE!</color>";
                btnSearch.interactable = true;
                SessionNetworkManager.Instance.AbandonarSala();
            }
        }
        else
        {
            btnSearch.interactable = true;
            if (panelInfoPartida != null) panelInfoPartida.SetActive(false);
            inputCodigoAmigo.text = "ERROR";
        }
    }


    // =======================================================
    // 3. CLIENTE FASE UNIÓN (Botón Join)
    // =======================================================
    private async void OnJoinConfirmed()
    {
        btnJoin.interactable = false;
        bBack.gameObject.SetActive(false);

        
        string codigo = inputCodigoAmigo.text.Trim().ToUpper();

        
        bool exito = await SessionNetworkManager.Instance.RealizarUnionDefinitiva(codigo);

        if (exito)
        {
            MenuManager.Instance.MostrarClientLobby();

            if (txtPrecioLobbyCliente != null) txtPrecioLobbyCliente.text = $"Fee: {feeSalaActual} M";
            if (txtPremioLobbyCliente != null) txtPremioLobbyCliente.text = $"Prize: {premioSalaActual} M";

            ResetearBuscador();
        }
        else
        {
            btnJoin.interactable = true;
            bBack.gameObject.SetActive(true);
            Debug.LogError("Error al intentar unirse definitivamente a la sala.");
        }
    }

    // =======================================================
    // 4. LIMPIEZA Y ABANDONO
    // =======================================================
    public void AbandonarLobby()
    {
        SessionNetworkManager.Instance.AbandonarSala();
        MenuManager.Instance.MostrarHub();
        ResetearBuscador();
        pToolBar.SetActive(true);
        bBack.gameObject.SetActive(true);

        if (txtListaNombresHost != null) txtListaNombresHost.text = "";
        if (txtListaNombresCliente != null) txtListaNombresCliente.text = "";
    }

    public void ResetearBuscador()
    {
        btnSearch.gameObject.SetActive(true);
        btnSearch.interactable = true;
        btnJoin.gameObject.SetActive(false);
        if (panelInfoPartida != null) panelInfoPartida.SetActive(false);
        inputCodigoAmigo.text = "";
    }

    public void ResetearLobbyCompleto()
    {
        ResetearBuscador();

        if (bBack != null) bBack.gameObject.SetActive(true);
        if (txtListaNombresHost != null) txtListaNombresHost.text = "";
        if (txtListaNombresCliente != null) txtListaNombresCliente.text = "";

        if (MenuManager.Instance != null)
        {
            if (MenuManager.Instance.privatePlayers != null) MenuManager.Instance.privatePlayers.ResetearComponente();
            if (MenuManager.Instance.privateEntryFee != null) MenuManager.Instance.privateEntryFee.ResetearComponente();
            if (MenuManager.Instance.privateDifficulty != null) MenuManager.Instance.privateDifficulty.ResetearComponente();
            if (MenuManager.Instance.privateTime != null) MenuManager.Instance.privateTime.ResetearComponente();

            MenuManager.Instance.CalcularPremioPrivada();
        }
    }
}