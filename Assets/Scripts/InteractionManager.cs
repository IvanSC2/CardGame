using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public enum GameState {P1_TURN, P2_TURN, WAITING}

public class InteractionManager : MonoBehaviour
{
    // Singleton
    public static InteractionManager Instance;

    [Header("UI Feedback")]
    public TextMeshProUGUI infoLineText;

    [Header("Control de Rondas")]
    public int currentRoundCards = 5;
    private int roundDelta = -1;

    [Header("Estadísticas de Jugador")]
    public int p1Vidas = 3;
    public int p2Vidas = 3;

    [Header("Referencias de Turno")]
    public CanvasGroup handGroupP1; 
    public CanvasGroup handGroupP2; 
    
    public GameState currentState;
    public UICard SelectedCard { get; private set; }
    public bool isPaused = false;

    // --- Helpers de Mensajes ---
    public void SetInfoMessage(string message)
    {
        if (infoLineText != null)
        {
            infoLineText.text = message;
        }
        Debug.Log("[INFO-UI]: " + message);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
        
        currentState = GameState.WAITING;
        UpdateVisualStates();
    }

    public void InitializeGame()
    {
        isPaused = false;
        currentState = GameState.P1_TURN;
        UpdateVisualStates();
    }

    // =================================================================================
    // 🧠 LÓGICA DE TURNOS E IA
    // =================================================================================

    public void ChangeTurn()
    {
        // Alternar Turno
        if (currentState == GameState.P1_TURN) currentState = GameState.P2_TURN;
        else if (currentState == GameState.P2_TURN) currentState = GameState.P1_TURN;
        
        UpdateVisualStates();
        ClearSelection();

        // >>> DISPARADOR DE LA IA <<<
        if (currentState == GameState.P2_TURN)
        {
            StartCoroutine(AITurnRoutine());
        }
    }

    // Corrutina que controla el pensamiento y acción de la IA
    IEnumerator AITurnRoutine()
    {
        Debug.Log("🤖 IA: Pensando jugada...");
        yield return new WaitForSeconds(1.5f); // Pequeña pausa para dar realismo

        // 1. Obtener la carta que hay en la mesa (si la hay)
        Card cardOnTable = null;
        if (TableZone.Instance != null && TableZone.Instance.transform.childCount > 0)
        {
            // Asumimos que el primer hijo de la mesa es la carta del rival
            cardOnTable = TableZone.Instance.transform.GetChild(0).GetComponent<UICard>().cardData;
        }

        // 2. Obtener la mano de la IA (Lista de componentes UICard)
        List<UICard> aiHand = new List<UICard>();
        foreach (Transform t in handGroupP2.transform)
        {
            UICard c = t.GetComponent<UICard>();
            if (c != null) aiHand.Add(c);
        }

        // 3. Consultar al Cerebro (AIController)
        int currentWins = TableZone.Instance.p2Wins;
        int targetBet = BettingManager.Instance.p2Bet;

        UICard cardToPlay = AIController.Instance.ChooseCardToPlay(aiHand, cardOnTable, currentWins, targetBet);

        // 4. Ejecutar la jugada simulando clicks
        if (cardToPlay != null)
        {
            Debug.Log($"🤖 IA: Juega {cardToPlay.cardData.rank} de {cardToPlay.cardData.suit}");
            
            // A) Seleccionar la carta
            SelectCard(cardToPlay); 
            
            yield return new WaitForSeconds(0.5f); // Breve pausa visual

            // B) Jugarla en la mesa
            TableZone.Instance.OnPointerClick(null);
        }
        else
        {
            Debug.LogError("IA Error: No se encontró carta válida para jugar.");
        }
    }

    // =================================================================================
    // 🃏 LÓGICA DE SELECCIÓN DE CARTAS
    // =================================================================================

    public void SelectCard(UICard card)
    {
        // 1. Verificación de pertenencia y turno
        bool isP1Card = card.transform.parent == handGroupP1.transform;
        bool isP2Card = card.transform.parent == handGroupP2.transform;

        bool canSelect = (currentState == GameState.P1_TURN && isP1Card) || 
                        (currentState == GameState.P2_TURN && isP2Card); // Permitimos P2 para que la IA pueda seleccionarse a sí misma

        if (canSelect)
        {
            // CASO 1: Deseleccionar (Toggle)
            if (SelectedCard == card)
            {
                ClearSelection();
                return; 
            }

            // CASO 2: Limpiar anterior
            if (SelectedCard != null)
            {
                SelectedCard.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            }

            // CASO 3: Nueva selección
            SelectedCard = card;
            SelectedCard.GetComponent<UnityEngine.UI.Image>().color = Color.yellow;
        }
        else
        {
            // Solo mostramos log si es un humano intentando hacer trampa
            if(currentState == GameState.P1_TURN && isP2Card) 
                Debug.Log("No es tu turno o no es tu carta.");
        }
    }

    public void ClearSelection()
    {
        if (SelectedCard != null)
        {
            SelectedCard.GetComponent<UnityEngine.UI.Image>().color = Color.white;
        }
        SelectedCard = null;
    }

    public bool HasCardSelected()
    {
        return SelectedCard != null;
    }

    // =================================================================================
    // ⚙️ GESTIÓN DE ESTADO (GAME LOOP)
    // =================================================================================

    public void ResetGameTotal()
    {
        Debug.Log("🔄 REINICIANDO SISTEMA DE JUEGO...");

        p1Vidas = 3;
        p2Vidas = 3;

        currentRoundCards = 5;
        roundDelta = -1;

        CardDatabase.GenerateDeck(); 
        ClearSelection();

        Debug.Log("✅ JUEGO NUEVO LISTO.");
    }

    public void AdvanceRoundSequence()
    {
        currentRoundCards += roundDelta;

        if(currentRoundCards <= 2)
        {
            currentRoundCards = 2;
            roundDelta = 1;
        }
        else if(currentRoundCards >= 5)
        {
            currentRoundCards = 5;
            roundDelta = -1;
        }
        Debug.Log($"<color=orange>PRÓXIMA RONDA: {currentRoundCards} CARTAS</color>");
    }

    public void UpdateVisualStates()
    {
        if (isPaused)
        {
            SetGroupState(handGroupP1, false, 0.5f);
            SetGroupState(handGroupP2, false, 0.5f);
            return;
        }

        SetGroupState(handGroupP1, currentState == GameState.P1_TURN, currentState == GameState.P1_TURN ? 1f : 0.5f);
        SetGroupState(handGroupP2, currentState == GameState.P2_TURN, currentState == GameState.P2_TURN ? 1f : 0.5f);
    }

    private void SetGroupState(CanvasGroup group, bool active, float alpha)
    {
        if (group != null)
        {
            group.interactable = active;
            group.blocksRaycasts = active;
            
            // Fondo opaco siempre
            group.alpha = 1f; 

            // Cartas semitransparentes si no es su turno
            foreach (Transform childCard in group.transform)
            {
                CanvasGroup cardGroup = childCard.GetComponent<CanvasGroup>();
                if (cardGroup == null) cardGroup = childCard.gameObject.AddComponent<CanvasGroup>();
                cardGroup.alpha = active ? 1f : alpha; 
            }
        }
    }
}