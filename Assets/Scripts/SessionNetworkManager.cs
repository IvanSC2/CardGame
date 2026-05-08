using UnityEngine;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Services.CloudCode;
using Unity.Services.CloudSave;

public class SessionNetworkManager : MonoBehaviour
{
    public static SessionNetworkManager Instance;

    public ISession currentSession;

    private async void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                Debug.Log("[1/3] Inicializando UGS...");
                InitializationOptions options = new InitializationOptions();
                options.SetProfile("Jugador_" + System.Guid.NewGuid().ToString().Substring(0, 6));

                await UnityServices.InitializeAsync(options);
                Debug.Log("[2/3] UGS Inicializado correctamente.");
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[3/3] Autenticado en la nube. PlayerID: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (System.Exception e) { Debug.LogError($"Error grave al arrancar UGS: {e.Message}"); }
    }

    // =========================================================================
    // 1. HOST: CREAR LA SALA PRIVADA Y PUBLICARLA EN CLOUD SAVE
    // =========================================================================
    public async Task<string> CrearSalaPrivada(int maxPlayers, int entryFee, int prizeTotal, int difficulty, int turnTime)
    {
        try
        {
            var sessionOptions = new SessionOptions
            {
                MaxPlayers = maxPlayers,
                IsPrivate = true
            };
            sessionOptions.WithNetworkOptions(new NetworkOptions());

            string roomName = System.Guid.NewGuid().ToString();
            currentSession = await MultiplayerService.Instance.CreateOrJoinSessionAsync(roomName, sessionOptions);

            string codigoGenerado = currentSession.Code;
            if (currentSession.IsHost)
            {
                string jsonPayload = $"{{\"joinCode\":\"{codigoGenerado}\", \"price\":{entryFee}, \"prize\":{prizeTotal}}}";

                var argumentos = new Dictionary<string, object>
                {
                    { "payload", jsonPayload }
                };

                try
                {
                    Debug.Log($"[CLOUD] Enviando paquete Trojan Horse para la sala: {codigoGenerado}");
                    await CloudCodeService.Instance.CallEndpointAsync("PublishLobbyPreview", argumentos);
                    Debug.Log($"[CLOUD] ¡Éxito! Script JS ejecutado y barrera superada.");
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
            Debug.Log($"[CLOUD] Buscando sala: '{cleanCode}'");

            var queryResult = await CloudSaveService.Instance.Data.Custom.LoadAllAsync(cleanCode);

            if (queryResult.TryGetValue("lobby_metadata", out var metaItem))
            {
                var settings = metaItem.Value.GetAs<LobbyMetadata>();
                Debug.Log($"[CLOUD] ¡Éxito! Precio: {settings.entryPrice}, Premio: {settings.totalPrize}");
                return (settings.entryPrice, settings.totalPrize);
            }

            Debug.LogWarning($"[CLOUD] La sala no tiene metadatos.");
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
            Debug.Log($"[JOIN] Iniciando unión definitiva a la sesión: {joinCode}");
            var joinOptions = new JoinSessionOptions();
            joinOptions.WithNetworkOptions(new NetworkOptions());

            currentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode, joinOptions);

            if (currentSession != null)
            {
                currentSession.Deleted += OnSessionDeleted;
                currentSession.SessionHostChanged += OnHostChanged;

                if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
                {
                    Debug.Log("[JOIN] Confirmado: Arrancando el motor NGO en modo Cliente...");
                    NetworkManager.Singleton.StartClient();
                }
                
                return true;
            }
            return false;
        }
        catch (SessionException e)
        {
            Debug.LogError($"Error al unirse definitivamente: {e.Message}");
            return false;
        }
    }

    // =========================================================================
    // 5. MATCHMAKING PÚBLICO (BUSCA O CREA 100% AUTOMATIZADO UNITY 6.2)
    // =========================================================================
    public async Task IniciarMatchmakingPublico(int feeFijo)
    {
        int premioTotal = feeFijo * 4;

        try
        {
            Debug.Log("[MATCHMAKING] Iniciando emparejamiento automatizado...");

            var quickJoinOptions = new QuickJoinOptions
            {
                Filters = new List<FilterOption>
                {
                    new FilterOption(FilterField.AvailableSlots, "1", FilterOperation.GreaterOrEqual)
                },
                Timeout = System.TimeSpan.FromSeconds(10),
                CreateSession = true 
            };

          
            var sessionOptions = new SessionOptions
            {
                MaxPlayers = 4,
                IsPrivate = false
            }.WithRelayNetwork(); 

            // Ejecutamos la llamada unificada
            currentSession = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoinOptions, sessionOptions);

            currentSession.Deleted += OnSessionDeleted;
            currentSession.SessionHostChanged += OnHostChanged;

            if (currentSession.IsHost)
            {
                Debug.Log("[MATCHMAKING] Eres Host. Red levantada automáticamente por el SDK.");
                
                string jsonPayload = $"{{\"joinCode\":\"{currentSession.Code}\", \"price\":{feeFijo}, \"prize\":{premioTotal}}}";
                var argumentos = new Dictionary<string, object> { { "payload", jsonPayload } };
                try { await CloudCodeService.Instance.CallEndpointAsync("PublishLobbyPreview", argumentos); } catch { }
            }
            else
            {
                Debug.Log("[MATCHMAKING] Eres Cliente. Conexión de red gestionada por el SDK.");
            }

            // Mostramos la interfaz (El cliente ya se está conectando por detrás)
            MenuManager.Instance.IniciarFlujoPublico();
        }
        catch (SessionException e)
        {
            Debug.LogError($"[MATCHMAKING-ERROR] Error crítico: {e.Message}");
        }
    }

    // --- EVENTOS DE SEGURIDAD ---

    private void OnSessionDeleted()
    {
        Debug.LogWarning("[SEGURIDAD] La sesión ha sido destruida por el Host. Saliendo...");
        ExpulsarAlHubPorCaidaHost();
    }

    private void OnHostChanged(string newHostId)
    {
        Debug.LogWarning("[SEGURIDAD] El Host original se ha desconectado en la fase de espera. Saliendo...");
        ExpulsarAlHubPorCaidaHost();
    }

    private void ExpulsarAlHubPorCaidaHost()
    {
        if (MenuManager.Instance != null) MenuManager.Instance.MostrarHub();
        AbandonarSala();
    }

    // =========================================================================
    // 4. ABANDONAR / CANCELAR SALA
    // =========================================================================
    public async void AbandonarSala()
    {
        if (currentSession != null)
        {
            currentSession.Deleted -= OnSessionDeleted;
            currentSession.SessionHostChanged -= OnHostChanged;

            try
            {
                if (currentSession.IsHost)
                {
                    try
                    {
                        // await CloudSaveService.Instance.Data.Custom.DeleteAsync(currentSession.Code);
                    }
                    catch
                    {
                        Debug.Log("Nota: CloudSave no pudo borrar la clave antigua.");
                    }

                    await currentSession.AsHost().DeleteAsync();
                }
                else
                {
                    await currentSession.LeaveAsync();
                }
            }
            catch (System.Exception e) { Debug.LogError($"Error al salir: {e.Message}"); }

            currentSession = null;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }

    private void OnTransportCrash()
    {
        Debug.LogError("🚨 [CHIVATO] ¡EL TRANSPORTE DE UNITY HA CHOCADO! El puerto UDP ya está en uso.");
    }

    private void OnDestroy()
    {
        AbandonarSala();
    }
}