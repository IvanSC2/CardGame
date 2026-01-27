using UnityEngine;


public enum GameState {P1_TURN,P2_TURN,WAITING}
public class InteractionManager : MonoBehaviour
{

    //Singleton
    public static InteractionManager Instance;
    //Carta que tenemos en la mano
    

    [Header("Referencias de Turno")]
    public CanvasGroup handGroupP1; // Arrastra el CanvasGroup del HandArea P1
    public CanvasGroup handGroupP2; // Arrastra el CanvasGroup del HandArea P2
    
    public GameState currentState;
    
    public UICard SelectedCard { get; private set; }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
        
        // Empezamos en WAITING hasta que se repartan cartas
        currentState = GameState.WAITING;
    }

    public void InitializeGame()
    {
        currentState = GameState.P1_TURN;
        UpdateVisualStates();
    }
    // Método llamado por la carta al ser clicada
    public void SelectCard(UICard card)
    {
        // SEGURIDAD: Solo permitimos seleccionar si la carta pertenece al jugador del turno actual
        // Para esto, comprobaremos el padre de la carta
        bool isP1Card = card.transform.parent == handGroupP1.transform;
        bool isP2Card = card.transform.parent == handGroupP2.transform;

        if ((currentState == GameState.P1_TURN && isP1Card) || 
            (currentState == GameState.P2_TURN && isP2Card))
        {
            SelectedCard = card;
            Debug.Log("Carta seleccionada correctamente.");
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
    private void UpdateVisualStates()
    {
        // Si es turno de P1: P1 opaco(1) y activo. P2 gris(0.5) y bloqueado.
        handGroupP1.alpha = (currentState == GameState.P1_TURN) ? 1.0f : 0.5f;
        handGroupP1.interactable = (currentState == GameState.P1_TURN);
        handGroupP1.blocksRaycasts = (currentState == GameState.P1_TURN);

        handGroupP2.alpha = (currentState == GameState.P2_TURN) ? 1.0f : 0.5f;
        handGroupP2.interactable = (currentState == GameState.P2_TURN);
        handGroupP2.blocksRaycasts = (currentState == GameState.P2_TURN);
    }

    // Método llamado por la mesa al recibir la carta
    public void ClearSelection()
    {
        SelectedCard = null;
        Debug.Log("Selección limpiada.");
    }

    // Utilidad para saber si hay algo seleccionado
    public bool HasCardSelected()
    {
        return SelectedCard != null;
    }
}
