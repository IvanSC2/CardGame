using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public enum GameState {PLAYER_TURN, AI_TURN, WAITING}

public class InteractionManager : MonoBehaviour
{
    // Singleton
    public static InteractionManager Instance;

    [Header("UI Feedback")]
    public TextMeshProUGUI infoLineText;
    
    [Header("Debug")]
    public bool isDebugAIVisible = false; 
    
    [Header("Control de Rondas")]
    public int currentRoundCards = 5;
    private int roundDelta = -1;

    // --- ¡NUEVO SISTEMA DE ARRAYS MULTIJUGADOR! ---
    [Header("Estadísticas de Jugadores (Ronda Actual)")]
    public int[] vidas;
    public int[] apuestas;
    public int[] bazasGanadas;

    [Header("Control de Turnos")]
    public int currentTurnIndex=0;
    public int manoMesaIndex=0; 
    public int totalPlayers=0;
    public GameState currentState; 

    [Header("Sillas")]
    public List<CanvasGroup> playerHands = new List<CanvasGroup>();

    // ARRAYS PARA LAS ESTADÍSTICAS GLOBALES
    [Header("Estadísticas Globales (Fin de Partida)")]
    public int rondasJugadasTotales = 0;
    public int[] apuestasAcertadasTotales;
    public int[] bazasTotales; 
    
    public UICard SelectedCard { get; private set; }
    public bool isPaused = false;

    // --- Helpers de Mensajes ---
    public void SetInfoMessage(string message)
    {
        if (infoLineText != null) infoLineText.text = message;
        Debug.Log("[INFO-UI]: " + message);
    }

   private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
        currentState = GameState.WAITING;
        
        string[] nombresDificultad = { "Pacifico", "Normal", "Difícil", "Experto", "Imposible" };
        int numBots = GameConfig.nPlayers; 
        int difIndex = GameConfig.difficulty;
        SetInfoMessage($"Numero de bots: {numBots}\nDificultad: {nombresDificultad[difIndex]}");
        
        UpdateVisualStates();
    }

    private void Start()
    {
        int numBots = GameConfig.nPlayers; 
        
        if (TableManagerLayout.Instance != null)
        {
            TableManagerLayout.Instance.GenerarMesa(numBots + 1);

            if (TableManagerLayout.Instance.manosActivas.Count >= 2)
            {
                playerHands = new List<CanvasGroup>(TableManagerLayout.Instance.manosActivas);
                totalPlayers = playerHands.Count;

                // --- INICIALIZAMOS LOS ARRAYS DE DATOS ---
                vidas = new int[totalPlayers];
                apuestas = new int[totalPlayers];
                bazasGanadas = new int[totalPlayers];
                apuestasAcertadasTotales = new int[totalPlayers];
                bazasTotales = new int[totalPlayers];

                // Rellenamos datos base: 3 vidas, apuestas a -1 (nulas), 0 bazas
                for (int i = 0; i < totalPlayers; i++)
                {
                    vidas[i] = 3; 
                    apuestas[i] = -1;
                    bazasGanadas[i] = 0;
                    apuestasAcertadasTotales[i] = 0;
                    bazasTotales[i] = 0;
                }

                // Sorteo de quién empieza la partida
                manoMesaIndex = Random.Range(0, totalPlayers);
                currentTurnIndex = manoMesaIndex;
                currentState = (currentTurnIndex == 0) ? GameState.PLAYER_TURN : GameState.AI_TURN;
                
                // Actualizamos la UI inicial
                ActualizarTodosLosPerfilesUI();
            }
        }
        else
        {
            Debug.LogError("Error: TableManagerLayout no encontrado al arrancar.");
        }
    }

    // UIPerfiles
    public void ActualizarTodosLosPerfilesUI()
    {
        for (int i = 0; i < totalPlayers; i++)
        {
            if (TableManagerLayout.Instance.perfilesActivos.Count > i)
            {
                string nombre = (i == 0) ? "TÚ" : $"BOT {i}";
                TableManagerLayout.Instance.perfilesActivos[i].ActualizarPerfil(nombre, vidas[i], bazasGanadas[i], apuestas[i]);
            }
        }
    }

    public void InitializeGame()
    {
        isPaused = false;
        currentTurnIndex = manoMesaIndex;
        currentState = (currentTurnIndex == 0) ? GameState.PLAYER_TURN : GameState.AI_TURN;
        UpdateVisualStates();
        RefreshHandVisibility();
        if (currentState == GameState.AI_TURN) StartCoroutine(AITurnRoutine());
    }

    public void ToggleAIDebugView()
    {
        isDebugAIVisible = !isDebugAIVisible;
        RefreshHandVisibility();
    }

    public void RefreshHandVisibility()
    {
        if (playerHands.Count == 0) return;
        
        if (currentRoundCards == 1)
        {
            foreach (Transform t in playerHands[0].transform)
                if (t.GetComponent<UICard>()) t.GetComponent<UICard>().SetFaceUp(false);

            for (int i = 1; i < playerHands.Count; i++)
            {
                foreach (Transform t in playerHands[i].transform)
                    if (t.GetComponent<UICard>()) t.GetComponent<UICard>().SetFaceUp(true);
            }
            return;
        }

        foreach (Transform t in playerHands[0].transform) 
            if(t.GetComponent<UICard>()) t.GetComponent<UICard>().SetFaceUp(true);

        for (int i = 1; i < playerHands.Count; i++)
        {
            foreach (Transform t in playerHands[i].transform) 
            {
                UICard card = t.GetComponent<UICard>();
                if (card != null) card.SetFaceUp(isDebugAIVisible);
            }
        }
    }

    public void ChangeTurn()
    {
        int limitador = 0;
        do 
        {
            currentTurnIndex = (currentTurnIndex + 1) % totalPlayers;
            limitador++;
            if (limitador > totalPlayers) break; // Seguro anti-cuelgues
        } 
        while (vidas[currentTurnIndex] <= 0); 

        currentState = (currentTurnIndex == 0) ? GameState.PLAYER_TURN : GameState.AI_TURN;
        
        UpdateVisualStates();
        ClearSelection();

        if (currentState == GameState.AI_TURN) StartCoroutine(AITurnRoutine());
    }

    public void SetTurn(int newPlayerIndex)
    {
        currentTurnIndex = newPlayerIndex;
        currentState = (currentTurnIndex == 0) ? GameState.PLAYER_TURN : GameState.AI_TURN;
        UpdateVisualStates();
        ClearSelection();

        if (currentState == GameState.AI_TURN) StartCoroutine(AITurnRoutine());
    }

    IEnumerator AITurnRoutine()
    {
        Debug.Log($"🤖 IA {currentTurnIndex}: Pensando jugada...");
        yield return new WaitForSeconds(1.5f); 

        // 1. LEEMOS TODAS LAS CARTAS DE LA MESA ACTUALMENTE
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

        // 2. LE PASAMOS LA LISTA ENTERA DE LA MESA AL CEREBRO
        UICard cardToPlay = AIController.Instance.ChooseCardToPlay(aiHand, cardsOnTable, currentWins, targetBet);

        if (cardToPlay != null)
        {
            Debug.Log($"🤖 IA {currentTurnIndex}: Juega {cardToPlay.cardData.rank} de {cardToPlay.cardData.suit}");
            cardToPlay.SetFaceUp(true);
            SelectCard(cardToPlay); 
            
            yield return new WaitForSeconds(0.5f); 
            TableZone.Instance.OnPointerClick(null);
        }
        else
        {
            Debug.LogError($"IA {currentTurnIndex} Error: No se encontró carta válida para jugar.");
        }
    }

    private int GetSuitValue(string suit)
    {
        if (suit == "Diamantes") return 4;
        if (suit == "Corazones") return 3;
        if (suit == "Picas") return 2;
        return 1;
    }

    public void SelectCard(UICard card)
    {
        bool isCurrentPlayerCard = card.transform.parent == playerHands[currentTurnIndex].transform;
        bool canSelect = (currentState == GameState.PLAYER_TURN && currentTurnIndex == 0 && isCurrentPlayerCard) || 
                         (currentState == GameState.AI_TURN && isCurrentPlayerCard); 

        if (canSelect)
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
        else
        {
            if(currentState == GameState.PLAYER_TURN && !isCurrentPlayerCard) 
                Debug.Log("No es tu turno o no es tu carta.");
        }
    }

    public void ClearSelection()
    {
        if (SelectedCard != null) SelectedCard.GetComponent<UnityEngine.UI.Image>().color = Color.white;
        SelectedCard = null;
    }

    public bool HasCardSelected()
    {
        return SelectedCard != null;
    }

    public void StartNewGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void AdvanceRoundSequence()
    {
        currentRoundCards += roundDelta;

        if(currentRoundCards <= 1)
        {
            currentRoundCards = 1;
            roundDelta = 1; 
        }
        else if(currentRoundCards >= 5)
        {
            currentRoundCards = 5;
            roundDelta = -1;
        }

        int limitador = 0;
        do 
        {
            manoMesaIndex = (manoMesaIndex + 1) % totalPlayers;
            limitador++;
            if (limitador > totalPlayers) break; // Seguro anti-cuelgues
        } 
        while (vidas[manoMesaIndex] <= 0);
        
        currentTurnIndex = manoMesaIndex;
        currentState = (currentTurnIndex == 0) ? GameState.PLAYER_TURN : GameState.AI_TURN;
        
        Debug.Log($"<color=orange>PRÓXIMA RONDA: {currentRoundCards} CARTAS</color>");
    }

    public void ResolveBlindRoundImmediate()
    {
        StartCoroutine(BlindRoundRoutine());
    }

    IEnumerator BlindRoundRoutine()
    {
        yield return new WaitForSeconds(1.0f);
        SetInfoMessage("¡RESOLVIENDO RONDA CIEGA!");
        
        foreach (CanvasGroup hand in playerHands)
        {
            if (hand.transform.childCount > 0)
            {
                UICard c = hand.transform.GetChild(0).GetComponent<UICard>();
                if (c != null) c.SetFaceUp(true);
            }
        }

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
            
            if (winnerIndex == 0) SetInfoMessage("¡TÚ TIENES LA CARTA MÁS ALTA!");
            else SetInfoMessage($"¡EL JUGADOR {winnerIndex} TIENE LA CARTA MÁS ALTA!");
            
            // Actualizamos la UI para que se vea la baza que acaba de ganar
            ActualizarTodosLosPerfilesUI(); 
        }

        TableZone.Instance.bazasJugadas = 1; 
        
        yield return new WaitForSeconds(1.5f);

        foreach (UICard c in cartasJugadas) c.transform.SetParent(TableZone.Instance.transform);
        
        TableZone.Instance.ForceEndRoundAnalysis();
    }

    public void UpdateVisualStates()
    {
        if (isPaused || playerHands.Count == 0)
        {
            foreach (var hand in playerHands) SetGroupState(hand, false, 0.5f);
            return;
        }

        for (int i = 0; i < playerHands.Count; i++)
        {
            bool isHisTurn = (i == currentTurnIndex);
            SetGroupState(playerHands[i], isHisTurn, isHisTurn ? 1f : 0.5f);
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
}