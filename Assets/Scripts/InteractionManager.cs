using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.IO;

public enum GameState { PLAYER_TURN, AI_TURN, WAITING }

public class InteractionManager : NetworkBehaviour
{
    public static InteractionManager Instance;

    [Header("Control de Estado")]
    public bool yaHeJugadoMiTurno = false;

    [Header("Red")]
    //Se pueden cambiar los persmisos de acceso a la variable
    public NetworkVariable<int> totalJugadoresRed = new NetworkVariable<int>(2, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> turnEndTime = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("UI Feedback")]
    public TextMeshProUGUI infoLineText;

    [Header("Debug")]
    public bool isDebugAIVisible = false;

    [Header("Control de Rondas")]
    public int currentRoundCards = 5;
    private int roundDelta = -1;

    [Header("Estadísticas de Jugadores (Ronda Actual)")]
    public int[] vidas;
    public int[] apuestas;
    public int[] bazasGanadas;

    [Header("Control de Turnos")]
    public int currentTurnIndex = 0;
    public int manoMesaIndex = 0;
    public int totalPlayers = 0;
    public GameState currentState;

    [Header("Sillas")]
    public List<CanvasGroup> playerHands = new List<CanvasGroup>();

    [Header("Estadísticas Globales (Fin de Partida)")]
    public int rondasJugadasTotales = 0;
    public int[] apuestasAcertadasTotales;
    public int[] bazasTotales;

    public UICard SelectedCard { get; private set; }
    public bool isPaused = false;

    //lambda guardada para desuscribir en OnNetworkDespawn
    private System.Action<ulong> _onClientDisconnectHandler;
   
    private bool _mesaGenerada = false;

    // SISTEMA DE ASIENTOS
    // El servidor asigna explícitamente asiento 0..N-1 a cada cliente.
    private int _mySeatIndex = 0;
    public int MySeatIndex => _mySeatIndex;

    private System.Collections.Generic.Dictionary<ulong, int> _clientToSeat
        = new System.Collections.Generic.Dictionary<ulong, int>();
    private System.Collections.Generic.Dictionary<int, ulong> _seatToClient
        = new System.Collections.Generic.Dictionary<int, ulong>();

    // SISTEMA DE NOMBRES (sincronizado por red)
    private string[] _playerNames;

    // SISTEMA DE AVATARES (sincronizado por red)
    private Sprite[] _playerAvatars;

    public bool IsPlayerConnectedAndHuman(int seatIndex)
    {
        return _seatToClient.TryGetValue(seatIndex, out ulong cid) && NetworkManager.Singleton.ConnectedClients.ContainsKey(cid);
    }
    
    public ulong GetClientIdForSeat(int seatIndex)
    {
        if (_seatToClient.TryGetValue(seatIndex, out ulong cid)) return cid;
        return ulong.MaxValue;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        GameConfig.gameStarted = true;
        GameConfig.trophyAwarded = false; // Resetear trofeos para esta partida
        GameConfig.trophyBote = 0;        // Resetear bote de trofeos
        currentState = GameState.WAITING;

        string[] nombresDificultad = { "Pacifico", "Normal", "Difícil", "Experto", "Imposible" };
        int numBots = GameConfig.nPlayers;
        int difIndex = GameConfig.difficulty;
        SetInfoMessage($"<b>¡EMPIEZA LA PARTIDA!</b>\nRonda 1: {currentRoundCards} Cartas", 5f);
    }

    /// <summary>
    /// Reemplaza al Start() tradicional cuando usamos Unity Netcode. Arranca el motor de red. Controla la desconexión
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // MONETIZACIÓN: Cobro por adelantado al entrar a la partida
        if (GameConfig.currentFee > 0 && !GameConfig.prizeAwarded)
        {
            TopBarUI.QueuePendingDelta(0, -GameConfig.currentFee);
            Debug.Log($"[ECONOMÍA] Partida arrancada. Fee deducido y encolado: -{GameConfig.currentFee}");
        }

        // ANTI-RAGEQUIT FLAG: Registrar que la partida empezó para castigar cierres por deslizamiento en móvil
        if (!GameConfig.isPrivateMatch)
        {
            PlayerPrefs.SetInt("PartidaEnCurso", 1);
            int ultimoPuesto = Mathf.Clamp(GameConfig.nPlayers - 1, 0, GameConfig.trophyDeltaByRank.Length - 1);
            int penalizacionMax = Mathf.Min(0, GameConfig.trophyDeltaByRank[ultimoPuesto]);
            PlayerPrefs.SetInt("RageQuit_Trophies", penalizacionMax);
            PlayerPrefs.Save();
            Debug.Log($"[ANTI-RAGEQUIT] Bandera activada. Posible castigo trofeos: {penalizacionMax}");
        }

        if (IsServer)
        {
            totalJugadoresRed.Value = GameConfig.nPlayers < 2 ? 2 : GameConfig.nPlayers;
            NetworkManager.Singleton.OnClientDisconnectCallback += ControlarDesconexion;
            // El servidor espera a que todos conecten y luego asigna asientos
            StartCoroutine(AsignarAsientosYArrancar());
        }
        // else: los clientes esperan el RPC AsignarAsientosRpc para generar la mesa

        //lambda para detectar caída del host en el cliente
        _onClientDisconnectHandler = (id) =>
        {
            if (!IsServer && NetworkManager.Singleton.IsConnectedClient)
            {
                if (SessionNetworkManager.Instance != null)
                    SessionNetworkManager.Instance.ExpulsarAlHubPorCaidaHost();
                else
                {
                    NetworkManager.Singleton.Shutdown();
                    UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                }
            }
        };
        NetworkManager.Singleton.OnClientDisconnectCallback += _onClientDisconnectHandler;
    }

    /// <summary>
    /// Servidor: espera a que todos los clientes esperados conecten a NGO,
    /// asigna asientos 0..N-1 de forma determinista y lo difunde via RPC.
    /// </summary>
    private IEnumerator AsignarAsientosYArrancar()
    {
        // En lugar de esperar a totalJugadoresRed.Value, esperamos a los humanos reales.
        // Si por algún motivo es <= 0 (partida de práctica o viejo flujo), usamos totalJugadoresRed.
        int esperados = GameConfig.nHumanPlayers;
        if (esperados <= 0) esperados = totalJugadoresRed.Value;

        float timeout = 15f;
        while (NetworkManager.Singleton.ConnectedClientsIds.Count < esperados && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        if (timeout <= 0)
            Debug.LogWarning($"[ASIENTOS] Timeout: solo {NetworkManager.Singleton.ConnectedClientsIds.Count}/{esperados} conectaron.");

        // Asignamos asientos en orden ascendente de clientId para que sea determinista
        var ids = new System.Collections.Generic.List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        ids.Sort();
        ulong[] asignaciones = ids.ToArray();

        Debug.Log($"[ASIENTOS] Asignando {asignaciones.Length} asientos humanos en mesa de {totalJugadoresRed.Value}.");
        AsignarAsientosRpc(asignaciones, totalJugadoresRed.Value);
    }

    [Rpc(SendTo.Everyone)]
    private void AsignarAsientosRpc(ulong[] asignaciones, int totalMesa)
    {
        _clientToSeat.Clear();
        _seatToClient.Clear();
        for (int s = 0; s < asignaciones.Length; s++)
        {
            _clientToSeat[asignaciones[s]] = s;
            _seatToClient[s] = asignaciones[s];
        }

        ulong myId = NetworkManager.Singleton.LocalClientId;
        if (_clientToSeat.TryGetValue(myId, out int seat))
            _mySeatIndex = seat;
        else
            Debug.LogError($"[ASIENTOS] ClientId {myId} no encontrado en la asignación.");

        Debug.Log($"[ASIENTOS] ClientId={myId} → Asiento #{_mySeatIndex}. Generando mesa para {totalMesa}.");

        // Inicializar array de nombres con valores por defecto
        _playerNames = new string[totalMesa];
        for (int i = 0; i < totalMesa; i++)
            _playerNames[i] = $"JUGADOR {i}";

        // Enviar mi nombre real al servidor para que lo distribuya
        string miNombre = "Invitado";
        if (ProfileManager.Instance != null)
            miNombre = ProfileManager.Instance.GetDisplayName();
        
        _playerNames[_mySeatIndex] = miNombre;
        EnviarNombreAlServidorRpc(miNombre);

        // Inicializar array de avatares
        _playerAvatars = new Sprite[totalMesa];

        // Enviar mi avatar al servidor para que lo distribuya
        byte[] avatarBytes = ObtenerMiAvatarComprimido();
        if (avatarBytes != null && avatarBytes.Length > 0)
            EnviarAvatarAlServidorRpc(avatarBytes);

        if (!_mesaGenerada)
        {
            _mesaGenerada = true;
            StartCoroutine(EsperarYGenerar(totalMesa));
        }
    }

    // --- SINCRONIZACIÓN DE NOMBRES ---
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void EnviarNombreAlServidorRpc(string nombre, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (_clientToSeat.TryGetValue(senderId, out int seat))
        {
            if (_playerNames == null || seat >= _playerNames.Length) return;
            _playerNames[seat] = nombre;
            Debug.Log($"[NOMBRES] Asiento {seat} = \"{nombre}\" (clientId={senderId})");

            // Reenviar a todos los clientes unidos por "|" para evitar el error de serialización de arrays
            string nombresUnidos = string.Join("|", _playerNames);
            SincronizarNombresClientRpc(nombresUnidos);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void SincronizarNombresClientRpc(string nombresUnidos)
    {
        if (!string.IsNullOrEmpty(nombresUnidos))
        {
            _playerNames = nombresUnidos.Split('|');
        }
        // Solo refrescar si la mesa ya está generada
        if (_mesaGenerada)
            ActualizarTodosLosPerfilesUI();
    }

    // --- SINCRONIZACIÓN DE AVATARES ---
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void EnviarAvatarAlServidorRpc(byte[] avatarBytes, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (_clientToSeat.TryGetValue(senderId, out int seat))
        {
            Debug.Log($"[AVATAR] Recibido avatar del asiento {seat} ({avatarBytes.Length} bytes)");
            SincronizarAvatarClientRpc(seat, avatarBytes);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void SincronizarAvatarClientRpc(int seatIndex, byte[] avatarBytes)
    {
        if (avatarBytes == null || avatarBytes.Length == 0) return;
        if (_playerAvatars == null || seatIndex < 0 || seatIndex >= _playerAvatars.Length) return;

        Texture2D tex = new Texture2D(2, 2);
        if (tex.LoadImage(avatarBytes))
        {
            _playerAvatars[seatIndex] = Sprite.Create(tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f));
            Debug.Log($"[AVATAR] Avatar del asiento {seatIndex} reconstruido ({tex.width}x{tex.height})");
        }

        if (_mesaGenerada)
            ActualizarTodosLosPerfilesUI();
    }

    /// <summary>Devuelve el avatar del jugador en el asiento indicado (null si no tiene).</summary>
    public Sprite GetPlayerAvatar(int seatIndex)
    {
        if (_playerAvatars != null && seatIndex >= 0 && seatIndex < _playerAvatars.Length)
            return _playerAvatars[seatIndex];
        return null;
    }

    /// <summary>Comprime el avatar local del jugador a PNG de 128x128 para enviar por red.</summary>
    private byte[] ObtenerMiAvatarComprimido()
    {
        if (ProfileManager.Instance == null) return null;
        var profile = ProfileManager.Instance.Profile;

        // Solo si tiene avatar personalizado
        if (profile.avatarId != -1 || string.IsNullOrEmpty(profile.customAvatarPath)) return null;
        if (!File.Exists(profile.customAvatarPath)) return null;

        try
        {
            byte[] rawBytes = File.ReadAllBytes(profile.customAvatarPath);
            Texture2D original = new Texture2D(2, 2);
            if (!original.LoadImage(rawBytes)) return null;

            // Redimensionar a 128x128 para minimizar tráfico de red
            int targetSize = 128;
            RenderTexture rt = RenderTexture.GetTemporary(targetSize, targetSize);
            Graphics.Blit(original, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D scaled = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, false);
            scaled.ReadPixels(new Rect(0, 0, targetSize, targetSize), 0, 0);
            scaled.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            byte[] compressed = scaled.EncodeToPNG();
            Debug.Log($"[AVATAR] Avatar local comprimido: {compressed.Length} bytes");
            return compressed;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[AVATAR] Error al comprimir avatar: {e.Message}");
            return null;
        }
    }

    /// <summary>Devuelve el nombre visible del jugador en el asiento indicado.</summary>
    public string GetPlayerName(int seatIndex)
    {
        if (_playerNames != null && seatIndex >= 0 && seatIndex < _playerNames.Length)
            return _playerNames[seatIndex];
        return $"JUGADOR {seatIndex}";
    }


    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= ControlarDesconexion;
            if (_onClientDisconnectHandler != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= _onClientDisconnectHandler;
        }
        _onClientDisconnectHandler = null;
        _mesaGenerada = false;
        _mySeatIndex = 0;
        _clientToSeat.Clear();
        _seatToClient.Clear();
    }

    private void ControlarDesconexion(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
        if (!IsServer) return;

        // Convertimos clientId → seatIndex usando el mapa del servidor
        if (!_clientToSeat.TryGetValue(clientId, out int playerIndex))
        {
            Debug.LogWarning($"[DESCONEXION] clientId {clientId} no tiene asiento asignado (ya procesado).");
            return;
        }
        if (vidas == null || playerIndex < 0 || playerIndex >= vidas.Length) return;
        Debug.Log($"⚠️ ClientId={clientId} (Asiento {playerIndex}) se ha desconectado.");

        // 1. Lo eliminamos de la partida (0 vidas)
        vidas[playerIndex] = 0;

        // 2. RECUPERAR CARTAS A LA BARAJA
        if (playerIndex < playerHands.Count && playerHands[playerIndex] != null)
        {
            foreach (Transform child in playerHands[playerIndex].transform)
            {
                UICard uiCard = child.GetComponent<UICard>();
                if (uiCard != null) CardDatabase.deck.Add(uiCard.cardData);
            }
            // Borramos los gráficos en todos los ordenadores
            DestruirCartasManoClientRpc(playerIndex);
        }

        string nombreReal = GetPlayerName(playerIndex);
        SetInfoMessage($"<color=#FF5555><b>{nombreReal}</b></color> se ha desconectado.", 5f);
        SincronizarDesconexionClientRpc(playerIndex);

        int jugadoresVivos = 0;
        int posibleGanador = -1;
        for (int i = 0; i < totalPlayers; i++)
        {
            if (vidas[i] > 0) { jugadoresVivos++; posibleGanador = i; }
        }
        // Si hay más de 1 vivo, no hay ganador todavía
        if (jugadoresVivos > 1) posibleGanador = -1;

        if (jugadoresVivos <= 1)
        {
            isPaused = true;
            TerminarPartidaPorAbandonoClientRpc(posibleGanador);
            return;
        }


        if (BettingManager.Instance != null && BettingManager.Instance.panelRoot.activeSelf)
        {
            BettingManager.Instance.FuerzaPasarTurnoDesconectado(playerIndex);
        }

        else
        {
            // Si estábamos en la fase de jugar cartas, comprobamos si la mesa ahora está llena
            if (TableZone.Instance != null) TableZone.Instance.ForzarValidacionMesa();

            if (currentTurnIndex == playerIndex && !isPaused)
            {
                ChangeTurn();
            }
        }
    }
    [Rpc(SendTo.Everyone)]
    private void SincronizarDesconexionClientRpc(int idJugadorQueSeFue)
    {
        if (!IsServer) vidas[idJugadorQueSeFue] = 0;
        string nombreReal = GetPlayerName(idJugadorQueSeFue);
        SetInfoMessage($"<color=#FF5555><b>{nombreReal}</b></color> se ha desconectado.", 5f);
        ActualizarTodosLosPerfilesUI();

        // Cada cliente comprueba si, al irse este, él se ha quedado solo en la sala
        int humanosVivos = 0;
        for (int i = 0; i < totalPlayers; i++)
        {
            // Usamos _seatToClient para saber qué clientId ocupa el asiento i
            if (vidas[i] > 0 && _seatToClient.TryGetValue(i, out ulong cid)
                && NetworkManager.Singleton.ConnectedClients.ContainsKey(cid))
                humanosVivos++;
        }

        // Si me he quedado yo solo como humano vivo, gano automáticamente
        if (humanosVivos <= 1 && vidas[_mySeatIndex] > 0)
        {
            if (PauseManager.Instance != null) PauseManager.Instance.TriggerGameOver(1);
        }
    }
    [Rpc(SendTo.Everyone)]
    private void DestruirCartasManoClientRpc(int playerIndex)
    {
        if (playerIndex >= playerHands.Count) return;
        CanvasGroup mano = playerHands[playerIndex];
        foreach (Transform child in mano.transform) Destroy(child.gameObject);
    }

    [Rpc(SendTo.Everyone)]
    private void TerminarPartidaPorAbandonoClientRpc(int ganadorIndex)
    {
        isPaused = true;

        if (_mySeatIndex == ganadorIndex)
            SetInfoMessage("<color=#55FF55><b>¡VICTORIA POR ABANDONO!</b></color> Todos los rivales han huido.", 8f);

        if (PauseManager.Instance != null)
        {
            int puesto = (_mySeatIndex == ganadorIndex) ? 1 : 2;
            PauseManager.Instance.TriggerGameOver(puesto);
        }
    }

    private IEnumerator EsperarYGenerar(int numJugadores)
    {
        yield return new WaitUntil(() => TableManagerLayout.Instance != null);
        yield return new WaitForSeconds(0.2f);
        ArrancarMesaLocal(numJugadores);
        // Refresco de nombres y avatares: si los RPCs llegaron antes de generar la mesa,
        // los perfiles se quedaron con "JUGADOR X" y sin avatar. Este refresco los corrige.
        ActualizarTodosLosPerfilesUI();

        LoadingManager.Instance?.OcultarCargando();
    }

    private void ArrancarMesaLocal(int numJugadores)
    {
        Debug.Log($"[ASIENTOS] ClientId={NetworkManager.Singleton.LocalClientId} (Asiento #{_mySeatIndex}) generando mesa para {numJugadores}");
        IniciarPartidaEnRed(numJugadores, _mySeatIndex);
    }

    public void IniciarPartidaEnRed(int totalJugadoresEnSala, int localSeatIndex)
    {
        if (TableManagerLayout.Instance != null)
        {
            TableManagerLayout.Instance.GenerarMesa(totalJugadoresEnSala, localSeatIndex);

            if (TableManagerLayout.Instance.manosActivas.Count >= 2)
            {
                playerHands = new List<CanvasGroup>(TableManagerLayout.Instance.manosActivas);
                totalPlayers = playerHands.Count;

                vidas = new int[totalPlayers];
                apuestas = new int[totalPlayers];
                bazasGanadas = new int[totalPlayers];
                apuestasAcertadasTotales = new int[totalPlayers];
                bazasTotales = new int[totalPlayers];

                for (int i = 0; i < totalPlayers; i++)
                {
                    vidas[i] = 3;
                    apuestas[i] = -1;
                    bazasGanadas[i] = 0;
                }

                if (IsServer)
                {
                    int sorteo = Random.Range(0, totalPlayers);
                    manoMesaIndex = sorteo;
                    currentTurnIndex = manoMesaIndex;
                    Debug.Log($"🎲 SORTEO: El Asiento {manoMesaIndex} empieza la partida.");
                    
                    // Automatización: arrancar la primera ronda
                    StartCoroutine(StartNewRoundServer());
                }
            }
        }
    }

    public IEnumerator StartNewRoundServer()
    {
        if (!IsServer) yield break;
        
        Debug.Log("[GAME LOOP] Iniciando nueva ronda. Esperando 3 segundos...");
        yield return new WaitForSeconds(3.0f);
        
        if (HandTester.Instance != null)
        {
            Debug.Log("[GAME LOOP] Llamando a DrawNewHand automáticamente.");
            HandTester.Instance.DrawNewHand();
        }
        else
        {
            Debug.LogError("[GAME LOOP] HandTester.Instance es NULL!");
        }
    }

    // ========================================================================
    // 2. MÁQUINA DE ESTADOS (Orquestada por el Servidor)
    // ========================================================================
    public void InitializeGame()
    {
        if (!IsServer) return; // Solo el host inicia el juego físico
        isPaused = false;
        currentTurnIndex = manoMesaIndex;
        SincronizarTurnoClientRpc(currentTurnIndex);
    }

    public void ChangeTurn()
    {
        if (!IsServer) return; // Solo el host cambia de turno

        int limitador = 0;
        do
        {
            currentTurnIndex = (currentTurnIndex + 1) % totalPlayers;
            limitador++;
            if (limitador > totalPlayers) break;
        }
        while (vidas[currentTurnIndex] <= 0);

        SincronizarTurnoClientRpc(currentTurnIndex);
    }

    public void SetTurn(int newPlayerIndex)
    {
        if (!IsServer) return;
        currentTurnIndex = newPlayerIndex;
        SincronizarTurnoClientRpc(currentTurnIndex);
    }

    [Rpc(SendTo.Everyone)]
    private void SincronizarTurnoClientRpc(int nuevoTurno)
    {
        currentTurnIndex = nuevoTurno;

        if (vidas[currentTurnIndex] <= 0)
        {
            currentState = GameState.WAITING;
            return;
        }

        // Usamos _mySeatIndex (NO LocalClientId) para saber si es nuestro turno
        currentState = (currentTurnIndex == _mySeatIndex) ? GameState.PLAYER_TURN : GameState.WAITING;

        if (currentState == GameState.PLAYER_TURN)
            SetInfoMessage("<color=#55FF55><b>¡Es tu turno de jugar carta!</b></color>", 5f);
        else
            SetInfoMessage($"Turno de <color=#AAAAAA><b>{GetPlayerName(currentTurnIndex)}</b></color>...", 4f);

        UpdateVisualStates();
        ClearSelection();

        if (IsServer)
        {
            // Comprobamos si el asiento actual pertenece a un humano usando el mapa de asientos
            bool currentPlayerIsHuman = _seatToClient.TryGetValue(currentTurnIndex, out ulong cid)
                                        && NetworkManager.Singleton.ConnectedClients.ContainsKey(cid);

            if (!currentPlayerIsHuman)
            {
                currentState = GameState.AI_TURN;
                StartCoroutine(AITurnRoutine());
            }
            else
            {
                StartCoroutine(TurnTimerRoutine(currentTurnIndex));
            }
        }
    }

    public void AdvanceRoundSequence()
    {
        if (!IsServer) return; // Solo el Host avanza de ronda

        currentRoundCards += roundDelta;
        if (currentRoundCards <= 1) { currentRoundCards = 1; roundDelta = 1; }
        else if (currentRoundCards >= 5) { currentRoundCards = 5; roundDelta = -1; }

        int limitador = 0;
        do
        {
            manoMesaIndex = (manoMesaIndex + 1) % totalPlayers;
            limitador++;
            if (limitador > totalPlayers) break;
        }
        while (vidas[manoMesaIndex] <= 0);

        AvanzarRondaClientRpc(currentRoundCards, manoMesaIndex);
    }

    [Rpc(SendTo.Everyone)]
    private void AvanzarRondaClientRpc(int nuevasCartas, int nuevoMano)
    {
        currentRoundCards = nuevasCartas;
        manoMesaIndex = nuevoMano;
        currentTurnIndex = manoMesaIndex;

        Debug.Log($"<color=orange>PRÓXIMA RONDA: {currentRoundCards} CARTAS</color>");
    }

    // ========================================================================
    // 3. SELECCIÓN LOCAL Y VISUALES
    // ========================================================================
    public void SelectCard(UICard card)
    {
        bool isMyCard = card.transform.parent == playerHands[_mySeatIndex].transform;

        bool canSelectAsHuman = (currentState == GameState.PLAYER_TURN && isMyCard);
        bool canSelectAsAI = (IsServer && currentState == GameState.AI_TURN && card.transform.parent == playerHands[currentTurnIndex].transform);
        bool canForcePlay = (IsServer && card.transform.parent == playerHands[currentTurnIndex].transform);

        if (canSelectAsHuman || canSelectAsAI || canForcePlay)
        {
            if (SelectedCard == card) { ClearSelection(); return; }
            if (SelectedCard != null) SelectedCard.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            SelectedCard = card;
            SelectedCard.GetComponent<UnityEngine.UI.Image>().color = Color.yellow;
        }
    }

    public void ClearSelection()
    {
        if (SelectedCard != null) SelectedCard.GetComponent<UnityEngine.UI.Image>().color = Color.white;
        SelectedCard = null;
    }

    public bool HasCardSelected() { return SelectedCard != null; }

    public void UpdateVisualStates()
    {
        if (isPaused || playerHands.Count == 0) return;

        for (int i = 0; i < playerHands.Count; i++)
        {
            bool isMyTurnAndMyHand = (i == currentTurnIndex && i == _mySeatIndex);
            SetGroupState(playerHands[i], isMyTurnAndMyHand, isMyTurnAndMyHand ? 1f : 0.5f);
        }
    }

    private void SetGroupState(CanvasGroup group, bool active, float alpha)
    {
        if (group != null)
        {
            group.interactable = active;
            group.blocksRaycasts = active;
            group.alpha = 1f;
            foreach (Transform childCard in group.transform)
            {
                CanvasGroup cardGroup = childCard.GetComponent<CanvasGroup>();
                if (cardGroup == null) cardGroup = childCard.gameObject.AddComponent<CanvasGroup>();
                cardGroup.alpha = active ? 1f : alpha;
            }
        }
    }

    // ========================================================================
    // 4. INTELIGENCIA ARTIFICIAL (Solo corre en el Host)
    // ========================================================================
    IEnumerator AITurnRoutine()
    {
        // El reloj de turno se oculta/pausa para los humanos mientras juega el bot
        turnEndTime.Value = 0f;
        
        Debug.Log($"🤖 IA {currentTurnIndex}: Pensando jugada...");
        
        float[] aiDelays = { 3.5f, 2.5f, 2.0f, 1.5f, 1.0f, 0.5f, 0.2f };
        int diff = Mathf.Clamp(GameConfig.difficulty, 0, 6);
        yield return new WaitForSeconds(aiDelays[diff]);


        List<Card> cardsOnTable = new List<Card>();
        if (TableZone.Instance != null)
        {
            foreach (Transform t in TableZone.Instance.transform)
            {
                UICard c = t.GetComponent<UICard>();
                if (c != null) cardsOnTable.Add(c.cardData);
            }
        }

        List<UICard> aiHand = new List<UICard>();
        foreach (Transform t in playerHands[currentTurnIndex].transform)
        {
            UICard c = t.GetComponent<UICard>();
            if (c != null) aiHand.Add(c);
        }

        int currentWins = bazasGanadas[currentTurnIndex];
        int targetBet = apuestas[currentTurnIndex];

        UICard cardToPlay = AIController.Instance.ChooseCardToPlay(aiHand, cardsOnTable, currentWins, targetBet);

        if (cardToPlay != null)
        {
            Debug.Log($"🤖 IA {currentTurnIndex}: Juega {cardToPlay.cardData.rank} de {cardToPlay.cardData.suit}");
            SelectCard(cardToPlay);
            TableZone.Instance.OnPointerClick(null);
        }
    }

    IEnumerator TurnTimerRoutine(int playerIndex)
    {
        float timeLimit = GameConfig.turnTime > 0 ? GameConfig.turnTime : 15f;
        turnEndTime.Value = (float)NetworkManager.Singleton.ServerTime.Time + timeLimit;

        while (currentTurnIndex == playerIndex && !isPaused)
        {
            if ((float)NetworkManager.Singleton.ServerTime.Time >= turnEndTime.Value)
            {
                Debug.LogWarning($"[TIMEOUT] El jugador {playerIndex} ha tardado demasiado. Forzando jugada.");
                
                List<UICard> hand = new List<UICard>();
                foreach (Transform t in playerHands[playerIndex].transform)
                {
                    UICard c = t.GetComponent<UICard>();
                    if (c != null) hand.Add(c);
                }

                if (hand.Count > 0)
                {
                    UICard cardToPlay = hand[0];
                    SelectCard(cardToPlay);
                    TableZone.Instance.OnPointerClick(null);
                }
                
                break;
            }
            yield return null;
        }
    }

    // ========================================================================
    // 5. RESOLUCIÓN DE RONDA CIEGA (Sincronizada)
    // ========================================================================
    public void ResolveBlindRoundImmediate()
    {
        if (IsServer) StartCoroutine(BlindRoundRoutineServer());
    }

    IEnumerator BlindRoundRoutineServer()
    {
        yield return new WaitForSeconds(1.0f);
        RevelarCartasCiegasClientRpc();

        yield return new WaitForSeconds(1.5f);

        int maxScore = -1;
        int winnerIndex = -1;
        List<UICard> cartasJugadas = new List<UICard>();

        for (int i = 0; i < playerHands.Count; i++)
        {
            if (playerHands[i].transform.childCount > 0)
            {
                UICard card = playerHands[i].transform.GetChild(0).GetComponent<UICard>();
                cartasJugadas.Add(card);

                int score = (card.cardData.value * 10) + GetSuitValue(card.cardData.suit);
                if (score > maxScore)
                {
                    maxScore = score;
                    winnerIndex = i;
                }
            }
        }

        if (winnerIndex != -1)
        {
            bazasGanadas[winnerIndex]++;
            SincronizarGanadorBazaClientRpc(winnerIndex, bazasGanadas[winnerIndex]);
        }

        TableZone.Instance.bazasJugadas = 1;
        yield return new WaitForSeconds(1.5f);

        // Mover cartas a la mesa en el servidor antes de forzar análisis
        foreach (UICard c in cartasJugadas) c.transform.SetParent(TableZone.Instance.transform);
        MoverCartasCiegasAMesaClientRpc();
        TableZone.Instance.ForceEndRoundAnalysis();
    }
    [Rpc(SendTo.Everyone)]
    private void MoverCartasCiegasAMesaClientRpc()
    {
        // Todos los ordenadores cogen la carta de la mano y la tiran a la mesa central
        for (int i = 0; i < playerHands.Count; i++)
        {
            if (playerHands[i].transform.childCount > 0)
            {
                Transform card = playerHands[i].transform.GetChild(0);
                card.SetParent(TableZone.Instance.transform);
                card.localPosition = Vector3.zero;
                card.localScale = new Vector3(0.85f, 0.85f, 0.85f);
                card.localEulerAngles = Vector3.zero;
            }
        }
    }
    [Rpc(SendTo.Everyone)]
    private void RevelarCartasCiegasClientRpc()
    {
        SetInfoMessage("<b>¡RONDA CIEGA A LA VISTA!</b>", 4f);
        foreach (CanvasGroup hand in playerHands)
        {
            if (hand.transform.childCount > 0)
            {
                UICard c = hand.transform.GetChild(0).GetComponent<UICard>();
                if (c != null) c.SetFaceUp(true);
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    private void SincronizarGanadorBazaClientRpc(int winnerIndex, int totalBazasDelGanador)
    {
        bazasGanadas[winnerIndex] = totalBazasDelGanador;

        if (winnerIndex == _mySeatIndex) 
            SetInfoMessage("<color=#55FF55><b>¡Tu carta es la más alta de la mesa!</b></color>", 5f);
        else 
            SetInfoMessage($"<color=#AAAAAA><b>{GetPlayerName(winnerIndex)}</b></color> tiene la carta más alta.", 5f);

        ActualizarTodosLosPerfilesUI();
    }

    // ========================================================================
    // UTILIDADES
    // ========================================================================
    public void ActualizarTodosLosPerfilesUI()
    {
        if (TableManagerLayout.Instance == null) return;
        for (int i = 0; i < totalPlayers; i++)
        {
            if (TableManagerLayout.Instance.perfilesActivos.Count > i)
            {
                string nombre = GetPlayerName(i);
                Sprite avatar = GetPlayerAvatar(i);
                TableManagerLayout.Instance.perfilesActivos[i].ActualizarPerfil(nombre, vidas[i], bazasGanadas[i], apuestas[i], avatar);
            }
        }
    }
    private Coroutine _infoMessageCoroutine;

    public void SetInfoMessage(string message, float duration = 4f)
    {
        if (infoLineText != null)
        {
            infoLineText.text = message;
            if (_infoMessageCoroutine != null) StopCoroutine(_infoMessageCoroutine);
            
            // Si el mensaje está vacío, no lanzamos corrutina (se queda borrado)
            if (!string.IsNullOrEmpty(message))
            {
                _infoMessageCoroutine = StartCoroutine(ClearInfoMessageAfter(duration));
            }
        }
    }

    private IEnumerator ClearInfoMessageAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (infoLineText != null) infoLineText.text = "";
    }

    public void RefreshHandVisibility()
    {
        if (playerHands == null || playerHands.Count == 0) return;

        for (int i = 0; i < playerHands.Count; i++)
        {
            CanvasGroup hand = playerHands[i];
            if (hand == null) continue;

            bool shouldBeFaceUp = (i == _mySeatIndex);
            if (currentRoundCards == 1) shouldBeFaceUp = (i != _mySeatIndex);

            foreach (Transform child in hand.transform)
            {
                UICard card = child.GetComponent<UICard>();
                if (card != null) card.SetFaceUp(shouldBeFaceUp);
            }
        }
    }

    private int GetSuitValue(string suit)
    {
        if (suit == "Diamantes") return 4;
        if (suit == "Corazones") return 3;
        if (suit == "Picas") return 2;
        return 1;
    }
    /*public override void OnNetworkDespawn()
{
    if (NetworkManager.Singleton != null)
    {
        NetworkManager.Singleton.OnClientDisconnectCallback -= ControlarDesconexion;
        NetworkManager.Singleton.OnClientDisconnectCallback -= _onClientDisconnectHandler;
    }
}*/
    public void StartNewGame() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void ToggleAIDebugView() { isDebugAIVisible = !isDebugAIVisible; RefreshHandVisibility(); }
}