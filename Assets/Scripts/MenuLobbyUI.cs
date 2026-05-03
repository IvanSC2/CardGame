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
        
        btnStartHost.onClick.AddListener(() => 
        {
            Debug.Log($"[DEBUG] NetworkManager: {NetworkManager.Singleton != null}");
            Debug.Log($"[DEBUG] IsServer: {NetworkManager.Singleton.IsServer}");
            Debug.Log($"[DEBUG] NetworkConfig EnableSceneManagement: {NetworkManager.Singleton.NetworkConfig.EnableSceneManagement}");
            Debug.Log($"[DEBUG] SceneManager: {NetworkManager.Singleton.SceneManager != null}");

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
    }

    private void Update()
    {
        if (SessionNetworkManager.Instance != null && SessionNetworkManager.Instance.currentSession != null)
        {
            var session = SessionNetworkManager.Instance.currentSession;
            int conectados = session.Players.Count;
            int maxJugadores = session.MaxPlayers;

            string textoContador = $"{conectados}/{maxJugadores}";
            if (txtJugadoresHost != null) txtJugadoresHost.text = textoContador;
            if (txtJugadoresCliente != null) txtJugadoresCliente.text = textoContador;

            string listaGenerada = "";
            for (int i = 0; i < conectados; i++)
            {
                if (i == 0) listaGenerada += "TÚ (Host)\n";
                else listaGenerada += $"JUGADOR {i}\n";
            }

            if (txtListaNombresHost != null) txtListaNombresHost.text = listaGenerada;
            if (txtListaNombresCliente != null) txtListaNombresCliente.text = listaGenerada;
        }
    }

    // =======================================================
    // 1. EL HOST CREA LA SALA
    // =======================================================
    public async void CrearSalaPrivadaDesdeUI()
    {
        // 1. Bloqueamos el botón Start por seguridad (como hicimos en el parche anterior)
        if (btnStartHost != null) btnStartHost.interactable = false;
        
        pToolBar.SetActive(false);

        // 2. Capturamos TODO de los selectores del MenuManager
        int maxJugadores = MenuManager.Instance.privatePlayers.ObtenerIndice() + 2;
        int entryFee = int.Parse(MenuManager.Instance.privateEntryFee.opciones[MenuManager.Instance.privateEntryFee.ObtenerIndice()]);
        int prizeTotal = MenuManager.Instance.ObtenerPremioPrivadaCalculado();
        
        int difficulty = MenuManager.Instance.privateDifficulty.ObtenerIndice();
        
        // Convertimos el tiempo (ej: "30s" -> 30)
        string tiempoStr = MenuManager.Instance.privateTime.opciones[MenuManager.Instance.privateTime.ObtenerIndice()].Replace("s", "");
        int turnTime = int.Parse(tiempoStr);

        // Guardamos en el GameConfig local (por si la IA o la UI lo necesita pronto)
        GameConfig.difficulty = difficulty;
        GameConfig.nPlayers = maxJugadores;

        // 3. Mandamos a la nube los 5 parametros
        string codigo = await SessionNetworkManager.Instance.CrearSalaPrivada(maxJugadores, entryFee, prizeTotal, difficulty, turnTime);

        // 4. Si la sala se crea bien, actualizamos la interfaz
        if (!string.IsNullOrEmpty(codigo))
        {
            if (txtCodigoGeneradoHost != null) txtCodigoGeneradoHost.text = $"CODE: {codigo}";
            
            // Llevamos al jugador a la sala de espera
            MenuManager.Instance.MostrarPrivateLobby();
            
            // Volvemos a encender el botón Start
            if (btnStartHost != null) btnStartHost.interactable = true;
        }
        else
        {
            Debug.LogError("Error en la UI: Falló la creación de la sala.");
            if (btnStartHost != null) btnStartHost.interactable = true;
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

        // EXTRAEMOS EL CÓDIGO DEL CAMPO DE TEXTO
        string codigo = inputCodigoAmigo.text.Trim().ToUpper();

        // LE PASAMOS EL CÓDIGO A LA FUNCIÓN DE UNIÓN
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
}