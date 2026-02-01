using UnityEngine;


public enum GameState {P1_TURN,P2_TURN,WAITING}



public class InteractionManager : MonoBehaviour
{

    //Singleton
    public static InteractionManager Instance;
    //Carta que tenemos en la mano
    
    [Header("Estadísticas de Jugador")]
    public int p1Vidas = 3; // Empezamos con 3 corazones
    public int p2Vidas = 3;
    [Header("Referencias de Turno")]
    public CanvasGroup handGroupP1; // Arrastra el CanvasGroup del HandArea P1
    public CanvasGroup handGroupP2; // Arrastra el CanvasGroup del HandArea P2
    
    public GameState currentState;
    
    public UICard SelectedCard { get; private set; }

    public bool isPaused = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
        
        // Empezamos en WAITING hasta que se repartan cartas
        currentState = GameState.WAITING;
        UpdateVisualStates();
    }

    public void InitializeGame()
    {
        isPaused= false;
        currentState = GameState.P1_TURN;
        UpdateVisualStates();
    }
    // Método llamado por la carta al ser clicada
    public void SelectCard(UICard card)
{
    // 1. Verificación de pertenencia y turno
    bool isP1Card = card.transform.parent == handGroupP1.transform;
    bool isP2Card = card.transform.parent == handGroupP2.transform;

    bool canSelect = (currentState == GameState.P1_TURN && isP1Card) || 
                    (currentState == GameState.P2_TURN && isP2Card);

    if (canSelect)
    {
        // CASO 1: Pulsas la misma carta que ya tenías (Deseleccionar)
        if (SelectedCard == card)
        {
            ClearSelection();
            Debug.Log("Misma carta pulsada: Deseleccionando.");
            return; 
        }

        // CASO 2: Tenías otra carta antes (Limpiar color de la anterior)
        if (SelectedCard != null)
        {
            SelectedCard.GetComponent<UnityEngine.UI.Image>().color = Color.white;
        }

        // CASO 3: Nueva selección
        SelectedCard = card;
        SelectedCard.GetComponent<UnityEngine.UI.Image>().color = Color.yellow;
        Debug.Log("Nueva carta seleccionada: " + card.name);
    }
    else
    {
        Debug.Log("No es tu turno o no es tu carta.");
    }
}

    public void ChangeTurn()
    {
        if (currentState == GameState.P1_TURN) currentState = GameState.P2_TURN;
        else if (currentState == GameState.P2_TURN) currentState = GameState.P1_TURN;
        
        UpdateVisualStates();
        ClearSelection();
    }

    // El efecto "Gris" y bloqueo
   public void UpdateVisualStates()
    {
        // --- NUEVA LÓGICA DE BLOQUEO VISUAL ---
        if (isPaused)
        {
            // Si está pausado, BLOQUEAMOS A LOS DOS
            SetGroupState(handGroupP1, false, 0.5f);
            SetGroupState(handGroupP2, false, 0.5f);
            return;
        }

        // Comportamiento normal de turnos
        SetGroupState(handGroupP1, currentState == GameState.P1_TURN, currentState == GameState.P1_TURN ? 1f : 0.5f);
        SetGroupState(handGroupP2, currentState == GameState.P2_TURN, currentState == GameState.P2_TURN ? 1f : 0.5f);
    }


    // Helper para no repetir código
    private void SetGroupState(CanvasGroup group, bool active, float alpha)
    {
        if (group != null)
        {
            group.interactable = active;
            group.blocksRaycasts = active;
            group.alpha = alpha;
        }
    }
    // Método llamado por la mesa al recibir la carta
    public void ClearSelection()
    {
        // Siempre que limpiamos la selección, aseguramos que el color vuelva a blanco
    if (SelectedCard != null)
    {
        SelectedCard.GetComponent<UnityEngine.UI.Image>().color = Color.white;
    }
    
    SelectedCard = null;
    Debug.Log("Selección reseteada.");
    }

    // Utilidad para saber si hay algo seleccionado
    public bool HasCardSelected()
    {
        return SelectedCard != null;
    }
}
