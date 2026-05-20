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
    private bool procesandoPeticionRed = false;
    private float tiempoEsperaArranque = 5f; // Los 5 segundos de cortesía

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
                var session = SessionNetworkManager.Instance?.currentSession;
                if (session != null)
                {
                    // FUNCIONALIDAD PRIVADA: Rellenar con bots.
                    // Si configuraste 6 pero sois 4, nPlayers = 6. 
                    // El InteractionManager creará 6 asientos y 2 de ellos (sin clientId) serán bots.
                    GameConfig.nPlayers = session.MaxPlayers;
                    int jugadoresConectadosUGS = session.Players.Count;
                    GameConfig.nHumanPlayers = jugadoresConectadosUGS;
                    
                    // MONETIZACIÓN: Configuramos la partida para que al cargar la escena se cobren las monedas
                    GameConfig.currentFee = int.Parse(MenuManager.Instance.privateEntryFee.opciones[MenuManager.Instance.privateEntryFee.ObtenerIndice()]);
                    GameConfig.currentPrize = MenuManager.Instance.ObtenerPremioPrivadaCalculado();
                    GameConfig.isPrivateMatch = true;
                    GameConfig.isHostLobby = true;
                    GameConfig.prizeAwarded = false;

                    this.enabled = false;
                    if (CardDatabase.deck != null) CardDatabase.deck.Clear();
                    
                    StartCoroutine(EsperarConexionesYCargar(jugadoresConectadosUGS));
                }
                else
                {
                    // Fallback de seguridad
                    if (CardDatabase.deck != null) CardDatabase.deck.Clear();
                    NetworkManager.Singleton.SceneManager.LoadScene("MainGame", UnityEngine.SceneManagement.LoadSceneMode.Single);
                }
            }
        });
        
        if (btnLeaveMatchmaking != null) btnLeaveMatchmaking.onClick.AddListener(AbandonarLobby);
    }

    private void OnEnable()
    {
        // El timer siempre empieza en 5f. El reset lo garantiza OnDisable.
        tiempoEsperaArranque = 5f;

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
        // Resetear el timer aquí garantiza que siempre empiece limpio en el próximo OnEnable,
        // independientemente de cómo terminó la sesión anterior (abandono, error, timeout...).
        tiempoEsperaArranque = 5f;
    }

    // El Callback Oficial del Host para cambiar de escena
    private void EvaluacionCondicionTransicion(ulong clientId)
    {
        // Solo el Host tiene autoridad para transicionar la escena
        if (!NetworkManager.Singleton.IsHost) return;

        if (SessionNetworkManager.Instance != null && SessionNetworkManager.Instance.currentSession != null)
        {
            var session = SessionNetworkManager.Instance.currentSession;

            // Solo aplica en matchmaking público
            if (!session.IsPrivate)
            {
                int conexionesFisicas = NetworkManager.Singleton.ConnectedClientsList.Count;
                Debug.Log($"[MATCHMAKING] Nodo {clientId} validado en red. Ocupación física: {conexionesFisicas}/{session.MaxPlayers}");

                bool salaLlena = (conexionesFisicas >= session.MaxPlayers);
                if (salaLlena)
                {
                    Debug.Log("[MATCHMAKING] Sala NGO llena. Preparando viaje a MainGame...");
                    this.enabled = false; // Paramos el Update
                    // BUG-FIX: fijar el número de jugadores AQUÍ también,
                    // porque este path puede dispararse ANTES de que el timer llegue a 0
                    GameConfig.nPlayers = session.Players.Count;
                    GameConfig.nHumanPlayers = session.Players.Count;
                    if (CardDatabase.deck != null) CardDatabase.deck.Clear();
                    StartCoroutine(EsperarConexionesYCargar(conexionesFisicas));
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

    private int _lastConectadosNube = -1;
    private string _listaGeneradaCache = "";

  private void Update()
    {
        if (SessionNetworkManager.Instance == null || SessionNetworkManager.Instance.currentSession == null) return;
        if (MenuManager.Instance == null) return;
        var session = SessionNetworkManager.Instance.currentSession;
        int conectadosNube = session.Players.Count;
        int maxJugadores = session.MaxPlayers;

        if (conectadosNube != _lastConectadosNube)
        {
            _lastConectadosNube = conectadosNube;
            int premioActual = CalcularPremioDinamico(conectadosNube, maxJugadores);
            string textoPremio = $"{premioActual} $";

            if (txtPremioLobbyHost != null) txtPremioLobbyHost.text = textoPremio;
            if (txtPremioLobbyMatchmaking != null) txtPremioLobbyMatchmaking.text = textoPremio;
            if (txtPremioLobbyCliente != null) txtPremioLobbyCliente.text = textoPremio;
            if (txtDetallePrize != null) txtDetallePrize.text = textoPremio;

            string textoContador = session.IsPrivate ? $"{conectadosNube}/{maxJugadores}" : $"{conectadosNube}/{conectadosNube}";
            if (txtJugadoresHost != null) txtJugadoresHost.text = textoContador;
            if (txtJugadoresCliente != null) txtJugadoresCliente.text = textoContador;
            if (txtJugadoresMatchmaking != null) txtJugadoresMatchmaking.text = textoContador;

            string listaBase = "";
            for (int i = 0; i < conectadosNube; i++)
            {
                if (i == 0) listaBase += session.IsHost ? "TÚ (Host)\n" : "HOST\n";
                else listaBase += $"JUGADOR {i}\n";
            }
            // Guardamos la base de la lista. En matchmaking se le añadirá el timer cada frame.
            _listaGeneradaCache = listaBase;
            
            // Si es privada, ya podemos setear el texto final (porque no hay timer)
            if (session.IsPrivate)
            {
                if (txtListaNombresHost != null) txtListaNombresHost.text = _listaGeneradaCache;
                if (txtListaNombresCliente != null) txtListaNombresCliente.text = _listaGeneradaCache;
                if (txtListaNombresMatchmaking != null) txtListaNombresMatchmaking.text = _listaGeneradaCache;
            }
        }

        // --- LÓGICA DE AUTOSTART Y CUENTA ATRÁS (SOLO PÚBLICAS) ---
        if (!session.IsPrivate)
        {
            // Restamos tiempo en cada frame
            tiempoEsperaArranque -= Time.deltaTime;

            // Añadimos la cuenta atrás visualmente a la lista de jugadores
            string listaFinal = _listaGeneradaCache;
            if (tiempoEsperaArranque > 0)
            {
                listaFinal += $"\n<color=yellow>¡Partida encontrada!\nIniciando en {Mathf.CeilToInt(tiempoEsperaArranque)}...</color>";
            }
            else
            {
                listaFinal += "\n<color=green>¡Conectando...</color>";
            }

            // 3. ASIGNACIÓN A TODAS LAS LISTAS (Con el contador incluido)
            if (txtListaNombresMatchmaking != null) txtListaNombresMatchmaking.text = listaFinal;
            if (txtListaNombresHost != null) txtListaNombresHost.text = listaFinal;
            if (txtListaNombresCliente != null) txtListaNombresCliente.text = listaFinal;

            // 4. AutoStart (Solo el Host ejecuta la carga de escena)
            if (session.IsHost && NetworkManager.Singleton.IsServer)
            {
                int conectadosRed = NetworkManager.Singleton.ConnectedClientsList.Count;

                if (Time.frameCount % 120 == 0)
                {
                    Debug.Log($"[DIAGNÓSTICO] Nube: {conectadosNube}/{maxJugadores} | Red NGO: {conectadosRed}/{maxJugadores}");
                }

                if (tiempoEsperaArranque <= 0)
                {
                    // Desactivamos YA para no volver a entrar en este bloque
                    this.enabled = false;

                    
                    // (puede ser < maxJugadores si el timer expiró antes de que la sala llenara)
                    GameConfig.nPlayers = conectadosNube;
                    GameConfig.nHumanPlayers = conectadosNube;

                    // MONETIZACIÓN: Partida pública auto-start
                    GameConfig.currentFee = 200; // El feeMatchmaking hardcodeado en MenuManager
                    GameConfig.currentPrize = 200 * 4;
                    GameConfig.isPrivateMatch = false;
                    GameConfig.isHostLobby = true; // Somos el Host de esta pública
                    GameConfig.prizeAwarded = false;

                    if (CardDatabase.deck != null) CardDatabase.deck.Clear();

                    Debug.Log($"[MATCHMAKING] Timer expirado. Esperando handshake NGO para {conectadosNube} jugadores...");
                    StartCoroutine(EsperarConexionesYCargar(conectadosNube));
                }
            }
        }
    }

    // =======================================================
    // 1. EL HOST CREA LA SALA
    // =======================================================
    
    public void CrearSalaPrivadaDesdeUI() => _ = CrearSalaPrivadaDesdeUIAsync();

    private async System.Threading.Tasks.Task CrearSalaPrivadaDesdeUIAsync()
    {
        // 1. CANDADO LÓGICO: Si ya está creando una, ignoramos más clics
        if (procesandoPeticionRed) return; 
        // 2. EXTRAER COSTES Y VALIDAR ECONOMÍA ANTES DE NADA
        int entryFee = int.Parse(MenuManager.Instance.privateEntryFee.opciones[MenuManager.Instance.privateEntryFee.ObtenerIndice()]);

        if (TopBarUI.Instance != null && !TopBarUI.Instance.TieneSuficientes(entryFee))
        {
            Debug.LogWarning("<color=red>FONDOS INSUFICIENTES:</color> No puedes crear esta sala.");
            
           
            if (txtPrecioLobbyHost != null) txtPrecioLobbyHost.text = "<color=red>SIN DINERO</color>";
            
            return; // CORTAMOS EL CÓDIGO AQUÍ. No hay dinero, no hay sala.
        }
        procesandoPeticionRed = true;

        // 2. CANDADO VISUAL: Desactivamos el botón de crear
        if (bCreate != null) bCreate.interactable = false;
        if (btnStartHost != null) btnStartHost.interactable = false;
        
        Debug.Log("Lanzando petición de partida Privada a la nube...");
        
        pToolBar.SetActive(false);
        bBack.gameObject.SetActive(false);

        int maxJugadores = MenuManager.Instance.privatePlayers.ObtenerIndice() + 2;
        //int entryFee = int.Parse(MenuManager.Instance.privateEntryFee.opciones[MenuManager.Instance.privateEntryFee.ObtenerIndice()]);
        int prizeTotal = MenuManager.Instance.ObtenerPremioPrivadaCalculado();

        int difficulty = MenuManager.Instance.privateDifficulty.ObtenerIndice();

        string tiempoStr = MenuManager.Instance.privateTime.opciones[MenuManager.Instance.privateTime.ObtenerIndice()].Replace("s", "");
        int turnTime = int.Parse(tiempoStr);

        GameConfig.difficulty = difficulty;
        GameConfig.nPlayers = maxJugadores;

        // Llamada pesada a la nube
        string codigo = await SessionNetworkManager.Instance.CrearSalaPrivada(maxJugadores, entryFee, prizeTotal, difficulty, turnTime);

        if (!string.IsNullOrEmpty(codigo))
        {
            if (txtCodigoGeneradoHost != null) txtCodigoGeneradoHost.text = $"CODE: {codigo}";

            if (txtPrecioLobbyHost != null) txtPrecioLobbyHost.text = $"{entryFee}";
            if (txtPremioLobbyHost != null) txtPremioLobbyHost.text = $"{prizeTotal}";

            MenuManager.Instance.MostrarPrivateLobby();

            if (btnStartHost != null) btnStartHost.interactable = true;
        }
        else
        {
            Debug.LogError("Error en la UI: Falló la creación de la sala.");
            pToolBar.SetActive(true); 
            bBack.gameObject.SetActive(true);
        }

        // 3. LIBERAR CANDADOS
        if (bCreate != null) bCreate.interactable = true;
        procesandoPeticionRed = false;
    }
    // =======================================================
    // 2. CLIENTE FASE BÚSQUEDA (Botón Search)
    // =======================================================
    
    private void OnSearchClicked() => _ = OnSearchClickedAsync();

    private async System.Threading.Tasks.Task OnSearchClickedAsync()
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
            if (txtDetalleFee != null) txtDetalleFee.text = $"{feeSalaActual}";
            if (txtDetallePrize != null) txtDetallePrize.text = $"{premioSalaActual}"; // Pintamos el premio extraído de la nube

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
                await SessionNetworkManager.Instance.AbandonarSala();
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
    private void OnJoinConfirmed() => _ = OnJoinConfirmedAsync();

    private async System.Threading.Tasks.Task OnJoinConfirmedAsync()
    {
        btnJoin.interactable = false;
        bBack.gameObject.SetActive(false);

        
        string codigo = inputCodigoAmigo.text.Trim().ToUpper();

        
        bool exito = await SessionNetworkManager.Instance.RealizarUnionDefinitiva(codigo);

        if (exito)
        {
            MenuManager.Instance.MostrarClientLobby();

            if (txtPrecioLobbyCliente != null) txtPrecioLobbyCliente.text = $"{feeSalaActual}";
            if (txtPremioLobbyCliente != null) txtPremioLobbyCliente.text = $"{premioSalaActual}";

            // MONETIZACIÓN: Configuramos la partida para el CLIENTE
            GameConfig.currentFee = feeSalaActual;
            GameConfig.currentPrize = premioSalaActual;
            GameConfig.isPrivateMatch = true;
            GameConfig.isHostLobby = false;
            GameConfig.prizeAwarded = false;

            ResetearBuscador();
        }
        else
        {
            btnJoin.interactable = true;
            bBack.gameObject.SetActive(true);
            Debug.LogError("Error al intentar unirse definitivamente a la sala.");
        }
    }
    public int CalcularPremioDinamico(int conectados, int maximo)
{
    // 1. Obtenemos el Fee (Cuota) que configuró el Host
    // Asumimos que lo tienes guardado en GameConfig o lo leemos del selector
    string textoFee = MenuManager.Instance.privateEntryFee.opciones[MenuManager.Instance.privateEntryFee.ObtenerIndice()];
    int entryFee = int.Parse(textoFee);

    // 2. Cálculo base: Jugadores que han pagado la entrada
    int premioBase = conectados * entryFee;

    // 3. Cálculo de Bots: Cuántos huecos quedan
    int numBots = maximo - conectados;
    
    // 4. Bonus por dificultad (solo si hay bots)
    // Si la dificultad es "Alta" (índice 2), el bonus es mayor
    int bonusDificultadIA = MenuManager.Instance.privateDifficulty.ObtenerIndice() * 25; 
    int premioBots = numBots * bonusDificultadIA;

    return premioBase + premioBots;
}
    // =======================================================
    // 4. LIMPIEZA Y ABANDONO
    // =======================================================
    public async System.Threading.Tasks.Task AbandonarLobbyAsync() 
{
    if (SessionNetworkManager.Instance == null) return;

    try 
    {
        await SessionNetworkManager.Instance.AbandonarSala();
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[Red] Error crítico al abandonar sala: {e.Message}");
    }

    
    // Si durante el 'await' Unity destruyó este menú, abortamos la función aquí mismo.
    if (this == null) return; 

    if (MenuManager.Instance != null) MenuManager.Instance.MostrarHub();
    ResetearBuscador();
    
    if (pToolBar != null) pToolBar.SetActive(true);
    
    // Doble comprobación visual para evitar el MissingReferenceException
    if (bBack != null && bBack.gameObject != null) bBack.gameObject.SetActive(true);

    if (txtListaNombresHost != null) txtListaNombresHost.text = "";
    if (txtListaNombresCliente != null) txtListaNombresCliente.text = "";
}

    // Corrutina principal de transición: espera (máx 10s) a que todas las conexiones
    // NGO estén listas antes de cargar la escena. Soluciona la race condition donde
    // el timer o EvaluacionCondicionTransicion disparan LoadScene antes de que el
    // handshake NGO de todos los clientes esté completo → solo X de N jugadores jugaban.
    private System.Collections.IEnumerator EsperarConexionesYCargar(int jugadoresEsperados)
    {
        float timeout = 10f;
        Debug.Log($"[MATCHMAKING] Esperando handshake NGO de {jugadoresEsperados} jugadores (max 10s)...");

        while (NetworkManager.Singleton != null
               && NetworkManager.Singleton.ConnectedClientsList.Count < jugadoresEsperados
               && timeout > 0)
        {
            timeout -= Time.deltaTime;
            if (Time.frameCount % 60 == 0)
                Debug.Log($"[MATCHMAKING] NGO: {NetworkManager.Singleton.ConnectedClientsList.Count}/{jugadoresEsperados} | timeout: {timeout:F1}s");
            yield return null;
        }

        if (timeout <= 0)
            Debug.LogWarning($"[MATCHMAKING] Timeout esperando conexiones. Arrancando con {NetworkManager.Singleton?.ConnectedClientsList.Count}/{jugadoresEsperados} jugadores.");
        else
            Debug.Log($"[MATCHMAKING] Handshake completo ({jugadoresEsperados} jugadores). Lanzando MainGame.");

        if (NetworkManager.Singleton?.SceneManager != null)
            NetworkManager.Singleton.SceneManager.LoadScene("MainGame", UnityEngine.SceneManagement.LoadSceneMode.Single);
        else
            StartCoroutine(ReintentarCargaEscena());
    }

    // Corrutina de reintento: si SceneManager era null, esperamos un frame y reintentamos.
    private System.Collections.IEnumerator ReintentarCargaEscena()
    {
        yield return null;
        if (NetworkManager.Singleton?.SceneManager != null)
        {
            Debug.Log("[MATCHMAKING] Reintentando carga de escena tras esperar un frame...");
            NetworkManager.Singleton.SceneManager.LoadScene("MainGame", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("[MATCHMAKING] SceneManager sigue siendo null. Abortando partida pública.");
            if (SessionNetworkManager.Instance != null)
                _ = SessionNetworkManager.Instance.AbandonarSala();
            if (MenuManager.Instance != null) MenuManager.Instance.MostrarHub();
        }
    }

public void AbandonarLobby()
{
    tiempoEsperaArranque = 5f; // Reset explícito para que el próximo matchmaking empiece limpio
    _ = AbandonarLobbyAsync();
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