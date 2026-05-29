using UnityEngine;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;
using UnityEngine.UI;

public class SessionNetworkManager : MonoBehaviour
{
    public static SessionNetworkManager Instance;

    public ISession currentSession;
    private CancellationTokenSource matchmakingCts;
    public Button bLeave;

    // Evita que OnSessionDeleted y OnHostChanged se ejecuten a la vez
    private bool _expulsandoAlHub = false;

    private async void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        LoadingManager.Instance?.MostrarCargando("Conectando con el servidor...");

        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                Debug.Log("[1/3] Inicializando UGS...");
                InitializationOptions options = new InitializationOptions();
                options.SetEnvironmentName("production");
#if UNITY_EDITOR
                options.SetProfile("Jugador_" + System.Guid.NewGuid().ToString().Substring(0, 6));
#endif
                await UnityServices.InitializeAsync(options);
                Debug.Log("[2/3] UGS Inicializado correctamente.");
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[3/3] Autenticado en la nube. PlayerID: {AuthenticationService.Instance.PlayerId}");

                if (TopBarUI.Instance != null) await TopBarUI.Instance.CargarEconomiaNube();
                if (ProfileManager.Instance != null) 
                {
                    await ProfileManager.Instance.CargarPerfilCompleto();

                    // Subir a la nube cualquier partida jugada sin internet
                    await ProfileManager.Instance.SubirPendientesALaNube();

                    // Mostramos el panel de bienvenida SOLO si estamos en el menú y no tiene nombre
                    if (!ProfileManager.Instance.TieneNickname() && MenuManager.Instance != null && MenuManager.Instance.welcomePanel != null)
                    {
                        MenuManager.Instance.welcomePanel.SetActive(true);
                    }
                }
                await SincronizarNoAds();

                // ANALÍTICAS: Iniciar recolección tras autenticar
                if (AnalyticsManager.Instance != null) AnalyticsManager.Instance.IniciarRecoleccion();

                // TEST A/B: Descargar configuración remota (estrategia de anuncios)
                if (RemoteConfigManager.Instance != null) await RemoteConfigManager.Instance.FetchConfigs();
            }
        }
        catch (System.Exception e) { Debug.LogError($"Error grave al arrancar UGS: {e.Message}"); }
        finally
        {
            LoadingManager.Instance?.OcultarCargando();
        }
    }

    // =========================================================================
    // 1. HOST: CREAR LA SALA PRIVADA
    // =========================================================================
    public async Task<string> CrearSalaPrivada(int maxPlayers, int entryFee, int prizeTotal, int difficulty, int turnTime)
    {
        if (currentSession != null)
        {
            Debug.LogWarning("⚠️ Se ha detectado una sesión previa sin cerrar. Destruyéndola antes de crear una nueva...");
            await AbandonarSala();
            await Task.Delay(500);
        }

        try
        {
            var sessionOptions = new SessionOptions
            {
                MaxPlayers = maxPlayers,
                IsPrivate = true
            }.WithRelayNetwork();

            string roomName = System.Guid.NewGuid().ToString();
            currentSession = await MultiplayerService.Instance.CreateOrJoinSessionAsync(roomName, sessionOptions);

            string codigoGenerado = currentSession.Code;

            
            // Desuscripción defensiva: evita listeners zombie si se reutiliza el método
            currentSession.Deleted -= OnSessionDeleted;
            currentSession.SessionHostChanged -= OnHostChanged;
            currentSession.Deleted += OnSessionDeleted;
            currentSession.SessionHostChanged += OnHostChanged;

            if (currentSession.IsHost)
            {
                string jsonPayload = $"{{\"joinCode\":\"{codigoGenerado}\", \"price\":{entryFee}, \"prize\":{prizeTotal}}}";
                var argumentos = new Dictionary<string, object> { { "payload", jsonPayload } };

                try
                {
                    await CloudCodeService.Instance.CallEndpointAsync("PublishLobbyPreview", argumentos);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[CLOUD-ERROR] Falló la ejecución: {ex.Message}");
                }

                if (!NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.StartHost();
                }
            }

            return codigoGenerado;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error general al crear la sala: {e.Message}");
            if (MenuManager.Instance != null)
                MenuManager.Instance.MostrarPopupInfo("No se pudo crear la sala. Comprueba tu conexión a internet e inténtalo de nuevo.");
            return null;
        }
    }

    // =========================================================================
    // 2. CLIENTE (SEARCH): PREVISUALIZAR SIN ENSUCIAR EL LOBBY
    // =========================================================================
    [System.Serializable]
    public class LobbyMetadata { public int entryPrice; public int totalPrize; public long creationTimestamp; }

    public async Task<(int fee, int prize)> PrevisualizarSalaExterna(string joinCode)
    {
        try
        {
            string cleanCode = joinCode.Trim().ToUpper();
            var queryResult = await CloudSaveService.Instance.Data.Custom.LoadAllAsync(cleanCode);

            if (queryResult.TryGetValue("lobby_metadata", out var metaItem))
            {
                var settings = metaItem.Value.GetAs<LobbyMetadata>();
                return (settings.entryPrice, settings.totalPrize);
            }
            return (-1, -1);
        }
        catch (System.Exception e) { Debug.LogError($"Error leyendo nube: {e.Message}"); return (-1, -1); }
    }

    // =========================================================================
    // 3. CLIENTE (JOIN): CONFIRMAR Y ARRANCAR MOTOR NGO
    // =========================================================================
    public async Task<bool> RealizarUnionDefinitiva(string joinCode)
    {
        try
        {
            var joinOptions = new JoinSessionOptions();
            joinOptions.WithNetworkOptions(new NetworkOptions());

            currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode, joinOptions);

            if (currentSession != null)
            {
                // Desuscripción defensiva: evita listeners zombie
                currentSession.Deleted -= OnSessionDeleted;
                currentSession.SessionHostChanged -= OnHostChanged;
                currentSession.Deleted += OnSessionDeleted;
                currentSession.SessionHostChanged += OnHostChanged;

                if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.StartClient();
                }
                return true;
            }
            return false;
        }
        catch (SessionException e)
        {
            Debug.LogError($"Error al unirse definitivamente: {e.Message}");
            if (MenuManager.Instance != null)
                MenuManager.Instance.MostrarPopupInfo("No se pudo unir a la sala. Es posible que la sala esté llena o haya cerrado.");
            return false;
        }
    }

    // =========================================================================
    // 5. MATCHMAKING PÚBLICO (BLINDADO CONTRA ERROR 404)
    // =========================================================================
    public async Task IniciarMatchmakingPublico(int feeFijo)
    {
        int premioTotal = feeFijo * 4;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[RED] Limpiando puerto de red zombi antes de buscar...");
            NetworkManager.Singleton.Shutdown();
            await Task.Delay(500); 
        }

        matchmakingCts?.Cancel();
        matchmakingCts = new CancellationTokenSource(System.TimeSpan.FromSeconds(60));

        MenuLobbyUI lobbyUI = Object.FindFirstObjectByType<MenuLobbyUI>();

        try
        {
            Debug.Log("[MATCHMAKING] Iniciando emparejamiento por Tickets en la nube...");

            // ANALÍTICAS: Evento matchmaking_started
            if (AnalyticsManager.Instance != null) AnalyticsManager.Instance.EventoMatchmakingStarted("public");
            
            if (lobbyUI != null && lobbyUI.btnLeaveMatchmaking != null)
                lobbyUI.btnLeaveMatchmaking.gameObject.SetActive(true);

            var matchmakerOptions = new MatchmakerOptions { QueueName = "PublicQueue" };

            // Enviamos los trofeos del jugador como atributo de ticket para emparejamiento por rango
            int misTrofeos = (TopBarUI.Instance != null) ? TopBarUI.Instance.GetTrofeos() : 100;
            matchmakerOptions.TicketAttributes = new Dictionary<string, object>
            {
                { "trophies", (double)misTrofeos }
            };

            var sessionOptions = new SessionOptions
            {
                MaxPlayers = 6, // Soporte para hasta 6 jugadores (relajaciones en UGS Dashboard)
                IsPrivate = false
            }.WithRelayNetwork(); 

            // EJECUCIÓN
            Debug.Log(">>> [TRAZA 1] Botón pulsado. Voy a pedirle el Matchmaking a la nube...");
            currentSession = await MultiplayerService.Instance.MatchmakeSessionAsync(
                matchmakerOptions, 
                sessionOptions, 
                matchmakingCts.Token
            );
            Debug.Log(">>> [TRAZA 2] La nube ha respondido y currentSession existe.");

            if (lobbyUI != null && lobbyUI.btnLeaveMatchmaking != null)
                lobbyUI.btnLeaveMatchmaking.gameObject.SetActive(false);

            // Desuscripción defensiva: evita listeners zombie
            currentSession.Deleted -= OnSessionDeleted;
            currentSession.SessionHostChanged -= OnHostChanged;
            currentSession.Deleted += OnSessionDeleted;
            currentSession.SessionHostChanged += OnHostChanged;

            if (currentSession.IsHost)
            {
                string jsonPayload = $"{{\"joinCode\":\"{currentSession.Code}\", \"price\":{feeFijo}, \"prize\":{premioTotal}}}";
                var argumentos = new Dictionary<string, object> { { "payload", jsonPayload } };
                try { await CloudCodeService.Instance.CallEndpointAsync("PublishLobbyPreview", argumentos); } catch(System.Exception e) {Debug.LogError($">>> [TRAZA FATAL] El código ha petado aquí. Motivo: {e.Message}"); }
            }
            
            // MONETIZACIÓN: Configuramos la partida (Pública)
            GameConfig.currentFee = feeFijo;
            GameConfig.currentPrize = feeFijo * currentSession.Players.Count;
            GameConfig.isPrivateMatch = false;
            GameConfig.isHostLobby = currentSession.IsHost;
            GameConfig.prizeAwarded = false;

            Debug.Log(">>> [TRAZA 3] Rol asignado. Voy a llamar a MenuManager.IniciarFlujoPublico()...");
            MenuManager.Instance.IniciarFlujoPublico();
            
        }
       
        
        catch (Unity.Services.Core.RequestFailedException e)
        {
            if (e.Message.Contains("404") || e.Message.Contains("EntityNotFound"))
            {
                Debug.LogWarning("[MATCHMAKING RED] UGS devolvió un Ticket Huérfano (404). Purgando estado...");
                currentSession = null;
                if (MenuManager.Instance != null) MenuManager.Instance.MostrarHub();
            }
            else
            {
                Debug.LogError($"[MATCHMAKING-ERROR] Fallo crítico Multiplayer: {e.Message}");
                await AbandonarSala(true);
                if (MenuManager.Instance != null)
                {
                    MenuManager.Instance.MostrarHub();
                    MenuManager.Instance.MostrarPopupInfo("Se ha perdido la conexión con el servidor de matchmaking. Vuelves al menú principal.");
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            Debug.LogWarning("[MATCHMAKING] Búsqueda cancelada por el usuario o tiempo agotado.");
            if (MenuManager.Instance != null) MenuManager.Instance.MostrarHub();
        }
        catch (SessionException e)
        {
           
            bool esTicketHuerfano = e.Message.Contains("404")
                                 || e.Message.Contains("EntityNotFound")
                                 || e.Message.Contains("fetching Matchmaking Results")
                                 || e.Message.Contains("fetching matchmaking");

            if (esTicketHuerfano)
            {
                Debug.LogWarning("[MATCHMAKING] Ticket huérfano detectado (SessionException 404). Purgando y reintentando en 2s...");

                // Limpieza de estado sin tocar red (la sesión ya está muerta en UGS)
                currentSession = null;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    NetworkManager.Singleton.Shutdown();

                // Esperamos a que los sockets se liberen y reintentamos automáticamente
                await Task.Delay(2000);

                // Comprobamos que el jugador sigue en el menú (no canceló mientras esperábamos)
                if (MenuManager.Instance != null && matchmakingCts != null && !matchmakingCts.IsCancellationRequested)
                {
                    Debug.Log("[MATCHMAKING] Reintentando matchmaking automáticamente...");
                    await IniciarMatchmakingPublico(feeFijo);   // Reintento único
                }
                else
                {
                    if (MenuManager.Instance != null) MenuManager.Instance.MostrarHub();
                }
            }
            else
            {
                // Error real de sesión — no reintentamos
                Debug.LogError($"[MATCHMAKING-ERROR] Fallo crítico Session: {e.Message}");
                await AbandonarSala(true);
                if (MenuManager.Instance != null)
                {
                    MenuManager.Instance.MostrarHub();
                    if (!abandonandoVoluntariamente && (matchmakingCts == null || !matchmakingCts.IsCancellationRequested))
                    {
                        MenuManager.Instance.MostrarPopupInfo("Error inesperado en la sesión. Vuelves al menú principal.");
                    }
                }
            }
        }
    }

    private async Task SincronizarNoAds()
    {
        var query = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { "NoAdsOwned" });
        if (query.TryGetValue("NoAdsOwned", out var item))
        {
            bool tieneNoAds = item.Value.GetAs<bool>();
            PlayerPrefs.SetInt("NoAds", tieneNoAds ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // --- EVENTOS DE SEGURIDAD ---
    private void OnSessionDeleted()
    {
        // Disparamos la Task de forma segura desde un evento síncrono
        _ = ExpulsarAlHubPorCaidaHostAsync();
    }

    private void OnHostChanged(string newHostId)
    {
        _ = ExpulsarAlHubPorCaidaHostAsync();
    }

    
    public async Task ExpulsarAlHubPorCaidaHostAsync()
    {
        // Si ya estamos ejecutando esta rutina, ignoramos la llamada duplicada
        if (_expulsandoAlHub) return;
        _expulsandoAlHub = true;

        LoadingManager.Instance?.MostrarCargando("Volviendo al menú...");

        try
        {
            Time.timeScale = 1f;

            // ANALÍTICAS: Evento host_abandoned (el host nos ha echado)
            if (AnalyticsManager.Instance != null) 
                AnalyticsManager.Instance.EventoHostAbandoned(GameConfig.currentMatchMode);

            // MONETIZACIÓN: Reembolsos por culpa del Host
            // Si esto se llama, significa que el Servidor se ha ido y somos un cliente (o fuimos expulsados).
            // Si el fee ya fue deducido (la partida arrancó o estamos en lobby) y no se dio premio aún:
            if (GameConfig.currentFee > 0 && !GameConfig.prizeAwarded)
            {
                if (GameConfig.gameStarted)
                {
                    // Calcular cuántos clientes siguen vivos
                    int clientesVivos = 1; // Mínimo 1 (tú mismo)
                    
                    if (InteractionManager.Instance != null && InteractionManager.Instance.vidas != null)
                    {
                        clientesVivos = 0;
                        for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
                        {
                            ulong cid = InteractionManager.Instance.GetClientIdForSeat(i);
                            // Si es humano (tiene cid), NO es el Host (cid != 0), y sigue vivo
                            if (cid != ulong.MaxValue && cid != 0 && InteractionManager.Instance.vidas[i] > 0)
                            {
                                clientesVivos++;
                            }
                        }
                        if (clientesVivos < 1) clientesVivos = 1; // Failsafe
                    }

                    if (GameConfig.isPrivateMatch)
                    {
                        // El host siempre pierde fee×2. Ese pool se reparte entre supervivientes por igual.
                        int poolTotal = 2 * GameConfig.currentFee;
                        int recompensa = poolTotal / clientesVivos;
                        
                        TopBarUI.Instance.ActualizarMonedas(recompensa);

                        if (ProfileManager.Instance != null)
                        {
                            List<string> nombres = new List<string>();
                            if (InteractionManager.Instance != null)
                            {
                                for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
                                    nombres.Add(InteractionManager.Instance.GetPlayerName(i));
                            }

                            ProfileManager.Instance.RegistrarResultadoPartida(
                                GameConfig.currentMatchMode,
                                clientesVivos,
                                GameConfig.nPlayers,
                                recompensa - GameConfig.currentFee,
                                nombres,
                                GameConfig.difficulty,
                                "Interrumpida"
                            );
                        }
                    }
                    else
                    {
                        // Pública: Devolvemos fee íntegro + gestionamos trofeos del host
                        // Usamos QueuePendingDelta (seguro desde escena de juego sin TopBarUI)
                        TopBarUI.QueuePendingDelta(0, GameConfig.currentFee); // Reembolso del fee

                        // --- TROFEOS: El host recibe la penalización de su puesto actual ---
                        if (!GameConfig.trophyAwarded && GameConfig.currentMatchMode == "public")
                        {
                            int totalPlayers = InteractionManager.Instance != null ? InteractionManager.Instance.totalPlayers : GameConfig.nPlayers;
                            int puestoHost = Mathf.Max(1, clientesVivos);
                            
                            // El host recibe la penalización de perder según el puesto del abandono
                            int trofeosPerdidosHost = GameConfig.CalcularTrofeosPerdidos(puestoHost, totalPlayers);
                            if (trofeosPerdidosHost == 0) trofeosPerdidosHost = 20; // Castigo mínimo por ragequit del host

                            int boteTotal = GameConfig.trophyBote + trofeosPerdidosHost;
                            int supervivientesReales = Mathf.Max(1, clientesVivos);
                            int trofeosPorSuperviviente = boteTotal / supervivientesReales;

                            // TROFEOS GARANTIZADOS: El cliente sobrevive y el host se rinde.
                            // Le damos como mínimo los trofeos del peor puesto que tenían asegurado.
                            int misTrofeosGarantizados = 0;
                            if (clientesVivos == 1)
                            {
                                // Iba a ganar seguro (era el único superviviente)
                                misTrofeosGarantizados = 30;
                            }
                            else if (clientesVivos == 2)
                            {
                                // Iba a quedar 2º como mínimo
                                misTrofeosGarantizados = 10;
                            }

                            TopBarUI.QueuePendingDelta(trofeosPorSuperviviente + misTrofeosGarantizados);
                            GameConfig.trophyAwarded = true;
                            GameConfig.trophyBote = 0;

                            Debug.Log($"[TROFEOS SUMA-CERO] Host abandonó. Bote repartido: +{trofeosPorSuperviviente}. Rango Garantizado: +{misTrofeosGarantizados}. Total encolado: {trofeosPorSuperviviente + misTrofeosGarantizados}");
                        }

                        if (ProfileManager.Instance != null)
                        {
                            List<string> nombres = new List<string>();
                            if (InteractionManager.Instance != null)
                            {
                                for (int i = 0; i < InteractionManager.Instance.totalPlayers; i++)
                                    nombres.Add(InteractionManager.Instance.GetPlayerName(i));
                            }

                            ProfileManager.Instance.RegistrarResultadoPartida(
                                GameConfig.currentMatchMode,
                                clientesVivos,
                                GameConfig.nPlayers,
                                0, // No pierde ni gana dinero
                                nombres,
                                GameConfig.difficulty,
                                "Interrumpida"
                            );
                        }
                    }
                }
                else
                {
                    // El juego no había empezado (estábamos en el Lobby buscando o esperando).
                    // Simplemente devolvemos el dinero de la entrada sin registrar partida.
                    TopBarUI.Instance.ActualizarMonedas(GameConfig.currentFee);
                }

                GameConfig.prizeAwarded = true; // Para no devolver dos veces
            }

            await AbandonarSala(true);
            await Task.Delay(500);

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu")
            {
                // LoadingManager se ocultará cuando MenuManager y TopBarUI hayan cargado (TopBarUI lo apagará)
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
            else
            {
                if (MenuManager.Instance != null)
                {
                    MenuManager.Instance.MostrarHub();
                    if (!abandonandoVoluntariamente)
                    {
                        MenuManager.Instance.MostrarPopupInfo("La conexión con la sala se ha perdido. Has vuelto al menú principal.");
                    }
                }
                LoadingManager.Instance?.OcultarCargando();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RED] Error al expulsar al hub: {e.Message}");
        }
        finally
        {
            // Siempre liberamos el guard, aunque haya fallado
            _expulsandoAlHub = false;
        }
    }

    // Alias público por compatibilidad con cualquier llamada directa existente
    public void ExpulsarAlHubPorCaidaHost() => _ = ExpulsarAlHubPorCaidaHostAsync();
   
    // =========================================================================
    // 4. ABANDONAR / CANCELAR SALA (BLINDADO CONTRA WARNINGS)
    // =========================================================================
    public bool abandonandoVoluntariamente = false;

    public async Task AbandonarSala(bool sesionYaDestruidaExternamente = false)
    {
        abandonandoVoluntariamente = true;
        matchmakingCts?.Cancel();
        
        // Chivato para saber si UGS ya se encargó de apagar Netcode
        bool redApagadaPorUGS = false; 

        if (currentSession != null)
        {
            currentSession.Deleted -= OnSessionDeleted;
            currentSession.SessionHostChanged -= OnHostChanged;

            try
            {
                if (!sesionYaDestruidaExternamente)
                {
                    if (currentSession.IsHost)
                    {
                        // ESTO apaga el NetworkManager automáticamente por dentro
                        await currentSession.AsHost().DeleteAsync(); 
                    }
                    else
                    {
                        // ESTO apaga el NetworkManager automáticamente por dentro
                        await currentSession.LeaveAsync(); 
                    }
                    
                    // Si llegamos aquí sin errores, UGS ya ha apagado la red
                    redApagadaPorUGS = true; 
                }
            }
            catch (System.Exception e) { Debug.Log($"[RED] Limpieza local completada: {e.Message}"); }

            currentSession = null;
        }

       
        // SOLO apagamos a mano si no había sesión en la nube o si UGS falló
        if (!redApagadaPorUGS && NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }
        
        // Pequeña pausa para asegurar la limpieza antes de limpiar el flag
        await Task.Delay(100);
        abandonandoVoluntariamente = false;
    }

    private void OnDestroy()
    {
        matchmakingCts?.Cancel();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }
}