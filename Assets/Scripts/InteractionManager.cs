using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public enum GameState { PLAYER_TURN, AI_TURN, WAITING }

public class InteractionManager : NetworkBehaviour
{
    public static InteractionManager Instance;
// Añade esto al principio de tu InteractionManager
    [Header("Control de Estado")]
    public bool yaHeJugadoMiTurno = false;
    [Header("Red")]
    //Se pueden cambiar los persmisos de acceso a la variable
    public NetworkVariable<int> totalJugadoresRed = new NetworkVariable<int>(2, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
        currentState = GameState.WAITING;
        
        string[] nombresDificultad = { "Pacifico", "Normal", "Difícil", "Experto", "Imposible" };
        int numBots = GameConfig.nPlayers; 
        int difIndex = GameConfig.difficulty;
        SetInfoMessage($"Numero de bots: {numBots}\nDificultad: {nombresDificultad[difIndex]}");
    }

    /// <summary>
    /// Reemplaza al Start() tradicional cuando usamos Unity Netcode. Arranca el motor de red. Controla la desconexión
    /// </summary>
    public override void OnNetworkSpawn()
    {

        
        if (IsServer)
        {
            totalJugadoresRed.Value = GameConfig.nPlayers < 2 ? 2 : GameConfig.nPlayers;
            
            //Detecta la desconexion de un Jugador
            NetworkManager.Singleton.OnClientDisconnectCallback += ControlarDesconexion;
        }

        NetworkManager.Singleton.OnClientDisconnectCallback += (id) => 
        {
            // Si la ID que se desconecta es la mía o la del servidor (0)
            // y no soy el servidor, significa que el Host ha cerrado la partida.
            if (!NetworkManager.Singleton.IsServer)
            {
                SceneManager.LoadScene("MainMenu");
            }
        };

        totalJugadoresRed.OnValueChanged += (valorViejo, valorNuevo) => 
        {
            if (valorNuevo > 0) StartCoroutine(EsperarYGenerar(valorNuevo));
        };

        if (totalJugadoresRed.Value > 0)
        {
            StartCoroutine(EsperarYGenerar(totalJugadoresRed.Value));
        }
    }
private void ControlarDesconexion(ulong clientId)
    {

        
        if (!IsServer) return; 

        int playerIndex = (int)clientId;
        Debug.Log($"⚠️ El Jugador {playerIndex} se ha desconectado. Queda eliminado.");

        // 1. Lo eliminamos de la partida (0 vidas)
        if (playerIndex < vidas.Length) vidas[playerIndex] = 0;
        
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

        SetInfoMessage($"El Jugador {playerIndex} se ha desconectado.");
        SincronizarDesconexionClientRpc(playerIndex);

        int jugadoresVivos = 0;
        int posibleGanador = -1;
        for (int i = 0; i < totalPlayers; i++)
        {
            if (vidas[i] > 0) { jugadoresVivos++; posibleGanador = i; }
        }

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
        
        else if (currentTurnIndex == playerIndex)
        {
            ChangeTurn();
        }
    }
[Rpc(SendTo.Everyone)]
    private void SincronizarDesconexionClientRpc(int idJugadorQueSeFue)
    {
        vidas[idJugadorQueSeFue] = 0;
        SetInfoMessage($"El Jugador {idJugadorQueSeFue} se ha desconectado.");
        ActualizarTodosLosPerfilesUI();
        
        // Cada cliente comprueba si, al irse este, él se ha quedado solo en la sala
        int humanosVivos = 0;
        for (int i = 0; i < totalPlayers; i++)
        {
            if (vidas[i] > 0 && NetworkManager.Singleton.ConnectedClients.ContainsKey((ulong)i))
                humanosVivos++;
        }

        // Si me he quedado yo solo como humano vivo, gano automáticamente
        if (humanosVivos <= 1 && vidas[(int)NetworkManager.Singleton.LocalClientId] > 0)
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
        int localId = (int)NetworkManager.Singleton.LocalClientId;
        
        // Si yo soy el ganador (el que se ha quedado en la sala)
        if (localId == ganadorIndex)
        {
            SetInfoMessage("¡TODOS TUS RIVALES HAN ABANDONADO! ¡VICTORIA!");
        }

        // Mostramos el cartel de Game Over
        if (PauseManager.Instance != null)
        {
            // Te damos el puesto 1 si eres el ganador, o un puesto peor si fueras otro
            int puesto = (localId == ganadorIndex) ? 1 : 2; 
            PauseManager.Instance.TriggerGameOver(puesto);
        }
    }

    private IEnumerator EsperarYGenerar(int numJugadores)
    {
        yield return new WaitUntil(() => TableManagerLayout.Instance != null);
        yield return new WaitForSeconds(0.2f); 
        ArrancarMesaLocal(numJugadores);
    }

    private void ArrancarMesaLocal(int numJugadores)
    {
        Debug.Log($"Soy el ID {NetworkManager.Singleton.LocalClientId} y voy a generar mesa para {numJugadores}");
        IniciarPartidaEnRed(numJugadores);
    }
    
    public void IniciarPartidaEnRed(int totalJugadoresEnSala)
    {
        if (TableManagerLayout.Instance != null)
        {
            TableManagerLayout.Instance.GenerarMesa(totalJugadoresEnSala);

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

                if (IsServer) //Sorteo de quien empieza al principio
                {
                   
                    int sorteo = Random.Range(0, totalPlayers);
                    
                    // Asignamos el resultado al manoMesaIndex
                    manoMesaIndex = sorteo;
                    
                    // El turno actual empieza siendo el del elegido
                    currentTurnIndex = manoMesaIndex;

                    Debug.Log($"🎲 SORTEO: El Jugador {manoMesaIndex} empieza la partida.");
                }
            }
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
        int localId = (int)NetworkManager.Singleton.LocalClientId;
        
        
        if (vidas[currentTurnIndex] <= 0)
        {
            currentState = GameState.WAITING;
            return; // Los eliminados no juegan ni piensan
        }

        currentState = (currentTurnIndex == localId) ? GameState.PLAYER_TURN : GameState.WAITING;

        UpdateVisualStates();
        ClearSelection();

        if (IsServer)
        {
            bool isHuman = NetworkManager.Singleton.ConnectedClients.ContainsKey((ulong)currentTurnIndex);
            if (!isHuman)
            {
                currentState = GameState.AI_TURN;
                StartCoroutine(AITurnRoutine());
            }
        }
    }

    public void AdvanceRoundSequence()
    {
        if (!IsServer) return; // Solo el Host avanza de ronda

        currentRoundCards += roundDelta;
        if(currentRoundCards <= 1) { currentRoundCards = 1; roundDelta = 1; }
        else if(currentRoundCards >= 5) { currentRoundCards = 5; roundDelta = -1; }

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
        int localId = (int)NetworkManager.Singleton.LocalClientId;
        bool isMyCard = card.transform.parent == playerHands[localId].transform;
        
        // Si soy un humano en mi turno
        bool canSelectAsHuman = (currentState == GameState.PLAYER_TURN && isMyCard);
        // Si el Servidor está controlando un Bot (solo el Host puede hacer esto)
        bool canSelectAsAI = (IsServer && currentState == GameState.AI_TURN && card.transform.parent == playerHands[currentTurnIndex].transform);

        if (canSelectAsHuman || canSelectAsAI)
        {
            if (SelectedCard == card)
            {
                ClearSelection();
                return; 
            }

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

        int localId = (int)NetworkManager.Singleton.LocalClientId;

        for (int i = 0; i < playerHands.Count; i++)
        {
            // Solo brilla y es tocable si eres TÚ, y es TU turno.
            bool isMyTurnAndMyHand = (i == currentTurnIndex && i == localId);
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
        Debug.Log($"🤖 IA {currentTurnIndex}: Pensando jugada...");
        yield return new WaitForSeconds(1.5f); 

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
            cardToPlay.SetFaceUp(true);
            SelectCard(cardToPlay); 
            
            yield return new WaitForSeconds(0.5f); 
            TableZone.Instance.OnPointerClick(null);
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
        TableZone.Instance.ForceEndRoundAnalysis();
    }

    [Rpc(SendTo.Everyone)]
    private void RevelarCartasCiegasClientRpc()
    {
        SetInfoMessage("¡RESOLVIENDO RONDA CIEGA!");
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
        int localId = (int)NetworkManager.Singleton.LocalClientId;
        
        if (winnerIndex == localId) SetInfoMessage("¡TÚ TIENES LA CARTA MÁS ALTA!");
        else SetInfoMessage($"¡EL JUGADOR {winnerIndex} TIENE LA CARTA MÁS ALTA!");
        
        ActualizarTodosLosPerfilesUI(); 
    }

    // ========================================================================
    // UTILIDADES
    // ========================================================================
    public void ActualizarTodosLosPerfilesUI()
    {
        int localId = 0;
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost))
            localId = (int)NetworkManager.Singleton.LocalClientId;

        for (int i = 0; i < totalPlayers; i++)
        {
            if (TableManagerLayout.Instance.perfilesActivos.Count > i)
            {
                string nombre = (i == localId) ? "TÚ" : $"JUGADOR {i}";
                TableManagerLayout.Instance.perfilesActivos[i].ActualizarPerfil(nombre, vidas[i], bazasGanadas[i], apuestas[i]);
            }
        }
    }

    public void SetInfoMessage(string message)
    {
        if (infoLineText != null) infoLineText.text = message;
    }

    public void RefreshHandVisibility()
    {
        if (playerHands == null || playerHands.Count == 0) return;
        int localId = 0;
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost))
            localId = (int)NetworkManager.Singleton.LocalClientId;

        for (int i = 0; i < playerHands.Count; i++)
        {
            CanvasGroup hand = playerHands[i];
            if (hand == null) continue;

            bool shouldBeFaceUp = (i == localId);
            if (currentRoundCards == 1) shouldBeFaceUp = (i != localId);

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

    public void StartNewGame() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
    public void ToggleAIDebugView() { isDebugAIVisible = !isDebugAIVisible; RefreshHandVisibility(); }
}