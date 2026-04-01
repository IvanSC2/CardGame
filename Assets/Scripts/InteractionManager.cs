using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public enum GameState {P1_TURN, P2_TURN, WAITING}

public class InteractionManager : MonoBehaviour
{
    // Singleton
    public static InteractionManager Instance;

    [Header("UI Feedback")]
    public TextMeshProUGUI infoLineText;
    [Header("Debug")]
    public bool isDebugAIVisible = false; // VARIABLE DE TOGGLE
    [Header("Control de Rondas")]
    public int currentRoundCards = 5;
    private int roundDelta = -1;

    [Header("Estadísticas de Jugador")]
    public int p1Vidas = 3;
    public int p2Vidas = 3;
    [Header("Control de Turnos")]
    public GameState currentMano; 
    [Header("Referencias de Turno")]
    public CanvasGroup handGroupP1; 
    public CanvasGroup handGroupP2; 

    [Header("Estadísticas Globales")]
    public int rondasJugadasTotales = 0;
    public int p1ApuestasAcertadas = 0;
    public int p2ApuestasAcertadas = 0;
    public int p1BazasTotales = 0;
    public int p2BazasTotales = 0;
    
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
        // 1. Configuración propia del Singleton y estado
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
        currentState = GameState.WAITING;
        
        // 2. Sorteo del jugador que empieza
        int sorteo = Random.Range(0,2);
        currentMano= (sorteo ==0)? GameState.P1_TURN : GameState.P2_TURN;
        string[] nombresDificultad = { "Pacifico", "Normal", "Difícil", "Experto", "Imposible" };
        int numBots = GameConfig.nPlayers; 
        int difIndex = GameConfig.difficulty;
        SetInfoMessage($"Numero de bots: {numBots}\nDificultad: {nombresDificultad[difIndex]}");
        
        UpdateVisualStates();
    }

    private void Start()
    {
        // 3. Hablamos con otros scripts una vez que TODOS están despiertos
        int numBots = GameConfig.nPlayers; 
        
        if (TableManagerLayout.Instance != null)
        {
            TableManagerLayout.Instance.GenerarMesa(numBots + 1);

            // Inmediatamente después, enganchamos las manos buenas
            if (TableManagerLayout.Instance.manosActivas.Count >= 2)
            {
                handGroupP1 = TableManagerLayout.Instance.manosActivas[0];
                handGroupP2 = TableManagerLayout.Instance.manosActivas[1];
                Debug.Log("[SYSTEM] Manos dinámicas vinculadas con éxito. Ignorando molde.");
            }
        }
        else
        {
            Debug.LogError("Error: TableManagerLayout no encontrado al arrancar.");
        }
    }
    //Quita la pausa y asigna el turno
    public void InitializeGame()
    {
       
        isPaused = false;
        currentState = currentMano;
        UpdateVisualStates();
        RefreshHandVisibility();
        //Si le toca a la IA Activa su corrutina
        if (currentState == GameState.P2_TURN)
        {
            StartCoroutine(AITurnRoutine());
        }
    }

    // --- LÓGICA DE TOGGLE ---
    public void ToggleAIDebugView()
    {
        isDebugAIVisible = !isDebugAIVisible;
        RefreshHandVisibility();
    }

    public void RefreshHandVisibility()
    {
        // CASO ESPECIAL: RONDA DE 1 CARTA (CIEGA)
        if (currentRoundCards == 1)
        {
            // P1 (Tú): NO ves tu carta (Boca abajo)
            foreach (Transform t in handGroupP1.transform)
                if (t.GetComponent<UICard>()) t.GetComponent<UICard>().SetFaceUp(false);

            // P2 (IA): SÍ ves su carta (Boca arriba) para tener info
            foreach (Transform t in handGroupP2.transform)
                if (t.GetComponent<UICard>()) t.GetComponent<UICard>().SetFaceUp(true);
                
            return; // Salimos aquí, ignorando el resto
        }

        // --- LÓGICA NORMAL (Rondas 5, 4, 3, 2) ---
        foreach (Transform t in handGroupP1.transform) 
            if(t.GetComponent<UICard>()) t.GetComponent<UICard>().SetFaceUp(true);

        foreach (Transform t in handGroupP2.transform) 
        {
            UICard card = t.GetComponent<UICard>();
            if (card != null) card.SetFaceUp(isDebugAIVisible);
        }
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
    // --- NUEVO: Fuerza el turno para el ganador de la baza ---
    public void SetTurn(GameState newTurn)
    {
        currentState = newTurn;
        UpdateVisualStates();
        ClearSelection();

        // Si el ganador fue la IA, le decimos que empiece a pensar su jugada
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
            cardToPlay.SetFaceUp(true);
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
    private int GetSuitValue(string suit)
    {
        if (suit == "Diamantes") return 4;
        if (suit == "Corazones") return 3;
        if (suit == "Picas") return 2;
        return 1;
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
    public void StartNewGame()
{
    // Esto averigua cómo se llama la escena en la que estás ahora mismo, y la vuelve a cargar desde cero.
    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
}
    /*public void ResetGameTotal()
    {
        Debug.Log("🔄 REINICIANDO SISTEMA DE JUEGO...");

        p1Vidas = 3;
        p2Vidas = 3;

        currentRoundCards = 5;
        roundDelta = -1;
        //Sorteo
        int sorteo= Random.Range(0,2);
        currentMano = (sorteo ==0) ? GameState.P1_TURN : GameState.P2_TURN;
        string nomMano= (currentMano == GameState.P1_TURN) ? "JUGADOR 1" : "JUGADOR 2";
        SetInfoMessage($"Sorteo inicial: EMPIEZA {nomMano}");

        CardDatabase.GenerateDeck(); 
        ClearSelection();

        Debug.Log("✅ JUEGO NUEVO LISTO.");
    }
*/
    public void AdvanceRoundSequence()
    {
        currentRoundCards += roundDelta;

        // CAMBIO: Ahora el límite inferior es 1, no 2
        if(currentRoundCards <= 1)
        {
            currentRoundCards = 1;
            roundDelta = 1; // La próxima subirá a 2
        }
        else if(currentRoundCards >= 5)
        {
            currentRoundCards = 5;
            roundDelta = -1;
        }
       currentMano = (currentMano == GameState.P1_TURN) ? GameState.P2_TURN : GameState.P1_TURN;
        
        string tipoRonda = (currentRoundCards == 1) ? "RONDA CIEGA (INDIAN POKER)" : "NORMAL";
        Debug.Log($"<color=orange>PRÓXIMA RONDA: {currentRoundCards} CARTAS - {tipoRonda}</color>");
    }
    // 3. NUEVO: RESOLUCIÓN EXPRESS (Sin jugar cartas)
    public void ResolveBlindRoundImmediate()
    {
        StartCoroutine(BlindRoundRoutine());
    }

    IEnumerator BlindRoundRoutine()
    {
        yield return new WaitForSeconds(1.0f);
        SetInfoMessage("¡RESOLVIENDO RONDA CIEGA!");
        
        // 1. Revelamos TU carta (P1) para ver quién gana
        foreach (Transform t in handGroupP1.transform)
            if (t.GetComponent<UICard>()) t.GetComponent<UICard>().SetFaceUp(true);

        yield return new WaitForSeconds(1.5f); // Suspense...

        // 2. Comparamos valores directamente desde la mano
        // (Asumimos que solo hay 1 carta por mano)
        UICard p1Card = handGroupP1.transform.GetChild(0).GetComponent<UICard>();
        UICard p2Card = handGroupP2.transform.GetChild(0).GetComponent<UICard>();

        // Usamos la lógica de TableZone para calcular puntos (aunque no estén en la mesa)
        // Ojo: Necesitamos acceder a la lógica de "quién gana".
        // Para simplificar, lo calculamos aquí rápido:
        int score1 = (p1Card.cardData.value * 10) + GetSuitValue(p1Card.cardData.suit);
        int score2 = (p2Card.cardData.value * 10) + GetSuitValue(p2Card.cardData.suit);

        // 3. Asignar victorias
        if (score1 > score2) 
        {
            TableZone.Instance.p1Wins = 1;
            SetInfoMessage("¡P1 TIENE LA CARTA MAS ALTA!");
        }
        else 
        {
            TableZone.Instance.p2Wins = 1;
            SetInfoMessage("¡P2 TIENE LA CARTA MAS ALTA!");
        }

        TableZone.Instance.bazasJugadas = 1; // Forzamos que la ronda "acabó"
        
        yield return new WaitForSeconds(1.5f);

        // 4. Llamar a la limpieza final de TableZone
        // (Usamos un truco: llamamos a CheckWinner para que él active el fin de ronda)
        // Pero como CheckWinner espera cartas EN LA MESA, mejor llamamos directamente a ResolverApuestas
        // Sin embargo, TableZone.ResolveApuestas es privado. 
        // TRUCO: Vamos a mover las cartas a la mesa visualmente y llamar a CheckWinner.
        
        p1Card.transform.SetParent(TableZone.Instance.transform);
        p2Card.transform.SetParent(TableZone.Instance.transform);
        
        // Al ponerlas en la mesa, TableZone detectará 2 hijos... pero necesitamos disparar la lógica.
        // Llamamos manualmente a CheckWinner modificando TableZone o simplemente dejando que TableZone
        // maneje el final si lo hacemos público. 
        
        // OPCIÓN MÁS LIMPIA: Llamamos a una función pública en TableZone que creemos ahora.
        TableZone.Instance.ForceEndRoundAnalysis();
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